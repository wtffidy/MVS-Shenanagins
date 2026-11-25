# Samsung TV and Family Hub port

## Current baseline

- Fork: `wtffidy/MVS-Shenanagins`
- Upstream: `Arglax/MVS`
- Source baseline: MVS 1.0.5 source export; fork and upstream `main` have the same commit and the same `src` tree at the time this assessment was written.
- Work branch: `feature/tizen-platform-ports`
- The upstream repository is not being modified.

## Platform findings

### Samsung Smart TV

Samsung's current Smart TV testing FAQ says consumer Smart TVs install Web application packages (`.wgt`) and do not support native application packages (`.tpk`). The target application therefore needs a Tizen Web application shell, not a direct Android/Avalonia or Tizen native-app re-target.

The packaged Web application can contain its own HTML, JavaScript, CSS, and WebAssembly resources. Samsung documents WebAssembly support on Smart TVs from Tizen 5.5 onward. Samsung's Web Engine specifications list the WebSocket API as supported across the current TV generations. The MVS Ruffle assets are therefore a plausible reuse point, subject to real-device testing of WebGL, WASM memory/performance, and game input.

### Samsung Family Hub

Family Hub is a separate Tizen device profile. Samsung explicitly warns that Family Hub applications are not fully compatible with Smart TV applications and require modifications to meet TV application specifications. The project will keep separate app manifests/configuration and input/layout profiles rather than assuming one WGT is automatically compatible with both product families.

### Packaging and distribution

The app code and game assets must be packaged locally in the WGT. Samsung says hosted/cloud applications are generally not accepted except by special approval, and hosted apps do not have access to Tizen APIs. A remote service may provide game/session functionality, but it must not replace the installed app with a remote web page.

## MVS-specific technical constraint

The current Ruffle implementation is coupled to `Skua.Flash.Ruffle.RuffleHost`. That C# host:

- Serves the embedded Ruffle HTML, JavaScript, WebAssembly, and SWF resources.
- Proxies HTTP requests made by the game.
- Bridges Ruffle WebSockets to the game's raw TCP socket endpoints.
- Hosts the control WebSocket used for ExternalInterface calls between Ruffle and the C# `IFlashUtil` implementation.

The scripting system is also a .NET service. `Skua.Core.Compiler` uses Roslyn to compile community C# scripts at runtime, and `ScriptLoadContext` loads their assemblies. The Smart TV WGT runtime cannot simply run this Android/.NET service stack.

A front-end-only WGT would therefore display at best a partial game client. It would not preserve MVS's game proxy, script execution, script APIs, plugins, account/Army services, or other C# features.

## Port architecture under evaluation

1. **TV Web app:** a signed, locally packaged WGT that owns the TV layout, remote-control navigation, Ruffle player, bundled web assets, and device-specific input handling.
2. **Family Hub Web app:** a separate WGT profile with touch-first navigation, refrigerator-sized layout, and Family Hub-specific capabilities where the target firmware permits them.
3. **MVS service host:** a .NET service that reuses the existing Core, script runtime, Ruffle host/proxy, and session services. The installed web clients connect to it through an authenticated network channel. This host could run on a supported companion device or computer; it must not silently expose the existing loopback-only server to the LAN.
4. **Shared protocol:** a narrow, versioned client/service contract for pairing, session state, game bridge messages, and supported app actions. The app must fail clearly when no compatible service is available.

This is an architectural direction, not a claim that a finished companion host or device connection already exists. The next implementation work must validate the service transport, trust/pairing model, and which MVS features can be controlled remotely before the UI is presented as functional.

## Validation requirements

- Build both WGT profiles with Samsung's supported Tizen tooling and signed developer certificates.
- Test on the Samsung TV emulator and target TV hardware. Samsung notes that emulator behavior does not cover all TV hardware dependencies.
- Test on the actual Family Hub model/firmware separately; TV compatibility does not establish Family Hub compatibility.
- Verify Ruffle startup, WASM memory use, WebGL renderer, audio, remote/touch input, chat typing, reconnect behavior, HTTP proxying, and game socket stability.
- Verify script compilation/execution and all exposed service actions on the .NET host.
- Do not claim a device port is complete based solely on a successful WGT package build.

## Official platform references

- Samsung Smart TV application testing FAQ: https://developer.samsung.com/smarttv/develop/faq/application-testing.html
- Samsung Smart TV Web Engine specifications: https://developer.samsung.com/smarttv/develop/specifications/web-engine-specifications.html
- Samsung Smart TV WebAssembly overview: https://developer.samsung.com/smarttv/develop/extension-libraries/webassembly/overview.html
- Samsung hosted applications FAQ: https://developer.samsung.com/smarttv/develop/faq/hosted-applications.html
- Samsung Family Hub developer portal: https://developer.samsung.com/family-hub
- Samsung Smart TV FAQ on Family Hub compatibility: https://developer.samsung.com/smarttv/develop/faq/other-features.html
- Tizen Web application and hybrid application model: https://github.com/Samsung/tizen-docs/blob/master/docs/application/web/index.md
