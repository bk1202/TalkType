# TalkType 0.1.11-alpha

- Replace fixed window offsets with measured composer-toolbar anchoring for Discord and WhatsApp.
- Place the microphone left of the first detected right-hand toolbar control and check its entire hit area against native controls and reported text bounds.
- Hide the chat microphone when safe placement cannot be determined; do not substitute a floating button over the messaging app. The keyboard shortcut remains available.
- Chat microphone is white when idle and red while recording, with a bar-sampled background and no dark circle. Keep the app UI and global button unchanged.

## Install / update

Exit TalkType from its system-tray menu, then run `TalkType-Setup-0.1.11-alpha-x64.exe`. Existing preferences and models are retained.

Unsigned alpha installers may trigger SmartScreen. Use only official release assets and do not disable Windows security protections.

## Known limitations

This remains an overlay, not native toolbar integration. Placement depends on accessibility information supplied by the target app; missing text/toolbar information or crowded input can hide the microphone. Layout is refreshed every 300 ms, not synchronously with Discord. Toolbar geometry, resizing/sidebar, multiline alignment, collision rejection and UI render/state tests passed. The user confirmed the updated placement works in their Discord setup. Live WhatsApp placement and microphone transcription have not been retested for this build.
