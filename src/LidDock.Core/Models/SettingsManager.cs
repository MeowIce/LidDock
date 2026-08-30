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

    private static void broadcastSettingsChanged()
    {
        try
        {
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
            appSettings? settings = null;
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    var json = File.ReadAllText(settingsFilePath);
                    settings = JsonSerializer.Deserialize(json, appSettingsJsonContext.Default.appSettings);
                }
            }
            catch
            {
            }

            var result = settings ?? new appSettings();

            try
            {
                using var liddockKey = Registry.CurrentUser.OpenSubKey(@"Software\LidDock", true);
                if (liddockKey?.GetValue("StartWithWindows") is int installerChoice)
                {
                    result.startWithWindows = installerChoice != 0;
                    liddockKey.DeleteValue("StartWithWindows", false);
                    saveSettings(result);
                    return result;
                }
            }
            catch
            {
            }

            if (settings == null)
            {
                saveSettings(result);
            }

            return result;
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
        return Environment.ProcessPath ?? Path.Combine(settingsDirectory, "LidDock.exe");
    }

    public static void ensurePermanentInstallation()
    {
        try
        {
            var oldPortablePath = Path.Combine(settingsDirectory, "LidDock.exe");
            var currentPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(currentPath) &&
                !string.Equals(currentPath, oldPortablePath, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(oldPortablePath))
            {
                File.Delete(oldPortablePath);
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
                var targetPath = Environment.ProcessPath;
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

    public static void performUninstall(bool keepSettings = false)
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(runRegistryKey, true);
            runKey?.DeleteValue(appName, false);
        }
        catch
        {
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\LidDock", false);
        }
        catch
        {
        }

        try
        {
            var localDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LidDock");
            if (Directory.Exists(localDir))
            {
                Directory.Delete(localDir, true);
            }
        }
        catch
        {
        }

        if (!keepSettings)
        {
            try
            {
                if (Directory.Exists(settingsDirectory))
                {
                    Directory.Delete(settingsDirectory, true);
                }
            }
            catch
            {
            }
        }
    }

    public static string getSettingsFilePath() => settingsFilePath;
}
