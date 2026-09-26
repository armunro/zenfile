using System.Collections.Generic;

namespace Zenfile.Models;

public class AppConfig
{
    public List<WorkRoot> WorkRoots { get; set; } = [];
    public TransferMode DefaultMode { get; set; } = TransferMode.Move;
    public ConflictResolution DefaultConflictResolution { get; set; } = ConflictResolution.AutoRename;
    public bool RegisterCascadingSubmenus { get; set; } = true;
    public bool RegisterPopupVerb { get; set; } = true;
    public bool IncludeDirectoriesInContextMenu { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool OpenFolderAfterTransfer { get; set; } = false;
    public bool AutoClosePopupOnSuccess { get; set; } = true;
    public int AutoCloseDelayMs { get; set; } = 1200;
    public List<string> RecentDestinations { get; set; } = [];
    public List<TransferItem> TransferHistory { get; set; } = [];
    public int MaxHistoryCount { get; set; } = 200;
}
