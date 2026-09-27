using System;
using System.IO;
using Zenfile.Models;
using Zenfile.Services;
using Xunit;

namespace Zenfile.Tests;

public class WorkRootServiceTests : IDisposable
{
    private readonly string _tempRootDirectory;
    private readonly WorkRootService _service;

    public WorkRootServiceTests()
    {
        _tempRootDirectory = Path.Combine(Path.GetTempPath(), $"zenfile_test_root_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRootDirectory);
        _service = new WorkRootService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootDirectory))
        {
            try { Directory.Delete(_tempRootDirectory, true); } catch { }
        }
    }

    [Theory]
    [InlineData("ValidName", true)]
    [InlineData("Project-123_test", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Invalid/Name", false)]
    [InlineData("Invalid\\Name", false)]
    [InlineData("Invalid?Name", false)]
    public void IsValidFolderName_ValidatesCorrectly(string name, bool expectedValid)
    {
        var isValid = _service.IsValidFolderName(name, out _);
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void ScanTasks_FindsSubdirectoriesWithFileCounts()
    {
        var task1 = Path.Combine(_tempRootDirectory, "TaskA");
        var task2 = Path.Combine(_tempRootDirectory, "TaskB");
        Directory.CreateDirectory(task1);
        Directory.CreateDirectory(task2);

        File.WriteAllText(Path.Combine(task1, "file1.txt"), "hello");
        File.WriteAllText(Path.Combine(task1, "file2.txt"), "world");

        var root = new WorkRoot
        {
            Name = "MainRoot",
            Path = _tempRootDirectory
        };

        var tasks = _service.ScanTasks(root);

        Assert.Equal(2, tasks.Count);
        var taskA = tasks.Find(t => t.Name == "TaskA");
        Assert.NotNull(taskA);
        Assert.Equal(2, taskA.FileCount);
        Assert.Equal("MainRoot", taskA.WorkRootName);
    }

    [Fact]
    public void CreateTaskFolder_CreatesDirectoryAndReturnsTask()
    {
        var root = new WorkRoot
        {
            Name = "MainRoot",
            Path = _tempRootDirectory
        };

        var task = _service.CreateTaskFolder(root, "NewProject");

        Assert.NotNull(task);
        Assert.Equal("NewProject", task.Name);
        Assert.True(Directory.Exists(task.FullPath));
    }

    [Fact]
    public void DeleteTaskFolder_RemovesDirectory()
    {
        var subDir = Path.Combine(_tempRootDirectory, "ToDelete");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "dummy.txt"), "data");

        Assert.True(Directory.Exists(subDir));
        var result = _service.DeleteTaskFolder(subDir);

        Assert.True(result);
        Assert.False(Directory.Exists(subDir));
    }
}
