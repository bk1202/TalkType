# Contributing to TalkType

Thanks for helping make private voice typing more accessible.

## Development

Requirements:

- Windows 10 or 11
- .NET 10 SDK
- A microphone for end-to-end testing

Build the desktop app:

```powershell
dotnet build .\src\LockIn.Desktop\LockIn.Desktop.csproj
```

The first app launch can download `whisper.cpp` and a local Whisper model. Do
not commit downloaded engines, models, recordings, transcripts, or local
settings.

## Pull requests

- Keep audio local unless a feature explicitly and visibly opts into a remote provider.
- Do not log transcript contents or microphone audio.
- Preserve Discord and WhatsApp's native controls; TalkType must remain an overlay.
- Include build verification and describe any manual microphone/UI testing.
- Keep changes focused and document user-visible behavior.

By contributing, you agree that your contribution may be distributed under the
project's selected open-source license.
