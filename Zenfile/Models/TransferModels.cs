using System;
using System.Collections.Generic;

namespace Zenfile.Models;

public class TransferItem
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TransferMode Mode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string SourceFileName => System.IO.Path.GetFileName(SourcePath);
    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
}

public class TransferRequest
{
    public List<string> SourcePaths { get; set; } = [];
    public string DestinationDirectory { get; set; } = string.Empty;
    public TransferMode Mode { get; set; } = TransferMode.Move;
    public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.AutoRename;
}

public class TransferResult
{
    public bool Success => FailedCount == 0 && TotalCount > 0;
    public int TotalCount => TransferredItems.Count;
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public List<TransferItem> TransferredItems { get; set; } = [];
    public string DestinationDirectory { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? OverallMessage { get; set; }
}
