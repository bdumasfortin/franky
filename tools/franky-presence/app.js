(() => {
  const phases = new Set([
    'offline',
    'idle',
    'listening',
    'transcribing',
    'processing',
    'acting',
    'speaking',
    'error'
  ]);
  const privacyStates = new Set(['available', 'muted', 'unknown']);
  const phaseLabels = Object.freeze({
    offline: 'Offline',
    idle: 'Ready',
    listening: 'Listening',
    transcribing: 'Transcribing',
    processing: 'Processing',
    acting: 'Acting',
    speaking: 'Speaking',
    privacy: 'Microphone off',
    error: 'Error'
  });
  const cycleDurations = Object.freeze({
    offline: 2800,
    idle: 5200,
    listening: 3400,
    transcribing: 3000,
    processing: 3600,
    acting: 3600,
    speaking: 6200,
    privacy: 4200,
    error: 4600,
    long: 12800
  });
  const liveDisconnectGraceMs = 3500;

  const presence = document.querySelector('#presence');
  const announcement = document.querySelector('#presenceAnnouncement');
  const phaseNode = presence.querySelector('[data-field="phase"]');
  const transcriptNode = presence.querySelector('[data-field="transcript"]');
  const replyNode = presence.querySelector('[data-field="reply"]');
  const activityNode = presence.querySelector('[data-field="activity"]');
  const params = new URLSearchParams(window.location.search);
  const reducedMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
  const mock = window.FrankyPresenceMock;

  let currentSequence = -1;
  let currentEvent = null;
  let phraseTimer = 0;
  let cycleTimer = 0;
  let liveDisconnectTimer = 0;
  let liveRequestTimer = 0;
  let liveChannel = null;
  let liveStale = false;
  let harnessReducedMotion = params.get('reducedMotion') === 'true' ? true : null;

  function optionalText(value) {
    if (value === null || value === undefined) return null;
    if (typeof value !== 'string') throw new TypeError('Display-event text fields must be strings or null.');
    const trimmed = value.trim();
    return trimmed.length ? trimmed : null;
  }

  function normalizeEvent(candidate) {
    if (!candidate || typeof candidate !== 'object') throw new TypeError('Display event must be an object.');
    if (candidate.version !== 1) throw new RangeError('Unsupported display-event version.');
    if (!Number.isSafeInteger(candidate.sequence) || candidate.sequence < 0) throw new RangeError('Display-event sequence must be a non-negative safe integer.');
    if (!phases.has(candidate.phase)) throw new RangeError('Display event has an unknown phase.');
    if (!privacyStates.has(candidate.privacy)) throw new RangeError('Display event has an unknown privacy state.');
    if (candidate.privacy === 'unknown' && candidate.phase !== 'offline' && candidate.phase !== 'error') {
      throw new RangeError('Unknown privacy state is valid only while Franky is offline or in error.');
    }
    if (candidate.turnId !== null && typeof candidate.turnId !== 'string') throw new TypeError('Display-event turnId must be a string or null.');
    const isoTimestamp = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?(?:Z|[+-]\d{2}:\d{2})$/;
    if (typeof candidate.occurredAt !== 'string' || !isoTimestamp.test(candidate.occurredAt) || Number.isNaN(Date.parse(candidate.occurredAt))) {
      throw new TypeError('Display-event occurredAt must be an ISO-8601 timestamp.');
    }

    return Object.freeze({
      version: 1,
      sequence: candidate.sequence,
      turnId: candidate.turnId,
      phase: candidate.phase,
      transcript: optionalText(candidate.transcript),
      reply: optionalText(candidate.reply),
      activity: optionalText(candidate.activity),
      privacy: candidate.privacy,
      occurredAt: candidate.occurredAt
    });
  }

  function visualPhase(event) {
    if (event.privacy === 'muted') return 'privacy';
    return event.phase;
  }

  function phraseChunks(text, maxWords = 15) {
    if (!text) return [];
    const words = text.trim().split(/\s+/);
    if (words.length <= maxWords) return [text.trim()];

    const chunks = [];
    let cursor = 0;
    while (cursor < words.length) {
      const hardEnd = Math.min(words.length, cursor + maxWords);
      let end = hardEnd;
      const minimum = Math.min(hardEnd, cursor + 8);
      for (let index = hardEnd - 1; index >= minimum; index -= 1) {
        if (/[,:;.!?]$/.test(words[index])) {
          end = index + 1;
          break;
        }
      }
      chunks.push(words.slice(cursor, end).join(' '));
      cursor = end;
    }
    return chunks;
  }

  function prefersReducedMotion() {
    return harnessReducedMotion === null ? reducedMotionQuery.matches : harnessReducedMotion;
  }

  function updateMotionPreference() {
    document.documentElement.dataset.reducedMotion = String(prefersReducedMotion());
  }

  function clearPhraseStaging() {
    window.clearTimeout(phraseTimer);
    phraseTimer = 0;
    presence.classList.remove('is-staging');
  }

  function stageReply(reply, phase) {
    clearPhraseStaging();
    const chunks = phraseChunks(reply);
    const shouldStage = phase === 'speaking' && chunks.length > 1 && !prefersReducedMotion();

    presence.classList.toggle('is-long-copy', chunks.length > 1 || (reply?.length ?? 0) > 130);
    if (!shouldStage) {
      replyNode.textContent = reply ?? '';
      return;
    }

    let index = 0;
    const showChunk = () => {
      replyNode.textContent = chunks[index];
      presence.classList.remove('is-staging');
      void presence.offsetWidth;
      presence.classList.add('is-staging');
      index = (index + 1) % chunks.length;
      phraseTimer = window.setTimeout(showChunk, 4200);
    };
    showChunk();
  }

  function accessibleSummary(event, phase) {
    if (phase === 'offline') return 'Franky is offline.';
    if (phase === 'privacy') return 'Microphone off.';
    if (phase === 'error') return `Error. ${event.activity ?? 'Franky is unavailable.'}`;
    if (phase === 'listening') return 'Listening.';
    if (phase === 'transcribing') return 'Transcribing.';

    const parts = [];
    if (event.transcript) parts.push(`Heard: ${event.transcript}`);
    if (event.reply) parts.push(`Franky: ${event.reply}`);
    if (event.activity && phase !== 'idle') parts.push(event.activity);
    return parts.join(' ') || phaseLabels[phase] || 'Franky status changed.';
  }

  function renderAcceptedEvent(event, { announce = true } = {}) {
    const phase = visualPhase(event);
    currentEvent = event;
    presence.dataset.phase = phase;
    presence.dataset.privacy = event.privacy;
    presence.dataset.sequence = String(event.sequence);
    phaseNode.textContent = phaseLabels[phase] ?? phase;
    transcriptNode.textContent = event.transcript ?? '';
    activityNode.textContent = phase === 'error'
      ? event.activity ?? 'Franky is unavailable'
      : event.activity ?? '';
    stageReply(event.reply, phase);

    presence.classList.remove('is-transitioning');
    void presence.offsetWidth;
    presence.classList.add('is-transitioning');
    document.title = `Franky — ${phaseLabels[phase] ?? phase}`;
    announcement.setAttribute('aria-live', announce ? 'polite' : 'off');
    announcement.textContent = accessibleSummary(event, phase);
  }

  function acceptEvent(candidate, options) {
    let event;
    try {
      event = normalizeEvent(candidate);
    } catch {
      return false;
    }
    if (event.sequence <= currentSequence) return false;
    currentSequence = event.sequence;
    renderAcceptedEvent(event, options);
    return true;
  }

  function occurredAtFor(sequence) {
    const base = Date.parse('2026-08-27T20:42:00-04:00');
    return new Date(base + sequence * 1000).toISOString();
  }

  function startFixedMock(key) {
    const event = mock.createEvent(key, 1, occurredAtFor(1));
    acceptEvent(event, { announce: false });
  }

  function startMockCycle() {
    const keys = mock.keys;
    let index = 0;
    let sequence = 0;

    const emitNext = () => {
      const key = keys[index];
      sequence += 1;
      acceptEvent(mock.createEvent(key, sequence, occurredAtFor(sequence)), { announce: sequence !== 1 });
      index = (index + 1) % keys.length;
      cycleTimer = window.setTimeout(emitNext, cycleDurations[key] ?? 4200);
    };
    emitNext();
  }

  function renderLiveOffline() {
    liveStale = true;
    renderAcceptedEvent({
      version: 1,
      sequence: Math.max(0, currentSequence + 1),
      turnId: null,
      phase: 'offline',
      transcript: null,
      reply: null,
      activity: null,
      privacy: 'unknown',
      occurredAt: new Date().toISOString()
    }, { announce: currentSequence >= 0 });
  }

  function scheduleLiveDisconnect() {
    window.clearTimeout(liveDisconnectTimer);
    liveDisconnectTimer = window.setTimeout(renderLiveOffline, liveDisconnectGraceMs);
  }

  function acceptLiveEvent(candidate) {
    let event;
    try {
      event = normalizeEvent(candidate);
    } catch {
      return;
    }

    scheduleLiveDisconnect();
    if (event.sequence > currentSequence) {
      currentSequence = event.sequence;
      renderAcceptedEvent(event);
    } else if (liveStale && event.sequence === currentSequence) {
      renderAcceptedEvent(event);
    }
    liveStale = false;
  }

  function requestLiveSnapshot() {
    liveChannel?.postMessage({ type: 'franky.presence.snapshot-request' });
  }

  function startLiveSource() {
    renderLiveOffline();
    if (typeof BroadcastChannel !== 'function') return;

    liveChannel = new BroadcastChannel('franky-presence-v1');
    liveChannel.addEventListener('message', event => {
      if (event.data?.type !== 'franky.presence.event') return;
      acceptLiveEvent(event.data.event);
    });
    requestLiveSnapshot();
    liveRequestTimer = window.setInterval(requestLiveSnapshot, 1000);
    scheduleLiveDisconnect();
  }

  function isSameOriginMessage(event) {
    return event.origin === window.location.origin;
  }

  function startHarnessSource() {
    renderAcceptedEvent(mock.createEvent('offline', 0, occurredAtFor(0)), { announce: false });
    currentSequence = 0;
    window.addEventListener('message', event => {
      if (!isSameOriginMessage(event) || !event.data || typeof event.data !== 'object') return;
      if (event.data.type === 'franky.presence.event') {
        acceptEvent(event.data.event);
      }
      if (event.data.type === 'franky.presence.preference' && typeof event.data.reducedMotion === 'boolean') {
        harnessReducedMotion = event.data.reducedMotion;
        updateMotionPreference();
        if (currentEvent) renderAcceptedEvent(currentEvent, { announce: false });
      }
    });
    window.parent.postMessage({ type: 'franky.presence.ready' }, window.location.origin);
  }

  reducedMotionQuery.addEventListener('change', () => {
    if (harnessReducedMotion !== null) return;
    updateMotionPreference();
    if (currentEvent) renderAcceptedEvent(currentEvent, { announce: false });
  });

  document.addEventListener('visibilitychange', () => {
    presence.classList.toggle('is-paused', document.hidden);
    if (!document.hidden) requestLiveSnapshot();
  });

  window.addEventListener('pagehide', () => {
    clearPhraseStaging();
    window.clearTimeout(cycleTimer);
    window.clearTimeout(liveDisconnectTimer);
    window.clearInterval(liveRequestTimer);
    liveChannel?.close();
  });

  updateMotionPreference();
  const source = params.get('source');
  const fixedMock = params.get('mock');
  if (source === 'harness') {
    startHarnessSource();
  } else if (fixedMock && mock.keys.includes(fixedMock)) {
    startFixedMock(fixedMock);
  } else if (source === 'mock') {
    startMockCycle();
  } else {
    startLiveSource();
  }
})();
