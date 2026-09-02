# TalkType

Local-first, system-wide voice typing. Press a global shortcut, speak, press it
again, and the cleaned transcript is pasted into the app you were using.

TalkType is being prepared as an open-source desktop application. Windows users
receive both a portable build and a normal installer; GitHub Actions builds both
from the published source for tagged releases.

## Current milestone: Windows alpha

- Configurable global push-to-talk toggle (`Ctrl+Win+Space` by default)
- Always-on-top talk button: click once to listen and again to transcribe
- App-aware compact docking beside Discord and WhatsApp message controls
- Discord Stable, Canary, PTB, Vesktop, and WhatsApp detection
- Transparent, icon-only docked microphone styled to blend with message toolbars
- Separate Discord and WhatsApp offsets that preserve native toolbar controls
- Contextual visibility by default, with a tray toggle to show it everywhere
- Local WAV recording (16 kHz, mono, 16-bit PCM)
- Offline transcription through `whisper.cpp`
- In-app engine and model download with integrity verification
- Conservative filler removal (`um`, `uh`, repeated hesitation only)
- Clipboard-safe paste into the previously focused application
- Personal vocabulary, optional history, and launch-at-login
- No account, subscription, telemetry, or audio upload

## Run the packaged Windows alpha

Extract the entire ZIP first—do not run the executable from inside the ZIP.
Then open the packaged `TalkType.exe`. On first launch, click
**Download local speech engine**. The default download is approximately 560 MiB.
After setup, focus any text field and press `Ctrl+Win+Space` to start and stop.
If that shortcut is occupied, TalkType automatically chooses an available
fallback and tells you which one it selected.

Developers can run from source with:

```powershell
dotnet run --project .\src\LockIn.Desktop
```

The accuracy-first default is `large-v3-turbo-q5_0`. Advanced users can still
override the managed engine and model with `LOCKIN_WHISPER_EXE` and
`LOCKIN_WHISPER_MODEL`.

## Product architecture

The desktop milestone proves the core loop. macOS needs a menu-bar app with
Accessibility permission. iOS needs a Keyboard Extension plus a containing app.
Those shells should share the
same transcription policy: local by default, explicit opt-in for any cloud
backend, and conservative cleanup that never invents content.

## Privacy

Recordings are created in the operating system temp directory and deleted after
transcription. `whisper.cpp` runs locally. The only lasting text is what the user
pastes into the destination app. Transcript history is optional and can be
disabled in Settings.

## Build an installer

Publish the self-contained app, then compile `installer/TalkType.iss` with Inno
Setup 7 (or a compatible Inno Setup 6 compiler). The installer uses a per-user
location and does not require administrator privileges.
