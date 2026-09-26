using System.Collections.Generic;
using Zenfile.Models;

namespace Zenfile.Services;

public interface IShellContextMenuService
{
    bool IsRegistered { get; }
    void Register(AppConfig config, IReadOnlyList<WorkTaskFolder> allTasks, string? executablePath = null);
    void Unregister();
}
