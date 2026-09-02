# Apple platform work

This directory begins the code shared by the native TalkType macOS and iOS apps. It can
be opened as a Swift package in Xcode on a Mac.

## macOS shell

The macOS target will be a SwiftUI menu-bar application. It requires microphone
permission for capture and Accessibility permission to paste into other apps.
The app will use the official `whisper.cpp` XCFramework and a downloaded local
model, matching the Windows privacy behavior.

## iOS shell

The iOS product requires two targets:

1. A containing app that downloads/manages the model and performs microphone
   recording and transcription.
2. A Keyboard Extension that inserts the completed transcript into the current
   text field.

iOS does not give ordinary third-party keyboard extensions unrestricted
microphone access. The initial design will use an explicit handoff to the
containing app and return the resulting text through an App Group. This must be
built, signed, and tested using Xcode on macOS.
