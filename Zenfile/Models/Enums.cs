namespace Zenfile.Models;

public enum TransferMode
{
    Move,
    Copy
}

public enum ConflictResolution
{
    AutoRename,
    Overwrite,
    Skip
}
