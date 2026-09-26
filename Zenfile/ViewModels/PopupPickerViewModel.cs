using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Zenfile.Models;
using Zenfile.Services;

namespace Zenfile.ViewModels;

public class PopupPickerViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IWorkRootService _workRootService;
    private readonly IFileTransferService _transferService;

    private string _searchFilter = string.Empty;
    private WorkTaskFolder? _selectedTask;
    private WorkRoot? _selectedRootForNewTask;
    private string _newTaskName = string.Empty;
    private bool _isCreatingNewTask;
    private bool _isBusy;
    private bool _isSuccess;
    private string _statusMessage = string.Empty;
    private TransferMode _transferMode = TransferMode.Move;
    private ConflictResolution _conflictResolution = ConflictResolution.AutoRename;
    private Action? _closeAction;

    public List<string> SourceFiles { get; }
    public ObservableCollection<WorkTaskFolder> AllTasks { get; } = [];
    public ObservableCollection<WorkTaskFolder> FilteredTasks { get; } = [];
    public ObservableCollection<WorkRoot> AvailableRoots { get; } = [];

    public int FileCount => SourceFiles.Count;
    public string FilesSummaryText => FileCount switch
    {
        0 => "No files selected",
        1 => Path.GetFileName(SourceFiles[0]),
        _ => $"{Path.GetFileName(SourceFiles[0])} and {FileCount - 1} other item(s)"
    };

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

    public WorkTaskFolder? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                (ExecuteTransferCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public WorkRoot? SelectedRootForNewTask
    {
        get => _selectedRootForNewTask;
        set => SetProperty(ref _selectedRootForNewTask, value);
    }

    public string NewTaskName
    {
        get => _newTaskName;
        set => SetProperty(ref _newTaskName, value);
    }

    public bool IsCreatingNewTask
    {
        get => _isCreatingNewTask;
        set => SetProperty(ref _isCreatingNewTask, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        set => SetProperty(ref _isSuccess, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public TransferMode TransferMode
    {
        get => _transferMode;
        set => SetProperty(ref _transferMode, value);
    }

    public ConflictResolution ConflictResolution
    {
        get => _conflictResolution;
        set => SetProperty(ref _conflictResolution, value);
    }

    // Commands
    public ICommand ExecuteTransferCommand { get; }
    public ICommand ToggleNewTaskModeCommand { get; }
    public ICommand ConfirmCreateNewTaskCommand { get; }
    public ICommand OpenDestinationCommand { get; }
    public ICommand CancelCommand { get; }

    public PopupPickerViewModel(
        List<string> sourceFiles,
        ISettingsService settingsService,
        IWorkRootService workRootService,
        IFileTransferService transferService,
        TransferMode? preferredMode = null,
        ConflictResolution? preferredConflict = null,
        Action? closeAction = null)
    {
        SourceFiles = sourceFiles ?? [];
        _settingsService = settingsService;
        _workRootService = workRootService;
        _transferService = transferService;
        _closeAction = closeAction;

        _transferMode = preferredMode ?? _settingsService.Config.DefaultMode;
        _conflictResolution = preferredConflict ?? _settingsService.Config.DefaultConflictResolution;

        ExecuteTransferCommand = new RelayCommand(async () => await ExecuteTransferAsync(), () => SelectedTask != null && !IsBusy && !IsSuccess);
        ToggleNewTaskModeCommand = new RelayCommand(() => IsCreatingNewTask = !IsCreatingNewTask);
        ConfirmCreateNewTaskCommand = new RelayCommand(CreateNewTask);
        OpenDestinationCommand = new RelayCommand(OpenDestination);
        CancelCommand = new RelayCommand(() => _closeAction?.Invoke());

        LoadTasks();
    }

    public void SetCloseAction(Action closeAction)
    {
        _closeAction = closeAction;
    }

    private void LoadTasks()
    {
        AvailableRoots.Clear();
        foreach (var root in _settingsService.Config.WorkRoots.Where(r => r.IsEnabled))
        {
            AvailableRoots.Add(root);
        }

        if (AvailableRoots.Count > 0)
        {
            SelectedRootForNewTask = AvailableRoots[0];
        }

        var tasks = _workRootService.ScanAllTasks(AvailableRoots);
        AllTasks.Clear();

        // Sort by recent destinations first, then alphabetically
        var recent = _settingsService.Config.RecentDestinations;
        var sorted = tasks.OrderBy(t =>
        {
            var idx = recent.FindIndex(p => string.Equals(p, t.FullPath, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 ? idx : 9999;
        }).ThenBy(t => t.WorkRootName).ThenBy(t => t.Name).ToList();

        foreach (var task in sorted)
        {
            AllTasks.Add(task);
        }

        ApplyFilter();

        if (FilteredTasks.Count > 0)
        {
            SelectedTask = FilteredTasks[0];
        }
    }

    private void ApplyFilter()
    {
        FilteredTasks.Clear();
        var query = SearchFilter?.Trim() ?? string.Empty;

        foreach (var task in AllTasks)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                task.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                task.WorkRootName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredTasks.Add(task);
            }
        }

        if (SelectedTask == null || !FilteredTasks.Contains(SelectedTask))
        {
            SelectedTask = FilteredTasks.FirstOrDefault();
        }
    }

    private void CreateNewTask()
    {
        if (SelectedRootForNewTask == null)
        {
            StatusMessage = "Please select a work root.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewTaskName))
        {
            StatusMessage = "Please enter a folder name.";
            return;
        }

        try
        {
            var task = _workRootService.CreateTaskFolder(SelectedRootForNewTask, NewTaskName);
            NewTaskName = string.Empty;
            IsCreatingNewTask = false;
            LoadTasks();
            SelectedTask = AllTasks.FirstOrDefault(t => string.Equals(t.FullPath, task.FullPath, StringComparison.OrdinalIgnoreCase));
            StatusMessage = $"Created '{task.Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public async Task ExecuteTransferAsync()
    {
        if (SelectedTask == null || SourceFiles.Count == 0)
            return;

        IsBusy = true;
        StatusMessage = "Transferring...";

        var request = new TransferRequest
        {
            SourcePaths = SourceFiles,
            DestinationDirectory = SelectedTask.FullPath,
            Mode = TransferMode,
            ConflictResolution = ConflictResolution
        };

        var result = await _transferService.ExecuteTransferAsync(request);

        foreach (var item in result.TransferredItems)
        {
            _settingsService.AddTransferHistory(item);
        }

        _settingsService.AddRecentDestination(SelectedTask.FullPath);

        IsBusy = false;
        IsSuccess = result.Success;
        StatusMessage = result.OverallMessage ?? "Transfer completed.";

        if (_settingsService.Config.OpenFolderAfterTransfer)
        {
            OpenDestination();
        }

        if (result.Success && _settingsService.Config.AutoClosePopupOnSuccess)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_settingsService.Config.AutoCloseDelayMs) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                _closeAction?.Invoke();
            };
            timer.Start();
        }
    }

    private void OpenDestination()
    {
        if (SelectedTask != null && Directory.Exists(SelectedTask.FullPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedTask.FullPath,
                UseShellExecute = true
            });
        }
    }
}
