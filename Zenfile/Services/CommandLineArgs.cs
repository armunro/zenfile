using System;
using System.Collections.Generic;
using System.IO;
using Zenfile.Models;

namespace Zenfile.Services;

public class ParsedCommandLine
{
    public bool IsPickerMode { get; set; }
    public string? TargetDirectory { get; set; }
    public TransferMode? Mode { get; set; }
    public ConflictResolution? ConflictResolution { get; set; }
    public List<string> Files { get; set; } = [];
    public bool HasFiles => Files.Count > 0;
    public bool IsDirectTransfer => !string.IsNullOrWhiteSpace(TargetDirectory) && HasFiles && !IsPickerMode;
}

public static class CommandLineParser
{
    public static ParsedCommandLine Parse(string[] args)
    {
        var result = new ParsedCommandLine();
        if (args == null || args.Length == 0)
        {
            return result;
        }

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].Trim();
            if (string.Equals(arg, "--picker", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-p", StringComparison.OrdinalIgnoreCase))
            {
                result.IsPickerMode = true;
            }
            else if (string.Equals(arg, "--target", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(arg, "-t", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    result.TargetDirectory = args[++i].Trim('\"');
                }
            }
            else if (string.Equals(arg, "--mode", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(arg, "-m", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    var modeStr = args[++i].Trim();
                    if (Enum.TryParse<TransferMode>(modeStr, true, out var mode))
                    {
                        result.Mode = mode;
                    }
                }
            }
            else if (string.Equals(arg, "--conflict", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(arg, "-c", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    var confStr = args[++i].Trim();
                    if (Enum.TryParse<ConflictResolution>(confStr, true, out var conf))
                    {
                        result.ConflictResolution = conf;
                    }
                }
            }
            else if (string.Equals(arg, "--files", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(arg, "-f", StringComparison.OrdinalIgnoreCase))
            {
                while (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    var path = args[++i].Trim('\"');
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        result.Files.Add(path);
                    }
                }
            }
            else if (!arg.StartsWith("-"))
            {
                // Standalone file/directory path passed e.g. "%1"
                var path = arg.Trim('\"');
                if (File.Exists(path) || Directory.Exists(path) || Path.IsPathRooted(path))
                {
                    result.Files.Add(path);
                }
            }
        }

        return result;
    }
}
