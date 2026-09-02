using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace LidDock.App.Helpers;

public static class accentColorHelper
{
    private const string accentKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string dwmKeyPath = @"Software\Microsoft\Windows\DWM";

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetColorizationColor")]
    private static extern int dwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

    public static (Color baseColor, Color hoverColor, Color subtleColor) getSystemAccentColors()
    {
        try
        {
            using var accentKey = Registry.CurrentUser.OpenSubKey(accentKeyPath);
            if (accentKey?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 20)
            {
                var baseColor = Color.FromRgb(palette[12], palette[13], palette[14]);
                var hoverColor = Color.FromRgb(palette[8], palette[9], palette[10]);
                var subtleColor = Color.FromArgb(0x26, palette[12], palette[13], palette[14]);
                return (baseColor, hoverColor, subtleColor);
            }
        }
        catch
        {
        }

        try
        {
            using var dwmKey = Registry.CurrentUser.OpenSubKey(dwmKeyPath);
            if (dwmKey?.GetValue("AccentColor") is int accentVal)
            {
                var r = (byte)(accentVal & 0xFF);
                var g = (byte)((accentVal >> 8) & 0xFF);
                var b = (byte)((accentVal >> 16) & 0xFF);
                var baseColor = Color.FromRgb(r, g, b);
                var hoverColor = lighten(baseColor, 0.15f);
                var subtleColor = Color.FromArgb(0x26, r, g, b);
                return (baseColor, hoverColor, subtleColor);
            }
        }
        catch
        {
        }

        try
        {
            if (dwmGetColorizationColor(out var colorization, out _) == 0)
            {
                var r = (byte)((colorization >> 16) & 0xFF);
                var g = (byte)((colorization >> 8) & 0xFF);
                var b = (byte)(colorization & 0xFF);
                var baseColor = Color.FromRgb(r, g, b);
                var hoverColor = lighten(baseColor, 0.15f);
                var subtleColor = Color.FromArgb(0x26, r, g, b);
                return (baseColor, hoverColor, subtleColor);
            }
        }
        catch
        {
        }

        var fallbackBase = Color.FromRgb(0x00, 0x78, 0xD4);
        var fallbackHover = Color.FromRgb(0x1E, 0x88, 0xE5);
        var fallbackSubtle = Color.FromArgb(0x26, 0x00, 0x78, 0xD4);
        return (fallbackBase, fallbackHover, fallbackSubtle);
    }

    public static void applySystemAccentColor(bool animate = false)
    {
        var (baseColor, hoverColor, subtleColor) = getSystemAccentColors();
        updateBrush("AccentBrush", baseColor, animate);
        updateBrush("AccentHoverBrush", hoverColor, animate);
        updateBrush("AccentSubtleBrush", subtleColor, animate);
    }

    private static void updateBrush(string resourceKey, Color targetColor, bool animate)
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        if (app.Resources[resourceKey] is SolidColorBrush brush && !brush.IsFrozen)
        {
            if (animate)
            {
                var anim = new System.Windows.Media.Animation.ColorAnimation
                {
                    To = targetColor,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                anim.Completed += (s, e) =>
                {
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    brush.Color = targetColor;
                };
                brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
            }
            else
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                brush.Color = targetColor;
            }
        }
        else
        {
            var newBrush = new SolidColorBrush(targetColor);
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

    private static Color lighten(Color c, float factor)
    {
        var r = (byte)Math.Clamp(c.R + (255 - c.R) * factor, 0, 255);
        var g = (byte)Math.Clamp(c.G + (255 - c.G) * factor, 0, 255);
        var b = (byte)Math.Clamp(c.B + (255 - c.B) * factor, 0, 255);
        return Color.FromArgb(c.A, r, g, b);
    }
}
