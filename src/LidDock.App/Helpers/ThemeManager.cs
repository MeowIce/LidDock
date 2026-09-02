using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace LidDock.App.Helpers;

public static class themeManager
{
    private const string personalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    static themeManager()
    {
        try
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.VisualStyle)
                {
                    Application.Current?.Dispatcher.InvokeAsync(() => applySystemTheme(true));
                }
            };
        }
        catch
        {
        }
    }

    public static bool isSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(personalizeKeyPath);
            if (key?.GetValue("AppsUseLightTheme") is int appLight)
            {
                return appLight == 0;
            }
            if (key?.GetValue("SystemUsesLightTheme") is int sysLight)
            {
                return sysLight == 0;
            }
        }
        catch
        {
        }
        return true;
    }

    public static void applySystemTheme(bool animate = false)
    {
        var isDark = isSystemDarkMode();
        if (animate)
        {
            performThemeTransition(isDark);
        }
        else
        {
            applyTheme(isDark);
            accentColorHelper.applySystemAccentColor();
        }
    }

    private static void performThemeTransition(bool isDark)
    {
        var app = Application.Current;
        if (app == null)
        {
            applyTheme(isDark);
            accentColorHelper.applySystemAccentColor();
            return;
        }

        var transitions = new List<(Window window, object originalContent, Grid container, Image overlay)>();

        foreach (Window window in app.Windows)
        {
            if (!window.IsVisible || window.ActualWidth <= 0 || window.ActualHeight <= 0 || window.Content is not UIElement rootElement)
            {
                continue;
            }

            try
            {
                var width = (int)Math.Max(1, rootElement.RenderSize.Width);
                var height = (int)Math.Max(1, rootElement.RenderSize.Height);
                var dpi = VisualTreeHelper.GetDpi(window);

                var rtb = new RenderTargetBitmap(
                    Math.Max(1, (int)(width * dpi.DpiScaleX)),
                    Math.Max(1, (int)(height * dpi.DpiScaleY)),
                    dpi.PixelsPerInchX,
                    dpi.PixelsPerInchY,
                    PixelFormats.Pbgra32);

                rtb.Render(rootElement);
                rtb.Freeze();

                var overlayImage = new Image
                {
                    Source = rtb,
                    Width = width,
                    Height = height,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    IsHitTestVisible = false
                };

                var originalContent = window.Content;
                var container = new Grid();

                window.Content = null;
                container.Children.Add((UIElement)originalContent);
                container.Children.Add(overlayImage);
                window.Content = container;

                transitions.Add((window, originalContent, container, overlayImage));
            }
            catch
            {
            }
        }

        applyTheme(isDark);
        accentColorHelper.applySystemAccentColor();

        foreach (var t in transitions)
        {
            var overlay = t.overlay;
            var window = t.window;
            var container = t.container;
            var originalContent = t.originalContent;

            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            fadeOut.Completed += (s, e) =>
            {
                try
                {
                    container.Children.Remove(overlay);
                    if (window.Content == container)
                    {
                        window.Content = null;
                        container.Children.Remove((UIElement)originalContent);
                        window.Content = originalContent;
                    }
                }
                catch
                {
                }
            };

            overlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }

    public static void applyTheme(bool isDark)
    {
        if (isDark)
        {
            updateBrush("WindowBackgroundBrush", Color.FromArgb(0xB8, 0x1C, 0x1C, 0x1C));
            updateBrush("SurfaceBrush", Color.FromArgb(0x99, 0x2E, 0x2E, 0x2E));
            updateBrush("SurfaceHoverBrush", Color.FromArgb(0xB3, 0x3E, 0x3E, 0x3E));
            updateBrush("CardBrush", Color.FromArgb(0x99, 0x26, 0x26, 0x26));
            updateBrush("CardBorderBrush", Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
            updateBrush("TextPrimaryBrush", Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3));
            updateBrush("TextSecondaryBrush", Color.FromArgb(0xFF, 0xA8, 0xA8, 0xA8));
            updateBrush("ControlBorderBrush", Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            updateBrush("SliderTrackEmptyBrush", Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            updateBrush("SliderTrackEmptyHoverBrush", Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
            updateBrush("ToggleTrackBrush", Color.FromArgb(0xFF, 0x33, 0x33, 0x33));
            updateBrush("ToggleThumbBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            updateBrush("RadioFillBrush", Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
            updateBrush("RadioHoverStrokeBrush", Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF));
        }
        else
        {
            updateBrush("WindowBackgroundBrush", Color.FromArgb(0xB8, 0xF9, 0xF9, 0xF9));
            updateBrush("SurfaceBrush", Color.FromArgb(0x99, 0xEB, 0xEB, 0xEB));
            updateBrush("SurfaceHoverBrush", Color.FromArgb(0xB3, 0xDE, 0xDE, 0xDE));
            updateBrush("CardBrush", Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF));
            updateBrush("CardBorderBrush", Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            updateBrush("TextPrimaryBrush", Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A));
            updateBrush("TextSecondaryBrush", Color.FromArgb(0xFF, 0x5C, 0x5C, 0x5C));
            updateBrush("ControlBorderBrush", Color.FromArgb(0x28, 0x00, 0x00, 0x00));
            updateBrush("SliderTrackEmptyBrush", Color.FromArgb(0x38, 0x00, 0x00, 0x00));
            updateBrush("SliderTrackEmptyHoverBrush", Color.FromArgb(0x55, 0x00, 0x00, 0x00));
            updateBrush("ToggleTrackBrush", Color.FromArgb(0xFF, 0xE6, 0xE6, 0xE6));
            updateBrush("ToggleThumbBrush", Color.FromArgb(0xFF, 0x5C, 0x5C, 0x5C));
            updateBrush("RadioFillBrush", Color.FromArgb(0x14, 0x00, 0x00, 0x00));
            updateBrush("RadioHoverStrokeBrush", Color.FromArgb(0x80, 0x00, 0x00, 0x00));
        }

        var app = Application.Current;
        if (app != null)
        {
            foreach (Window window in app.Windows)
            {
                windowBackdropHelper.updateWindowDarkMode(window, isDark);
            }
        }
    }

    private static void updateBrush(string resourceKey, Color color)
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        var newBrush = new SolidColorBrush(color);
        app.Resources[resourceKey] = newBrush;

        if (app.Resources.MergedDictionaries != null)
        {
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                dict[resourceKey] = newBrush;
            }
        }
    }
}
