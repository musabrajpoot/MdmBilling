
namespace MdmBilling.Services;

public class FileStorage
{
    private readonly string _root;

    public FileStorage(IConfiguration config)
    {
        _root = Path.GetFullPath(
            config["Storage:Root"] ?? "storage"
        );

        Directory.CreateDirectory(_root);
    }

    // Save a file and return its relative storage path.
    public async Task<string> SaveAsync(
        Stream content,
        string extension,
        string folder,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(
            _root,
            folder,
            DateTime.UtcNow.ToString("yyyyMM")
        );

        Directory.CreateDirectory(dir);

        var fullPath = Path.Combine(
            dir,
            Guid.NewGuid().ToString("N") + extension
        );

        await using var fs = File.Create(fullPath);

        await content.CopyToAsync(fs, ct);

        return Path.GetRelativePath(_root, fullPath);
    }

    // Open a stored file.
    public Stream Open(string relativePath)
    {
        return File.OpenRead(Resolve(relativePath));
    }

    // Check whether a stored file exists.
    public bool Exists(string relativePath)
    {
        return File.Exists(Resolve(relativePath));
    }

    // Resolve a relative path safely within the storage root.
    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(
            Path.Combine(_root, relativePath)
        );

        var rootWithSeparator =
            _root.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!full.StartsWith(
                rootWithSeparator,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Invalid file path."
            );
        }

        return full;
    }
}
