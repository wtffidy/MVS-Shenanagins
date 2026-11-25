# Samsung Tizen Port: Architecture and Implementation Plan

**Status:** Patched Ruffle asset export, TV Web-app smoke test, and native Family Hub runtime prototype added; device packaging and hardware testing remain open  
**Working branch:** `feature/tizen-tv-family-hub`  
**Upstream:** `Arglax/MVS` remains untouched

## Goal

Bring the existing MVS experience to Samsung Smart TVs and Samsung Family Hub refrigerators while preserving the existing AQW game integration, Ruffle modifications, C# scripting engine, bot services, and as much of the current UI/behavior as each device can support.

This is a platform port, not a rewrite of MVS and not a remote-control-only dashboard.

## What the repository already gives us

- `Skua.Core`, `Skua.Core.Interfaces`, `Skua.Core.Models`, and `Skua.Core.Utils` contain the scripting and bot foundation.
- `Skua.Flash.Ruffle` contains the customized Ruffle web assets, game HTTP proxy, socket bridge, WebSocket control protocol, and C# Flash integration.
- `MoVibeSkua.Android` wires those pieces together and provides Android-specific app services.
- The game frontend is already HTML, JavaScript, CSS, and WebAssembly. It is not inherently tied to Android's WebView.
- The current Ruffle host binds to loopback and its control channel accepts one active browser session. The TV/Family Hub work must account for both facts rather than exposing the existing listener casually.

## Platform findings

### Samsung Smart TV

The conservative install/test target is a Tizen **Web application** packaged as `.wgt`.

Samsung's current Application Testing FAQ says Samsung TVs support Web `.wgt` applications and do not support installing native `.tpk` packages for TV testing. Samsung also maintains legacy/current .NET TV framework and template documentation, so the existence of a .NET TV toolchain should not be confused with a package we can currently sideload and validate on a consumer TV. The first TV target here is therefore a Web `.wgt`; it is only a browser/rendering smoke test until a secure C# runtime connection is available.

The TV app needs a remote-first interface: focus navigation, directional keys, Enter/Back behavior, legible 10-foot typography, and no assumptions about touch or a physical keyboard.

### Samsung Family Hub

Family Hub is a separate Tizen device profile and must have its own package configuration and interaction layout. It should use a touch-first UI with large targets, responsive portrait/landscape layout, and safe idle/resume behavior.

The branch now contains a native Tizen .NET NUI prototype targeting `net8.0-tizen10.0` and the `common` profile. It starts MVS's existing loopback-only Ruffle host in-process, loads the packaged UI through NUI WebView, and exposes script discovery/load/start/pause/stop/status/log operations through the native JavaScript message bridge. It is a Family Hub prototype, not yet a finished MVS client: option dialogs are safely cancelled, while account management, file selection, plugin windows, complete helper screens, lifecycle behavior, and device-specific polish remain unfinished. The project requires a Tizen 10 compatible device/runtime as currently configured.

### Tizen .NET

Samsung documents Tizen .NET applications and NUI WebView. NUI WebView loads local packaged resources and supports a C#-to-JavaScript message bridge. The Family Hub prototype uses that approach.

The shared MVS runtime libraries are now multi-targeted to `net8.0;net10.0`, preserving the current .NET 10 target while providing a .NET 8 build for the Tizen 10 project. The Android Avalonia shell itself is not reused or repackaged.

The MVS script engine compiles and loads C# assemblies dynamically. The existing `ScriptLoadContext` already provides a custom `AssemblyLoadContext`, which is the right direction for Samsung's documented Tizen assembly-loading constraints. The Family Hub manifest deliberately does not request the optional `prefer_dotnet_aot` mode. Dynamic script compilation and assembly loading must still be tested on the actual device; no script-runtime success is claimed yet.

## Architecture decision

1. Keep the existing C# bot and script engine authoritative. Do not translate the bot API or community scripts into JavaScript.
2. Package a local Tizen Web client for each profile. It will use the existing Ruffle frontend and customized game assets wherever the device's WebView supports them.
3. Keep the Tizen-specific presentation thin. TV and Family Hub get separate navigation/layout adapters, while shared game integration remains common.
4. Prefer an on-device native Tizen .NET host where the device profile supports it. The Family Hub prototype uses NUI WebView to load the existing Ruffle page from the same app process, keeping the C# bot runtime and game proxy on-device.
5. Keep the existing loopback listener loopback-only. A Web `.wgt` that cannot host the C# runtime needs a separately designed companion bridge; it must not expose AQW login traffic or script-control messages over an unauthenticated, unencrypted LAN connection. Pairing, session authentication, replay protection, and encrypted transport are requirements, not optional polish.
6. Preserve the current Android/desktop behavior. New remote support must be opt-in and must not widen the existing loopback listener by default.

## Known engineering blockers

- `RuffleHost` currently binds to `IPAddress.Loopback`.
- `RuffleHost.WriteResponse()` currently emits wildcard CORS headers (`Access-Control-Allow-Origin: *` and wildcard headers/methods). This is acceptable only while the host remains loopback-bound; it must be replaced with an authenticated, origin-aware policy before any LAN listener is enabled.
- `RuffleHost.BuildConfigJson()` publishes loopback WebSocket socket-proxy URLs. This is correct for the on-device Family Hub host, but not directly usable from a remote TV Web app.
- `ControlChannel` owns one active WebSocket. A second client replaces the first.
- The game bridge derives its base URL from the local page origin. A remote TV client needs an authenticated endpoint and explicit origin handling.
- The current Ruffle bundle is patched at runtime by `RuffleHost.PatchRuffle`. Packaging the raw source web folder without applying those patches would silently lose important MVS behavior.
- The C# script compiler uses Roslyn to emit assemblies to disk and load them dynamically. The existing custom load context helps with Tizen's assembly resolution rules, but the runtime behavior is still unverified on hardware. A browser/WASM-only TV design cannot run this C# script engine by itself.
- Existing Android clipboard, file picker, process, audio, screenshot, lifecycle, thermal, and notification services are platform-specific. Each Tizen interaction must be implemented, adapted, or explicitly hidden.
- Samsung TV and Family Hub devices have their own lifecycle, input, WebView, memory, and package constraints.

## Implementation phases

### Phase 0: establish supported targets

- Record the exact TV and Family Hub models and firmware/Tizen versions when available.
- Verify each target's supported WebView/WebAssembly features and install path.
- Set a minimum supported Tizen profile per package; do not infer support from the product year alone.

### Phase 1: Tizen clients

- Add separate TV and Family Hub package configurations.
- Bundle and validate the patched Ruffle assets.
- Keep account credentials out of localStorage and query strings.
- Finish TV remote navigation and the Family Hub touch-first UI.
- Keep device-specific package resources under the Tizen `res` directory.

### Phase 2: C# runtime bridge

- Design an opt-in pairing flow with short-lived pairing credentials.
- Establish a cryptographically authenticated session and encrypted message transport.
- Expose only the narrow operations required by Ruffle's existing `ControlChannel` protocol and the game proxy.
- Handle disconnects, reconnects, host changes, and single-session ownership explicitly.
- Keep the existing loopback host as the default for Android.

### Phase 3: MVS integration

- Add a companion host to the existing MVS runtime rather than duplicating script behavior.
- Add remote-session status and ownership controls.
- Avoid letting an idle Android WebView reconnect and steal an active TV/Family Hub session.
- Reuse existing script controls, status reporting, and Ruffle overlays where the device input model allows them.

### Phase 4: device validation and release

- Build each package using the matching Samsung Tizen SDK/profile.
- Validate Ruffle startup, WebAssembly execution, AQW login, map loading, socket transport, C# ExternalInterface calls, scripts, input, audio, and lifecycle behavior.
- Test TV remote navigation separately from Family Hub touch navigation.
- Only call a device supported after installation and runtime testing on that device/profile.

## Current implementation status

**TV:** `src/Tizen/TV` contains a Samsung TV Web application manifest and remote-first smoke-test shell. It checks WebAssembly, WebGL, WebSockets, and Web Crypto, and can load the packaged MVS SWF through the exported Ruffle engine with network access disabled. It does not log into AQW, connect to the C# bot runtime, or run MVS scripts. A secure remote runtime bridge is still required for a useful full TV client.

**Family Hub:** `src/Tizen/FamilyHub` contains a native NUI .NET host targeting Tizen 10, a packaged touch UI under `res/web`, and a JavaScript message bridge for MVS status and script controls. It launches the existing loopback-only `RuffleHost` and reuses `RuffleFlashUtil` and the shared bot services. Unsupported dialogs currently cancel safely. This is an integration prototype, not a completed MVS interface.

**Shared code:** The core runtime projects and Ruffle assembly now target both `net8.0` and `net10.0`. The shared bot service registrations were moved into `Skua.Core.AppStartup.Services` so the Tizen host can compose the existing MVS services without bootstrapping Avalonia.

**Ruffle assets:** `src/Skua.Flash.Ruffle/RuffleAssetExporter.cs` exports the Ruffle resources embedded in the existing assembly and reuses the exact `RuffleHost.PatchRuffle` transformation. `src/tools/RuffleAssetPack` provides the build-time command used by the TV package script and CI validation.

## Build and verification status

The shared .NET 8/.NET 10 projects and the native Family Hub application have now compiled successfully in GitHub Actions using Samsung's official Tizen .NET workload (`net8.0-tizen10.0`). The same run validates the patched Ruffle export, both client JavaScript files, both manifests, and packaged Family Hub resources. No signed Tizen `.wgt` or `.tpk` has been produced, and neither client has been installed or run on hardware. Successful compilation does not yet prove Ruffle/WebAssembly or dynamic Roslyn script loading works on a real refrigerator. This execution environment does not have Samsung Tizen Studio/CLI, so a successful native Tizen package build cannot be claimed here.

The repository's `src/BuildProject.bat` references `Skua.sln` and `package-build.ps1`, neither of which appears in the checked-in tree. The documented Android build path is `src/build-android.sh`. Tizen builds will need their own explicit build scripts and documented SDK prerequisites.

## Official references

- [Samsung Smart TV Application Testing FAQ](https://developer.samsung.com/smarttv/develop/faq/application-testing.html)
- [Samsung TV .NET Framework](https://developer.samsung.com/smarttv/develop/tizen-net-tv.html)
- [Samsung Tizen .NET / TizenFX target framework matrix](https://samsung.github.io/TizenFX/)
- [Samsung Tizen NUI WebView guide](https://github.com/Samsung/tizen-docs/blob/master/docs/application/dotnet/guides/user-interface/nui/webview.md)
- [Tizen Web Applications](https://github.com/Samsung/tizen-docs/blob/master/docs/application/web/guides/applications/overview.md)
- [Samsung Family Hub developer page](https://developer.samsung.com/family-hub)
