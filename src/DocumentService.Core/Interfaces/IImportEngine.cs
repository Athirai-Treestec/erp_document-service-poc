using DocumentService.Core.Enums;
using DocumentService.Core.Models;

namespace DocumentService.Core.Interfaces;

/// <summary>
/// Contract implemented once per supported ImportFormat (Excel, Csv, [Word]).
/// Reads raw file bytes and converts them into the common DocumentModel.
/// </summary>
public interface IImportEngine
{
    ImportFormat Format { get; }
    Task<DocumentModel> ReadAsync(Stream fileStream, CancellationToken cancellationToken = default);
}
