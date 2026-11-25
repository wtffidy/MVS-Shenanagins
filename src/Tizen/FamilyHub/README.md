# MVS Samsung Family Hub client

This is the native Tizen .NET starting point for Samsung Family Hub. It targets the current Samsung IoT sample framework, `net8.0-tizen10.0`, and the Tizen `common` profile.

## What is wired up

- A native Tizen NUI application and WebView.
- The existing MVS `RuffleHost` runs locally in the same application process.
- The WebView loads the existing MVS game page through the host's loopback-only HTTP endpoint.
- The existing patched Ruffle bundle and embedded SWF are used. No separate copy of the game frontend is maintained.
- MVS's existing `.NET` bot service registrations and `RuffleFlashUtil` are initialized on-device.
- The existing MVS settings store and script interface are initialized.

This is a native runtime integration prototype, not yet a finished Family Hub UI. Script-option dialogs, file selection, plugin windows, account management, and the touch-first bot control surface still need Family Hub implementations. The initial dialog service safely cancels unsupported dialogs instead of pretending they were accepted.

## Build prerequisites

- Samsung Tizen Studio with the .NET / IoT 10.0 profile.
- .NET 8 Tizen workload and a configured Tizen author certificate.
- Samsung Family Hub device or matching Tizen IoT emulator for runtime validation.

Build from this directory using the Tizen .NET CLI:

```powershell
tizen build-cs
```

The Tizen SDK produces a signed `.tpk` package under the project's `bin` output folder. The app icon is copied from `src/tools/icon/monogram.png` into `shared/res/MvsFamilyHub.png` by the repository's build script.

## Important compatibility note

Tizen 10 is a provisional build target. Family Hub models use different generations of Tizen, and this project is not declared compatible with older devices until each target framework/profile is built and tested on that hardware.
