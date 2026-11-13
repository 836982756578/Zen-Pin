// Zen Pin - Pencere Sabitleyici & PiP (Always on Top)
// Geliştirici: Serkan ŞENTÜRK
// Telif Hakkı © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ZenPin
{
    public class HudOverlay : Window
    {
        private readonly TextBlock _textBlock;
        private readonly Border _pillBorder;
        private DispatcherTimer _hideTimer;

        public HudOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            Focusable = false;
            SizeToContent = SizeToContent.WidthAndHeight;

            _pillBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 24, 26, 36)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(18, 9, 20, 9),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 5,
                    Opacity = 0.6
                }
            };

            _textBlock = new TextBlock
            {
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            _pillBorder.Child = _textBlock;
            Content = _pillBorder;

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                FadeOut();
            };
        }

        public void ShowMessage(string message, Color accentColor)
        {
            _hideTimer.Stop();

            _textBlock.Text = message;
            _pillBorder.BorderBrush = new SolidColorBrush(accentColor);

            UpdateLayout();

            // Center horizontally near top of screen
            Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
            Top = 45;

            Opacity = 1.0;
            Show();

            _hideTimer.Start();
        }

        private void FadeOut()
        {
            DoubleAnimation fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(300));
            fade.Completed += (s, e) => Hide();
            BeginAnimation(OpacityProperty, fade);
        }
    }
}
