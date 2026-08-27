# Embedded SFX

`frankys-suuuper.pcm` is the firmware-ready version of the source MP3 supplied
by the repository owner. It is raw signed 16-bit little-endian PCM at 16 kHz,
mono, and lasts about 5.31 seconds. The firmware duplicates each sample to its
stereo 32-bit I2S output while playing it. The clip temporarily uses the
codec's maximum volume of 100, then restores the board-wide default of 80.

The conversion removes the source's attached 500 × 500 image, all text
metadata, its short leading silence, and unnecessary stereo/MP3 encoding. The
result contains audio samples only and has SHA-256:

```text
82F6F00BFCA566336C8FC358D3533F5DC2B09B7E946D72E48A4DD7C2B0FC7F00
```

The reproducible conversion uses FFmpeg:

```powershell
ffmpeg -i frankys-suuuper.mp3 -map 0:a:0 -vn -sn -dn -map_metadata -1 `
  -af "atrim=start=0.086644,asetpts=PTS-STARTPTS,loudnorm=I=-18:TP=-2:LRA=7,afade=t=in:st=0:d=0.01,afade=t=out:st=5.24:d=0.10" `
  -ar 16000 -ac 1 -c:a pcm_s16le -f s16le frankys-suuuper.pcm
```

The original MP3 is intentionally not stored in this repository. This derived
clip came from user-supplied media and is not automatically covered by the
project's MIT license; confirm redistribution rights before publishing it.
