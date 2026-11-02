using Taxi.Application.Common.Models;

namespace Taxi.Application.Common.Interfaces;

/// <summary>
/// Stores binary content under server-generated keys. Implemented by <c>LocalFileStorage</c> (disk);
/// swap in an S3 / Azure Blob implementation behind the same interface for multi-instance
/// deployments. Keys are opaque to callers — persist them, never build paths from them.
/// </summary>
public interface IFileStorage
{
    /// <summary>Saves <paramref name="content"/> as a new object in <paramref name="folder"/>.</summary>
    /// <returns>The key that identifies the stored object.</returns>
    Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken cancellationToken = default);

    /// <returns>A readable stream the caller must dispose, or <see langword="null"/> if the key does not exist.</returns>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes the object; a missing key is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
