const els = {
  appRoot: document.querySelector('#appRoot'),
  connectButton: document.querySelector('#connectButton'),
  disconnectButton: document.querySelector('#disconnectButton'),
  coreCenter: document.querySelector('#coreCenter'),
  coreReadout: document.querySelector('#coreReadout'),
  wakeEngineReadout: document.querySelector('#wakeEngineReadout'),
  linkReadout: document.querySelector('#linkReadout'),
  featureTitle: document.querySelector('#featureTitle'),
  featureMeta: document.querySelector('#featureMeta'),
  featureTabs: [...document.querySelectorAll('[data-feature]')],
  featurePanels: [...document.querySelectorAll('[data-feature-panel]')],
  labelInput: document.querySelector('#labelInput'),
  durationSelect: document.querySelector('#durationSelect'),
  channelSelect: document.querySelector('#channelSelect'),
  gainSelect: document.querySelector('#gainSelect'),
  recordButton: document.querySelector('#recordButton'),
  stopButton: document.querySelector('#stopButton'),
  captureStatus: document.querySelector('#captureStatus'),
  captureDetail: document.querySelector('#captureDetail'),
  recordings: document.querySelector('#recordings'),
  emptyState: document.querySelector('#emptyState'),
  clearButton: document.querySelector('#clearButton'),
  stateButtons: [...document.querySelectorAll('.state-chip')],
  wakeMonitor: document.querySelector('#wakeMonitor'),
  wakeStatus: document.querySelector('#wakeStatus'),
  wakeCount: document.querySelector('#wakeCount'),
  lastWake: document.querySelector('#lastWake'),
  sttStatus: document.querySelector('#sttStatus'),
  assistantStatus: document.querySelector('#assistantStatus'),
  lastTranscript: document.querySelector('#lastTranscript'),
  transcriptMeta: document.querySelector('#transcriptMeta'),
  lastReply: document.querySelector('#lastReply'),
  assistantMeta: document.querySelector('#assistantMeta'),
  wakeCaptureMode: document.querySelector('#wakeCaptureMode'),
  deviceConnection: document.querySelector('#deviceConnection'),
  deviceAudioFormat: document.querySelector('#deviceAudioFormat'),
  firmwareReadout: document.querySelector('#firmwareReadout'),
  wakeEngineDetail: document.querySelector('#wakeEngineDetail'),
  terminalLog: document.querySelector('#terminalLog'),
  terminalPauseButton: document.querySelector('#terminalPauseButton'),
  terminalClearButton: document.querySelector('#terminalClearButton'),
  template: document.querySelector('#recordingTemplate'),
};

const textEncoder = new TextEncoder();
const textDecoder = new TextDecoder();
const objectUrls = new Set();
const statePresentation = {
  offline: { label: 'Offline', readout: 'Disconnected' },
  idle: { label: 'Idle', readout: 'Ready' },
  listening: { label: 'Listening', readout: 'Hearing' },
  processing: { label: 'Processing', readout: 'Thinking' },
  speaking: { label: 'Speaking', readout: 'Voice' },
  success: { label: 'Success', readout: 'Complete' },
  error: { label: 'Error', readout: 'Attention' },
  updating: { label: 'Updating', readout: 'Update' },
};
const featurePresentation = {
  audio: ['Audio testing', 'Ready'],
  leds: ['LED testing', 'Preview'],
  wake: ['Wake activity', 'Armed'],
  device: ['Device information', 'Online'],
};
const FRANKY_SUUUPER_ACTION = 'device.sfx.frankys_suuuper';
const FRANKY_SUUUPER_SFX = 'frankys_suuuper';
const SFX_PLAYBACK_TIMEOUT_MS = 12000;

let port;
let reader;
let writer;
let readBuffer = new Uint8Array();
let audioHeader;
let recordingContext;
let countdownTimer;
let heartbeatTimer;
let wakePulseTimer;
let transcriptStateTimer;
let transcriptionStatusTimer;
let assistantStatusTimer;
let heartbeatInFlight = false;
let disconnecting = false;
let terminalPaused = false;
let transcriptionInFlight = false;
let transcriptionSequence = 0;
let pendingSfxPlayback;
let wakeCount = 0;
let currentState = 'offline';
let wakeProfile = {
  engineId: 'microwakeword',
  engineLabel: 'microWakeWord',
  phraseId: 'yo_franky',
  phraseLabel: 'Yo Franky',
};

function setWakeProfile(engineId, phraseId) {
  const phraseLabels = { yo_franky: 'Yo Franky', hi_esp: 'Hi ESP' };
  const engineLabels = { microwakeword: 'microWakeWord', wakenet9: 'WakeNet9' };
  wakeProfile = {
    engineId,
    engineLabel: engineLabels[engineId] || engineId,
    phraseId,
    phraseLabel: phraseLabels[phraseId] || phraseId.replaceAll('_', ' '),
  };
  els.wakeEngineDetail.textContent = wakeProfile.engineLabel;
  if (isConnected() && !recordingContext && !transcriptionInFlight) {
    els.wakeStatus.textContent = `Listening for “${wakeProfile.phraseLabel}”`;
  }
}

function isConnected() {
  return els.appRoot.dataset.connected === 'true';
}

function timeStamp() {
  return new Date().toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
}

function appendStateAwareText(container, message) {
  const statePattern = new RegExp(`\\b(${Object.keys(statePresentation).join('|')})\\b`, 'gi');
  let cursor = 0;

  for (const match of message.matchAll(statePattern)) {
    if (match.index > cursor) container.append(message.slice(cursor, match.index));

    const keyword = document.createElement('span');
    keyword.className = 'state-keyword';
    keyword.dataset.state = match[0].toLowerCase();
    keyword.textContent = match[0];
    container.append(keyword);
    cursor = match.index + match[0].length;
  }

  if (cursor < message.length) container.append(message.slice(cursor));
}

function appendTerminal(kind, message, style = '', eventState = '') {
  const line = document.createElement('div');
  line.className = `log-line ${style}`.trim();
  if (statePresentation[eventState]) line.dataset.state = eventState;

  const time = document.createElement('span');
  time.className = 'log-time';
  time.textContent = timeStamp();

  const category = document.createElement('span');
  category.className = 'log-kind';
  category.textContent = kind;

  const content = document.createElement('span');
  content.className = 'log-message';
  appendStateAwareText(content, message);

  line.append(time, category, content);
  els.terminalLog.append(line);

  while (els.terminalLog.children.length > 300) {
    els.terminalLog.firstElementChild.remove();
  }
  if (!terminalPaused) els.terminalLog.scrollTop = els.terminalLog.scrollHeight;
}

function setSystemState(state, { announce = true } = {}) {
  if (!statePresentation[state]) return;

  const changed = state !== currentState;
  currentState = state;
  els.appRoot.dataset.state = state;
  els.coreCenter.textContent = statePresentation[state].label;
  els.coreReadout.textContent = statePresentation[state].readout;
  for (const button of els.stateButtons) {
    button.classList.toggle('active', button.dataset.state === state);
  }

  if (announce && changed && isConnected()) {
    appendTerminal('STATE', `Franky changed to ${state}`, 'state-line', state);
  }
}

function setCaptureStatus(status, detail) {
  els.captureStatus.textContent = status;
  els.captureDetail.textContent = detail;
}

function setConnection(connected, { announce = true } = {}) {
  els.appRoot.dataset.connected = String(connected);
  els.connectButton.disabled = connected;
  els.connectButton.textContent = connected ? 'Connected' : 'Connect to Franky';
  els.disconnectButton.disabled = !connected;
  els.recordButton.disabled = !connected;
  els.stopButton.disabled = true;
  for (const button of els.stateButtons) button.disabled = !connected;

  if (connected) {
    els.linkReadout.textContent = 'Stable';
    els.wakeEngineReadout.textContent = 'Armed';
    els.deviceConnection.textContent = 'USB serial · connected';
    els.wakeStatus.textContent = `Listening for “${wakeProfile.phraseLabel}”`;
    setCaptureStatus('Ready', '16 kHz · 16-bit · raw stereo');
    setSystemState('idle', { announce });
    void refreshTranscriptionStatus();
    void refreshAssistantStatus();
  } else {
    els.linkReadout.textContent = 'Down';
    els.wakeEngineReadout.textContent = 'Offline';
    els.deviceConnection.textContent = 'USB serial · disconnected';
    els.wakeStatus.textContent = 'Wake engine offline';
    els.wakeEngineDetail.textContent = 'Offline';
    els.sttStatus.textContent = 'Offline';
    els.assistantStatus.textContent = 'Offline';
    setCaptureStatus('Offline', 'Connect to Franky to begin');
    setSystemState('offline', { announce: false });
    clearTimeout(transcriptionStatusTimer);
    transcriptionStatusTimer = undefined;
    clearTimeout(assistantStatusTimer);
    assistantStatusTimer = undefined;
  }
}

async function refreshAssistantStatus() {
  clearTimeout(assistantStatusTimer);
  assistantStatusTimer = undefined;
  try {
    const response = await fetch('/api/assistant/status', { cache: 'no-store' });
    if (!response.ok) throw new Error(`status ${response.status}`);
    const status = await response.json();
    els.assistantStatus.textContent = status.toolSelectionEnabled ? 'Ready · tools' : 'Demo · no tools';
    els.assistantMeta.textContent = status.toolSelectionEnabled
      ? `${status.provider} · model-selected named capabilities`
      : 'Local demo · configure FRANKY_ASSISTANT_PROVIDER to enable model-selected commands';
  } catch {
    els.assistantStatus.textContent = 'Unavailable';
    els.assistantMeta.textContent = 'Start Franky with the control-board runtime';
  }
}

async function refreshTranscriptionStatus() {
  clearTimeout(transcriptionStatusTimer);
  transcriptionStatusTimer = undefined;
  try {
    const response = await fetch('/api/transcriptions/status', { cache: 'no-store' });
    if (!response.ok) throw new Error(`status ${response.status}`);
    const status = await response.json();
    const labels = {
      preparing: 'Preparing',
      downloading: 'Downloading',
      loading: 'Loading',
      ready: 'Ready · local',
      error: 'Needs attention',
    };
    els.sttStatus.textContent = labels[status.state] || status.state;
    els.transcriptMeta.textContent = `${status.model || 'Local Whisper'} · ${status.detail}`;
    if (!status.isReady && status.state !== 'error') {
      transcriptionStatusTimer = setTimeout(refreshTranscriptionStatus, 2000);
    }
  } catch {
    els.sttStatus.textContent = 'Unavailable';
    els.transcriptMeta.textContent = 'Start Franky with the local transcription service';
  }
}

function selectFeature(feature) {
  if (!featurePresentation[feature]) return;
  for (const tab of els.featureTabs) tab.classList.toggle('active', tab.dataset.feature === feature);
  for (const panel of els.featurePanels) {
    panel.classList.toggle('active', panel.dataset.featurePanel === feature);
  }
  els.featureTitle.textContent = featurePresentation[feature][0];
  els.featureMeta.textContent = featurePresentation[feature][1];
}

function appendBytes(left, right) {
  const result = new Uint8Array(left.length + right.length);
  result.set(left);
  result.set(right, left.length);
  return result;
}

function newlineIndex(bytes) {
  for (let index = 0; index < bytes.length; index += 1) {
    if (bytes[index] === 10) return index;
  }
  return -1;
}

function processReadBuffer() {
  while (readBuffer.length > 0) {
    if (audioHeader) {
      if (readBuffer.length < audioHeader.byteCount) return;

      const pcm = readBuffer.slice(0, audioHeader.byteCount);
      readBuffer = readBuffer.slice(audioHeader.byteCount);
      const completedHeader = audioHeader;
      audioHeader = undefined;
      addRecording(completedHeader, pcm);
      continue;
    }

    const lineEnd = newlineIndex(readBuffer);
    if (lineEnd < 0) return;

    const line = textDecoder.decode(readBuffer.slice(0, lineEnd)).replace(/\r$/, '').trim();
    readBuffer = readBuffer.slice(lineEnd + 1);
    handleLine(line);
  }
}

function handleLine(line) {
  if (!line || line === 'END') return;

  if (line.startsWith('READY FRANKY_DEVICE') || line.startsWith('READY AUDIO_PROBE')) {
    const parts = line.split(/\s+/);
    const sampleRate = Number(parts[3] ?? 16000);
    const channels = Number(parts[4] ?? 2);
    const bits = Number(parts[5] ?? 16);
    const gain = Number(parts[6] ?? 30);
    const protocolVersion = Number(parts[2] ?? 0);
    if (Number.isFinite(gain)) els.gainSelect.value = String(gain);
    if (Number.isFinite(protocolVersion)) els.firmwareReadout.textContent = `Franky Device v${protocolVersion}`;
    els.deviceAudioFormat.textContent = `${sampleRate / 1000} kHz · ${bits}-bit · ${channels === 2 ? 'stereo' : `${channels} ch`}`;
    setConnection(true, { announce: false });
    startHeartbeat();
    appendTerminal('LINK', 'Franky connected over USB serial');
    appendTerminal('AUDIO', `Microphone array ready · ${sampleRate / 1000} kHz · ${channels === 2 ? 'stereo' : `${channels} channels`} · ${gain} dB`);
    return;
  }

  if (line.startsWith('WAKE_ENGINE ')) {
    const [, engineId, phraseId] = line.split(/\s+/);
    if (!engineId || !phraseId) return;
    setWakeProfile(engineId, phraseId);
    appendTerminal('WAKE', `${wakeProfile.engineLabel} armed · phrase “${wakeProfile.phraseLabel}”`);
    return;
  }

  if (line.startsWith('GAIN ')) {
    const gain = Number(line.split(/\s+/)[1]);
    if (!recordingContext) setCaptureStatus('Ready', `${gain} dB input gain`);
    appendTerminal('AUDIO', `Input gain changed to ${gain} dB`);
    return;
  }

  if (line.startsWith('SFX_START ')) {
    const sfxName = line.split(/\s+/)[1];
    if (!sfxName) return;
    setSystemState('speaking');
    setCaptureStatus('Speaking', 'Franky is playing the requested sound');
    els.wakeStatus.textContent = 'Franky is speaking…';
    appendTerminal('ACTION', `${sfxName} · playing`, 'action-line');
    return;
  }

  if (line.startsWith('SFX_DONE ')) {
    const sfxName = line.split(/\s+/)[1];
    if (!sfxName) return;
    if (pendingSfxPlayback?.name === sfxName) {
      const pending = pendingSfxPlayback;
      pendingSfxPlayback = undefined;
      clearTimeout(pending.timeout);
      pending.resolve();
    }
    return;
  }

  if (line.startsWith('WAKE ')) {
    const phraseId = line.split(/\s+/)[1];
    if (!phraseId) return;
    if (phraseId !== wakeProfile.phraseId) setWakeProfile(wakeProfile.engineId, phraseId);
    wakeCount += 1;
    const detectedAt = new Date();
    els.wakeCount.textContent = String(wakeCount).padStart(2, '0');
    els.lastWake.textContent = detectedAt.toLocaleTimeString([], {
      hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false,
    });
    els.wakeStatus.textContent = `Heard “${wakeProfile.phraseLabel}”`;
    els.wakeMonitor.classList.add('wake-monitor-detected');
    clearTimeout(wakePulseTimer);
    wakePulseTimer = setTimeout(() => els.wakeMonitor.classList.remove('wake-monitor-detected'), 900);
    recordingContext = {
      label: `Wake ${wakeCount} · ${wakeProfile.phraseLabel}`,
      channelMode: 'stereo',
      gain: Number(els.gainSelect.value),
      source: 'wake',
    };
    els.recordButton.disabled = true;
    els.stopButton.disabled = true;
    for (const button of els.stateButtons) button.disabled = true;
    setSystemState('success');
    setCaptureStatus('Wake word detected', 'Waiting for your voice');
    appendTerminal('WAKE', `Detected “${wakeProfile.phraseLabel}” · waiting for speech`);
    return;
  }

  if (line.startsWith('UTTERANCE_START ')) {
    finishCapture();
    els.recordButton.disabled = true;
    for (const button of els.stateButtons) button.disabled = true;
    els.wakeStatus.textContent = 'Listening…';
    els.wakeCaptureMode.textContent = 'Listening until silence';
    setCaptureStatus('Listening', 'Speak naturally · Franky stops when you finish');
    setSystemState('listening');
    appendTerminal('WAKE', 'Listening for one complete utterance');
    return;
  }

  if (line.startsWith('UTTERANCE_END ')) {
    const reason = line.split(/\s+/)[1];
    els.wakeStatus.textContent = 'Transcribing locally…';
    els.wakeCaptureMode.textContent = 'Utterance captured';
    setCaptureStatus('Processing', reason === 'max_duration' ? '20-second safety cap reached' : 'Speech ended naturally');
    setSystemState('processing');
    appendTerminal('WAKE', reason === 'max_duration' ? 'Speech capture reached its safety cap' : 'Speech ended · local transcription next');
    return;
  }

  if (line === 'NO_SPEECH') {
    finishCapture();
    recordingContext = undefined;
    els.wakeStatus.textContent = `Nothing heard · listening for “${wakeProfile.phraseLabel}”`;
    els.wakeCaptureMode.textContent = 'Natural endpointing enabled';
    els.transcriptMeta.textContent = 'Latest wake contained no speech';
    setCaptureStatus('Ready', 'No speech followed the wake word');
    setSystemState('idle');
    appendTerminal('WAKE', 'Nothing heard after the wake word');
    return;
  }

  if (line.startsWith('RECORDING ')) {
    const durationMs = Number(line.split(' ')[1]);
    beginCountdown(durationMs);
    appendTerminal('AUDIO', `Capture started · maximum ${(durationMs / 1000).toFixed(1)} seconds`);
    return;
  }

  if (line.startsWith('STATE ')) {
    const nextState = line.split(/\s+/)[1];
    if (!(transcriptionInFlight && nextState === 'idle')) setSystemState(nextState);
    return;
  }

  if (line === 'STOPPING') {
    setCaptureStatus('Stopping', 'Finishing the WAV clip');
    appendTerminal('AUDIO', 'Stop requested · finishing capture');
    return;
  }

  if (line.startsWith('AUDIO ')) {
    const [, byteCount, sampleRate, channels, bits] = line.split(/\s+/).map(Number);
    audioHeader = { byteCount, sampleRate, channels, bits };
    setCaptureStatus('Receiving audio', `${(byteCount / 1024).toFixed(0)} KB over USB`);
    setSystemState('processing');
    appendTerminal('AUDIO', `Receiving ${(byteCount / 1024).toFixed(0)} KB from Franky`);
    return;
  }

  if (line === 'BYE') {
    appendTerminal('LINK', 'Franky acknowledged disconnect');
    return;
  }

  if (line.startsWith('ERROR ')) {
    finishCapture();
    const message = line.slice(6).replaceAll('_', ' ');
    if (pendingSfxPlayback) {
      const pending = pendingSfxPlayback;
      pendingSfxPlayback = undefined;
      clearTimeout(pending.timeout);
      pending.reject(new Error(message));
    }
    setSystemState('error');
    setCaptureStatus('Board error', message);
    appendTerminal('ERROR', message, 'error-line');
    return;
  }

  if (/^[\x20-\x7E]+$/.test(line)) appendTerminal('BOARD', line);
}

async function readLoop() {
  try {
    while (port?.readable) {
      const activeReader = port.readable.getReader();
      reader = activeReader;
      try {
        while (true) {
          const { value, done } = await activeReader.read();
          if (done) break;
          if (value) {
            readBuffer = appendBytes(readBuffer, value);
            processReadBuffer();
          }
        }
      } finally {
        activeReader.releaseLock();
        if (reader === activeReader) reader = undefined;
      }
    }
  } catch (error) {
    if (port && !disconnecting) appendTerminal('ERROR', `USB connection lost · ${error.message}`, 'error-line');
  } finally {
    if (port && !disconnecting) await disconnect({ notifyBoard: false, reason: 'USB connection lost' });
  }
}

async function sendCommand(command) {
  if (!writer) throw new Error('Franky is not connected.');
  await writer.write(textEncoder.encode(`${command}\n`));
}

async function playDeviceSfx(sfxName) {
  if (sfxName !== FRANKY_SUUUPER_SFX) throw new Error('That device sound is not allowlisted.');
  if (!writer || !isConnected()) throw new Error('Franky is not connected.');
  if (pendingSfxPlayback) throw new Error('Franky is already playing a sound.');

  let resolvePlayback;
  let rejectPlayback;
  const completion = new Promise((resolve, reject) => {
    resolvePlayback = resolve;
    rejectPlayback = reject;
  });
  const timeout = setTimeout(() => {
    if (pendingSfxPlayback?.name !== sfxName) return;
    const pending = pendingSfxPlayback;
    pendingSfxPlayback = undefined;
    pending.reject(new Error('The board did not acknowledge playback in time.'));
  }, SFX_PLAYBACK_TIMEOUT_MS);
  pendingSfxPlayback = {
    name: sfxName,
    resolve: resolvePlayback,
    reject: rejectPlayback,
    timeout,
  };

  try {
    await sendCommand(`SFX ${sfxName}`);
    await completion;
  } catch (error) {
    if (pendingSfxPlayback?.name === sfxName) {
      clearTimeout(pendingSfxPlayback.timeout);
      pendingSfxPlayback = undefined;
    }
    throw error;
  }
}

function stopHeartbeat() {
  clearInterval(heartbeatTimer);
  heartbeatTimer = undefined;
}

function startHeartbeat() {
  stopHeartbeat();
  heartbeatTimer = setInterval(async () => {
    if (!port || !writer || heartbeatInFlight || disconnecting) return;
    heartbeatInFlight = true;
    try {
      await sendCommand('PING');
    } catch {
      // readLoop or the serial disconnect event owns the visible state change.
    } finally {
      heartbeatInFlight = false;
    }
  }, 1000);
}

async function connect() {
  if (!('serial' in navigator)) return;

  els.connectButton.disabled = true;
  els.connectButton.textContent = 'Connecting…';
  try {
    port = await navigator.serial.requestPort({ filters: [{ usbVendorId: 0x303a }] });
    await port.open({ baudRate: 115200, bufferSize: 65536 });
    writer = port.writable.getWriter();
    readBuffer = new Uint8Array();
    appendTerminal('LINK', 'Opening USB serial connection');
    void readLoop();
    await new Promise(resolve => setTimeout(resolve, 250));
    await sendCommand('HELLO');
  } catch (error) {
    appendTerminal('ERROR', `Could not connect · ${error.message}`, 'error-line');
    if (port) await disconnect({ notifyBoard: false, reason: 'Connection attempt ended' });
    else setConnection(false, { announce: false });
  } finally {
    if (!isConnected()) {
      els.connectButton.disabled = false;
      els.connectButton.textContent = 'Connect to Franky';
    }
  }
}

async function disconnect({ notifyBoard = true, reason = 'Disconnected from Franky' } = {}) {
  if (!port || disconnecting) return;

  disconnecting = true;
  stopHeartbeat();
  clearInterval(countdownTimer);
  countdownTimer = undefined;
  clearTimeout(transcriptStateTimer);
  transcriptStateTimer = undefined;
  transcriptionSequence += 1;
  transcriptionInFlight = false;
  if (pendingSfxPlayback) {
    const pending = pendingSfxPlayback;
    pendingSfxPlayback = undefined;
    clearTimeout(pending.timeout);
    pending.reject(new Error('Franky disconnected before playback finished.'));
  }

  if (notifyBoard) {
    try {
      await sendCommand('BYE');
      await new Promise(resolve => setTimeout(resolve, 450));
    } catch {
      // Abrupt disconnects are handled by the firmware timeout.
    }
  }

  const closingPort = port;
  const activeReader = reader;
  const activeWriter = writer;
  port = undefined;
  writer = undefined;
  reader = undefined;

  try { await activeReader?.cancel(); } catch { /* Port may already be gone. */ }
  try { activeWriter?.releaseLock(); } catch { /* Port may already be gone. */ }
  try { await closingPort?.close(); } catch { /* Port may already be gone. */ }

  finishCapture();
  recordingContext = undefined;
  audioHeader = undefined;
  setConnection(false, { announce: false });
  appendTerminal('LINK', reason);
  disconnecting = false;
}

function beginCountdown(durationMs) {
  const startedAt = performance.now();
  els.recordButton.classList.add('recording');
  els.recordButton.textContent = 'Recording';
  els.recordButton.disabled = true;
  els.stopButton.disabled = false;
  for (const button of els.stateButtons) button.disabled = true;
  setSystemState('listening');

  const update = () => {
    const remaining = Math.max(0, durationMs - (performance.now() - startedAt));
    setCaptureStatus('Recording', `${(remaining / 1000).toFixed(1)} seconds remaining`);
  };
  update();
  countdownTimer = setInterval(update, 100);
}

function finishCapture() {
  clearInterval(countdownTimer);
  countdownTimer = undefined;
  els.recordButton.classList.remove('recording');
  els.recordButton.textContent = 'Record audio';
  els.recordButton.disabled = !isConnected();
  els.stopButton.disabled = true;
  for (const button of els.stateButtons) button.disabled = !isConnected();
}

function writeAscii(view, offset, text) {
  for (let index = 0; index < text.length; index += 1) {
    view.setUint8(offset + index, text.charCodeAt(index));
  }
}

function createWavBlob(pcm, sampleRate, channels, bits) {
  const headerBytes = 44;
  const buffer = new ArrayBuffer(headerBytes + pcm.byteLength);
  const view = new DataView(buffer);
  const bytesPerSample = bits / 8;

  writeAscii(view, 0, 'RIFF');
  view.setUint32(4, 36 + pcm.byteLength, true);
  writeAscii(view, 8, 'WAVE');
  writeAscii(view, 12, 'fmt ');
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true);
  view.setUint16(22, channels, true);
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * channels * bytesPerSample, true);
  view.setUint16(32, channels * bytesPerSample, true);
  view.setUint16(34, bits, true);
  writeAscii(view, 36, 'data');
  view.setUint32(40, pcm.byteLength, true);
  new Uint8Array(buffer, headerBytes).set(pcm);
  return new Blob([buffer], { type: 'audio/wav' });
}

function selectChannels(pcm, inputChannels, mode) {
  if (inputChannels !== 2 || mode === 'stereo') {
    return { pcm, channels: inputChannels, label: 'raw stereo' };
  }

  const input = new DataView(pcm.buffer, pcm.byteOffset, pcm.byteLength);
  const frameCount = pcm.byteLength / 4;
  const outputBytes = new Uint8Array(frameCount * 2);
  const output = new DataView(outputBytes.buffer);

  for (let frame = 0; frame < frameCount; frame += 1) {
    const left = input.getInt16(frame * 4, true);
    const right = input.getInt16(frame * 4 + 2, true);
    let sample;
    if (mode === 'mic-a') sample = left;
    else if (mode === 'mic-b') sample = right;
    else sample = Math.round((left + right) / 2);
    output.setInt16(frame * 2, sample, true);
  }

  const labels = { mix: 'mono mix', 'mic-a': 'Mic A', 'mic-b': 'Mic B' };
  return { pcm: outputBytes, channels: 1, label: labels[mode] || 'mono mix' };
}

function calculateLevels(pcm) {
  const view = new DataView(pcm.buffer, pcm.byteOffset, pcm.byteLength);
  let peak = 0;
  let sumSquares = 0;
  const sampleCount = pcm.byteLength / 2;

  for (let offset = 0; offset < pcm.byteLength; offset += 2) {
    const value = view.getInt16(offset, true) / 32768;
    peak = Math.max(peak, Math.abs(value));
    sumSquares += value * value;
  }

  return {
    peakDb: peak ? 20 * Math.log10(peak) : -Infinity,
    rmsDb: sampleCount ? 20 * Math.log10(Math.sqrt(sumSquares / sampleCount)) : -Infinity,
  };
}

function formatDb(value) {
  return Number.isFinite(value) ? `${value.toFixed(1)} dBFS` : 'silent';
}

function drawWaveform(canvas, pcm, channels) {
  const ratio = window.devicePixelRatio || 1;
  const width = canvas.clientWidth;
  const height = canvas.clientHeight;
  canvas.width = Math.max(1, Math.round(width * ratio));
  canvas.height = Math.max(1, Math.round(height * ratio));
  const context = canvas.getContext('2d');
  context.scale(ratio, ratio);

  const view = new DataView(pcm.buffer, pcm.byteOffset, pcm.byteLength);
  const frameCount = pcm.byteLength / (2 * channels);
  const framesPerPixel = Math.max(1, Math.floor(frameCount / width));
  const colors = ['#82939d', '#536772'];

  context.strokeStyle = '#24313a';
  context.beginPath();
  context.moveTo(0, height / 2);
  context.lineTo(width, height / 2);
  context.stroke();

  for (let channel = 0; channel < channels; channel += 1) {
    context.strokeStyle = colors[channel] || colors[0];
    context.lineWidth = 1;
    context.globalAlpha = channels > 1 ? 0.78 : 1;
    context.beginPath();

    for (let x = 0; x < width; x += 1) {
      const startFrame = x * framesPerPixel;
      const endFrame = Math.min(frameCount, startFrame + framesPerPixel);
      let minimum = 1;
      let maximum = -1;

      for (let frame = startFrame; frame < endFrame; frame += 1) {
        const offset = (frame * channels + channel) * 2;
        const value = view.getInt16(offset, true) / 32768;
        minimum = Math.min(minimum, value);
        maximum = Math.max(maximum, value);
      }

      context.moveTo(x + 0.5, (1 - maximum) * height / 2);
      context.lineTo(x + 0.5, (1 - minimum) * height / 2);
    }
    context.stroke();
  }
  context.globalAlpha = 1;
}

function safeFilename(label) {
  const cleaned = label.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  return cleaned || 'franky-recording';
}

function addRecording(header, pcm) {
  finishCapture();
  const captureContext = recordingContext;
  const selected = selectChannels(pcm, header.channels, captureContext?.channelMode || 'mix');
  const blob = createWavBlob(selected.pcm, header.sampleRate, selected.channels, header.bits);
  const durationSeconds = selected.pcm.byteLength / (header.sampleRate * selected.channels * (header.bits / 8));
  const label = captureContext?.label || `Recording ${els.recordings.children.length + 1}`;
  const levels = calculateLevels(selected.pcm);
  const source = captureContext?.source;
  recordingContext = undefined;

  if (source === 'wake') {
    void transcribeWakeUtterance(blob, durationSeconds);
    return;
  }

  const url = URL.createObjectURL(blob);
  objectUrls.add(url);
  const fragment = els.template.content.cloneNode(true);
  const card = fragment.querySelector('.recording-card');
  const audio = fragment.querySelector('audio');
  const download = fragment.querySelector('.download-button');

  fragment.querySelector('.recording-label').textContent = label;
  fragment.querySelector('.recording-meta').textContent =
    `${durationSeconds.toFixed(1)} s · ${selected.label} · ${captureContext?.gain ?? 30} dB · ` +
    new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  fragment.querySelector('.levels').innerHTML =
    `<span class="level-pill">Peak ${formatDb(levels.peakDb)}</span>` +
    `<span class="level-pill">RMS ${formatDb(levels.rmsDb)}</span>`;
  audio.src = url;
  download.href = url;
  download.download = `${safeFilename(label)}.wav`;
  fragment.querySelector('.delete-button').addEventListener('click', () => {
    URL.revokeObjectURL(url);
    objectUrls.delete(url);
    card.remove();
    updateEmptyState();
  });

  els.recordings.prepend(fragment);
  requestAnimationFrame(() => drawWaveform(card.querySelector('.waveform'), selected.pcm, selected.channels));
  updateEmptyState();
  setCaptureStatus('Recording ready', 'Press play or start another capture');
  els.wakeStatus.textContent = `Listening for “${wakeProfile.phraseLabel}”`;
  appendTerminal('AUDIO', `Recording ready · ${durationSeconds.toFixed(1)} seconds · peak ${formatDb(levels.peakDb)} · RMS ${formatDb(levels.rmsDb)}`);
  if (isConnected()) setSystemState('idle');
}

async function transcribeWakeUtterance(wave, durationSeconds) {
  const sequence = ++transcriptionSequence;
  transcriptionInFlight = true;
  setSystemState('processing');
  setCaptureStatus('Processing', 'Transcribing locally on this computer');
  els.wakeStatus.textContent = 'Transcribing locally…';
  els.wakeCaptureMode.textContent = 'Local Whisper is processing';

  try {
    let transcription;
    try {
      const response = await fetch('/api/transcriptions', {
        method: 'POST',
        headers: { 'Content-Type': 'audio/wav' },
        body: wave,
      });
      transcription = await response.json();
      if (!response.ok) {
        throw new Error(transcription.detail || `Transcription failed (${response.status})`);
      }
    } catch (error) {
      if (sequence !== transcriptionSequence || !isConnected()) return;
      els.wakeStatus.textContent = 'Transcription failed · wake word will retry';
      els.wakeCaptureMode.textContent = 'Local transcription needs attention';
      setCaptureStatus('Transcription failed', error.message);
      setSystemState('error');
      appendTerminal('ERROR', `Could not transcribe speech · ${error.message}`, 'error-line');
      void refreshTranscriptionStatus();
      return;
    }

    if (sequence !== transcriptionSequence || !isConnected()) return;
    const transcript = String(transcription.text || '').trim();
    if (!transcript) {
      els.lastTranscript.textContent = 'Nothing understood.';
      els.transcriptMeta.textContent = `${transcription.model || 'Local Whisper'} · ${durationSeconds.toFixed(1)} s · no text returned`;
      els.wakeStatus.textContent = `No speech recognized · listening for “${wakeProfile.phraseLabel}”`;
      els.wakeCaptureMode.textContent = 'Natural endpointing enabled';
      setCaptureStatus('Ready', 'The clip did not contain recognizable speech');
      appendTerminal('HEARD', 'No recognizable speech in the utterance');
      setSystemState('idle');
      return;
    }

    els.lastTranscript.textContent = transcript;
    els.transcriptMeta.textContent =
      `${transcription.model || 'Local Whisper'} · ${durationSeconds.toFixed(1)} s audio · ${(transcription.elapsedMs / 1000).toFixed(1)} s processing`;
    appendTerminal('HEARD', transcript, 'transcript-line');

    try {
      await processAssistantTurn(transcript, sequence);
    } catch (error) {
      if (sequence !== transcriptionSequence || !isConnected()) return;
      els.lastReply.textContent = 'I could not complete that request.';
      els.assistantMeta.textContent = error.message;
      els.wakeStatus.textContent = `Assistant unavailable · listening for “${wakeProfile.phraseLabel}”`;
      els.wakeCaptureMode.textContent = 'The transcript was understood; the assistant turn failed';
      setCaptureStatus('Assistant failed', error.message);
      setSystemState('error');
      appendTerminal('ERROR', `Could not process the request · ${error.message}`, 'error-line');
      void refreshAssistantStatus();
    }
  } finally {
    if (sequence === transcriptionSequence) {
      transcriptionInFlight = false;
      finishCapture();
    }
  }
}

async function processAssistantTurn(transcript, sequence) {
  setSystemState('processing');
  setCaptureStatus('Understanding', 'Matching the request to Franky’s named capabilities');
  els.wakeStatus.textContent = 'Franky is thinking…';
  els.wakeCaptureMode.textContent = 'Model-selected command routing';
  appendTerminal('INTENT', 'Selecting an allowlisted capability', 'intent-line');

  const response = await fetch('/api/assistant/turns', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ text: transcript }),
  });
  const result = await response.json();
  if (!response.ok) throw new Error(result.detail || `Assistant request failed (${response.status})`);
  if (sequence !== transcriptionSequence || !isConnected()) return;

  const actions = Array.isArray(result.actions) ? result.actions : [];
  let actionFailed = false;
  for (const action of actions) {
    const name = String(action.name || 'unknown action');
    const success = action.success === true;

    if (name === FRANKY_SUUUPER_ACTION) {
      if (!success) {
        actionFailed = true;
        appendTerminal('ACTION', `${name} · rejected`, 'error-line');
        continue;
      }

      appendTerminal('ACTION', `${name} · requested`, 'action-line');
      try {
        await playDeviceSfx(FRANKY_SUUUPER_SFX);
        appendTerminal('ACTION', `${name} · success`, 'action-line');
      } catch (error) {
        actionFailed = true;
        appendTerminal('ERROR', `${name} · ${error.message}`, 'error-line');
      }
      continue;
    }

    if (!success) actionFailed = true;
    appendTerminal(
      'ACTION',
      `${name} · ${success ? 'success' : 'failed'}`,
      success ? 'action-line' : 'error-line');
  }

  if (sequence !== transcriptionSequence || !isConnected()) return;

  const reply = String(result.text || '').trim() || 'Request completed without a text response.';
  els.lastReply.textContent = reply;
  els.assistantMeta.textContent =
    `${result.provider || 'Franky'} · ${actions.length} named action${actions.length === 1 ? '' : 's'}`;
  els.wakeStatus.textContent = `Request complete · listening for “${wakeProfile.phraseLabel}”`;
  els.wakeCaptureMode.textContent = 'Natural endpointing enabled';
  setCaptureStatus('Request complete', 'Wake audio was discarded after local transcription');
  appendTerminal('FRANKY', reply, 'assistant-line');

  setSystemState(actionFailed ? 'error' : 'success');
  clearTimeout(transcriptStateTimer);
  transcriptStateTimer = setTimeout(() => {
    if (isConnected() && currentState === 'success') setSystemState('idle');
  }, 1400);
}

function updateEmptyState() {
  const hasRecordings = els.recordings.children.length > 0;
  els.emptyState.hidden = hasRecordings;
  els.clearButton.disabled = !hasRecordings;
}

for (const tab of els.featureTabs) {
  tab.addEventListener('click', () => selectFeature(tab.dataset.feature));
}

els.connectButton.addEventListener('click', connect);
els.disconnectButton.addEventListener('click', () => disconnect());

els.recordButton.addEventListener('click', async () => {
  const gain = Number(els.gainSelect.value);
  recordingContext = {
    label: els.labelInput.value.trim(),
    channelMode: els.channelSelect.value,
    gain,
    source: 'manual',
  };
  const durationMs = Number(els.durationSelect.value);
  els.recordButton.disabled = true;
  setCaptureStatus('Starting', 'Preparing both microphones');
  try {
    await sendCommand(`GAIN ${gain}`);
    await sendCommand(`RECORD ${durationMs}`);
  } catch (error) {
    finishCapture();
    setCaptureStatus('Could not start', error.message);
    appendTerminal('ERROR', error.message, 'error-line');
  }
});

els.stopButton.addEventListener('click', async () => {
  els.stopButton.disabled = true;
  try {
    await sendCommand('STOP');
  } catch (error) {
    appendTerminal('ERROR', error.message, 'error-line');
  }
});

for (const button of els.stateButtons) {
  button.addEventListener('click', async () => {
    try {
      await sendCommand(`STATE ${button.dataset.state}`);
      appendTerminal('LED', `Preview requested · ${button.dataset.state}`);
    } catch (error) {
      appendTerminal('ERROR', `Could not change LED state · ${error.message}`, 'error-line');
    }
  });
}

els.clearButton.addEventListener('click', () => {
  for (const url of objectUrls) URL.revokeObjectURL(url);
  objectUrls.clear();
  els.recordings.replaceChildren();
  updateEmptyState();
  appendTerminal('AUDIO', 'Session recordings cleared');
});

els.terminalPauseButton.addEventListener('click', () => {
  terminalPaused = !terminalPaused;
  els.terminalPauseButton.textContent = terminalPaused ? 'Resume' : 'Pause';
  els.terminalLog.classList.toggle('terminal-paused', terminalPaused);
  if (!terminalPaused) els.terminalLog.scrollTop = els.terminalLog.scrollHeight;
});

els.terminalClearButton.addEventListener('click', () => {
  els.terminalLog.replaceChildren();
});

navigator.serial?.addEventListener('disconnect', event => {
  if (event.target === port) void disconnect({ notifyBoard: false, reason: 'Franky disconnected unexpectedly' });
});

window.addEventListener('beforeunload', () => {
  stopHeartbeat();
  for (const url of objectUrls) URL.revokeObjectURL(url);
});

updateEmptyState();
setConnection(false, { announce: false });
selectFeature('audio');
appendTerminal('SYSTEM', 'Franky control board loaded · waiting for connection');
void refreshTranscriptionStatus();
void refreshAssistantStatus();

if (!('serial' in navigator)) {
  els.connectButton.disabled = true;
  els.connectButton.textContent = 'Web Serial unavailable';
}
