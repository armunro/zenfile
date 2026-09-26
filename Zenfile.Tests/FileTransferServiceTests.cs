using System;
using System.IO;
using Zenfile.Models;
using Zenfile.Services;
using Xunit;

namespace Zenfile.Tests;

public class FileTransferServiceTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _destDir;
    private readonly FileTransferService _service;

    public FileTransferServiceTests()
    {
        var temp = Path.GetTempPath();
        _sourceDir = Path.Combine(temp, $"workdrop_src_{Guid.NewGuid():N}");
        _destDir = Path.Combine(temp, $"workdrop_dst_{Guid.NewGuid():N}");

        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_destDir);

        _service = new FileTransferService();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_sourceDir)) Directory.Delete(_sourceDir, true); } catch { }
        try { if (Directory.Exists(_destDir)) Directory.Delete(_destDir, true); } catch { }
    }

    [Fact]
    public void Copy_SingleFile_CopiesAndLeavesSourceIntact()
    {
        var srcFile = Path.Combine(_sourceDir, "test.txt");
        File.WriteAllText(srcFile, "hello content");

        var request = new TransferRequest
        {
            SourcePaths = [srcFile],
            DestinationDirectory = _destDir,
            Mode = TransferMode.Copy,
            ConflictResolution = ConflictResolution.AutoRename
        };

        var result = _service.ExecuteTransfer(request);

        Assert.True(result.Success);
        Assert.Equal(1, result.SucceededCount);
        Assert.True(File.Exists(srcFile));
        Assert.True(File.Exists(Path.Combine(_destDir, "test.txt")));
        Assert.Equal("hello content", File.ReadAllText(Path.Combine(_destDir, "test.txt")));
    }

    [Fact]
    public void Move_SingleFile_MovesAndDeletesSource()
    {
        var srcFile = Path.Combine(_sourceDir, "document.pdf");
        File.WriteAllText(srcFile, "pdf content");

        var request = new TransferRequest
        {
            SourcePaths = [srcFile],
            DestinationDirectory = _destDir,
            Mode = TransferMode.Move,
            ConflictResolution = ConflictResolution.AutoRename
        };

        var result = _service.ExecuteTransfer(request);

        Assert.True(result.Success);
        Assert.Equal(1, result.SucceededCount);
        Assert.False(File.Exists(srcFile));
        Assert.True(File.Exists(Path.Combine(_destDir, "document.pdf")));
    }

    [Fact]
    public void Collision_AutoRename_CreatesNumberedCopy()
    {
        var existingFile = Path.Combine(_destDir, "report.docx");
        File.WriteAllText(existingFile, "original");

        var srcFile = Path.Combine(_sourceDir, "report.docx");
        File.WriteAllText(srcFile, "new version");

        var request = new TransferRequest
        {
            SourcePaths = [srcFile],
            DestinationDirectory = _destDir,
            Mode = TransferMode.Copy,
            ConflictResolution = ConflictResolution.AutoRename
        };

        var result = _service.ExecuteTransfer(request);

        Assert.True(result.Success);
        Assert.True(File.Exists(existingFile));
        Assert.Equal("original", File.ReadAllText(existingFile));

        var renamedFile = Path.Combine(_destDir, "report (1).docx");
        Assert.True(File.Exists(renamedFile));
        Assert.Equal("new version", File.ReadAllText(renamedFile));
    }

    [Fact]
    public void Collision_Overwrite_ReplacesExistingFile()
    {
        var existingFile = Path.Combine(_destDir, "notes.txt");
        File.WriteAllText(existingFile, "old notes");

        var srcFile = Path.Combine(_sourceDir, "notes.txt");
        File.WriteAllText(srcFile, "updated notes");

        var request = new TransferRequest
        {
            SourcePaths = [srcFile],
            DestinationDirectory = _destDir,
            Mode = TransferMode.Copy,
            ConflictResolution = ConflictResolution.Overwrite
        };

        var result = _service.ExecuteTransfer(request);

        Assert.True(result.Success);
        Assert.Equal("updated notes", File.ReadAllText(existingFile));
    }

    [Fact]
    public void Collision_Skip_LeavesOriginalFile()
    {
        var existingFile = Path.Combine(_destDir, "locked.txt");
        File.WriteAllText(existingFile, "original");

        var srcFile = Path.Combine(_sourceDir, "locked.txt");
        File.WriteAllText(srcFile, "skip me");

        var request = new TransferRequest
        {
            SourcePaths = [srcFile],
            DestinationDirectory = _destDir,
            Mode = TransferMode.Copy,
            ConflictResolution = ConflictResolution.Skip
        };

        var result = _service.ExecuteTransfer(request);

        Assert.True(result.Success);
        Assert.Equal("original", File.ReadAllText(existingFile));
    }

    [Fact]
    public void Copy_DirectoryRecursively_CopiesAllFilesAndSubfolders()
    {
        var folderA = Path.Combine(_sourceDir, "FolderA");
        var subFolder = Path.Combine(folderA, "Sub");
        Directory.CreateDirectory(subFolder);

        File.WriteAllText(Path.Combine(folderA, "file1.txt"), "f1");
        File.WriteAllText(Path.Combine(subFolder, "file2.txt"), "f2");

        var request = new TransferRequest
        {
            SourcePaths = [folderA],
            DestinationDirectory = _destDir,
            Mode = TransferMode.Copy
        };

        var result = _service.ExecuteTransfer(request);

        Assert.True(result.Success);
        var targetFolderA = Path.Combine(_destDir, "FolderA");
        Assert.True(Directory.Exists(targetFolderA));
        Assert.True(File.Exists(Path.Combine(targetFolderA, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(targetFolderA, "Sub", "file2.txt")));
    }
}
