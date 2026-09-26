using System;
using System.IO;
using Zenfile.Models;
using Zenfile.Services;
using Xunit;

namespace Zenfile.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempConfigPath;

    public SettingsServiceTests()
    {
        _tempConfigPath = Path.Combine(Path.GetTempPath(), $"workdrop_test_config_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempConfigPath))
        {
            try { File.Delete(_tempConfigPath); } catch { }
        }
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_InitializesDefaultConfig()
    {
        var service = new SettingsService(_tempConfigPath);
        Assert.NotNull(service.Config);
        Assert.Empty(service.Config.WorkRoots);
        Assert.Equal(TransferMode.Move, service.Config.DefaultMode);
    }

    [Fact]
    public void SaveAndLoad_PersistsConfigCorrectly()
    {
        var service = new SettingsService(_tempConfigPath);
        service.Config.DefaultMode = TransferMode.Copy;
        service.Config.DefaultConflictResolution = ConflictResolution.Overwrite;
        service.Config.WorkRoots.Add(new WorkRoot
        {
            Name = "My Projects",
            Path = @"C:\Work\Projects",
            ColorHex = "#FF0000"
        });

        service.Save();

        var service2 = new SettingsService(_tempConfigPath);
        Assert.Equal(TransferMode.Copy, service2.Config.DefaultMode);
        Assert.Equal(ConflictResolution.Overwrite, service2.Config.DefaultConflictResolution);
        Assert.Single(service2.Config.WorkRoots);
        Assert.Equal("My Projects", service2.Config.WorkRoots[0].Name);
        Assert.Equal(@"C:\Work\Projects", service2.Config.WorkRoots[0].Path);
    }

    [Fact]
    public void AddTransferHistory_PrependsAndLimitsCount()
    {
        var service = new SettingsService(_tempConfigPath);
        service.Config.MaxHistoryCount = 3;

        service.AddTransferHistory(new TransferItem { SourcePath = "file1.txt", DestinationPath = "dest1" });
        service.AddTransferHistory(new TransferItem { SourcePath = "file2.txt", DestinationPath = "dest2" });
        service.AddTransferHistory(new TransferItem { SourcePath = "file3.txt", DestinationPath = "dest3" });
        service.AddTransferHistory(new TransferItem { SourcePath = "file4.txt", DestinationPath = "dest4" });

        Assert.Equal(3, service.Config.TransferHistory.Count);
        Assert.Equal("file4.txt", service.Config.TransferHistory[0].SourcePath);
    }

    [Fact]
    public void AddRecentDestination_DeduplicatesAndMovesToTop()
    {
        var service = new SettingsService(_tempConfigPath);
        service.AddRecentDestination(@"C:\DestA");
        service.AddRecentDestination(@"C:\DestB");
        service.AddRecentDestination(@"C:\DestA");

        Assert.Equal(2, service.Config.RecentDestinations.Count);
        Assert.Equal(@"C:\DestA", service.Config.RecentDestinations[0]);
        Assert.Equal(@"C:\DestB", service.Config.RecentDestinations[1]);
    }
}
