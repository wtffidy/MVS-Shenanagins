# MVS Samsung TV Probe

This is a small, offline Tizen Web application used to validate the browser capabilities needed by the MVS Smart TV port. It is a development probe, not the MVS client itself.

## What it checks

- WebAssembly module execution
- WebGL 2 and WebGL 1 renderer availability
- WebSocket API presence
- Web Audio API presence
- Local storage and IndexedDB
- Fetch and Request APIs
- Pointer events and touch support
- Samsung TVInputDevice API and optional key registration
- Animation-frame support
- Remote key events, including Arrow, Enter, and Back

The probe does not connect to AQW, send device information to a server, or store credentials. It only writes a temporary local-storage value and temporary IndexedDB database, then removes them.

## Build

Install Samsung Tizen Studio with the TV Extension and the Web CLI. Create a Tizen certificate profile in Certificate Manager.

From PowerShell:

```powershell
.\build.ps1 -CertificateProfile "YourCertificateProfileName"
```

The signed package is copied to `artifacts\MVS-TV-Probe.wgt`.

Samsung's documented CLI flow is `tizen build-web` followed by `tizen package -t wgt -s <profile>`. Only WGT packages can be installed on Samsung Smart TVs.

## Install and test

Enable Developer Mode on the TV, connect it through Tizen Studio's Remote Device Manager, and permit application installation. You can then install the WGT with Tizen Studio or the Tizen CLI.

Run the probe and press the physical remote's arrow keys, Enter, and Back. Run **Copy report** and save the report for the MVS port work.

The emulator is useful for the first pass, but real-device testing is still required for WebGL, WebAssembly memory/performance, audio, and remote input.
