(() => {
  const channel = typeof BroadcastChannel === 'function'
    ? new BroadcastChannel('franky-presence-v1')
    : null;

  let sequence = Date.now();
  let turnSequence = 0;
  let currentEvent = createEvent({
    phase: 'offline',
    privacy: 'unknown',
    turnId: null,
    transcript: null,
    reply: null,
    activity: null,
  });

  function optionalText(value) {
    if (value === null || value === undefined) return null;
    const text = String(value).trim();
    return text || null;
  }

  function createEvent(state) {
    sequence += 1;
    const exceptional = state.phase === 'offline' || state.phase === 'error';
    const privateState = state.privacy === 'muted';
    return Object.freeze({
      version: 1,
      sequence,
      turnId: exceptional ? null : state.turnId,
      phase: state.phase,
      transcript: exceptional || privateState ? null : optionalText(state.transcript),
      reply: exceptional || privateState ? null : optionalText(state.reply),
      activity: state.phase === 'offline' || privateState ? null : optionalText(state.activity),
      privacy: state.privacy,
      occurredAt: new Date().toISOString(),
    });
  }

  function broadcast() {
    channel?.postMessage({
      type: 'franky.presence.event',
      event: currentEvent,
    });
  }

  channel?.addEventListener('message', event => {
    if (event.data?.type === 'franky.presence.snapshot-request') broadcast();
  });

  function update(patch) {
    currentEvent = createEvent({
      phase: patch.phase ?? currentEvent.phase,
      privacy: patch.privacy ?? currentEvent.privacy,
      turnId: patch.turnId === undefined ? currentEvent.turnId : patch.turnId,
      transcript: patch.transcript === undefined ? currentEvent.transcript : patch.transcript,
      reply: patch.reply === undefined ? currentEvent.reply : patch.reply,
      activity: patch.activity === undefined ? currentEvent.activity : patch.activity,
    });
    broadcast();
  }

  function beginTurn() {
    turnSequence += 1;
    update({
      phase: 'listening',
      privacy: 'available',
      turnId: `browser-${Date.now()}-${turnSequence}`,
      transcript: null,
      reply: null,
      activity: 'Listening',
    });
  }

  function setReady({ transcript, reply } = {}) {
    update({
      phase: 'idle',
      privacy: 'available',
      transcript: transcript === undefined ? currentEvent.transcript : transcript,
      reply: reply === undefined ? currentEvent.reply : reply,
      activity: null,
    });
  }

  function setOffline() {
    update({
      phase: 'offline',
      privacy: 'unknown',
      turnId: null,
      transcript: null,
      reply: null,
      activity: null,
    });
  }

  function setError(activity) {
    update({
      phase: 'error',
      privacy: 'available',
      turnId: null,
      transcript: null,
      reply: null,
      activity: activity || 'Franky is unavailable',
    });
  }

  function dispose() {
    setOffline();
    channel?.close();
  }

  window.FrankyPresenceFeed = Object.freeze({
    available: Boolean(channel),
    beginTurn,
    update,
    setReady,
    setOffline,
    setError,
    dispose,
  });
})();
