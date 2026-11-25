# MVS Tizen TV client

This is the first **Tizen compatibility package**, not yet the complete TV port.

## What this stage includes

- A Samsung Smart TV Web application manifest targeting the `tv-samsung` profile.
- A TV-first interface with remote-control focus and Back-key handling.
- A build-time exporter that copies the exact Ruffle web resources embedded by MVS.
- The same Ruffle compatibility patches used by the existing C# `RuffleHost`.
- A local smoke test that loads the packaged `skua.swf` with AQW networking disabled.
- Runtime checks for WebAssembly, WebGL, WebSockets, and Web Crypto.

The local smoke test is intended to verify the Tizen browser/graphics path on a real television. It does **not** log into AQW, connect to the bot, or execute scripts yet.

## Why the C# runtime is not exposed over Wi-Fi yet

The current MVS runtime binds its HTTP server to loopback and its responses currently advertise wildcard CORS access. Simply changing the listener to bind to the LAN would expose an unauthenticated game proxy and WebSocket control channel. That would put account and script-control traffic at risk.

The next integration stage must add authenticated pairing, protected transport, and explicit remote-session ownership before a Tizen client can connect to the C# runtime.

## Prerequisites

- .NET 10 SDK (the asset exporter references the existing `Skua.Flash.Ruffle` project).
- Samsung Tizen Studio / CLI with a TV profile that supports WebAssembly.
- A configured Tizen signing certificate profile.

## Build

In PowerShell, from the repository root:

```powershell
$env:TIZEN_CERT_PROFILE = "YourCertificateProfile"; .\src\Tizen\build-tizen-tv.ps1
```

You can also pass `-CertificateProfile` and `-Configuration` directly.

The resulting `.wgt` is copied to `src/Tizen/artifacts/`.

The package declares Tizen 5.5 as its minimum because the bundled Ruffle runtime requires WebAssembly. Older TV platforms are not targeted by this package.

## Family Hub

Family Hub is deliberately not built from the TV manifest. Samsung documents that Family Hub and Smart TV platforms are not fully compatible, and the Family Hub package needs its own profile and interaction validation. A fridge package will be added as a separate target rather than reusing this TV manifest and claiming cross-device compatibility.
