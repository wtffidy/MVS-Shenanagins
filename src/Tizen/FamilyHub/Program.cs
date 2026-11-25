using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Skua.Core.AppStartup;
using Skua.Core.Interfaces;
using Skua.Core.Interfaces.Services;
using Skua.Core.Models;
using Skua.Core.Services;
using Skua.Flash.Ruffle;
using System.Text.Json;
using Tizen;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Mvs.FamilyHub;

/// <summary>
/// Native Tizen NUI entry point for the MVS Family Hub client.
/// </summary>
public sealed class FamilyHubApplication : NUIApplication
{
    private ServiceProvider? _services;
    private WebView? _gameView;
    private RuffleFlashUtil? _flash;
    private IScriptManager? _scriptManager;
    private string? _gameUrl;
    private SynchronizationContext? _uiContext;
    private bool _disposed;

    protected override void OnCreate()
    {
        base.OnCreate();
        _uiContext = SynchronizationContext.Current;

        Window window = GetDefaultWindow();
        window.BackgroundColor = Color.Black;
        window.KeyEvent += OnKeyEvent;

        _gameView = new WebView
        {
            Size = window.Size,
            BackgroundColor = Color.Black
        };
        _gameView.PageLoadFinished += OnPageLoadFinished;
        _gameView.AddJavaScriptMessageHandler("MVSBridge", OnBridgeMessage);
        window.Add(_gameView);

        try
        {
            InitializeRuntime();
            if (_flash is null)
                throw new InvalidOperationException("The MVS Flash runtime was not initialized.");

            _flash.InitializeFlash();
            if (!_flash.Host.IsRunning)
                throw new InvalidOperationException("The MVS local game host did not start.");

            if (_services?.GetRequiredService<IScriptInterface>() is IScriptInterfaceManager scriptInterfaceManager)
                scriptInterfaceManager.Initialize();

            string appPage = Application.Current.DirectoryInfo.Resource + "web/index.html";
            _gameView.LoadUrl(appPage);
        }
        catch (Exception ex)
        {
            Log.Error("MVS-FamilyHub", $"Startup failed: {ex}");
            _gameView?.LoadHtmlString(CreateErrorPage(ex.Message));
        }
    }

    /// <summary>
    /// Sets up the shared MVS bot services, settings, and local Ruffle HTTP/WebSocket host.
    /// </summary>
    private void InitializeRuntime()
    {
        ServiceCollection services = new();
        services.AddCommonServices();
        services.AddCompiler();
        services.AddBotServices(includeRemoteControl: false);
        services.AddSingleton<ISettingsService, FamilyHubSettingsService>();
        services.AddSingleton<IDialogService, FamilyHubDialogService>();
        services.AddSingleton(_ => new RuffleHost(new RuffleHostOptions
        {
            Port = 0,
            Quality = "high",
            FrameRate = 60,
            MaxPixelRatio = 1.5,
            PreferredRenderer = "webgl",
            TouchListRowHeight = 72,
            EngineMemory = "fork"
        }));
        services.AddSingleton<RuffleFlashUtil>();
        services.AddSingleton<IFlashUtil>(provider => provider.GetRequiredService<RuffleFlashUtil>());

        _services = services.BuildServiceProvider();
        Ioc.Default.ConfigureServices(_services);

        IClientFilesService clientFiles = _services.GetRequiredService<IClientFilesService>();
        clientFiles.CreateDirectories();
        clientFiles.CreateFiles();

        _flash = _services.GetRequiredService<RuffleFlashUtil>();
        _flash.PageRequested += OnPageRequested;
        _flash.FlashCall += OnFlashCall;
        _scriptManager = _services.GetRequiredService<IScriptManager>();

        // Construction attaches the existing MVS Flash API handlers.
        _ = _services.GetRequiredService<IScriptInterface>();
    }

    private void OnPageRequested(string url)
    {
        _gameUrl = url;
        RunOnUiThread(SetGameUrl);
    }

    private void OnPageLoadFinished(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            SetGameUrl();
            RequestStatus();
        });
    }

    private void SetGameUrl()
    {
        if (_gameView is null || string.IsNullOrWhiteSpace(_gameUrl))
            return;

        string url = JsonSerializer.Serialize(_gameUrl);
        _gameView.EvaluateJavaScript($"window.MVSClient && window.MVSClient.setGameUrl({url});");
    }

    private void OnFlashCall(string function, params object[] args)
    {
        if (function == "requestLoadGame")
            _flash?.Call("loadClient");
    }

    private void OnBridgeMessage(string rawMessage)
    {
        try
        {
            using JsonDocument message = JsonDocument.Parse(rawMessage);
            JsonElement root = message.RootElement;
            string? action = root.TryGetProperty("action", out JsonElement actionElement)
                ? actionElement.GetString()
                : null;

            switch (action)
            {
                case "status":
                    RequestStatus();
                    break;
                case "scripts":
                    SendScripts();
                    break;
                case "loadScript":
                    LoadScript(root);
                    break;
                case "startScript":
                    _ = StartScriptAsync();
                    break;
                case "stopScript":
                    _ = StopScriptAsync();
                    break;
                case "pauseScript":
                    _ = SetScriptPausedAsync(root);
                    break;
                case "logs":
                    SendScriptLogs();
                    break;
                default:
                    SendToUi(new { type = "error", message = "Unknown MVS action." });
                    break;
            }
        }
        catch (Exception ex)
        {
            SendToUi(new { type = "error", message = ex.Message });
        }
    }

    private void RequestStatus()
    {
        IScriptInterface? bot = _services?.GetService<IScriptInterface>();
        SendToUi(new
        {
            type = "status",
            gameConnected = bot?.Player.LoggedIn ?? false,
            scriptRunning = _scriptManager?.ScriptRunning ?? false,
            scriptPaused = _scriptManager?.ScriptPaused ?? false,
            loadedScript = _scriptManager?.LoadedScript ?? string.Empty
        });
    }

    private void SendScripts()
    {
        string root = Path.GetFullPath(ClientFileSources.SkuaScriptsDIR);
        Directory.CreateDirectory(root);
        string rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;

        object[] scripts = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Contains("Cached-Scripts", StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Where(path => path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path => (object)new { name = Path.GetFileNameWithoutExtension(path), path })
            .ToArray();

        SendToUi(new { type = "scripts", items = scripts });
    }

    private void LoadScript(JsonElement root)
    {
        if (_scriptManager is null || !root.TryGetProperty("path", out JsonElement pathElement))
            return;

        string? requestedPath = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(requestedPath))
            return;

        string scriptsRoot = Path.GetFullPath(ClientFileSources.SkuaScriptsDIR);
        string rootPrefix = Path.TrimEndingDirectorySeparator(scriptsRoot) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(requestedPath);

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            || !fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fullPath))
        {
            SendToUi(new { type = "error", message = "The selected script is outside the MVS scripts folder or does not exist." });
            return;
        }

        _scriptManager.SetLoadedScript(fullPath);
        SendToUi(new { type = "scriptLoaded", path = fullPath, name = Path.GetFileNameWithoutExtension(fullPath) });
        RequestStatus();
    }

    private async Task StartScriptAsync()
    {
        if (_scriptManager is null)
            return;

        try
        {
            Exception? error = await _scriptManager.StartScript().ConfigureAwait(false);
            RunOnUiThread(() =>
            {
                SendToUi(new { type = "operation", action = "startScript", success = error is null, message = error?.Message ?? "Script started." });
                RequestStatus();
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                SendToUi(new { type = "operation", action = "startScript", success = false, message = ex.Message });
                RequestStatus();
            });
        }
    }

    private async Task StopScriptAsync()
    {
        if (_scriptManager is null)
            return;

        try
        {
            await _scriptManager.StopScript().ConfigureAwait(false);
            RunOnUiThread(() =>
            {
                SendToUi(new { type = "operation", action = "stopScript", success = true, message = "Script stopped." });
                RequestStatus();
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                SendToUi(new { type = "operation", action = "stopScript", success = false, message = ex.Message });
                RequestStatus();
            });
        }
    }

    private async Task SetScriptPausedAsync(JsonElement root)
    {
        if (_scriptManager is null || !root.TryGetProperty("paused", out JsonElement pausedElement))
            return;

        try
        {
            bool paused = pausedElement.GetBoolean();
            bool success = await _scriptManager.SetPausedAsync(paused).ConfigureAwait(false);
            RunOnUiThread(() =>
            {
                string result = paused ? "Script paused." : "Script resumed.";
                SendToUi(new { type = "operation", action = "pauseScript", success, message = success ? result : "Could not change script state." });
                RequestStatus();
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                SendToUi(new { type = "operation", action = "pauseScript", success = false, message = ex.Message });
                RequestStatus();
            });
        }
    }

    private void SendScriptLogs()
    {
        ILogService? logService = _services?.GetService<ILogService>();
        string[] entries = logService?.GetLogs(LogType.Script).TakeLast(150).ToArray() ?? Array.Empty<string>();
        SendToUi(new { type = "logs", items = entries });
    }

    private void SendToUi(object message)
    {
        string json = JsonSerializer.Serialize(message);
        RunOnUiThread(() => _gameView?.EvaluateJavaScript($"window.MVSClient && window.MVSClient.receive({json});"));
    }

    private void RunOnUiThread(Action action)
    {
        if (_uiContext is null)
        {
            action();
            return;
        }

        _uiContext.Post(_ => action(), null);
    }

    private void OnKeyEvent(object? sender, Window.KeyEventArgs e)
    {
        if (e.Key.State == Key.StateType.Down
            && (e.Key.KeyPressedName is "XF86Back" or "Escape" or "BackSpace"))
            Exit();
    }

    private static string CreateErrorPage(string message)
    {
        string encoded = System.Net.WebUtility.HtmlEncode(message);
        return string.Concat(
            """
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <style>
                    body { margin: 0; padding: 5vw; background: #10131a; color: #f3f5fa; font: 24px Arial, sans-serif; }
                    h1 { color: #91adff; }
                    pre { white-space: pre-wrap; color: #ffb8c0; }
                </style>
            </head>
            <body>
                <h1>MVS could not start</h1>
                <p>The local game runtime did not initialize.</p>
                <pre>
            """,
            encoded,
            """
                </pre>
            </body>
            </html>
            """);
    }

    protected override void OnTerminate()
    {
        DisposeRuntime();
        base.OnTerminate();
    }

    private void DisposeRuntime()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_flash is not null)
        {
            _flash.PageRequested -= OnPageRequested;
            _flash.FlashCall -= OnFlashCall;
        }

        _gameView?.Dispose();
        _gameView = null;

        _services?.Dispose();
        _services = null;
        _flash = null;
        _scriptManager = null;
    }

    public static void Main(string[] args)
    {
        IsUsingXaml = false;
        new FamilyHubApplication().Run(args);
    }
}

/// <summary>
/// Uses MVS's existing cross-platform settings store for Family Hub.
/// </summary>
internal sealed class FamilyHubSettingsService : ISettingsService
{
    private readonly UnifiedSettingsService _settings = new();

    public FamilyHubSettingsService()
    {
        _settings.Initialize(AppRole.Client);
    }

    public void Set<T>(string key, T value) => _settings.Set(key, value);
    public T? Get<T>(string key) => _settings.Get<T>(key);
    public T Get<T>(string key, T defaultValue) => _settings.Get(key, defaultValue);
    public void Initialize(AppRole role) => _settings.Initialize(role);
    public SharedSettings GetShared() => _settings.GetShared();
    public ClientSettings GetClient() => _settings.GetClient();
    public ManagerSettings GetManager() => _settings.GetManager();
    public void SetApplicationVersion() => _settings.SetApplicationVersion();
    public void ReloadSettings() => _settings.ReloadSettings();
    public bool TryReloadSettings() => _settings.TryReloadSettings();
    public bool TryUpdate<T>(string key, T defaultValue, Func<T, T> change, out T result)
        => _settings.TryUpdate(key, defaultValue, change, out result);
}

/// <summary>
/// Safe placeholder for dialogs until the touch-first Family Hub dialog surface is implemented.
/// </summary>
internal sealed class FamilyHubDialogService : IDialogService
{
    public bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class => false;
    public bool? ShowDialog<TViewModel>(TViewModel viewModel, string Title) where TViewModel : class => false;
    public bool? ShowDialog<TViewModel>(TViewModel viewModel, Action<TViewModel> callback) where TViewModel : class => false;
    public void ShowDialog(IOptionContainer optionContainer, Action<Skua.Core.Interfaces.ViewModels.IOptionContainerViewModel> callback)
        => Log.Warn("MVS-FamilyHub", "Script option dialogs are not available in this initial device host.");
    public void ShowMessageBox(string message, string caption)
        => Log.Warn("MVS-FamilyHub", $"{caption}: {message}");
    public bool? ShowMessageBox(string message, string caption, bool yesAndNo)
    {
        Log.Warn("MVS-FamilyHub", $"{caption}: {message}");
        return false;
    }
    public DialogResult ShowMessageBox(string message, string caption, params string[] buttons)
    {
        Log.Warn("MVS-FamilyHub", $"{caption}: {message}");
        return DialogResult.Cancelled;
    }
    public Skua.Core.Interfaces.ViewModels.IInputDialogViewModel CreateInputDialog(string title, string dialogHint)
        => new FamilyHubInputDialogViewModel(title, dialogHint);
}

internal sealed class FamilyHubInputDialogViewModel : Skua.Core.Interfaces.ViewModels.IInputDialogViewModel
{
    public FamilyHubInputDialogViewModel(string title, string dialogHint)
    {
        DialogTextInput = string.Empty;
        DialogHint = dialogHint;
        TextBoxHint = title;
    }

    public bool NumberOnly => false;
    public string DialogTextInput { get; }
    public string DialogHint { get; }
    public string TextBoxHint { get; }
}
