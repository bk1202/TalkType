# TalkType

Local-first, system-wide voice typing. Press a global shortcut, speak, press it
again, and the cleaned transcript is pasted into the app you were using.

TalkType is being made as an open-source desktop application. Windows users
receive both a portable build and a normal installer; GitHub Actions builds both
from the published source for tagged releases.

## Current milestone: Windows alpha

- Configurable global push-to-talk toggle (`Ctrl+Win+Space` by default)
- Always-on-top talk button: click once to listen and again to transcribe
- App-aware Talk button above Discord and WhatsApp message fields
- Discord Stable, Canary, PTB, Vesktop, and WhatsApp detection
- Labelled Talk / Stop / Working states
- Alternate chat positions and a draggable fallback outside the message toolbar
- Contextual visibility by default, with a tray toggle to show it everywhere
- Local WAV recording (16 kHz, mono, 16-bit PCM)
- Offline transcription through `whisper.cpp`
- In-app engine and model download with integrity verification
- Conservative filler removal (`um`, `uh`, repeated hesitation only)
- Clipboard-safe paste into the previously focused application
- Personal vocabulary, optional history, and launch-at-login
- No account, subscription, telemetry, or audio upload

## Run the packaged Windows alpha

### Install TalkType

1. Open the [latest TalkType release](https://github.com/bk1202/TalkType/releases/latest).
2. Under **Assets**, download `TalkType-Setup-*-x64.exe`.
3. Exit an older TalkType copy from its system-tray menu, then run the installer.
4. Launch TalkType and select **Download voice model** on Home if setup is needed. The initial
   engine and balanced English model download is approximately 252 MiB.
5. Focus a text box and press `Ctrl+Win+Space` once to record and again to
   transcribe. TalkType automatically selects a fallback shortcut if that one
   is already registered by another application.

The installer is per-user, requires no administrator access, creates an
uninstaller, and can optionally add a desktop shortcut. A portable ZIP is also
included in each release; extract the entire ZIP before opening `TalkType.exe`.

### Windows SmartScreen notice

TalkType is open source, but these early builds are not yet code-signed with a
commercial certificate. Windows SmartScreen may therefore show **Windows
protected your PC** or list the publisher as **Unknown publisher**, especially
when a release has few downloads. This warning may not appear on every computer.

Only continue if the installer came from the official
[`bk1202/TalkType` releases page](https://github.com/bk1202/TalkType/releases).
In the SmartScreen dialog, select **More info**, verify that the app name is the
TalkType installer you downloaded, and then select **Run anyway**. Do not disable
SmartScreen or Microsoft Defender. Published release notes include a SHA-256
checksum for users who want to verify the download.

### Run from source

Developers can run from source with:

```powershell
dotnet run --project .\src\TalkType.Desktop
```

The balanced English default is `small.en-q8_0`. Advanced users can still
override the managed engine and model with `TALKTYPE_WHISPER_EXE` and
`TALKTYPE_WHISPER_MODEL`.

## Privacy

Recordings are created in the operating system temp directory and deleted after
transcription. `whisper.cpp` runs locally. The only lasting text is what the user
pastes into the destination app. Transcript history is optional and can be
disabled in Settings.
