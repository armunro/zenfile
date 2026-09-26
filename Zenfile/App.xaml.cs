using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Zenfile.Models;
using Zenfile.Services;
using Zenfile.ViewModels;
using Zenfile.Views;

namespace Zenfile;

public partial class App : Application
{
    public static ISettingsService SettingsService { get; private set; } = null!;
    public static IWorkRootService WorkRootService { get; private set; } = null!;
    public static IFileTransferService FileTransferService { get; private set; } = null!;
    public static IShellContextMenuService ShellContextMenuService { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize core services
        SettingsService = new SettingsService();
        WorkRootService = new WorkRootService();
        FileTransferService = new FileTransferService();
        ShellContextMenuService = new ShellContextMenuService();

        var parsedArgs = CommandLineParser.Parse(e.Args);

        // Case 1: Direct transfer via context menu (--target "..." "%1")
        if (parsedArgs.IsDirectTransfer && !string.IsNullOrWhiteSpace(parsedArgs.TargetDirectory))
        {
            var mode = parsedArgs.Mode ?? SettingsService.Config.DefaultMode;
            var conflict = parsedArgs.ConflictResolution ?? SettingsService.Config.DefaultConflictResolution;

            var request = new TransferRequest
            {
                SourcePaths = parsedArgs.Files,
                DestinationDirectory = parsedArgs.TargetDirectory,
                Mode = mode,
                ConflictResolution = conflict
            };

            var result = await FileTransferService.ExecuteTransferAsync(request);
            foreach (var item in result.TransferredItems)
            {
                SettingsService.AddTransferHistory(item);
            }
            SettingsService.AddRecentDestination(parsedArgs.TargetDirectory);

            if (SettingsService.Config.OpenFolderAfterTransfer && Directory.Exists(parsedArgs.TargetDirectory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = parsedArgs.TargetDirectory,
                    UseShellExecute = true
                });
            }

            Shutdown(result.FailedCount == 0 ? 0 : 1);
            return;
        }

        // Case 2: Popup Picker mode (--picker or files passed without target)
        if (parsedArgs.IsPickerMode || parsedArgs.HasFiles)
        {
            var pickerVm = new PopupPickerViewModel(
                parsedArgs.Files,
                SettingsService,
                WorkRootService,
                FileTransferService,
                parsedArgs.Mode,
                parsedArgs.ConflictResolution);

            var pickerWindow = new PopupPickerWindow(pickerVm);
            pickerVm.SetCloseAction(() => pickerWindow.Close());

            MainWindow = pickerWindow;
            pickerWindow.Show();
            return;
        }

        // Case 3: Normal Launch -> Main Window
        var mainVm = new MainViewModel(
            SettingsService,
            WorkRootService,
            FileTransferService,
            ShellContextMenuService);

        var mainWindow = new MainWindow(mainVm);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
