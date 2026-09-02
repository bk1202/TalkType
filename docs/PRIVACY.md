# TalkType privacy model

- Microphone audio is recorded only after an explicit button or shortcut action.
- Audio is transcribed locally through `whisper.cpp` by default.
- Temporary recordings are deleted after transcription.
- No TalkType account, analytics, advertising SDK, or telemetry is included.
- Optional transcript history is stored locally and can be disabled.
- Discord and WhatsApp integration uses window/accessibility geometry only; it
  does not read message contents or modify either application.
- The model and engine setup process downloads files from the official
  `whisper.cpp` GitHub release and model repository.

Future remote transcription providers must be opt-in and must disclose exactly
what data leaves the device before activation.
