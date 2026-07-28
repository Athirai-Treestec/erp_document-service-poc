namespace DocumentService.Core.Exceptions;

/// <summary>
/// Wraps any failure inside an engine/service (bad JSON, unsupported format,
/// third-party library failure) so callers only ever need to catch one exception type.
/// </summary>
public class DocumentServiceException : Exception
{
    public DocumentServiceException(string message) : base(message) { }
    public DocumentServiceException(string message, Exception innerException) : base(message, innerException) { }
}
