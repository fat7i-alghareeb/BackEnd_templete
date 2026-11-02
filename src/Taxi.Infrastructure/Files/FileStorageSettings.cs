using System.ComponentModel.DataAnnotations;

namespace Taxi.Infrastructure.Files;

/// <summary>Bound from the <c>FileStorage</c> section and validated on start.</summary>
public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Gets or sets the directory for <c>LocalFileStorage</c>; relative paths are resolved against the
    /// content root. Deliberately outside <c>wwwroot</c>: files are served only through authorized
    /// endpoints, never as anonymous static files. Mount a volume here in containers.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string LocalRootPath { get; set; } = "storage";
}
