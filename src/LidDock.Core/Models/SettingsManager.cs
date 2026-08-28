using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace LidDock.Core.Models;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(appSettings))]
[JsonSerializable(typeof(gitHubReleaseInfo))]
internal partial class appSettingsJsonContext : JsonSerializerContext
{
}

public static class settingsManager
{
    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    private static extern uint registerWindowMessage(string lpString);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    private static extern bool postMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static uint settingsMessageId;
    private const int hwndBroadcast = 0xffff;
    public static event Action? onSettingsChanged;

    private static void broadcastSettingsChanged()
    {
        try
        {
            onSettingsChanged?.Invoke();
            if (settingsMessageId == 0)
            {
                settingsMessageId = registerWindowMessage("LidDock_SettingsChanged_Event");
            }
            if (settingsMessageId != 0)
            {
                postMessage((IntPtr)hwndBroadcast, settingsMessageId, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch
        {
        }
    }

    private static readonly string settingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LidDock");

    private static readonly string settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    private const string runRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string appName = "LidDock";

    private static readonly object fileLock = new object();

    public static appSettings loadSettings()
    {
        lock (fileLock)
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    var json = File.ReadAllText(settingsFilePath);
                    var settings = JsonSerializer.Deserialize(json, appSettingsJsonContext.Default.appSettings);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch
            {
            }

            var defaultSettings = new appSettings();
            saveSettings(defaultSettings);
            return defaultSettings;
        }
    }

    public static void saveSettings(appSettings settings)
    {
        lock (fileLock)
        {
            try
            {
                if (!Directory.Exists(settingsDirectory))
                {
                    Directory.CreateDirectory(settingsDirectory);
                }

                var json = JsonSerializer.Serialize(settings, appSettingsJsonContext.Default.appSettings);
                var tempFilePath = settingsFilePath + ".tmp";
                File.WriteAllText(tempFilePath, json);
                File.Move(tempFilePath, settingsFilePath, true);

                updateAutoStartRegistry(settings.startWithWindows);
                broadcastSettingsChanged();
            }
            catch
            {
            }
        }
    }

    public static string getPermanentDaemonPath()
    {
        return Path.Combine(settingsDirectory, "LidDock.exe");
    }

    public static void ensurePermanentInstallation()
    {
        try
        {
            var currentPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentPath) ||
                currentPath.EndsWith("LidDock.UI.exe", StringComparison.OrdinalIgnoreCase) ||
                currentPath.EndsWith("LidDock.App.exe", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!Directory.Exists(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            var permanentPath = getPermanentDaemonPath();
            if (!string.Equals(currentPath, permanentPath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(permanentPath))
                {
                    var srcInfo = new FileInfo(currentPath);
                    var dstInfo = new FileInfo(permanentPath);
                    if (srcInfo.Length == dstInfo.Length)
                    {
                        return;
                    }
                }
                File.Copy(currentPath, permanentPath, true);
            }
        }
        catch
        {
        }
    }

    public static void updateAutoStartRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runRegistryKey, true);
            if (key == null)
            {
                return;
            }

            if (enable)
            {
                ensurePermanentInstallation();
                var permanentPath = getPermanentDaemonPath();
                var targetPath = File.Exists(permanentPath) ? permanentPath : Environment.ProcessPath;

                if (!string.IsNullOrEmpty(targetPath))
                {
                    key.SetValue(appName, $"\"{targetPath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch
        {
        }
    }

    public static string getSettingsFilePath() => settingsFilePath;
}
