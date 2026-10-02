using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using TimeOnScreen.Core;
using Forms = System.Windows.Forms;

namespace TimeOnScreen;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private Forms.NotifyIcon? _tray;
    private System.Drawing.Icon? _icon;
    private Forms.ToolStripMenuItem? _pauseItem;
    private DispatcherTimer? _timer;
    private ScheduleEngine? _schedule;
    private SettingsStore? _store;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private HwndSource? _messageWindow;
    private bool _locked;
    private bool _suspended;
    private bool _eventsSubscribed;
    private bool _verifying;
    internal SettingsWindow CurrentSettingsWindow => _settingsWindow ?? throw new InvalidOperationException("Settings window is closed");
    internal OverlayWindow CurrentOverlay => _overlay ?? throw new InvalidOperationException("Overlay is not initialized");
    internal ClockSettings Settings { get; private set; } = new();
    internal bool IsPaused { get; private set; }
    internal string? InitialWarning { get; private set; }
    internal string[] TrayMenuText => _tray?.ContextMenuStrip?.Items.OfType<Forms.ToolStripMenuItem>().Select(item => item.Text ?? "").ToArray() ?? Array.Empty<string>();
    internal string TrayTitle => _tray?.Text ?? "";
    private static readonly uint ShowSettingsMessage = NativeMethods.RegisterWindowMessage("TimeOnScreen.ShowSettings.v1");
    private static readonly string InstanceScope = Environment.UserName + "." + System.Diagnostics.Process.GetCurrentProcess().SessionId;
    private string InstancePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeOnScreen", "instance-" + System.Diagnostics.Process.GetCurrentProcess().SessionId + ".txt");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        UiText.SetLanguage(Settings.Language);
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            _overlay?.Dismiss();
            WriteError(args.Exception);
            _settingsWindow?.ShowMessage(UiText.Get("UnexpectedError"));
        };
        if (e.Args.Contains("--verify"))
        {
            var directory = e.Args.SkipWhile(a => a != "--verify").Skip(1).FirstOrDefault();
            directory ??= Path.Combine(AppContext.BaseDirectory, "verification");
            _verifying = true;
            _store = new SettingsStore(Path.Combine(directory, "test-settings.json"));
            _schedule = new ScheduleEngine(DateTime.Now, Settings);
            _overlay = new OverlayWindow();
            InitializeTray();
            OpenSettings();
            _ = Verification.RunAsync(this, directory);
            return;
        }
        _mutex = new Mutex(false, @"Local\TimeOnScreen.v1." + InstanceScope);
        try { _ownsMutex = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsMutex = true; }
        if (!_ownsMutex)
        {
            if (!e.Args.Contains("--background")) NotifyExistingInstance();
            Shutdown();
            return;
        }
        _store = new SettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeOnScreen", "settings.json"));
        Settings = _store.Load();
        UiText.SetLanguage(Settings.Language);
        InitialWarning = _store.LoadWarning;
        try { Settings = Settings with { StartWithWindows = StartupRegistration.IsEnabled() }; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { InitialWarning = UiText.Get("StartupReadError"); }
        _schedule = new ScheduleEngine(DateTime.Now, Settings);
        _overlay = new OverlayWindow();
        InitializeMessageWindow();
        InitializeTray();
        SystemEvents.TimeChanged += TimeChanged;
        SystemEvents.PowerModeChanged += PowerModeChanged;
        SystemEvents.SessionSwitch += SessionSwitch;
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        _eventsSubscribed = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += Tick;
        _timer.Start();
        RefreshPersistentDisplay();
        if (!e.Args.Contains("--background")) OpenSettings();
    }

    private void InitializeMessageWindow()
    {
        _messageWindow = new HwndSource(new HwndSourceParameters("TimeOnScreen.Messages") { Width = 0, Height = 0, WindowStyle = 0 });
        _messageWindow.AddHook((IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled) =>
        {
            if ((uint)msg == ShowSettingsMessage) { handled = true; OpenSettings(); }
            return IntPtr.Zero;
        });
        Directory.CreateDirectory(Path.GetDirectoryName(InstancePath)!);
        File.WriteAllText(InstancePath, _messageWindow.Handle.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private void NotifyExistingInstance()
    {
        try
        {
            if (long.TryParse(File.ReadAllText(InstancePath), out var hwnd))
                NativeMethods.PostMessage(new IntPtr(hwnd), ShowSettingsMessage, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void InitializeTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(UiText.Get("Preview"), null, (_, _) => Dispatcher.Invoke(() => Preview(Settings)));
        menu.Items.Add(UiText.Get("Settings"), null, (_, _) => Dispatcher.Invoke(OpenSettings));
        _pauseItem = new Forms.ToolStripMenuItem(UiText.Get("Pause"), null, (_, _) => Dispatcher.Invoke(TogglePause));
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(UiText.Get("Exit"), null, (_, _) => Dispatcher.Invoke(() => Shutdown()));
        var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/clock.ico"));
        using (resource.Stream) _icon = new System.Drawing.Icon(resource.Stream);
        _tray = new Forms.NotifyIcon { Icon = _icon, Text = UiText.Get("ProductName"), ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
    }

    internal void OpenSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
        _settingsWindow.UpdateStatus();
    }

    internal void Preview(ClockSettings settings) => _overlay?.Play(settings.Normalize());
    internal void TogglePause()
    {
        IsPaused = !IsPaused;
        RefreshPersistentDisplay();
        _schedule?.Recalculate(DateTime.Now, Settings);
        UpdateTrayText();
        _settingsWindow?.UpdateStatus();
    }

    internal void SaveSettings(ClockSettings settings)
    {
        if (_store is null) throw new InvalidOperationException(UiText.Get("StoreNotReady"));
        var previousStartup = !_verifying && StartupRegistration.IsEnabled();
        if (!_verifying && previousStartup != settings.StartWithWindows) StartupRegistration.SetEnabled(settings.StartWithWindows);
        try { _store.Save(settings); }
        catch
        {
            if (!_verifying && previousStartup != settings.StartWithWindows) StartupRegistration.SetEnabled(previousStartup);
            throw;
        }
        Settings = settings.Normalize();
        SetUiLanguage(Settings.Language);
        _schedule?.Recalculate(DateTime.Now, Settings);
        RefreshPersistentDisplay();
        _settingsWindow?.UpdateStatus();
    }

    internal void SetUiLanguage(AppLanguage language, bool refreshSettings = true)
    {
        if (UiText.Language == language) return;
        UiText.SetLanguage(language);
        UpdateTrayText();
        if (refreshSettings) _settingsWindow?.RefreshLanguage();
    }

    private void UpdateTrayText()
    {
        if (_tray?.ContextMenuStrip is not { } menu) return;
        menu.Items[0].Text = UiText.Get("Preview");
        menu.Items[1].Text = UiText.Get("Settings");
        if (_pauseItem is not null) _pauseItem.Text = IsPaused ? UiText.Get("Resume") : UiText.Get("Pause");
        menu.Items[4].Text = UiText.Get("Exit");
        _tray.Text = IsPaused ? UiText.Get("TrayPaused") : UiText.Get("ProductName");
    }

    private void Tick(object? sender, EventArgs e)
        => ProcessScheduledReminders(DateTime.Now);

    internal void ProcessScheduledReminders(DateTime now)
    {
        var due = _schedule?.Poll(now, Settings, IsPaused || _locked || _suspended);
        if (due is not null) _overlay?.Play(Settings);
        _settingsWindow?.UpdateStatus();
    }
    private void RefreshPersistentDisplay() =>
        _overlay?.SetPersistent(Settings.KeepVisible && !_locked && !_suspended ? Settings : null);

    internal void SetSessionLocked(bool locked)
    {
        _locked = locked;
        if (!locked) _schedule?.Recalculate(DateTime.Now, Settings);
        RefreshPersistentDisplay();
    }

    internal void SetSuspended(bool suspended)
    {
        _suspended = suspended;
        RefreshPersistentDisplay();
    }

    private void TimeChanged(object? sender, EventArgs e) => OnUi(() =>
    { _schedule?.Recalculate(DateTime.Now, Settings); RefreshPersistentDisplay(); });
    private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) OnUi(() => SetSuspended(true));
        else if (e.Mode == PowerModes.Resume) OnUi(() => SetSuspended(false));
        // Keep the pending boundary on resume; Poll skips missed hours and honors the 5-second grace.
    }
    private void SessionSwitch(object sender, SessionSwitchEventArgs e) => OnUi(() =>
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff or SessionSwitchReason.RemoteDisconnect or SessionSwitchReason.ConsoleDisconnect)
        { SetSessionLocked(true); }
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon or SessionSwitchReason.RemoteConnect or SessionSwitchReason.ConsoleConnect)
        { SetSessionLocked(false); }
    });
    private void DisplaySettingsChanged(object? sender, EventArgs e) => OnUi(() =>
    { _settingsWindow?.RefreshMonitors(); RefreshPersistentDisplay(); });
    private void OnUi(Action action)
    { if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(action); }
    private static void WriteError(Exception exception)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeOnScreen", "errors.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTime.Now:O}\n{exception}\n\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        if (_eventsSubscribed)
        {
            SystemEvents.TimeChanged -= TimeChanged;
            SystemEvents.PowerModeChanged -= PowerModeChanged;
            SystemEvents.SessionSwitch -= SessionSwitch;
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
        }
        _overlay?.Close();
        _settingsWindow?.Close();
        if (_tray is not null) { _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Dispose(); }
        _icon?.Dispose();
        _messageWindow?.Dispose();
        if (_ownsMutex)
        {
            try { File.Delete(InstancePath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            _mutex?.ReleaseMutex();
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
