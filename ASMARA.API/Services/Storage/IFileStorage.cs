namespace ASMARA.API.Services.Storage;

public interface IFileStorage
{
    Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default);

    bool Exists(string relativePath);

    Stream OpenRead(string relativePath);

    void Delete(string relativePath);
}