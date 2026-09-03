(() => {
  const occurredAt = '2026-08-27T20:42:00-04:00';

  const templates = [
    {
      key: 'offline',
      turnId: null,
      phase: 'offline',
      transcript: null,
      reply: null,
      activity: null,
      privacy: 'unknown'
    },
    {
      key: 'ready',
      turnId: null,
      phase: 'idle',
      transcript: null,
      reply: null,
      activity: null,
      privacy: 'available'
    },
    {
      key: 'idle',
      turnId: 'turn-0041',
      phase: 'idle',
      transcript: 'How is the setup looking?',
      reply: 'Microphones and speaker are ready.',
      activity: null,
      privacy: 'available'
    },
    {
      key: 'listening',
      turnId: 'turn-0042',
      phase: 'listening',
      transcript: null,
      reply: null,
      activity: 'Listening',
      privacy: 'available'
    },
    {
      key: 'transcribing',
      turnId: 'turn-0042',
      phase: 'transcribing',
      transcript: null,
      reply: null,
      activity: 'Transcribing',
      privacy: 'available'
    },
    {
      key: 'processing',
      turnId: 'turn-0042',
      phase: 'processing',
      transcript: 'Is the player available?',
      reply: null,
      activity: 'Preparing a response',
      privacy: 'available'
    },
    {
      key: 'acting',
      turnId: 'turn-0042',
      phase: 'acting',
      transcript: 'Is the player available?',
      reply: null,
      activity: 'Checking the player',
      privacy: 'available'
    },
    {
      key: 'speaking',
      turnId: 'turn-0042',
      phase: 'speaking',
      transcript: 'Is the player available?',
      reply: 'The player is available and ready.',
      activity: 'Speaking',
      privacy: 'available'
    },
    {
      key: 'privacy',
      turnId: 'turn-0042',
      phase: 'idle',
      transcript: 'Is the player available?',
      reply: 'The player is available and ready.',
      activity: 'Microphone off',
      privacy: 'muted'
    },
    {
      key: 'error',
      turnId: 'turn-0043',
      phase: 'error',
      transcript: 'What is the weather outside?',
      reply: null,
      activity: 'Conversation provider is unavailable',
      privacy: 'available'
    },
    {
      key: 'long',
      turnId: 'turn-0044',
      phase: 'speaking',
      transcript: 'Can you give me a fuller status update on the microphones, speaker, wake detector, local transcription, and the computer runtime before I head downstairs?',
      reply: 'The microphones and speaker are available, the wake detector is armed, local transcription is ready, and the computer runtime is connected. The physical spoken-command path still needs observation, so I cannot claim that part is fully verified yet.',
      activity: 'Speaking',
      privacy: 'available'
    }
  ].map(template => Object.freeze(template));

  const byKey = new Map(templates.map(template => [template.key, template]));

  function createEvent(key, sequence, timestamp = occurredAt) {
    const template = byKey.get(key);
    if (!template) {
      throw new Error(`Unknown Franky Presence mock event: ${key}`);
    }

    return Object.freeze({
      version: 1,
      sequence,
      turnId: template.turnId,
      phase: template.phase,
      transcript: template.transcript,
      reply: template.reply,
      activity: template.activity,
      privacy: template.privacy,
      occurredAt: timestamp
    });
  }

  window.FrankyPresenceMock = Object.freeze({
    keys: Object.freeze(templates.map(template => template.key)),
    createEvent
  });
})();
