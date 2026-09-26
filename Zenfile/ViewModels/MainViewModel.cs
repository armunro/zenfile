using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Zenfile.Models;
using Zenfile.Services;

namespace Zenfile.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IWorkRootService _workRootService;
    private readonly IFileTransferService _transferService;
    private readonly IShellContextMenuService _shellService;

    private WorkRoot? _selectedRoot;
    private WorkTaskFolder? _selectedTask;
    private string _searchFilter = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isBusy;
    private bool _isContextMenuRegistered;
    private int _selectedTabIndex = 0;
    private string _newTaskName = string.Empty;

    public ObservableCollection<WorkRoot> WorkRoots { get; } = [];
    public ObservableCollection<WorkTaskFolder> TaskFolders { get; } = [];
    public ObservableCollection<WorkTaskFolder> FilteredTaskFolders { get; } = [];
    public ObservableCollection<TransferItem> TransferHistory { get; } = [];

    public AppConfig Config => _settingsService.Config;

    public WorkRoot? SelectedRoot
    {
        get => _selectedRoot;
        set
        {
            if (SetProperty(ref _selectedRoot, value))
            {
                RefreshTasks();
                (CreateTaskCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RemoveRootCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public WorkTaskFolder? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                (DeleteTaskCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (OpenTaskInExplorerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string NewTaskName
    {
        get => _newTaskName;
        set => SetProperty(ref _newTaskName, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public bool IsContextMenuRegistered
    {
        get => _isContextMenuRegistered;
        set => SetProperty(ref _isContextMenuRegistered, value);
    }

    public TransferMode SelectedMode
    {
        get => _settingsService.Config.DefaultMode;
        set
        {
            if (_settingsService.Config.DefaultMode != value)
            {
                _settingsService.Config.DefaultMode = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public ConflictResolution SelectedConflictResolution
    {
        get => _settingsService.Config.DefaultConflictResolution;
        set
        {
            if (_settingsService.Config.DefaultConflictResolution != value)
            {
                _settingsService.Config.DefaultConflictResolution = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool RegisterCascadingSubmenus
    {
        get => _settingsService.Config.RegisterCascadingSubmenus;
        set
        {
            if (_settingsService.Config.RegisterCascadingSubmenus != value)
            {
                _settingsService.Config.RegisterCascadingSubmenus = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool RegisterPopupVerb
    {
        get => _settingsService.Config.RegisterPopupVerb;
        set
        {
            if (_settingsService.Config.RegisterPopupVerb != value)
            {
                _settingsService.Config.RegisterPopupVerb = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool IncludeDirectoriesInContextMenu
    {
        get => _settingsService.Config.IncludeDirectoriesInContextMenu;
        set
        {
            if (_settingsService.Config.IncludeDirectoriesInContextMenu != value)
            {
                _settingsService.Config.IncludeDirectoriesInContextMenu = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    // Commands
    public ICommand AddRootCommand { get; }
    public ICommand RemoveRootCommand { get; }
    public ICommand RefreshAllCommand { get; }
    public ICommand CreateTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand OpenTaskInExplorerCommand { get; }
    public ICommand OpenRootInExplorerCommand { get; }
    public ICommand SyncContextMenuCommand { get; }
    public ICommand UnregisterContextMenuCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand OpenHistoryItemCommand { get; }

    public MainViewModel(
        ISettingsService settingsService,
        IWorkRootService workRootService,
        IFileTransferService transferService,
        IShellContextMenuService shellService)
    {
        _settingsService = settingsService;
        _workRootService = workRootService;
        _transferService = transferService;
        _shellService = shellService;

        AddRootCommand = new RelayCommand(AddRoot);
        RemoveRootCommand = new RelayCommand(RemoveRoot, () => SelectedRoot != null);
        RefreshAllCommand = new RelayCommand(RefreshAll);
        CreateTaskCommand = new RelayCommand(CreateTask, () => SelectedRoot != null);
        DeleteTaskCommand = new RelayCommand(DeleteTask, () => SelectedTask != null);
        OpenTaskInExplorerCommand = new RelayCommand<WorkTaskFolder>(OpenTaskInExplorer);
        OpenRootInExplorerCommand = new RelayCommand<WorkRoot>(OpenRootInExplorer);
        SyncContextMenuCommand = new RelayCommand(SyncContextMenu);
        UnregisterContextMenuCommand = new RelayCommand(UnregisterContextMenu);
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        OpenHistoryItemCommand = new RelayCommand<TransferItem>(OpenHistoryItem);

        LoadInitialData();
    }

    private void LoadInitialData()
    {
        WorkRoots.Clear();
        foreach (var root in _settingsService.Config.WorkRoots)
        {
            WorkRoots.Add(root);
        }

        TransferHistory.Clear();
        foreach (var item in _settingsService.Config.TransferHistory)
        {
            TransferHistory.Add(item);
        }

        IsContextMenuRegistered = _shellService.IsRegistered;

        if (WorkRoots.Count > 0)
        {
            SelectedRoot = WorkRoots[0];
        }
        else
        {
            RefreshTasks();
        }
    }

    public void AddWorkRoot(string name, string path, string colorHex = "#007ACC")
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        _workRootService.EnsureDirectoryExists(path);

        var root = new WorkRoot
        {
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path.TrimEnd('\\', '/')) : name,
            Path = path,
            ColorHex = colorHex
        };

        _settingsService.Config.WorkRoots.Add(root);
        _settingsService.Save();
        WorkRoots.Add(root);
        SelectedRoot = root;

        StatusMessage = $"Added work root '{root.Name}'";

        if (IsContextMenuRegistered)
        {
            SyncContextMenu();
        }
    }

    private void AddRoot()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Work Root Folder",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                var folderName = Path.GetFileName(dialog.FolderName.TrimEnd('\\', '/'));
                AddWorkRoot(folderName, dialog.FolderName);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting folder: {ex.Message}";
        }
    }

    private void RemoveRoot()
    {
        if (SelectedRoot == null) return;

        var rootToRemove = SelectedRoot;
        _settingsService.Config.WorkRoots.Remove(rootToRemove);
        _settingsService.Save();
        WorkRoots.Remove(rootToRemove);

        SelectedRoot = WorkRoots.FirstOrDefault();
        StatusMessage = $"Removed work root '{rootToRemove.Name}'";

        if (IsContextMenuRegistered)
        {
            SyncContextMenu();
        }
    }

    public void RefreshTasks()
    {
        TaskFolders.Clear();
        if (SelectedRoot != null)
        {
            var tasks = _workRootService.ScanTasks(SelectedRoot);
            foreach (var task in tasks)
            {
                TaskFolders.Add(task);
            }
        }
        else
        {
            var allTasks = _workRootService.ScanAllTasks(WorkRoots);
            foreach (var task in allTasks)
            {
                TaskFolders.Add(task);
            }
        }
        ApplyFilter();
    }

    public void RefreshAll()
    {
        RefreshTasks();
        IsContextMenuRegistered = _shellService.IsRegistered;
        StatusMessage = $"Refreshed. Found {TaskFolders.Count} project folder(s).";
    }

    private void ApplyFilter()
    {
        FilteredTaskFolders.Clear();
        var query = SearchFilter?.Trim() ?? string.Empty;

        foreach (var task in TaskFolders)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                task.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                task.WorkRootName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                task.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredTaskFolders.Add(task);
            }
        }
    }

    private void CreateTask()
    {
        if (SelectedRoot == null) return;
        if (string.IsNullOrWhiteSpace(NewTaskName))
        {
            StatusMessage = "Please enter a project/task folder name.";
            return;
        }

        try
        {
            var task = _workRootService.CreateTaskFolder(SelectedRoot, NewTaskName);
            NewTaskName = string.Empty;
            RefreshTasks();
            SelectedTask = TaskFolders.FirstOrDefault(t => string.Equals(t.FullPath, task.FullPath, StringComparison.OrdinalIgnoreCase));
            StatusMessage = $"Created project folder '{task.Name}' in {SelectedRoot.Name}";

            if (IsContextMenuRegistered)
            {
                SyncContextMenu();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating folder: {ex.Message}";
        }
    }

    private void DeleteTask()
    {
        if (SelectedTask == null) return;

        var taskToDelete = SelectedTask;
        var confirm = MessageBox.Show(
            $"Are you sure you want to delete folder '{taskToDelete.Name}' and all its contents?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            if (_workRootService.DeleteTaskFolder(taskToDelete.FullPath))
            {
                RefreshTasks();
                StatusMessage = $"Deleted folder '{taskToDelete.Name}'";

                if (IsContextMenuRegistered)
                {
                    SyncContextMenu();
                }
            }
            else
            {
                StatusMessage = $"Could not delete '{taskToDelete.Name}'";
            }
        }
    }

    private void OpenTaskInExplorer(WorkTaskFolder? task)
    {
        var target = task ?? SelectedTask;
        if (target != null && Directory.Exists(target.FullPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target.FullPath,
                UseShellExecute = true
            });
        }
    }

    private void OpenRootInExplorer(WorkRoot? root)
    {
        var target = root ?? SelectedRoot;
        if (target != null && Directory.Exists(target.Path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target.Path,
                UseShellExecute = true
            });
        }
    }

    public void SyncContextMenu()
    {
        try
        {
            var allTasks = _workRootService.ScanAllTasks(WorkRoots);
            _shellService.Register(_settingsService.Config, allTasks);
            IsContextMenuRegistered = _shellService.IsRegistered;
            StatusMessage = "Windows Explorer context menu synchronized successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to register context menu: {ex.Message}";
        }
    }

    public void UnregisterContextMenu()
    {
        try
        {
            _shellService.Unregister();
            IsContextMenuRegistered = _shellService.IsRegistered;
            StatusMessage = "Windows Explorer context menu unregistered.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to unregister context menu: {ex.Message}";
        }
    }

    private void ClearHistory()
    {
        _settingsService.ClearHistory();
        TransferHistory.Clear();
        StatusMessage = "Transfer history cleared.";
    }

    private void OpenHistoryItem(TransferItem? item)
    {
        if (item == null) return;
        var folder = Directory.Exists(item.DestinationPath)
            ? item.DestinationPath
            : Path.GetDirectoryName(item.DestinationPath);

        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }

    public async Task DropFilesAsync(IEnumerable<string> paths, WorkTaskFolder targetTask)
    {
        if (targetTask == null || string.IsNullOrWhiteSpace(targetTask.FullPath))
        {
            StatusMessage = "Please select a target project folder.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Transferring items to {targetTask.Name}...";

        var request = new TransferRequest
        {
            SourcePaths = paths.ToList(),
            DestinationDirectory = targetTask.FullPath,
            Mode = SelectedMode,
            ConflictResolution = SelectedConflictResolution
        };

        var result = await _transferService.ExecuteTransferAsync(request);

        foreach (var item in result.TransferredItems)
        {
            _settingsService.AddTransferHistory(item);
            TransferHistory.Insert(0, item);
        }

        _settingsService.AddRecentDestination(targetTask.FullPath);
        RefreshTasks();
        IsBusy = false;
        StatusMessage = result.OverallMessage ?? "Transfer complete.";
    }
}
