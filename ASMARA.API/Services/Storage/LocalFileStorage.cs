namespace ASMARA.API.Services.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IWebHostEnvironment env, IConfiguration configuration)
    {
        var configured = configuration["FileStorage:RootPath"];

        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
            : configured);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var extension = Path.GetExtension(file.FileName);
        if (extension.Length > 10) extension = string.Empty;

        var relativePath = Path.Combine(
            folder,
            now.ToString("yyyy"),
            now.ToString("MM"),
            $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}");

        var fullPath = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream, ct);
        }

        return relativePath.Replace('\\', '/');
    }

    public bool Exists(string relativePath) => File.Exists(GetFullPath(relativePath));

    public Stream OpenRead(string relativePath)
        => new FileStream(GetFullPath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

    public void Delete(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private string GetFullPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));

        if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file path.");

        return fullPath;
    }
}