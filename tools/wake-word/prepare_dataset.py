"""Prepare synthetic and ambient features for the Yo Franky wake-word model."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import wave
import zipfile
from pathlib import Path

import numpy as np
import webrtcvad

TOOL_ROOT = Path(__file__).resolve().parent
CACHE_ROOT = TOOL_ROOT / ".cache"
VENDOR_ROOT = CACHE_ROOT / "vendor"
PIPER_SOURCE = VENDOR_ROOT / "piper-sample-generator"
MICRO_WAKE_WORD_SOURCE = VENDOR_ROOT / "micro-wake-word"
GENERATOR_MODEL = CACHE_ROOT / "models" / "en_US-libritts_r-medium.pt"
POSITIVE_AUDIO = CACHE_ROOT / "audio" / "positive"
HARD_NEGATIVE_AUDIO = CACHE_ROOT / "audio" / "hard-negative"
FEATURE_ROOT = CACHE_ROOT / "features"
NEGATIVE_ROOT = CACHE_ROOT / "negative-datasets"
RECORDINGS_ROOT = CACHE_ROOT / "recordings"
PHYSICAL_CORPUS_COUNTS = {"positive": 30, "hard-negative": 20}
PHYSICAL_FEATURE_REPEATS = 16

NEGATIVE_ARCHIVES = {
    "dinner_party.zip": 444_310_142,
    "dinner_party_eval.zip": 82_329_019,
    "no_speech.zip": 2_000_317_854,
    "speech.zip": 3_183_001_091,
}
NEGATIVE_BASE_URL = "https://huggingface.co/datasets/kahrendt/microwakeword/resolve/main/"
WINDOWS_DLL_HANDLES = []


def configure_windows_ffmpeg() -> None:
    """Expose a supported Winget FFmpeg Shared install to TorchCodec."""
    if sys.platform != "win32":
        return

    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        return

    package_root = Path(local_app_data) / "Microsoft" / "WinGet" / "Packages"
    candidates = []
    for major in range(8, 3, -1):
        candidates.extend(
            package_root.glob(
                f"Gyan.FFmpeg.Shared*/ffmpeg-{major}.*-full_build-shared/bin"
            )
        )
    if not candidates:
        return

    ffmpeg_bin = sorted(candidates, reverse=True)[0]
    os.environ["PATH"] = f"{ffmpeg_bin}{os.pathsep}{os.environ.get('PATH', '')}"
    WINDOWS_DLL_HANDLES.append(os.add_dll_directory(str(ffmpeg_bin)))


configure_windows_ffmpeg()

if not MICRO_WAKE_WORD_SOURCE.is_dir():
    raise RuntimeError("microWakeWord source is missing; run bootstrap.ps1 first.")
sys.path.insert(0, str(MICRO_WAKE_WORD_SOURCE))

from mmap_ninja.ragged import RaggedMmap
from microwakeword.audio.augmentation import Augmentation
from microwakeword.audio.clips import Clips
from microwakeword.audio.spectrograms import SpectrogramGeneration


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--positive-samples", type=int, default=3000)
    parser.add_argument("--hard-negative-samples", type=int, default=1500)
    parser.add_argument(
        "--skip-ambient",
        action="store_true",
        help="Do not download the pre-generated ambient negative feature sets.",
    )
    parser.add_argument(
        "--only-physical-corpus",
        action="store_true",
        help="Prepare only the approved private 30/20 physical corpus features.",
    )
    parser.add_argument(
        "--physical-candidate",
        choices=("v1", "v2"),
        default="v2",
        help="Feature recipe to use with --only-physical-corpus (default: v2).",
    )
    return parser.parse_args()


def count_wavs(directory: Path) -> int:
    return sum(1 for _ in directory.glob("*.wav")) if directory.exists() else 0


def load_generator():
    if not PIPER_SOURCE.is_dir():
        raise RuntimeError("Piper source is missing; run bootstrap.ps1 first.")
    if str(PIPER_SOURCE) not in sys.path:
        sys.path.insert(0, str(PIPER_SOURCE))

    from piper_sample_generator.__main__ import generate_samples

    return generate_samples


def generate_audio(phrase_file: Path, destination: Path, sample_count: int) -> None:
    existing = count_wavs(destination)
    if existing >= sample_count:
        print(f"[audio] {destination.name}: using {existing} existing samples")
        return
    if existing:
        raise RuntimeError(
            f"{destination} contains only {existing}/{sample_count} samples. "
            "Move it aside or finish generation before rerunning."
        )

    destination.mkdir(parents=True, exist_ok=True)
    generate_samples = load_generator()
    print(f"[audio] generating {sample_count} samples in {destination}")
    generate_samples(
        text=str(phrase_file),
        output_dir=destination,
        model=GENERATOR_MODEL,
        max_samples=sample_count,
        batch_size=64,
        slerp_weights=(0.2, 0.5, 0.8),
        length_scales=(0.72, 0.86, 1.0, 1.16, 1.3),
        noise_scales=(0.5, 0.667, 0.8),
        noise_scale_ws=(0.6, 0.8, 1.0),
        max_speakers=300,
    )


def feature_augmenter(*, truncate_randomly: bool = False) -> Augmentation:
    import audiomentations
    import piper_sample_generator

    # Piper 3.2.0 intentionally pins audiomentations 0.33.0, while the pinned
    # microWakeWord commit expects the later AddColorNoise name. Gaussian SNR
    # noise has the same constructor contract and keeps both projects on their
    # reviewed dependency versions.
    if not hasattr(audiomentations, "AddColorNoise"):
        audiomentations.AddColorNoise = audiomentations.AddGaussianSNR

    impulse_directory = Path(piper_sample_generator.__file__).resolve().parent / "impulses"
    return Augmentation(
        augmentation_duration_s=2.2,
        augmentation_probabilities={
            "SevenBandParametricEQ": 0.15,
            "TanhDistortion": 0.05,
            "PitchShift": 0.12,
            "BandStopFilter": 0.10,
            "AddColorNoise": 0.50,
            "AddBackgroundNoise": 0.0,
            "Gain": 1.0,
            "GainTransition": 0.20,
            "RIR": 0.60,
        },
        impulse_paths=[str(impulse_directory)],
        background_paths=[],
        color_min_snr_db=4,
        color_max_snr_db=24,
        min_gain_db=-34,
        max_gain_db=2,
        min_jitter_s=0.14,
        max_jitter_s=0.30,
        truncate_randomly=truncate_randomly,
    )


def load_canonical_pcm(path: Path) -> np.ndarray:
    with wave.open(str(path), "rb") as source:
        if (
            source.getnchannels() != 1
            or source.getsampwidth() != 2
            or source.getframerate() != 16_000
            or source.getcomptype() != "NONE"
        ):
            raise ValueError(f"{path.name} is not canonical 16 kHz mono PCM16 WAV audio.")
        return np.frombuffer(
            source.readframes(source.getnframes()), dtype="<i2"
        ).copy()


def align_utterance(pcm: np.ndarray) -> np.ndarray:
    """Trim fixed capture padding while preserving the complete spoken utterance."""
    frame_samples = 480
    vad = webrtcvad.Vad(0)
    voiced_frames = [
        index
        for index in range(0, len(pcm) - frame_samples + 1, frame_samples)
        if vad.is_speech(pcm[index : index + frame_samples].tobytes(), 16_000)
    ]
    if not voiced_frames:
        raise ValueError("A physical corpus sample contains no VAD speech frames.")

    leading_context_samples = 1_600
    trailing_context_samples = 3_200
    start = max(0, voiced_frames[0] - leading_context_samples)
    end = min(len(pcm), voiced_frames[-1] + frame_samples)
    aligned = pcm[start:end]
    return np.pad(aligned, (0, trailing_context_samples))


class PhysicalCorpusClips:
    def __init__(self, paths: list[Path], *, align: bool):
        self.paths = paths
        self.align = align

    def audio_generator(self, split: str | None = None, repeat: int = 1):
        if split is not None:
            raise ValueError("Physical corpus features are training-only.")
        for _ in range(repeat):
            for path in self.paths:
                pcm = load_canonical_pcm(path)
                if self.align:
                    pcm = align_utterance(pcm)
                yield pcm.astype(np.float32) / 32_768.0


def read_physical_corpus() -> dict[str, list[tuple[Path, dict[str, object]]]]:
    corpus: dict[str, list[tuple[Path, dict[str, object]]]] = {
        category: [] for category in PHYSICAL_CORPUS_COUNTS
    }
    seen_hashes: set[str] = set()
    for category, expected_count in PHYSICAL_CORPUS_COUNTS.items():
        directory = RECORDINGS_ROOT / category
        for metadata_path in sorted(directory.glob("*.json")):
            metadata = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
            if str(metadata.get("purpose") or "corpus") != "corpus":
                continue
            if metadata.get("category") != category:
                raise ValueError(f"Category mismatch in {metadata_path.name}.")
            if metadata.get("capturePipeline") != "afe_processed_mono_v1":
                raise ValueError(f"Unexpected capture pipeline in {metadata_path.name}.")

            wave_path = metadata_path.with_suffix(".wav")
            if not wave_path.is_file():
                raise FileNotFoundError(f"Missing WAV for {metadata_path.name}.")
            digest = hashlib.sha256(wave_path.read_bytes()).hexdigest()
            if digest != metadata.get("sha256"):
                raise ValueError(f"Hash mismatch for {wave_path.name}.")
            if digest in seen_hashes:
                raise ValueError(f"Duplicate physical corpus audio: {wave_path.name}.")
            seen_hashes.add(digest)
            load_canonical_pcm(wave_path)
            corpus[category].append((wave_path, metadata))

        if len(corpus[category]) != expected_count:
            raise ValueError(
                f"Expected {expected_count} {category} corpus samples, "
                f"found {len(corpus[category])}."
            )
    return corpus


def physical_manifest(
    corpus: dict[str, list[tuple[Path, dict[str, object]]]],
    candidate: str,
) -> dict[str, object]:
    manifest: dict[str, object] = {
        "schemaVersion": 1,
        "featureSet": f"physical-corpus-{candidate}",
        "augmentationRepeats": PHYSICAL_FEATURE_REPEATS,
        "positiveAlignment": "vad_envelope_with_100ms_leading_and_200ms_trailing_v1",
        "samples": [
            {
                "id": metadata["id"],
                "category": category,
                "sha256": metadata["sha256"],
            }
            for category in PHYSICAL_CORPUS_COUNTS
            for _, metadata in corpus[category]
        ],
    }
    if candidate == "v2":
        manifest["hardNegativeAlignment"] = (
            "raw_full_capture_plus_vad_aligned_augmentation_v1"
        )
    return manifest


def prepare_physical_corpus_features(candidate: str) -> None:
    physical_feature_root = FEATURE_ROOT / f"physical-corpus-{candidate}"
    corpus = read_physical_corpus()
    manifest = physical_manifest(corpus, candidate)
    manifest_path = physical_feature_root / "manifest.json"
    feature_maps = [
        physical_feature_root / category / "training" / f"physical_{category.replace('-', '_')}_{kind}_mmap"
        for category in PHYSICAL_CORPUS_COUNTS
        for kind in ("raw", "augmented")
    ]
    if all(path.exists() for path in feature_maps) and manifest_path.is_file():
        existing = json.loads(manifest_path.read_text(encoding="utf-8"))
        if existing != manifest:
            raise RuntimeError(
                f"{physical_feature_root} does not match the current corpus. "
                "Move it aside before regenerating."
            )
        print("[features] physical corpus: using validated existing feature maps")
        return
    if any(path.exists() for path in feature_maps) or manifest_path.exists():
        raise RuntimeError(
            f"{physical_feature_root} contains a partial feature set. "
            "Move it aside before rerunning."
        )

    for category, samples in corpus.items():
        paths = [path for path, _ in samples]
        training_directory = physical_feature_root / category / "training"
        training_directory.mkdir(parents=True, exist_ok=True)
        label = category.replace("-", "_")

        raw_spectrograms = SpectrogramGeneration(
            clips=PhysicalCorpusClips(paths, align=category == "positive"),
            augmenter=None,
            slide_frames=4,
            step_ms=10,
        )
        print(f"[features] physical {category}: raw")
        RaggedMmap.from_generator(
            out_dir=str(training_directory / f"physical_{label}_raw_mmap"),
            sample_generator=raw_spectrograms.spectrogram_generator(),
            batch_size=100,
            verbose=True,
        )

        augmented_spectrograms = SpectrogramGeneration(
            clips=PhysicalCorpusClips(
                paths,
                align=category == "positive" or candidate == "v2",
            ),
            augmenter=feature_augmenter(truncate_randomly=candidate == "v1"),
            slide_frames=2,
            step_ms=10,
        )
        print(f"[features] physical {category}: augmented")
        RaggedMmap.from_generator(
            out_dir=str(training_directory / f"physical_{label}_augmented_mmap"),
            sample_generator=augmented_spectrograms.spectrogram_generator(
                repeat=PHYSICAL_FEATURE_REPEATS
            ),
            batch_size=100,
            verbose=True,
        )

    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("[features] physical corpus: 30 positive and 20 hard-negative samples prepared")


def generate_features(audio_directory: Path, feature_directory: Path, label: str) -> None:
    completed = [feature_directory / split / f"{label}_mmap" for split in ("training", "validation", "testing")]
    if all(path.exists() for path in completed):
        print(f"[features] {label}: using existing feature maps")
        return
    if any(path.exists() for path in completed):
        raise RuntimeError(
            f"{feature_directory} contains a partial feature set. Move it aside before rerunning."
        )

    clips = Clips(
        input_directory=str(audio_directory),
        file_pattern="*.wav",
        max_clip_duration_s=None,
        remove_silence=False,
        random_split_seed=26,
        split_count=0.10,
    )
    augmenter = feature_augmenter()

    for split in ("training", "validation", "testing"):
        split_directory = feature_directory / split
        split_directory.mkdir(parents=True, exist_ok=True)
        source_split = {"training": "train", "validation": "validation", "testing": "test"}[split]
        repeat = 2 if split == "training" else 1
        slide_frames = 1 if split == "testing" else 10
        spectrograms = SpectrogramGeneration(
            clips=clips,
            augmenter=augmenter,
            slide_frames=slide_frames,
            step_ms=10,
        )
        print(f"[features] {label}: {split}")
        RaggedMmap.from_generator(
            out_dir=str(split_directory / f"{label}_mmap"),
            sample_generator=spectrograms.spectrogram_generator(
                split=source_split,
                repeat=repeat,
            ),
            batch_size=100,
            verbose=True,
        )


def download(url: str, destination: Path, expected_size: int) -> None:
    if destination.exists() and destination.stat().st_size == expected_size:
        print(f"[ambient] using {destination.name}")
        return
    if destination.exists():
        raise RuntimeError(
            f"{destination} has an unexpected size. Move it aside before retrying."
        )

    destination.parent.mkdir(parents=True, exist_ok=True)
    partial = destination.with_suffix(destination.suffix + ".part")
    curl = shutil.which("curl")
    if not curl:
        raise RuntimeError("curl is required for resumable ambient dataset downloads.")

    print(f"[ambient] downloading {destination.name}")
    result = subprocess.run(
        [
            curl,
            "--location",
            "--fail",
            "--retry",
            "5",
            "--retry-all-errors",
            "--retry-delay",
            "2",
            "--speed-limit",
            "1024",
            "--speed-time",
            "60",
            "--continue-at",
            "-",
            "--output",
            str(partial),
            url,
        ],
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(
            f"Download failed for {destination.name} (curl exit {result.returncode})."
        )
    if partial.stat().st_size != expected_size:
        raise RuntimeError(
            f"Incomplete download for {destination.name}: {partial.stat().st_size}/{expected_size} bytes"
        )
    partial.replace(destination)


def safe_extract(archive: Path, destination: Path) -> None:
    marker = destination / f".{archive.stem}.complete"
    if marker.exists():
        print(f"[ambient] {archive.stem}: already extracted")
        return

    destination.mkdir(parents=True, exist_ok=True)
    destination_root = destination.resolve()
    print(f"[ambient] extracting {archive.name}")
    with zipfile.ZipFile(archive) as zipped:
        for member in zipped.infolist():
            member_path = (destination / member.filename).resolve()
            if destination_root not in member_path.parents and member_path != destination_root:
                raise RuntimeError(f"Unsafe archive member: {member.filename}")
        zipped.extractall(destination)
    marker.touch()


def prepare_ambient_features() -> None:
    archive_root = CACHE_ROOT / "downloads"
    for filename, expected_size in NEGATIVE_ARCHIVES.items():
        archive = archive_root / filename
        download(NEGATIVE_BASE_URL + filename, archive, expected_size)
        safe_extract(archive, NEGATIVE_ROOT)


def main() -> None:
    args = parse_args()
    if args.only_physical_corpus:
        prepare_physical_corpus_features(args.physical_candidate)
        print("Physical corpus preparation complete.")
        return
    if not GENERATOR_MODEL.exists():
        raise RuntimeError("Generator model is missing; run bootstrap.ps1 first.")

    generate_audio(TOOL_ROOT / "positive-phrases.txt", POSITIVE_AUDIO, args.positive_samples)
    generate_audio(
        TOOL_ROOT / "hard-negative-phrases.txt",
        HARD_NEGATIVE_AUDIO,
        args.hard_negative_samples,
    )
    generate_features(POSITIVE_AUDIO, FEATURE_ROOT / "positive", "positive")
    generate_features(HARD_NEGATIVE_AUDIO, FEATURE_ROOT / "hard-negative", "hard_negative")
    if not args.skip_ambient:
        prepare_ambient_features()

    print("Dataset preparation complete.")


if __name__ == "__main__":
    main()
