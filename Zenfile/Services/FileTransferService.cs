using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Zenfile.Models;

namespace Zenfile.Services;

public class FileTransferService : IFileTransferService
{
    public string ResolveUniqueFilePath(string destinationDirectory, string originalFileName)
    {
        var targetPath = Path.Combine(destinationDirectory, originalFileName);
        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
        var ext = Path.GetExtension(originalFileName);

        int counter = 1;
        while (true)
        {
            var candidate = Path.Combine(destinationDirectory, $"{nameWithoutExt} ({counter}){ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
            counter++;
        }
    }

    public string ResolveUniqueDirectoryPath(string destinationDirectory, string originalDirName)
    {
        var targetPath = Path.Combine(destinationDirectory, originalDirName);
        if (!Directory.Exists(targetPath))
        {
            return targetPath;
        }

        int counter = 1;
        while (true)
        {
            var candidate = Path.Combine(destinationDirectory, $"{originalDirName} ({counter})");
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
            counter++;
        }
    }

    public Task<TransferResult> ExecuteTransferAsync(
        TransferRequest request,
        IProgress<TransferItem>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ExecuteTransfer(request, progress, cancellationToken), cancellationToken);
    }

    public TransferResult ExecuteTransfer(
        TransferRequest request,
        IProgress<TransferItem>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TransferResult
        {
            DestinationDirectory = request.DestinationDirectory
        };

        if (string.IsNullOrWhiteSpace(request.DestinationDirectory))
        {
            result.OverallMessage = "Destination directory was not specified.";
            return result;
        }

        try
        {
            if (!Directory.Exists(request.DestinationDirectory))
            {
                Directory.CreateDirectory(request.DestinationDirectory);
            }
        }
        catch (Exception ex)
        {
            result.OverallMessage = $"Failed to create destination directory: {ex.Message}";
            return result;
        }

        foreach (var sourcePath in request.SourcePaths)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (string.IsNullOrWhiteSpace(sourcePath))
                continue;

            var trimmedPath = sourcePath.Trim('\"', ' ');
            if (File.Exists(trimmedPath))
            {
                var item = TransferSingleFile(trimmedPath, request.DestinationDirectory, request.Mode, request.ConflictResolution);
                result.TransferredItems.Add(item);
                if (item.Success) result.SucceededCount++;
                else result.FailedCount++;
                progress?.Report(item);
            }
            else if (Directory.Exists(trimmedPath))
            {
                var item = TransferSingleDirectory(trimmedPath, request.DestinationDirectory, request.Mode, request.ConflictResolution);
                result.TransferredItems.Add(item);
                if (item.Success) result.SucceededCount++;
                else result.FailedCount++;
                progress?.Report(item);
            }
            else
            {
                var item = new TransferItem
                {
                    SourcePath = trimmedPath,
                    DestinationPath = request.DestinationDirectory,
                    IsDirectory = false,
                    Success = false,
                    ErrorMessage = "Source file or directory does not exist.",
                    Mode = request.Mode,
                    Timestamp = DateTime.Now
                };
                result.TransferredItems.Add(item);
                result.FailedCount++;
                progress?.Report(item);
            }
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        result.OverallMessage = result.FailedCount == 0
            ? $"Successfully { (request.Mode == TransferMode.Move ? "moved" : "copied") } {result.SucceededCount} item(s)."
            : $"Transferred {result.SucceededCount} item(s) with {result.FailedCount} error(s).";

        return result;
    }

    private TransferItem TransferSingleFile(
        string sourceFilePath,
        string destinationDir,
        TransferMode mode,
        ConflictResolution conflictResolution)
    {
        var fileInfo = new FileInfo(sourceFilePath);
        var fileName = fileInfo.Name;
        var destinationPath = Path.Combine(destinationDir, fileName);

        var item = new TransferItem
        {
            SourcePath = sourceFilePath,
            DestinationPath = destinationPath,
            IsDirectory = false,
            SizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            Mode = mode,
            Timestamp = DateTime.Now
        };

        try
        {
            if (File.Exists(destinationPath))
            {
                // Check if source and destination are the exact same file
                if (string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
                {
                    item.Success = true;
                    item.ErrorMessage = "Source and destination are the same file.";
                    return item;
                }

                switch (conflictResolution)
                {
                    case ConflictResolution.Skip:
                        item.Success = true;
                        item.ErrorMessage = "Skipped existing file.";
                        return item;

                    case ConflictResolution.AutoRename:
                        destinationPath = ResolveUniqueFilePath(destinationDir, fileName);
                        item.DestinationPath = destinationPath;
                        break;

                    case ConflictResolution.Overwrite:
                        // Overwrite existing file
                        File.Delete(destinationPath);
                        break;
                }
            }

            if (mode == TransferMode.Move)
            {
                File.Move(sourceFilePath, destinationPath, true);
            }
            else
            {
                File.Copy(sourceFilePath, destinationPath, true);
            }

            item.Success = true;
        }
        catch (Exception ex)
        {
            item.Success = false;
            item.ErrorMessage = ex.Message;
        }

        return item;
    }

    private TransferItem TransferSingleDirectory(
        string sourceDirPath,
        string destinationDir,
        TransferMode mode,
        ConflictResolution conflictResolution)
    {
        var dirInfo = new DirectoryInfo(sourceDirPath);
        var dirName = dirInfo.Name;
        var destinationPath = Path.Combine(destinationDir, dirName);

        var item = new TransferItem
        {
            SourcePath = sourceDirPath,
            DestinationPath = destinationPath,
            IsDirectory = true,
            Mode = mode,
            Timestamp = DateTime.Now
        };

        try
        {
            // Avoid moving into itself or its own subdirectory
            var fullSource = Path.GetFullPath(sourceDirPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullDestDir = Path.GetFullPath(destinationDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (fullDestDir.StartsWith(fullSource, StringComparison.OrdinalIgnoreCase))
            {
                item.Success = false;
                item.ErrorMessage = "Cannot move a directory inside itself or its children.";
                return item;
            }

            if (Directory.Exists(destinationPath))
            {
                if (string.Equals(fullSource, Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
                {
                    item.Success = true;
                    item.ErrorMessage = "Source and destination directory are the same.";
                    return item;
                }

                switch (conflictResolution)
                {
                    case ConflictResolution.Skip:
                        item.Success = true;
                        item.ErrorMessage = "Skipped existing directory.";
                        return item;

                    case ConflictResolution.AutoRename:
                        destinationPath = ResolveUniqueDirectoryPath(destinationDir, dirName);
                        item.DestinationPath = destinationPath;
                        break;

                    case ConflictResolution.Overwrite:
                        Directory.Delete(destinationPath, true);
                        break;
                }
            }

            if (mode == TransferMode.Move)
            {
                // Try Directory.Move, fallback to recursive copy + delete for cross-volume
                try
                {
                    Directory.Move(sourceDirPath, destinationPath);
                }
                catch (IOException)
                {
                    CopyDirectoryRecursively(sourceDirPath, destinationPath);
                    Directory.Delete(sourceDirPath, true);
                }
            }
            else
            {
                CopyDirectoryRecursively(sourceDirPath, destinationPath);
            }

            item.Success = true;
        }
        catch (Exception ex)
        {
            item.Success = false;
            item.ErrorMessage = ex.Message;
        }

        return item;
    }

    private static void CopyDirectoryRecursively(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            var nextTargetSubDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectoryRecursively(subDir.FullName, nextTargetSubDir);
        }
    }
}
