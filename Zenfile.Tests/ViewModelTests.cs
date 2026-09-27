using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zenfile.Models;
using Zenfile.Services;
using Zenfile.ViewModels;
using Xunit;

namespace Zenfile.Tests;

public class ViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly SettingsService _settingsService;
    private readonly WorkRootService _workRootService;
    private readonly FileTransferService _transferService;
    private readonly ShellContextMenuService _shellService;

    public ViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zenfile_vm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");

        _settingsService = new SettingsService(_configPath);
        _workRootService = new WorkRootService();
        _transferService = new FileTransferService();
        _shellService = new ShellContextMenuService();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void MainViewModel_AddWorkRoot_AddsToCollectionAndSaves()
    {
        var rootDir = Path.Combine(_tempDir, "Root1");
        Directory.CreateDirectory(rootDir);

        var vm = new MainViewModel(_settingsService, _workRootService, _transferService, _shellService);
        vm.AddWorkRoot("My Root", rootDir);

        Assert.Single(vm.WorkRoots);
        Assert.Equal("My Root", vm.WorkRoots[0].Name);
        Assert.Equal(vm.WorkRoots[0], vm.SelectedRoot);
    }

    [Fact]
    public void MainViewModel_CreateTask_CreatesSubfolder()
    {
        var rootDir = Path.Combine(_tempDir, "Root1");
        Directory.CreateDirectory(rootDir);

        var vm = new MainViewModel(_settingsService, _workRootService, _transferService, _shellService);
        vm.AddWorkRoot("My Root", rootDir);

        vm.NewTaskName = "ProjectX";
        vm.CreateTaskCommand.Execute(null);

        Assert.Single(vm.TaskFolders);
        Assert.Equal("ProjectX", vm.TaskFolders[0].Name);
        Assert.True(Directory.Exists(Path.Combine(rootDir, "ProjectX")));
    }

    [Fact]
    public void MainViewModel_Filter_FiltersTasksByQuery()
    {
        var rootDir = Path.Combine(_tempDir, "Root1");
        Directory.CreateDirectory(rootDir);
        Directory.CreateDirectory(Path.Combine(rootDir, "AlphaProject"));
        Directory.CreateDirectory(Path.Combine(rootDir, "BetaProject"));

        var vm = new MainViewModel(_settingsService, _workRootService, _transferService, _shellService);
        vm.AddWorkRoot("My Root", rootDir);
        vm.RefreshTasks();

        Assert.Equal(2, vm.TaskFolders.Count);

        vm.SearchFilter = "Alpha";
        Assert.Single(vm.FilteredTaskFolders);
        Assert.Equal("AlphaProject", vm.FilteredTaskFolders[0].Name);

        vm.SearchFilter = "";
        Assert.Equal(2, vm.FilteredTaskFolders.Count);
    }

    [Fact]
    public async Task PopupPickerViewModel_ExecuteTransfer_TransfersFilesAndRecordsRecent()
    {
        var rootDir = Path.Combine(_tempDir, "Root1");
        var taskDir = Path.Combine(rootDir, "Task1");
        Directory.CreateDirectory(taskDir);

        _settingsService.Config.WorkRoots.Add(new WorkRoot { Name = "Root1", Path = rootDir });
        _settingsService.Save();

        var srcFile = Path.Combine(_tempDir, "sample.txt");
        File.WriteAllText(srcFile, "data");

        bool closed = false;
        var vm = new PopupPickerViewModel(
            [srcFile],
            _settingsService,
            _workRootService,
            _transferService,
            TransferMode.Move,
            ConflictResolution.AutoRename,
            () => closed = true);

        Assert.NotNull(vm.SelectedTask);
        Assert.Equal("Task1", vm.SelectedTask.Name);

        await vm.ExecuteTransferAsync();

        Assert.True(vm.IsSuccess);
        Assert.True(File.Exists(Path.Combine(taskDir, "sample.txt")));
        Assert.False(File.Exists(srcFile));
        Assert.Contains(taskDir, _settingsService.Config.RecentDestinations);
    }
}
