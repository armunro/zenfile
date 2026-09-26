using System.Collections.Generic;
using Zenfile.Models;

namespace Zenfile.Services;

public interface IWorkRootService
{
    List<WorkTaskFolder> ScanTasks(WorkRoot root);
    List<WorkTaskFolder> ScanAllTasks(IEnumerable<WorkRoot> roots);
    WorkTaskFolder CreateTaskFolder(WorkRoot root, string taskFolderName);
    bool DeleteTaskFolder(string taskFolderPath);
    bool EnsureDirectoryExists(string path);
    bool IsValidFolderName(string name, out string? errorMessage);
}
