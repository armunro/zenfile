using System;
using System.Collections.Generic;

namespace Zenfile.Models;

public class WorkRoot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#007ACC";
    public TransferMode DefaultMode { get; set; } = TransferMode.Move;
    public bool IsEnabled { get; set; } = true;
    public bool AutoCreateFolderIfMissing { get; set; } = true;
    public int ScanDepth { get; set; } = 1;

    public List<WorkTaskFolder> CachedTasks { get; set; } = [];

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Path : Name;
}
