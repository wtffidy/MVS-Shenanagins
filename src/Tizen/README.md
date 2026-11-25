# Samsung Tizen clients

This folder contains separate clients for Samsung Smart TV and Samsung Family Hub. They are not interchangeable packages.

## Samsung Smart TV

`TV/config.xml` targets the `tv-samsung` web profile. The current WGT is a **browser/rendering smoke test**:

- remote-friendly initial shell
- WebAssembly, WebGL, WebSocket, and Web Crypto capability checks
- locally bundled MVS Ruffle engine and SWF
- Ruffle is started with network access disabled for the smoke test

It does not yet log into AQW, expose the C# bot runtime, or execute MVS scripts. A secure runtime connection is still needed for a full TV client.

Build with Samsung Tizen Studio, a TV Web profile, and a configured signing certificate:

```powershell
$env:TIZEN_CERT_PROFILE = "YourCertificateProfile"; .\src\Tizen\build-tizen-tv.ps1
```

The resulting package is copied to `src/Tizen/artifacts/MVS-Tizen-TV.wgt`.

Samsung's TV testing FAQ currently describes WGT as the TV app testing package and says TPK packages cannot be installed for TV testing. Samsung also publishes .NET TV framework documentation, so distribution options are model- and program-dependent. Do not treat the Family Hub TPK as a TV package.

## Samsung Family Hub

`FamilyHub/` is a native Tizen .NET NUI application targeting `net8.0-tizen10.0` and the `common` profile.

It runs the MVS C# runtime locally, starts the existing loopback-only Ruffle host, loads the original Ruffle page, and exposes a touch-first Game / Scripts / Bot / Logs UI. The current bridge supports script discovery, loading, start, pause/resume, stop, status, and recent script logs.

Build using Samsung Tizen Studio with the Tizen 10 IoT/.NET workload and a configured author certificate:

```powershell
.\src\Tizen\build-familyhub.ps1
```

The resulting package is copied to `src/Tizen/artifacts/MVS-FamilyHub.tpk`.

This is a runtime-integration prototype, not a completed MVS client. Script-option dialogs, file selection, plugin windows, account management, all remaining helper screens, and device lifecycle handling are still unfinished. The target Tizen version is provisional until the exact refrigerator model and firmware are known.

## Shared Ruffle assets

`Skua.Flash.Ruffle` embeds the modified Ruffle assets. `RuffleAssetPack` exports those embedded files and applies the same Ruffle compatibility patch used by the desktop/Android host. Do not copy the raw upstream Ruffle folder into a Tizen package and assume it is equivalent.

## Validation

GitHub Actions builds the shared libraries for .NET 8 and .NET 10, exports the patched Ruffle assets, checks the TV and Family Hub JavaScript syntax, parses both manifests, and verifies the expected packaged resources.

The native Tizen application itself still requires the Samsung Tizen SDK and actual device validation. No hardware compatibility claim is made until it has been installed and exercised on the target model.
