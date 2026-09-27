using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Zenfile.Models;

namespace Zenfile.Services;

public class ShellContextMenuService : IShellContextMenuService
{
    private const string AppKeyName = "Zenfile";
    private static readonly string[] LegacyAppKeyNames = ["WorkDrop"];
    private const string FileShellPath = @"Software\Classes\*\shell";
    private const string DirectoryShellPath = @"Software\Classes\Directory\shell";

    public bool IsRegistered
    {
        get
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey($@"{FileShellPath}\{AppKeyName}");
                return key != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public string GetCurrentExecutablePath()
    {
        var mainModule = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(mainModule) && mainModule.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return mainModule;
        }

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "Zenfile.exe");
        if (File.Exists(candidate)) return candidate;

        return processPath ?? "Zenfile.exe";
    }

    public void Register(AppConfig config, IReadOnlyList<WorkTaskFolder> allTasks, string? executablePath = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var exePath = executablePath ?? GetCurrentExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        var targets = new List<string> { FileShellPath };
        if (config.IncludeDirectoriesInContextMenu)
        {
            targets.Add(DirectoryShellPath);
        }

        // Clean any previous registration first
        Unregister();

        foreach (var parentPath in targets)
        {
            try
            {
                using var parentKey = Registry.CurrentUser.CreateSubKey(parentPath);
                if (parentKey == null) continue;

                using var appKey = parentKey.CreateSubKey(AppKeyName);
                if (appKey == null) continue;

                var rootTitle = config.DefaultMode == TransferMode.Copy ? "Copy to Work Project" : "Move to Work Project";
                appKey.SetValue("MUIVerb", rootTitle);
                appKey.SetValue("Icon", $"\"{exePath}\",0");
                appKey.SetValue("SubCommands", "");

                using var shellKey = appKey.CreateSubKey("Shell");
                if (shellKey == null) continue;

                // 1. Popup launcher item
                if (config.RegisterPopupVerb)
                {
                    using var popupKey = shellKey.CreateSubKey("00_Popup");
                    if (popupKey != null)
                    {
                        popupKey.SetValue("MUIVerb", "⚡ Choose Destination Popup...");
                        popupKey.SetValue("Icon", $"\"{exePath}\",0");
                        using var cmdKey = popupKey.CreateSubKey("command");
                        cmdKey?.SetValue("", $"\"{exePath}\" --picker \"%1\"");
                    }
                }

                // 2. Direct Task folders
                if (config.RegisterCascadingSubmenus && allTasks != null && allTasks.Count > 0)
                {
                    int index = 1;
                    foreach (var task in allTasks.Take(30)) // Windows shell menus work best with up to 30 items
                    {
                        if (string.IsNullOrWhiteSpace(task.FullPath)) continue;

                        var itemKeyName = $"Item_{index:D2}_{SanitizeKeyName(task.Name)}";
                        using var itemKey = shellKey.CreateSubKey(itemKeyName);
                        if (itemKey != null)
                        {
                            var verb = string.IsNullOrWhiteSpace(task.WorkRootName)
                                ? task.Name
                                : $"{task.WorkRootName} ❯ {task.Name}";

                            itemKey.SetValue("MUIVerb", verb);
                            itemKey.SetValue("Icon", $"\"{exePath}\",0");

                            using var cmdKey = itemKey.CreateSubKey("command");
                            cmdKey?.SetValue("", $"\"{exePath}\" --target \"{task.FullPath}\" \"%1\"");
                        }
                        index++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShellContextMenuService] Error writing registry for {parentPath}: {ex.Message}");
            }
        }
    }

    public void Unregister()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var targets = new[] { FileShellPath, DirectoryShellPath };
        var keyNames = new[] { AppKeyName }.Concat(LegacyAppKeyNames);

        foreach (var parentPath in targets)
        {
            try
            {
                using var parentKey = Registry.CurrentUser.OpenSubKey(parentPath, true);
                if (parentKey != null)
                {
                    foreach (var keyName in keyNames)
                    {
                        parentKey.DeleteSubKeyTree(keyName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShellContextMenuService] Error unregistering {parentPath}: {ex.Message}");
            }
        }
    }

    private static string SanitizeKeyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Task";
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars);
    }
}
