"""Evaluate private Franky wake recordings with the deployed quantized model.

This reads only local files under .cache by default. It does not upload audio,
alter the model, or add recordings to the training dataset.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import wave
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path

import numpy as np

os.environ.setdefault("TF_CPP_MIN_LOG_LEVEL", "2")

from ai_edge_litert.interpreter import Interpreter


TOOL_ROOT = Path(__file__).resolve().parent
REPOSITORY_ROOT = TOOL_ROOT.parent.parent
VENDOR_ROOT = TOOL_ROOT / ".cache" / "vendor" / "micro-wake-word"
DEFAULT_MODEL = (
    REPOSITORY_ROOT
    / "firmware"
    / "franky-device"
    / "main"
    / "models"
    / "yo_franky.tflite"
)
DEFAULT_RECORDINGS = TOOL_ROOT / ".cache" / "recordings"
DEFAULT_OUTPUT = TOOL_ROOT / ".cache" / "evaluation" / "latest.json"
PARITY_MODEL_SHA256 = "987223a0697b9f8a382f6f00cc523026478ba99a21cef264e3686fd887b203dd"
DEFAULT_THRESHOLDS = (50, 60, 70, 80, 87, 92, 96, 99)
FEATURE_FRAMES_PER_INFERENCE = 3
PROBABILITY_WINDOW = 5
PROBABILITY_SCALE = 255 * PROBABILITY_WINDOW
INFERENCE_CADENCE_MS = 30

if not VENDOR_ROOT.is_dir():
    raise SystemExit(
        "The pinned microWakeWord source is missing. Run ./bootstrap.ps1 first."
    )
sys.path.insert(0, str(VENDOR_ROOT))

from microwakeword.audio.audio_utils import generate_features_for_clip  # noqa: E402


@dataclass(frozen=True)
class SampleResult:
    id: str
    category: str
    purpose: str
    prompt_id: str
    distance: str
    orientation: str
    duration_ms: int
    peak_raw_average: float
    peak_percent: int
    board_peak_score_percent: int | None
    board_score_status: str
    peak_score_delta: int | None
    threshold_crossings_ms: dict[str, int | None]


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Score Franky's private physical wake recordings offline."
    )
    parser.add_argument("--model", type=Path, default=DEFAULT_MODEL)
    parser.add_argument("--recordings-root", type=Path, default=DEFAULT_RECORDINGS)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument(
        "--thresholds",
        default=",".join(str(value) for value in DEFAULT_THRESHOLDS),
        help="Comma-separated percentage cutoffs from 50 through 99.",
    )
    return parser.parse_args()


def parse_thresholds(value: str) -> tuple[int, ...]:
    try:
        thresholds = tuple(sorted(set(int(part.strip()) for part in value.split(","))))
    except ValueError as error:
        raise ValueError("Thresholds must be comma-separated integers.") from error
    if not thresholds or any(threshold < 50 or threshold > 99 for threshold in thresholds):
        raise ValueError("Thresholds must be between 50 and 99 percent.")
    return thresholds


def load_pcm(path: Path) -> tuple[np.ndarray, int]:
    with wave.open(str(path), "rb") as source:
        if (
            source.getnchannels() != 1
            or source.getsampwidth() != 2
            or source.getframerate() != 16_000
            or source.getcomptype() != "NONE"
        ):
            raise ValueError(f"{path.name} is not canonical 16 kHz mono PCM16 WAV audio.")
        frame_count = source.getnframes()
        pcm = np.frombuffer(source.readframes(frame_count), dtype="<i2").copy()
    return pcm, round(frame_count * 1000 / 16_000)


def raw_predictions(model_path: Path, pcm: np.ndarray) -> list[int]:
    features = generate_features_for_clip(pcm, step_ms=10)
    if np.issubdtype(features.dtype, np.uint16):
        features = features.astype(np.float32) * 0.0390625
    elif np.issubdtype(features.dtype, np.float64):
        features = features.astype(np.float32)

    interpreter = Interpreter(model_path=str(model_path))
    interpreter.allocate_tensors()
    inputs = interpreter.get_input_details()
    outputs = interpreter.get_output_details()
    primary = inputs[0]
    for tensor in inputs:
        interpreter.set_tensor(tensor["index"], np.zeros(tensor["shape"], dtype=tensor["dtype"]))

    predictions: list[int] = []
    frame_count = int(primary["shape"][1])
    for last_index in range(frame_count, len(features) + 1, FEATURE_FRAMES_PER_INFERENCE):
        chunk = features[last_index - frame_count : last_index]
        if len(chunk) != frame_count:
            continue
        if primary["dtype"] == np.int8 and chunk.dtype != np.int8:
            scale = primary["quantization_parameters"]["scales"][0]
            zero_point = primary["quantization_parameters"]["zero_points"][0]
            chunk = (chunk / scale + zero_point).astype(np.int8)
        interpreter.set_tensor(primary["index"], np.reshape(chunk, primary["shape"]))
        interpreter.invoke()
        predictions.append(int(interpreter.get_tensor(outputs[0]["index"])[0][0]))
    return predictions


def score_sample(
    model_path: Path,
    wave_path: Path,
    metadata: dict[str, object],
    thresholds: tuple[int, ...],
    compare_board_score: bool,
) -> SampleResult:
    pcm, duration_ms = load_pcm(wave_path)
    predictions = raw_predictions(model_path, pcm)
    rolling_sums = [
        sum(predictions[index - PROBABILITY_WINDOW + 1 : index + 1])
        for index in range(PROBABILITY_WINDOW - 1, len(predictions))
    ]
    peak_sum = max(rolling_sums, default=0)
    peak_percent = (peak_sum * 100 + PROBABILITY_SCALE // 2) // PROBABILITY_SCALE
    board_peak_value = metadata.get("boardPeakScorePercent")
    board_peak_score = None if board_peak_value is None else int(board_peak_value)
    if board_peak_score is not None and not 0 <= board_peak_score <= 100:
        raise ValueError(f"Invalid board peak score in {wave_path.name} metadata.")
    board_score_status = str(metadata.get("boardScoreStatus") or "valid")
    if board_score_status not in ("valid", "invalid"):
        raise ValueError(f"Invalid board score status in {wave_path.name} metadata.")
    crossings: dict[str, int | None] = {}
    for threshold in thresholds:
        crossing = next(
            (
                (index + PROBABILITY_WINDOW) * INFERENCE_CADENCE_MS
                for index, score_sum in enumerate(rolling_sums)
                if score_sum * 100 >= threshold * PROBABILITY_SCALE
            ),
            None,
        )
        crossings[str(threshold)] = crossing

    return SampleResult(
        id=str(metadata.get("id", wave_path.stem)),
        category=str(metadata.get("category", wave_path.parent.name)),
        purpose=str(metadata.get("purpose") or "corpus"),
        prompt_id=str(metadata.get("promptId", "")),
        distance=str(metadata.get("distance", "")),
        orientation=str(metadata.get("orientation", "")),
        duration_ms=duration_ms,
        peak_raw_average=round(peak_sum / PROBABILITY_WINDOW, 2),
        peak_percent=peak_percent,
        board_peak_score_percent=board_peak_score,
        board_score_status=board_score_status,
        peak_score_delta=(
            peak_percent - board_peak_score
            if compare_board_score and board_peak_score is not None
            else None
        ),
        threshold_crossings_ms=crossings,
    )


def read_samples(root: Path) -> list[tuple[Path, dict[str, object]]]:
    samples: list[tuple[Path, dict[str, object]]] = []
    for category in ("positive", "hard-negative"):
        directory = root / category
        if not directory.is_dir():
            continue
        for metadata_path in sorted(directory.glob("*.json")):
            metadata = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
            wave_path = directory / f"{metadata_path.stem}.wav"
            if not wave_path.is_file():
                raise FileNotFoundError(f"Missing WAV for {metadata_path.name}")
            samples.append((wave_path, metadata))
    return samples


def summarize(results: list[SampleResult], thresholds: tuple[int, ...]) -> dict[str, object]:
    corpus = [result for result in results if result.purpose == "corpus"]
    positives = [result for result in corpus if result.category == "positive"]
    negatives = [result for result in corpus if result.category == "hard-negative"]
    summary: dict[str, object] = {}
    for threshold in thresholds:
        key = str(threshold)
        positive_hits = sum(result.threshold_crossings_ms[key] is not None for result in positives)
        negative_activations = sum(result.threshold_crossings_ms[key] is not None for result in negatives)
        summary[key] = {
            "positiveDetected": positive_hits,
            "positiveTotal": len(positives),
            "positiveRecall": round(positive_hits / len(positives), 4) if positives else None,
            "hardNegativeActivations": negative_activations,
            "hardNegativeTotal": len(negatives),
        }
    return summary


def summarize_parity(results: list[SampleResult]) -> dict[str, object]:
    compared = [
        result
        for result in results
        if result.purpose == "parity"
        and result.board_peak_score_percent is not None
        and result.board_score_status == "valid"
        and result.peak_score_delta is not None
    ]
    excluded_invalid = sum(
        result.purpose == "parity"
        and result.board_peak_score_percent is not None
        and result.board_score_status == "invalid"
        and result.peak_score_delta is not None
        for result in results
    )
    deltas = [abs(result.peak_score_delta or 0) for result in compared]
    return {
        "comparedSamples": len(compared),
        "exactMatches": sum(delta == 0 for delta in deltas),
        "withinOnePoint": sum(delta <= 1 for delta in deltas),
        "maximumAbsoluteDelta": max(deltas, default=None),
        "excludedInvalidMeasurements": excluded_invalid,
    }


def main() -> int:
    arguments = parse_arguments()
    thresholds = parse_thresholds(arguments.thresholds)
    model_path = arguments.model.resolve()
    recordings_root = arguments.recordings_root.resolve()
    output_path = arguments.output.resolve()
    if not model_path.is_file():
        raise FileNotFoundError(f"Wake model not found: {model_path}")

    model_sha256 = hashlib.sha256(model_path.read_bytes()).hexdigest()
    board_comparison_enabled = model_sha256 == PARITY_MODEL_SHA256
    samples = read_samples(recordings_root)
    results = [
        score_sample(
            model_path,
            wave_path,
            metadata,
            thresholds,
            compare_board_score=board_comparison_enabled,
        )
        for wave_path, metadata in samples
    ]
    parity_summary = summarize_parity(results)
    parity_limitation = (
        "Board scores were not compared because the selected model does not match the model used for the recorded parity samples."
        if not board_comparison_enabled
        else
        "Board/Python score parity remains unverified until matched samples are captured."
        if parity_summary["comparedSamples"] == 0
        else "A small parity set validates the scoring path only; it does not establish wake reliability."
    )
    report = {
        "schemaVersion": 4,
        "evaluatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "modelSha256": model_sha256,
        "capturePipeline": "afe_processed_mono_v1",
        "thresholds": thresholds,
        "sampleCount": len(results),
        "boardScoreComparisonEnabled": board_comparison_enabled,
        "summary": summarize(results, thresholds),
        "paritySummary": parity_summary,
        "samples": [asdict(result) for result in results],
        "limitations": [
            "Short hard-negative clips report activation counts, not false activations per hour.",
            parity_limitation,
        ],
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print(f"Evaluated {len(results)} local sample(s).")
    for threshold in thresholds:
        item = report["summary"][str(threshold)]
        print(
            f"{threshold}%: positives {item['positiveDetected']}/{item['positiveTotal']}; "
            f"hard-negative activations {item['hardNegativeActivations']}/{item['hardNegativeTotal']}"
        )
    parity = report["paritySummary"]
    if not board_comparison_enabled:
        print("Board/Python parity: comparison disabled for a different model hash")
    elif parity["comparedSamples"] == 0:
        print("Board/Python parity: no matched samples yet")
    else:
        print(
            "Board/Python parity: "
            f"{parity['comparedSamples']} compared; "
            f"{parity['withinOnePoint']} within one point; "
            f"maximum delta {parity['maximumAbsoluteDelta']}; "
            f"{parity['excludedInvalidMeasurements']} invalid measurement(s) excluded"
        )
    print(f"Private report: {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
