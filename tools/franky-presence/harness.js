(() => {
  const frame = document.querySelector('#presenceFrame');
  const status = document.querySelector('#harnessStatus');
  const autoPlay = document.querySelector('#autoPlay');
  const reducedMotion = document.querySelector('#reducedMotion');
  const stateButtons = [...document.querySelectorAll('[data-event]')];
  const mock = window.FrankyPresenceMock;

  const lifecycle = Object.freeze([
    ['idle', 4200],
    ['listening', 2600],
    ['transcribing', 2400],
    ['processing', 3000],
    ['acting', 3000],
    ['speaking', 5600]
  ]);

  let ready = false;
  let sequence = 0;
  let lifecycleIndex = 0;
  let autoplayTimer = 0;

  function occurredAt() {
    return new Date(Date.parse('2026-08-27T20:42:00-04:00') + sequence * 1000).toISOString();
  }

  function post(message) {
    if (!ready || !frame.contentWindow) return false;
    frame.contentWindow.postMessage(message, window.location.origin);
    return true;
  }

  function markSelected(key) {
    stateButtons.forEach(button => {
      button.setAttribute('aria-pressed', String(button.dataset.event === key));
    });
  }

  function sendState(key) {
    sequence += 1;
    const event = mock.createEvent(key, sequence, occurredAt());
    if (!post({ type: 'franky.presence.event', event })) return;
    markSelected(key);
    status.textContent = `Showing deterministic ${key} state, sequence ${sequence}.`;
  }

  function stopAutoplay() {
    window.clearTimeout(autoplayTimer);
    autoplayTimer = 0;
    lifecycleIndex = 0;
  }

  function playNext() {
    if (!autoPlay.checked || !ready) return;
    const [key, duration] = lifecycle[lifecycleIndex];
    sendState(key);
    lifecycleIndex = (lifecycleIndex + 1) % lifecycle.length;
    autoplayTimer = window.setTimeout(playNext, duration);
  }

  stateButtons.forEach(button => {
    button.setAttribute('aria-pressed', 'false');
    button.addEventListener('click', () => {
      autoPlay.checked = false;
      stopAutoplay();
      sendState(button.dataset.event);
    });
  });

  autoPlay.addEventListener('change', () => {
    stopAutoplay();
    if (autoPlay.checked) playNext();
  });

  reducedMotion.addEventListener('change', () => {
    post({
      type: 'franky.presence.preference',
      reducedMotion: reducedMotion.checked
    });
    status.textContent = reducedMotion.checked
      ? 'Reduced-motion simulation is on.'
      : 'Reduced-motion simulation is off.';
  });

  window.addEventListener('message', event => {
    if (event.origin !== window.location.origin || event.source !== frame.contentWindow) return;
    if (event.data?.type !== 'franky.presence.ready') return;
    ready = true;
    post({
      type: 'franky.presence.preference',
      reducedMotion: reducedMotion.checked
    });
    sendState('idle');
  });

  window.addEventListener('pagehide', stopAutoplay);
})();
