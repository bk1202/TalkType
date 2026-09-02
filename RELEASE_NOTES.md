# TalkType 0.1.10-alpha

- Restore the earlier 34-pixel in-bar microphone placement for Discord and WhatsApp.
- Remove gap-based hiding and above-bar fallback positions.
- Chat microphone is white when idle and red while recording, with a bar-sampled background and no dark circle. Keep the app UI and global button unchanged.

## Install / update

Exit TalkType from its system-tray menu, then run `TalkType-Setup-0.1.10-alpha-x64.exe`. Existing preferences and models are retained.

Unsigned alpha installers may trigger SmartScreen. Use only official release assets and do not disable Windows security protections.

## Known limitations

This restores the original overlay offsets; it is not native toolbar integration. The restored placement can overlap native controls in some layouts. Exact-offset regression tests and UI state/render checks passed; live Discord/WhatsApp placement and microphone transcription were not retested for this build.
