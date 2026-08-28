using System;
using System.Threading;
using System.Threading.Tasks;
using LidDock.Core.Models;

namespace LidDock.Core.Contracts;

public interface iUpdateChecker
{
    Task<appUpdateResult> checkForUpdatesAsync(Version currentVersion, CancellationToken cancellationToken = default);
}
