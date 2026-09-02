# TalkType 0.1.6-alpha

- Fix the disappearing chat button: try alternate positions above the composer, then retain a draggable fallback over chat content instead of hiding on every collision.
- Exclude profile-card dialog message fields from composer selection.
- Keep the overlay outside the message toolbar; it does not insert, remove, or rearrange Discord/WhatsApp controls. It may cover chat content and positioning remains experimental.
- Replace the clipped global microphone with labelled Talk / Stop / Working states.
- Add Home and Preferences screens, test recording with transcript preview and Copy, and inline setup feedback.
- Keep the recording indicator active until recording stops; preserve the previous shortcut when registration fails.

## Install / update

Exit TalkType from its system-tray menu, download `TalkType-Setup-0.1.6-alpha-x64.exe` below, and run it. Launch TalkType after installation. Existing models and preferences are retained.

These alpha builds are unsigned. Windows SmartScreen may show an unknown-publisher warning, although not every computer will display one. Only use assets from this official repository and verify `SHA256SUMS.txt` if needed. Do not disable Windows security protections.

## Verification

Release build, installer compilation, UI rendering/state checks, and alternate-position/fallback regression tests passed locally. Live Discord inspection was started but interrupted; the updated overlay is not fully live-verified. WhatsApp and microphone transcription were not retested for this release.
