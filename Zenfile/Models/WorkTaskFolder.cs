using System;

namespace Zenfile.Models;

public class WorkTaskFolder
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string WorkRootPath { get; set; } = string.Empty;
    public string WorkRootName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public int FileCount { get; set; }
    public int SubdirectoryCount { get; set; }

    public string DisplaySubtitle => string.IsNullOrWhiteSpace(WorkRootName)
        ? FullPath
        : $"{WorkRootName} • {FileCount} files";
}
