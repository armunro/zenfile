using System;
using Zenfile.Models;

namespace Zenfile.Services;

public interface ISettingsService
{
    AppConfig Config { get; }
    string ConfigFilePath { get; }
    void Load();
    void Save();
    void AddTransferHistory(TransferItem item);
    void AddRecentDestination(string destinationPath);
    void ClearHistory();
}
