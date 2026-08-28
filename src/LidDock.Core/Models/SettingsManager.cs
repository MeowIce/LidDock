using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace LidDock.Core.Models;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(appSettings))]
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
                        if (settings.activeProfile == operationalProfileType.custom)
                        {
                            settings.activeProfile = operationalProfileType.smartDocked;
                        }
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
                var processPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(processPath))
                {
                    var baseDir = Path.GetDirectoryName(processPath) ?? string.Empty;
                    var daemonPath = Path.Combine(baseDir, "LidDock.exe");
                    if (!File.Exists(daemonPath))
                    {
                        daemonPath = Path.Combine(baseDir, "LidDock.Daemon.exe");
                    }
                    var targetPath = File.Exists(daemonPath) ? daemonPath : processPath;
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
