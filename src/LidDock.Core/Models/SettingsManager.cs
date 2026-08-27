using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace LidDock.Core.Models;

public static class settingsManager
{
    private static readonly string settingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LidDock");

    private static readonly string settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    private const string runRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string appName = "LidDock";

    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

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
                    var settings = JsonSerializer.Deserialize<appSettings>(json, jsonOptions);
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

                var json = JsonSerializer.Serialize(settings, jsonOptions);
                var tempFilePath = settingsFilePath + ".tmp";
                File.WriteAllText(tempFilePath, json);
                File.Move(tempFilePath, settingsFilePath, true);

                updateAutoStartRegistry(settings.startWithWindows);
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
                    key.SetValue(appName, $"\"{processPath}\"");
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
