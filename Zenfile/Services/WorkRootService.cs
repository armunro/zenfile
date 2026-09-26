using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zenfile.Models;

namespace Zenfile.Services;

public class WorkRootService : IWorkRootService
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    public bool IsValidFolderName(string name, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            errorMessage = "Folder name cannot be empty.";
            return false;
        }

        if (name.IndexOfAny(InvalidChars) >= 0)
        {
            errorMessage = "Folder name contains invalid characters.";
            return false;
        }

        return true;
    }

    public bool EnsureDirectoryExists(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorkRootService] Error creating directory {path}: {ex.Message}");
            return false;
        }
    }

    public List<WorkTaskFolder> ScanTasks(WorkRoot root)
    {
        var result = new List<WorkTaskFolder>();
        if (root == null || string.IsNullOrWhiteSpace(root.Path))
            return result;

        if (!Directory.Exists(root.Path))
        {
            if (root.AutoCreateFolderIfMissing)
            {
                EnsureDirectoryExists(root.Path);
            }
            else
            {
                return result;
            }
        }

        try
        {
            var rootDirInfo = new DirectoryInfo(root.Path);
            var subDirs = rootDirInfo.GetDirectories();

            foreach (var dir in subDirs.OrderBy(d => d.Name))
            {
                // Skip hidden or system folders
                if ((dir.Attributes & FileAttributes.Hidden) != 0 ||
                    (dir.Attributes & FileAttributes.System) != 0)
                {
                    continue;
                }

                int fileCount = 0;
                int subDirCount = 0;
                try
                {
                    fileCount = dir.GetFiles().Length;
                    subDirCount = dir.GetDirectories().Length;
                }
                catch
                {
                    // Ignore access issues on counts
                }

                result.Add(new WorkTaskFolder
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    WorkRootPath = root.Path,
                    WorkRootName = string.IsNullOrWhiteSpace(root.Name) ? root.Path : root.Name,
                    RelativePath = dir.Name,
                    LastModified = dir.LastWriteTime,
                    FileCount = fileCount,
                    SubdirectoryCount = subDirCount
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorkRootService] Error scanning root {root.Path}: {ex.Message}");
        }

        root.CachedTasks = result;
        return result;
    }

    public List<WorkTaskFolder> ScanAllTasks(IEnumerable<WorkRoot> roots)
    {
        var allTasks = new List<WorkTaskFolder>();
        foreach (var root in roots.Where(r => r.IsEnabled))
        {
            allTasks.AddRange(ScanTasks(root));
        }
        return allTasks;
    }

    public WorkTaskFolder CreateTaskFolder(WorkRoot root, string taskFolderName)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (!IsValidFolderName(taskFolderName, out var err))
        {
            throw new ArgumentException(err ?? "Invalid folder name", nameof(taskFolderName));
        }

        EnsureDirectoryExists(root.Path);
        var targetPath = Path.Combine(root.Path, taskFolderName.Trim());

        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
        }

        var dirInfo = new DirectoryInfo(targetPath);
        var task = new WorkTaskFolder
        {
            Name = dirInfo.Name,
            FullPath = dirInfo.FullName,
            WorkRootPath = root.Path,
            WorkRootName = string.IsNullOrWhiteSpace(root.Name) ? root.Path : root.Name,
            RelativePath = dirInfo.Name,
            LastModified = dirInfo.LastWriteTime,
            FileCount = 0,
            SubdirectoryCount = 0
        };

        if (root.CachedTasks.All(t => !string.Equals(t.FullPath, task.FullPath, StringComparison.OrdinalIgnoreCase)))
        {
            root.CachedTasks.Add(task);
        }

        return task;
    }

    public bool DeleteTaskFolder(string taskFolderPath)
    {
        try
        {
            if (Directory.Exists(taskFolderPath))
            {
                Directory.Delete(taskFolderPath, true);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorkRootService] Error deleting {taskFolderPath}: {ex.Message}");
            return false;
        }
    }
}
