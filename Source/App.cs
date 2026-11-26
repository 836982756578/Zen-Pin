// Zen Pin - Pencere Sabitleyici & PiP (Always on Top)
// Geliştirici: Serkan ŞENTÜRK
// Telif Hakkı © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

namespace ZenPin
{
    public class App : System.Windows.Application
    {
        private static Mutex _mutex;
        private NotifyIcon _trayIcon;
        private HwndSource _msgWindow;
        private HudOverlay _hud;

        private const int HK_PIN = 8001;
        private const int HK_OPACITY_DOWN = 8002;
        private const int HK_OPACITY_UP = 8003;

        private readonly Dictionary<IntPtr, byte> _windowOpacities = new Dictionary<IntPtr, byte>();
        private readonly HashSet<IntPtr> _pinnedWindows = new HashSet<IntPtr>();

        [STAThread]
        public static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, "ZenPin_SingleInstance_Mutex_SerkanSenturk_2026", out createdNew);

            if (!createdNew)
            {
                System.Windows.MessageBox.Show(
                    Lang.T(
                        "Zen Pin is already running in the background!\nPress Ctrl + Space on any window to pin it on top.\n\nDeveloper: Serkan ŞENTÜRK",
                        "Zen Pin zaten arka planda çalışıyor!\nİstediğiniz bir pencereyi en üstte sabitlemek için Ctrl + Boşluk (Space) tuşlarına basabilirsiniz.\n\nGeliştirici: Serkan ŞENTÜRK"),
                    Lang.T("Zen Pin - Window Pinner", "Zen Pin - Pencere Sabitleyici"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            App app = new App();
            app.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _hud = new HudOverlay();

            CreateMessageWindow();
            SetupTrayIcon();

            // Periodic memory trimming (~1-2 MB RAM)
            System.Windows.Threading.DispatcherTimer trimTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            trimTimer.Tick += (s, ev) =>
            {
                try
                {
                    NativeMethods.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
                }
                catch { }
            };
            trimTimer.Start();
        }

        private void CreateMessageWindow()
        {
            HwndSourceParameters p = new HwndSourceParameters("ZenPinMessageWindow")
            {
                WindowStyle = 0,
                Width = 0,
                Height = 0
            };
            _msgWindow = new HwndSource(p);
            _msgWindow.AddHook(WndProc);

            IntPtr handle = _msgWindow.Handle;

            // Ctrl + Space (VK_SPACE = 0x20)
            NativeMethods.RegisterHotKey(handle, HK_PIN, NativeMethods.MOD_CONTROL | NativeMethods.MOD_NOREPEAT, 0x20);

            // Ctrl + Alt + Down (VK_DOWN = 0x28)
            NativeMethods.RegisterHotKey(handle, HK_OPACITY_DOWN, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, 0x28);

            // Ctrl + Alt + Up (VK_UP = 0x26)
            NativeMethods.RegisterHotKey(handle, HK_OPACITY_UP, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, 0x26);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HK_PIN)
                {
                    TogglePinForegroundWindow();
                    handled = true;
                }
                else if (id == HK_OPACITY_DOWN)
                {
                    AdjustOpacityForegroundWindow(-25);
                    handled = true;
                }
                else if (id == HK_OPACITY_UP)
                {
                    AdjustOpacityForegroundWindow(+25);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void TogglePinForegroundWindow()
        {
            IntPtr hWnd = NativeMethods.GetForegroundWindow();
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) return;

            string title = NativeMethods.GetWindowTitle(hWnd);
            if (title.Length > 28) title = title.Substring(0, 25) + "...";

            bool isCurrentlyTop = NativeMethods.IsTopMost(hWnd);

            if (isCurrentlyTop)
            {
                NativeMethods.SetTopMost(hWnd, false);
                _pinnedWindows.Remove(hWnd);
                _hud.ShowMessage(Lang.T("🔓 Unpinned: ", "🔓 Sabitleme Kaldırıldı: ") + title, System.Windows.Media.Color.FromRgb(150, 160, 180));
            }
            else
            {
                NativeMethods.SetTopMost(hWnd, true);
                _pinnedWindows.Add(hWnd);
                _hud.ShowMessage(Lang.T("📌 Pinned on Top: ", "📌 En Üstte Sabitlendi: ") + title, System.Windows.Media.Color.FromRgb(255, 90, 100));
            }

            NativeMethods.MessageBeep(0);
            UpdateTrayMenu();
        }

        private void AdjustOpacityForegroundWindow(int delta)
        {
            IntPtr hWnd = NativeMethods.GetForegroundWindow();
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) return;

            byte currentAlpha = 255;
            if (_windowOpacities.ContainsKey(hWnd))
            {
                currentAlpha = _windowOpacities[hWnd];
            }

            int newAlpha = Math.Max(50, Math.Min(255, (int)currentAlpha + delta));
            _windowOpacities[hWnd] = (byte)newAlpha;

            NativeMethods.SetOpacity(hWnd, (byte)newAlpha);

            int pct = (int)Math.Round((newAlpha / 255.0) * 100.0);
            _hud.ShowMessage(string.Format(Lang.T("👁️ Opacity: {0}%", "👁️ Şeffaflık: %{0}"), pct), System.Windows.Media.Color.FromRgb(90, 180, 255));
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = Lang.T("Zen Pin - Window Pinner | Serkan ŞENTÜRK", "Zen Pin - Pencere Sabitleyici | Serkan ŞENTÜRK");
            _trayIcon.Icon = LoadAppIcon();
            _trayIcon.Visible = true;

            UpdateTrayMenu();
        }

        private void UpdateTrayMenu()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Text = Lang.T("Zen Pin - Window Pinner | Serkan ŞENTÜRK", "Zen Pin - Pencere Sabitleyici | Serkan ŞENTÜRK");
            }

            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem titleItem = new ToolStripMenuItem(Lang.T("☯️ Zen Pin - Window Pinner", "☯️ Zen Pin - Pencere Sabitleyici"));
            titleItem.Font = new System.Drawing.Font(titleItem.Font, System.Drawing.FontStyle.Bold);
            titleItem.Enabled = false;
            menu.Items.Add(titleItem);

            ToolStripMenuItem authorItem = new ToolStripMenuItem(Lang.T("👤 Developer: Serkan ŞENTÜRK", "👤 Geliştirici: Serkan ŞENTÜRK"));
            authorItem.Font = new System.Drawing.Font(authorItem.Font.FontFamily, 8.5f, System.Drawing.FontStyle.Italic);
            authorItem.Enabled = false;
            menu.Items.Add(authorItem);

            menu.Items.Add(new ToolStripSeparator());

            // Info items
            ToolStripMenuItem hotkeyInfo = new ToolStripMenuItem(Lang.T("📌 Pin / Unpin: Ctrl + Space", "📌 Sabitle / Çöz: Ctrl + Boşluk (Space)"));
            hotkeyInfo.Enabled = false;
            menu.Items.Add(hotkeyInfo);

            ToolStripMenuItem opacityInfo = new ToolStripMenuItem(Lang.T("👁️ Opacity: Ctrl + Alt + Up / Down", "👁️ Şeffaflık: Ctrl + Alt + Yukarı/Aşağı"));
            opacityInfo.Enabled = false;
            menu.Items.Add(opacityInfo);

            menu.Items.Add(new ToolStripSeparator());

            // Clean invalid windows from pinned list
            _pinnedWindows.RemoveWhere(h => !NativeMethods.IsWindow(h));

            if (_pinnedWindows.Count > 0)
            {
                ToolStripMenuItem pinnedListMenu = new ToolStripMenuItem(string.Format(Lang.T("📌 Pinned Windows ({0})", "📌 Sabitli Pencereler ({0})"), _pinnedWindows.Count));
                foreach (IntPtr h in new List<IntPtr>(_pinnedWindows))
                {
                    string wTitle = NativeMethods.GetWindowTitle(h);
                    if (wTitle.Length > 24) wTitle = wTitle.Substring(0, 21) + "...";

                    ToolStripMenuItem winItem = new ToolStripMenuItem("• " + wTitle + Lang.T(" (Unpin)", " (Çöz)"));
                    IntPtr target = h;
                    winItem.Click += (s, e) =>
                    {
                        NativeMethods.SetTopMost(target, false);
                        _pinnedWindows.Remove(target);
                        UpdateTrayMenu();
                    };
                    pinnedListMenu.DropDownItems.Add(winItem);
                }
                menu.Items.Add(pinnedListMenu);

                ToolStripMenuItem unpinAllItem = new ToolStripMenuItem(Lang.T("🔓 Unpin All Windows", "🔓 Tüm Sabitlemeleri Kaldır"));
                unpinAllItem.Click += (s, e) =>
                {
                    foreach (IntPtr h in new List<IntPtr>(_pinnedWindows))
                    {
                        NativeMethods.SetTopMost(h, false);
                    }
                    _pinnedWindows.Clear();
                    UpdateTrayMenu();
                };
                menu.Items.Add(unpinAllItem);
            }
            else
            {
                ToolStripMenuItem noPinned = new ToolStripMenuItem(Lang.T("No pinned windows yet", "Henüz sabitlenmiş pencere yok"));
                noPinned.Enabled = false;
                menu.Items.Add(noPinned);
            }

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem langItem = new ToolStripMenuItem(Lang.T("🌐 Language: EN / TR (Switch to Türkçe)", "🌐 Dil: TR / EN (English'e Geç)"));
            langItem.Click += (s, e) =>
            {
                Lang.Toggle();
                UpdateTrayMenu();
                _hud.ShowMessage(Lang.T("🌐 Language: English (EN)", "🌐 Dil: Türkçe (TR)"), System.Windows.Media.Color.FromRgb(100, 210, 160));
            };
            menu.Items.Add(langItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem devProfileItem = new ToolStripMenuItem(Lang.T("🌐 Developer Page (GitHub)", "🌐 Geliştirici Sayfası (GitHub)"));
            devProfileItem.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/836982756578") { UseShellExecute = true }); } catch { }
            };
            menu.Items.Add(devProfileItem);

            ToolStripMenuItem projectRepoItem = new ToolStripMenuItem("⭐ Zen Pin GitHub");
            projectRepoItem.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/836982756578/Zen-Pin") { UseShellExecute = true }); } catch { }
            };
            menu.Items.Add(projectRepoItem);

            ToolStripMenuItem aboutItem = new ToolStripMenuItem(Lang.T("ℹ️ About Zen Pin", "ℹ️ Zen Pin Hakkında"));
            aboutItem.Click += (s, e) =>
            {
                System.Windows.MessageBox.Show(
                    Lang.T(
                        "Zen Pin - Window Pinner & PiP v1.1.0\n\n" +
                        "🌟 Developer: Serkan ŞENTÜRK\n" +
                        "Copyright: © 2026 Serkan ŞENTÜRK. All Rights Reserved.\n\n" +
                        "Pin any window (YouTube, Notes, Calculator, Video) on top of all other windows.\n\n" +
                        "• Ctrl + Space: Pin / Unpin active window on top\n" +
                        "• Ctrl + Alt + Up / Down: Increase / decrease window opacity\n" +
                        "• Ultra-lightweight (~1 MB RAM) and Portable (Zero-Install)\n\n" +
                        "GitHub: https://github.com/836982756578/Zen-Pin",
                        "Zen Pin - Pencere Sabitleyici & PiP v1.1.0\n\n" +
                        "🌟 Geliştirici: Serkan ŞENTÜRK\n" +
                        "Telif Hakkı: © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.\n\n" +
                        "İstediğiniz herhangi bir pencereyi (YouTube, Notlar, Hesap Makinesi, Video) ekranın en üstünde sabitler.\n\n" +
                        "• Ctrl + Boşluk (Space): En üstte sabitle / Kaldır\n" +
                        "• Ctrl + Alt + Yukarı/Aşağı: Şeffaflığı artır / azalt\n" +
                        "• Ultra Hafif (~1 MB RAM) ve Taşınabilir (Zero-Install Portable)\n\n" +
                        "GitHub: https://github.com/836982756578/Zen-Pin"),
                    Lang.T("About Zen Pin", "Zen Pin Hakkında"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            };
            menu.Items.Add(aboutItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem(Lang.T("🚪 Exit", "🚪 Çıkış"));
            exitItem.Click += (s, e) => ExitApplication();
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = menu;
        }

        private System.Drawing.Icon LoadAppIcon()
        {
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    return new System.Drawing.Icon(icoPath, SystemInformation.SmallIconSize);
                }
                return System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private void ExitApplication()
        {
            // Unpin all windows before exit
            foreach (IntPtr h in _pinnedWindows)
            {
                try { NativeMethods.SetTopMost(h, false); } catch { }
            }
            _pinnedWindows.Clear();

            // Restore opacities
            foreach (KeyValuePair<IntPtr, byte> kvp in _windowOpacities)
            {
                try { NativeMethods.SetOpacity(kvp.Key, 255); } catch { }
            }
            _windowOpacities.Clear();

            if (_msgWindow != null)
            {
                NativeMethods.UnregisterHotKey(_msgWindow.Handle, HK_PIN);
                NativeMethods.UnregisterHotKey(_msgWindow.Handle, HK_OPACITY_DOWN);
                NativeMethods.UnregisterHotKey(_msgWindow.Handle, HK_OPACITY_UP);
                _msgWindow.Dispose();
                _msgWindow = null;
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }

            Shutdown();
        }
    }
}
