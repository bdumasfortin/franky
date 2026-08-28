# Local TTS and Franky Voice Evaluation

Status: **Research complete; bake-off and selection pending**

## Recommendation

Use the provider-neutral speech-synthesis boundary to run one repeatable local
bake-off rather than selecting a voice from short desktop samples.

Evaluate these candidates through the same board-output pipeline:

1. KittenTTS Mini 0.8 through sherpa-onnx.
2. Kokoro-82M through sherpa-onnx.
3. One English Piper-compatible voice whose exact model card passes license
   review.

Use Windows `System.Speech` as an unscored control. It establishes a useful
latency, reliability, and integration baseline, but installed voices vary by
computer and should not be treated as a reproducible Franky identity.

Do not select the engine or voice until its audio has been resampled to the
board contract and heard through Franky's physical speaker.

## Candidate tradeoffs

| Candidate | Why evaluate it | Main caveat |
| --- | --- | --- |
| KittenTTS Mini 0.8 through sherpa-onnx | Compact modern English model, official sherpa-onnx .NET path, CPU operation | 24 kHz output must be resampled and the exact downloaded archive/license must be pinned |
| Kokoro-82M through sherpa-onnx | Strong comparison for voice variety and perceived quality through the same runtime | Multiple model variants exist; package, checksum, voice, and model license must be explicit |
| Piper-compatible English voice | Mature local voice ecosystem and PCM output | Current OHF Piper runtime is GPL-3.0, native .NET integration is weaker, and every voice has its own license |
| Windows `System.Speech` control | Minimal deployment work and a useful OS-local floor | Windows-only and dependent on voices installed on one machine |

Sherpa-onnx is Apache-2.0 and provides an official prebuilt C#/.NET package, but
that runtime license does not replace the license of any selected model or
voice. Piper model licenses likewise require individual review.

## Standard corpus

Use the same 20–30 utterances for every voice, including:

- very short acknowledgements and failures;
- a normal two-sentence answer;
- a deliberately long answer that should later be chunked;
- “Franky”, people's names, place names, dates, times, units, file names, and
  abbreviations;
- command results that clearly distinguish requested, completed, denied, and
  unavailable outcomes;
- calm repair prompts and one urgent stop/privacy message.

Do not use generated scores as a substitute for listening. The corpus should be
heard both close to the board and at ordinary room distance.

Use these required samples inside that corpus:

1. “Ready.”
2. “The kitchen timer has five minutes left.”
3. “I couldn't reach the device, so I did not turn it off.”
4. “I heard office light. Did you mean the desk lamp?”
5. “Bryan, your appointment with Doctor Nguyen is Thursday, September
   seventeenth at two forty-five p.m.”
6. “Franky is running version ten point zero point one hundred. Device seven
   has one thousand twenty-four megabytes free and is at thirty-seven percent.”
7. One identical neutral 45–75-word response containing two sentences, a short
   list, and a caution.
8. One representative 20–30-second response played three times to expose
   fatigue or irritating mannerisms.

Randomize candidate labels and playback order, hide engine and voice names from
listeners where practical, loudness-normalize without changing delivery, and do
not tune individual samples after seeing scores.

## Listening score

Rate every criterion from 1–5. Its weighted result is `rating / 5 * weight`.

| Criterion | Weight | What to judge |
| --- | ---: | --- |
| Calm but alive character | 15 | Warm and attentive without sounding sleepy, theatrical, bubbly, or synthetic |
| Near-field intelligibility | 10 | Words understood on first hearing |
| Room-distance intelligibility | 15 | Clarity through Franky's actual speaker at normal placement |
| Warm time to first audio | 10 | Delay before useful speech begins |
| Cold-start latency | 5 | Recovery after engine or model unload |
| Short-reply delivery | 10 | Crisp without sounding abrupt or padded |
| Long-reply delivery | 10 | Natural pacing, phrasing, and sentence boundaries |
| Names, dates, and numbers | 10 | Accurate pronunciation and unambiguous grouping |
| Failure and confirmation tone | 5 | Serious and clear without sounding alarming or cheerful |
| Listening fatigue | 10 | Comfortable after repeated and longer playback |
| **Total** | **100** | |

## Measurements

Record the following for a pinned engine, model, and voice version:

- cold initialization time and memory use;
- warm time to first playable PCM;
- full synthesis time and real-time factor;
- CPU and GPU use;
- cancellation latency;
- chunk cadence and any speaker underruns;
- pronunciation mistakes, repetitions, skipped words, and boundary artifacts;
- intelligibility at room distance;
- listener ratings for calmness, aliveness, fatigue, and fit with Franky;
- output quality after conversion to 16 kHz, 16-bit, mono PCM;
- source URL, version, checksum, runtime license, and voice/model license.

## Decision gates

A candidate can be selected only when:

- synthesis is local after installation and has no silent cloud fallback;
- its runtime and exact voice/model license are acceptable for Franky's intended
  use and distribution;
- cancellation behaves truthfully;
- its output passes the runtime's PCM bounds;
- it is intelligible and appealing through the physical board, not only desktop
  headphones;
- the user explicitly approves the final voice.

As an evaluation rule, require at least 75/100 overall, at least 4/5 for
room-distance intelligibility, at least 4/5 for listening fatigue, and no
unresolved clipping or pronunciation fault in safety-relevant responses. If two
candidates finish within five points, repeat with reordered samples and a second
listener.

Selection is consequential and should be recorded in a new ADR. Research or a
successful prototype alone does not accept a provider.

## Primary references

- [sherpa-onnx C# API](https://k2-fsa.github.io/sherpa/onnx/csharp-api/index.html)
- [sherpa-onnx license](https://github.com/k2-fsa/sherpa-onnx/blob/master/LICENSE)
- [KittenTTS Mini 0.8 model card](https://huggingface.co/KittenML/kitten-tts-mini-0.8)
- [KittenTTS in sherpa-onnx](https://k2-fsa.github.io/sherpa/onnx/tts/all/English/kitten-mini-en-v0_8.html)
- [Kokoro-82M model card](https://huggingface.co/hexgrad/Kokoro-82M)
- [Kokoro models in sherpa-onnx](https://k2-fsa.github.io/sherpa/onnx/tts/pretrained_models/kokoro.html)
- [OHF Piper project](https://github.com/OHF-Voice/piper1-gpl)
- [Piper voice-license guidance](https://github.com/OHF-Voice/piper1-gpl/blob/main/docs/VOICES.md)
- [System.Speech package](https://www.nuget.org/packages/System.Speech)
