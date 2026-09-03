# Franky Control Board

A local control board for Franky, the DIY voice assistant running on the Waveshare ESP32-S3-AUDIO-Board. Its state-driven interface includes an animated system core, a persistent event terminal, and separate Audio, LED, Wake, and Device areas. The offline screen stays focused on connecting to Franky.

The Audio area captures both onboard microphones as 16 kHz, 16-bit PCM over USB and creates playable WAV clips in the browser. It can listen to either microphone, a mono mix, or raw stereo, with selectable hardware input gain.

The firmware continuously recognizes its active wake phrase on the ESP32-S3.
The current custom build reports microWakeWord and **“Yo Franky”**; a clean
build without the ignored model reports the **“Hi ESP”** WakeNet fallback. The
page reads that identity from the board instead of hardcoding either phrase.
The speaker plays a short rising cue when the browser connects, the matching
descending cue when it disconnects, and a brighter acknowledgement when the
wake word is heard. After a detection, ESP-SR voice activity detection listens
until speech ends, sends a bounded mono utterance to the computer, and local
Whisper transcription appears in the Wake area and terminal. Manual raw-stereo
recording remains available between wake detections.

The current diagnostic firmware advertises temporary wake-threshold and
near-miss-score capabilities. In the Wake area, **Show near misses** reports the
highest smoothed model score for a candidate without saving or transferring
room audio. **Temporary cutoff** changes the custom detector from 50–99% over
USB and returns to the 96% default after a reboot. These controls are disabled
for firmware that does not advertise support and for the WakeNet fallback.

The **Dataset** area is the explicit, local-only collection surface for fixing
the current custom-model miss rate. It captures the exact processed mono stream
used by the wake model for three seconds, then requires **Keep sample** before
anything is written. **Discard and retry** writes nothing. Kept WAV files and
metadata sidecars remain under the ignored
`tools/wake-word/.cache/recordings` directory; individual and confirmed
whole-dataset deletion are available. The collector does not transcribe,
upload, or automatically train on these samples.

After a non-empty wake transcript, the same loopback service now sends the text
through Franky's conversation provider. Model tool calls remain constrained to
the fixed named-command allowlist, and the Wake area displays the transcript,
structured action outcome, and Franky reply separately. The current proof
commands report the operating-system account running Franky and the installed
.NET SDK version. A separate fixed device action maps questions such as “How is
it going?” to the embedded `frankys_suuuper` clip. The browser sends only the
allowlisted firmware command and does not show playback as successful until the
board returns `SFX_DONE`. Common short wording and contraction variants are
matched locally before Ollama so the signature response is not dependent on a
probabilistic tool choice.

The same loopback service serves **Franky Presence** at `/presence/`. Choose
**Presence ↗** from the control board to open the passive, room-readable page.
While this USB control tab is open, it publishes ephemeral lifecycle snapshots
to that page over a same-origin browser channel. The passive page has no serial
access, commands, settings, or stored conversation history and goes offline
within 3.5 seconds if the control tab stops publishing.

## Run

1. Flash `firmware/franky-device` to the board.
2. Install Ollama and run `ollama pull qwen3.5:4b` once. See the
   [local provider guide](../../docs/development/local-ollama.md).
3. Run `./serve.ps1` from this directory. It selects local Ollama by default and
   starts the .NET control-board service on loopback. The first run downloads
   the `small.en` Whisper model (roughly 466 MiB) to `%LOCALAPPDATA%\Franky\models`.
4. In the Chromium-based page that opens, choose **Connect to Franky** and select the Espressif USB serial device.
   After that first authorization, the page attempts to reopen the same
   Espressif port automatically after a physical unplug/replug or page reload.
   If automatic reopen fails, choose **Connect to Franky** again.
5. Say the phrase shown in the Wake area—**“Yo Franky”** on the current custom
   build—wait for the acknowledgement, and speak naturally. Franky stops after
   trailing silence, shows the local transcript, and processes it as an
   assistant turn. Try “What version of .NET are you running?” or “Which user
   account are you running as?” Ask “How is it going?” to request the embedded
   Franky clip. Open **Presence ↗** to observe the same live lifecycle in a
   separate passive page. Use the Audio area for manual recordings.

For sensitivity diagnosis, open **Wake**, enable **Show near misses**, and leave
the cutoff at 96% for the first set of attempts. Record every reported peak,
including successful detections. Then compare the same phrase, position, and
speaking style at 87%. Lower thresholds may increase false activations; they are
temporary evaluation settings, not accepted production tuning.

Manual clips remain in browser memory unless downloaded. Wake clips are
discarded after transcription and transcript text is not persisted. Audio stays
local. The default Ollama conversation also stays on this computer. When the
optional OpenAI provider is enabled, transcript text and conversation responses
cross the cloud boundary described in the
[OpenAI development notes](../../docs/development/openai-api.md).

The first approved wake corpus of 30 positives and 20 hard negatives has been
collected through the guided prompts and evaluated locally. At the current 96%
cutoff, the deployed model detected 25 positives and activated on five hard
negatives; no tested 50–99% cutoff separated the classes. The separate
[wake-word collection guide](../../docs/development/wake-word-data-collection.md)
documents privacy, the complete score table, and the retraining gate.

Switch modes explicitly when needed:

```powershell
.\serve.ps1 -AssistantProvider demo
$env:OPENAI_API_KEY = "your-api-key"
.\serve.ps1 -AssistantProvider openai
```
