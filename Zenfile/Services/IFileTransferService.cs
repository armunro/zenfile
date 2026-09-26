using System;
using System.Threading;
using System.Threading.Tasks;
using Zenfile.Models;

namespace Zenfile.Services;

public interface IFileTransferService
{
    TransferResult ExecuteTransfer(
        TransferRequest request,
        IProgress<TransferItem>? progress = null,
        CancellationToken cancellationToken = default);

    Task<TransferResult> ExecuteTransferAsync(
        TransferRequest request,
        IProgress<TransferItem>? progress = null,
        CancellationToken cancellationToken = default);

    string ResolveUniqueFilePath(string destinationDirectory, string originalFileName);
    string ResolveUniqueDirectoryPath(string destinationDirectory, string originalDirName);
}
