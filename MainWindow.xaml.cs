using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace Beamer_viewer;

public partial class MainWindow
{
  private const string HostName = "beamerviewer.local";
  private const string AppFolderName = "Beamer_viewer_app";

  private readonly AppConfig _cfg;
  private readonly string _cfgPath;

  private bool _allowClose;

  private const int WM_CLOSE = 0x0010;
  private const int WM_SYSCOMMAND = 0x0112;
  private const int SC_CLOSE = 0xF060;

  private const int SW_RESTORE = 9;

  private const uint FLASHW_ALL = 3;
  private const uint FLASHW_TIMERNOFG = 12;

  [StructLayout(LayoutKind.Sequential)]
  private struct FLASHWINFO
  {
    public uint cbSize;
    public IntPtr hwnd;
    public uint dwFlags;
    public uint uCount;
    public uint dwTimeout;
  }

  [DllImport("user32.dll")]
  private static extern bool SetForegroundWindow(IntPtr hWnd);

  [DllImport("user32.dll")]
  private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

  [DllImport("user32.dll")]
  private static extern bool IsIconic(IntPtr hWnd);

  [DllImport("user32.dll")]
  private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

  [DllImport("user32.dll")]
  private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

  [DllImport("user32.dll")]
  private static extern uint GetCurrentThreadId();

  [DllImport("user32.dll")]
  private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

  [DllImport("user32.dll")]
  private static extern IntPtr GetForegroundWindow();

  public MainWindow()
  {
    InitializeComponent();
    (_cfg, _cfgPath) = AppConfig.LoadOrCreate();

    Closing += (_, e) =>
    {
      if (!_allowClose) e.Cancel = true;
    };

    PreviewKeyDown += (_, e) =>
    {
      if (e.Key == Key.F4 && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
      {
        e.Handled = true;
      }
    };

    SourceInitialized += (_, _) =>
    {
      var src = (HwndSource)PresentationSource.FromVisual(this);
      src.AddHook(WndProc);
    };

    Loaded += async (_, _) => await InitAsync();
  }

  private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
  {
    if (!_allowClose)
    {
      if (msg == WM_CLOSE)
      {
        handled = true;
        return IntPtr.Zero;
      }

      if (msg == WM_SYSCOMMAND)
      {
        var cmd = wParam.ToInt64() & 0xFFF0;
        if (cmd == SC_CLOSE)
        {
          handled = true;
          return IntPtr.Zero;
        }
      }
    }

    return IntPtr.Zero;
  }

  private static string ReadEmbeddedText(string fileName)
  {
    var asm = typeof(MainWindow).Assembly;
    var res = asm.GetManifestResourceNames()
      .First(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
    using var s = asm.GetManifestResourceStream(res)!;
    using var r = new StreamReader(s);
    return r.ReadToEnd();
  }

  private static bool TryWriteText(string path, string content)
  {
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllText(path, content);
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static string ResolveWritableAppFolder()
  {
    var preferred = Path.Combine(AppContext.BaseDirectory, AppFolderName);
    try
    {
      Directory.CreateDirectory(preferred);
      var probe = Path.Combine(preferred, ".write_test");
      File.WriteAllText(probe, "ok");
      File.Delete(probe);
      return preferred;
    }
    catch
    {
      var fallback = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Beamer_viewer",
        AppFolderName
      );
      Directory.CreateDirectory(fallback);
      return fallback;
    }
  }

  private async Task InitAsync()
  {
    await Web.EnsureCoreWebView2Async();

    Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
    Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
    Web.CoreWebView2.Settings.AreDevToolsEnabled = true;

    try { Web.CoreWebView2.Profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.None; } catch { }

    Web.CoreWebView2.WebMessageReceived += (_, e) =>
    {
      try
      {
        using var doc = JsonDocument.Parse(e.WebMessageAsJson);
        if (!doc.RootElement.TryGetProperty("type", out var t)) return;
        var type = (t.GetString() ?? "").Trim();

        if (type == "new_message")
        {
          Dispatcher.Invoke(() => BringToFront(force: false));
          return;
        }

        if (type == "urgent_message")
        {
          Dispatcher.Invoke(() => BringToFront(force: true));
          _ = Dispatcher.InvokeAsync(async () => await ForceOpenWidgetAsync());
          return;
        }
      }
      catch
      {
      }
    };

    var appFolder = ResolveWritableAppFolder();
    var indexPath = Path.Combine(appFolder, "index.html");

    if (!File.Exists(indexPath))
    {
      var html = ReadEmbeddedText("index.html");
      _ = TryWriteText(indexPath, html);
    }

    Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
      HostName,
      appFolder,
      CoreWebView2HostResourceAccessKind.Allow
    );

    Web.CoreWebView2.NavigationCompleted += async (_, _) => { await InjectConfigAndBootAsync(); };

    Web.Source = new Uri($"https://{HostName}/index.html");
  }

  private void BringToFront(bool force)
  {
    var hwnd = new WindowInteropHelper(this).Handle;
    if (hwnd == IntPtr.Zero) return;

    try
    {
      if (IsIconic(hwnd) || WindowState == WindowState.Minimized)
      {
        ShowWindow(hwnd, SW_RESTORE);
        WindowState = WindowState.Normal;
      }

      Show();
      Activate();

      if (force)
      {
        var fg = GetForegroundWindow();
        var fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
        var curThread = GetCurrentThreadId();
        AttachThreadInput(curThread, fgThread, true);
        SetForegroundWindow(hwnd);
        AttachThreadInput(curThread, fgThread, false);

        var wasTop = Topmost;
        Topmost = true;
        Topmost = wasTop;
      }
      else
      {
        FlashTaskbar(hwnd);
      }

      Focus();
    }
    catch
    {
    }
  }

  private static void FlashTaskbar(IntPtr hwnd)
  {
    try
    {
      var fi = new FLASHWINFO
      {
        cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
        hwnd = hwnd,
        dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
        uCount = 2,
        dwTimeout = 0
      };
      FlashWindowEx(ref fi);
    }
    catch
    {
    }
  }

  private async Task ForceOpenWidgetAsync()
  {
    try
    {
      if (Web.CoreWebView2 is null) return;
      await Web.ExecuteScriptAsync("(function(){ if (window.__BV_FORCE_OPEN__) window.__BV_FORCE_OPEN__(); })();");
    }
    catch
    {
    }
  }

  private async Task InjectConfigAndBootAsync()
  {
    var payload = new
    {
      product_id = _cfg.ProductId,
      user_id = _cfg.UserId,
      refresh_ms = _cfg.RefreshMs,
      widget_width = _cfg.WidgetWidth,
      auto_open_on_launch = _cfg.AutoOpenOnLaunch,
      manual_close_cooldown_ms = _cfg.ManualCloseCooldownMs,
      pulse_on_new_message = _cfg.PulseOnNewMessage,
      pulse_min_interval_ms = _cfg.PulseMinIntervalMs,
      config_path = _cfgPath
    };

    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
      Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    var js = $$"""
      (function(){
        window.__BV_CFG__ = {{json}};
        if (typeof window.__BV_BOOT__ === 'function') window.__BV_BOOT__();
      })();
    """;

    await Web.ExecuteScriptAsync(js);
  }

  public void AllowCloseForUpdateOrSystem()
  {
    _allowClose = true;
  }
}
