using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Skua.Core.AppStartup;
using Skua.Core.Interfaces;
using Skua.Core.Interfaces.Services;
using Skua.Core.Models;
using Skua.Core.Services;
using Skua.Core.Scripts;
using Skua.Flash.Ruffle;
using System.Diagnostics;
using Tizen;
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
    private bool _disposed;

    /// <summary>
    /// Initializes the local MVS runtime and opens its Ruffle client in the NUI WebView.
    /// </summary>
    protected override void OnCreate()
    {
        base.OnCreate();

        Window window = GetDefaultWindow();
        window.BackgroundColor = Color.Black;
        window.KeyEvent += OnKeyEvent;

        _gameView = new WebView
        {
            Size = window.Size,
            BackgroundColor = Color.Black
        };
        window.Add(_gameView);

        try
        {
            InitializeRuntime();
            NavigateToGame();
        }
        catch (Exception ex)
        {
            Log.Error("MVS-FamilyHub", $"Startup failed: {ex}");
            _gameView.LoadHtmlString(CreateErrorPage(ex.Message));
        }
    }

    /// <summary>
    /// Sets up the existing MVS bot services and the local Ruffle HTTP/WebSocket host.
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

        // Constructing the script interface attaches MVS's existing Flash API handlers.
        _ = _services.GetRequiredService<IScriptInterface>();
    }

    /// <summary>
    /// Starts the original MVS Ruffle host and navigates the native WebView to its loopback URL.
    /// </summary>
    private void NavigateToGame()
    {
        if (_flash is null)
            throw new InvalidOperationException("The MVS Flash runtime was not initialized.");

        _flash.InitializeFlash();

        if (!_flash.Host.IsRunning)
            throw new InvalidOperationException("The MVS local game host did not start.");
    }

    private void OnPageRequested(string url)
    {
        _gameView?.LoadUrl(url);
    }

    /// <summary>
    /// Handles the initial game-load request before handing subsequent Flash calls to MVS.
    /// </summary>
    private void OnFlashCall(string function, params object[] args)
    {
        if (function == "requestLoadGame")
            _flash?.Call("loadClient");
    }

    private void OnKeyEvent(object? sender, Window.KeyEventArgs e)
    {
        if (e.Key.State != Key.StateType.Down)
            return;

        if (e.Key.KeyPressedName is "XF86Back" or "Escape" or "BackSpace")
            Exit();
    }

    private static string CreateErrorPage(string message)
    {
        string encoded = System.Net.WebUtility.HtmlEncode(message);
        return $"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <style>
                    body {{ margin: 0; padding: 5vw; background: #10131a; color: #f3f5fa; font: 24px Arial, sans-serif; }}
                    h1 {{ color: #91adff; }}
                    pre {{ white-space: pre-wrap; color: #ffb8c0; }}
                </style>
            </head>
            <body>
                <h1>MVS could not start</h1>
                <p>The local game runtime did not initialize.</p>
                <pre>{encoded}</pre>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// Releases the WebView and all local MVS runtime services when the app is terminated.
    /// </summary>
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
    }

    /// <summary>
    /// Starts the Family Hub application.
    /// </summary>
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
        DialogTextInput = title;
        DialogHint = dialogHint;
        TextBoxHint = string.Empty;
    }

    public bool NumberOnly => false;
    public string DialogTextInput { get; }
    public string DialogHint { get; }
    public string TextBoxHint { get; }
}
