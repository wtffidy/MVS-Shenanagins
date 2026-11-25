# Samsung Tizen Port: Architecture and Implementation Plan

**Status:** Architecture recorded; TV compatibility shell and patched Ruffle asset export added; runtime integration and device testing remain open  
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

Samsung's current Application Testing FAQ states that TVs support Web `.wgt` applications and do not support installing native `.tpk` packages through that testing path. Samsung also maintains .NET TV documentation and Seller Office documentation that mention .NET/`.tpk` packages, so native distribution is not being declared impossible. It is simply not the safe first target for a developer-tested TV build.

The TV app needs a remote-first interface: focus navigation, directional keys, Enter/Back behavior, legible 10-foot typography, and no assumptions about touch or a physical keyboard.

### Samsung Family Hub

Family Hub is a separate Tizen device profile and must have its own package configuration and interaction layout. It should use a touch-first UI with large targets, responsive portrait/landscape layout, and safe idle/resume behavior.

The TV and Family Hub frontends may share game assets and application logic, but they must not be treated as interchangeable packages. Exact model and Tizen profile support must be checked before claiming a build is installable on a specific refrigerator.

### Tizen .NET

Samsung documents Tizen .NET applications and NUI's WebView. NUI WebView can load a local packaged HTML page and exposes JavaScript messaging APIs, which could be useful for a future on-device C# shell.

However, the Tizen .NET framework targets Tizen-specific .NET versions (for example .NET 8 for current Tizen 10 platform targets), while this repository currently targets .NET 10. The existing Avalonia Android app is not a Tizen app and cannot be repackaged as one. A native Tizen shell would require a separately validated target framework, service layer, and packaging path.

For now, a Web `.wgt` client is the reliable common baseline. A native Tizen .NET Family Hub shell remains an option only after its model/runtime support and the MVS core's target compatibility are proven.

## Architecture decision

1. Keep the existing C# bot and script engine authoritative. Do not translate the bot API or community scripts into JavaScript.
2. Package a local Tizen Web client for each profile. It will use the existing Ruffle frontend and customized game assets wherever the device's WebView supports them.
3. Keep the Tizen-specific presentation thin. TV and Family Hub get separate navigation/layout adapters, while shared game integration remains common.
4. Provide a companion/runtime bridge for the C# services that cannot run inside a Web `.wgt`. The first implementation must prove the connection model before it is treated as a usable MVS port.
5. The bridge must not expose AQW login traffic or script-control messages over an unauthenticated, unencrypted LAN connection. Pairing, session authentication, replay protection, and encrypted transport are requirements, not optional polish.
6. Preserve the current Android/desktop behavior. New remote support must be opt-in and must not widen the existing loopback listener by default.

## Known engineering blockers

- `RuffleHost` currently binds to `IPAddress.Loopback`.
- `RuffleHost.WriteResponse()` currently emits wildcard CORS headers (`Access-Control-Allow-Origin: *` and wildcard headers/methods). This is acceptable only while the host remains loopback-bound; it must be replaced with an authenticated, origin-aware policy before any LAN listener is enabled.
- `RuffleHost.BuildConfigJson()` currently publishes loopback WebSocket socket-proxy URLs.
- `ControlChannel` owns one active WebSocket. A second client replaces the first.
- The game bridge derives its base URL from the local page origin. A packaged Tizen client needs an explicit, authenticated backend endpoint.
- The current Ruffle bundle is patched at runtime by `RuffleHost.PatchRuffle`. Packaging the raw source web folder without applying those patches would silently lose important MVS behavior.
- The C# script compiler uses Roslyn to emit assemblies to disk and load them dynamically. A browser/WASM-only design must not be assumed to support the full scripting runtime without a focused proof.
- Existing Android clipboard, file picker, process, audio, screenshot, lifecycle, thermal, and notification services are platform-specific. Each Tizen interaction must be implemented, adapted, or explicitly hidden.
- Samsung TV and Family Hub devices have their own lifecycle, input, WebView, memory, and package constraints.

## Implementation phases

### Phase 0: establish supported targets

- Record the exact TV and Family Hub models and firmware/Tizen versions when available.
- Verify each target's supported WebView/WebAssembly features and install path.
- Set a minimum supported Tizen profile per package; do not infer support from the product year alone.

### Phase 1: Tizen web clients

- Add a shared Tizen web-client source tree.
- Add separate TV and Family Hub package manifests/configuration.
- Bundle game scripts and Ruffle/WebAssembly assets locally.
- Keep account credentials out of localStorage and query strings.
- Add profile-specific focus/touch navigation and a recoverable startup/error screen.

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

The branch now contains `src/Tizen/TV`, a Samsung TV Web application manifest and remote-first smoke-test shell. It checks WebAssembly, WebGL, WebSockets, and Web Crypto, and can load the packaged MVS SWF through the exported Ruffle engine with network access disabled. This is intended to validate the television's browser/rendering path, not to represent a completed MVS client.

`src/Skua.Flash.Ruffle/RuffleAssetExporter.cs` exports the Ruffle resources embedded in the existing assembly and reuses the exact `RuffleHost.PatchRuffle` transformation. `src/tools/RuffleAssetPack` provides the build-time command used by `src/Tizen/build-tizen-tv.ps1`.

The Family Hub app and the authenticated runtime connection have not been implemented yet.

## Build and verification status

No Tizen package or .NET build has been produced yet. The current execution environment does not have the .NET SDK or Tizen Studio/CLI installed, so source inspection and repository changes can proceed here, but a successful local device package build cannot be claimed from this environment.

The repository's `src/BuildProject.bat` references `Skua.sln` and `package-build.ps1`, neither of which appears in the checked-in tree. The documented Android build path is `src/build-android.sh`. Tizen builds will need their own explicit build scripts and documented SDK prerequisites.

## Official references

- [Samsung Smart TV Application Testing FAQ](https://developer.samsung.com/smarttv/develop/faq/application-testing.html)
- [Samsung TV .NET Framework](https://developer.samsung.com/smarttv/develop/tizen-net-tv.html)
- [Samsung Tizen .NET / TizenFX target framework matrix](https://samsung.github.io/TizenFX/)
- [Samsung Tizen NUI WebView guide](https://github.com/Samsung/tizen-docs/blob/master/docs/application/dotnet/guides/user-interface/nui/webview.md)
- [Tizen Web Applications](https://github.com/Samsung/tizen-docs/blob/master/docs/application/web/guides/applications/overview.md)
- [Samsung Family Hub developer page](https://developer.samsung.com/family-hub)
