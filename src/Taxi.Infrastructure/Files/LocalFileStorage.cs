using System.Text.RegularExpressions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Taxi.Application.Common.Interfaces;

namespace Taxi.Infrastructure.Files;

/// <summary>
/// <see cref="IFileStorage"/> on the local file system. Keys look like <c>cars/3f2a…9c.png</c>: a folder
/// and a server-generated GUID, so no client-supplied name ever reaches a path. Every key is checked
/// against that shape and resolved inside the root before use (no traversal).
/// <para>
/// Disk is per machine: with several API instances, mount shared storage at the root or replace this
/// class with an object-store implementation.
/// </para>
/// </summary>
public sealed partial class LocalFileStorage(IOptions<FileStorageSettings> settings, IHostEnvironment environment) : IFileStorage
{
    private readonly string root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, settings.Value.LocalRootPath));

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken cancellationToken = default)
    {
        var key = $"{folder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = this.Resolve(key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);

        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = this.Resolve(key);

        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        File.Delete(this.Resolve(key));
        return Task.CompletedTask;
    }

    [GeneratedRegex("^[a-z0-9-]+/[a-f0-9]{32}\\.[a-z0-9]{1,5}$")]
    private static partial Regex KeyShape();

    private string Resolve(string key)
    {
        if (!KeyShape().IsMatch(key))
        {
            throw new ArgumentException($"Invalid storage key '{key}'.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(this.root, key));

        if (!path.StartsWith(this.root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Storage key '{key}' resolves outside the storage root.", nameof(key));
        }

        return path;
    }
}
