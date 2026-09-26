using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zenfile.Models;

namespace Zenfile.Services;

public class SettingsService : ISettingsService
{
    private readonly string _configFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppConfig Config { get; private set; } = new();
    public string ConfigFilePath => _configFilePath;

    public SettingsService(string? customConfigFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customConfigFilePath))
        {
            _configFilePath = customConfigFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "Zenfile");
            _configFilePath = Path.Combine(folder, "config.json");
        }

        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (loaded != null)
                {
                    Config = loaded;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to load config: {ex.Message}");
        }

        Config = new AppConfig();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(Config, JsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to save config: {ex.Message}");
        }
    }

    public void AddTransferHistory(TransferItem item)
    {
        Config.TransferHistory.Insert(0, item);
        if (Config.TransferHistory.Count > Config.MaxHistoryCount)
        {
            Config.TransferHistory.RemoveRange(
                Config.MaxHistoryCount,
                Config.TransferHistory.Count - Config.MaxHistoryCount);
        }
        Save();
    }

    public void AddRecentDestination(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath)) return;

        Config.RecentDestinations.RemoveAll(d => string.Equals(d, destinationPath, StringComparison.OrdinalIgnoreCase));
        Config.RecentDestinations.Insert(0, destinationPath);

        if (Config.RecentDestinations.Count > 10)
        {
            Config.RecentDestinations = Config.RecentDestinations.Take(10).ToList();
        }
        Save();
    }

    public void ClearHistory()
    {
        Config.TransferHistory.Clear();
        Save();
    }
}
