"""Prepare synthetic and ambient features for the Yo Franky wake-word model."""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

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


def feature_augmenter() -> Augmentation:
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
    )


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
