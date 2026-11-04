using System.Buffers.Binary;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace EstateFlow.Services;

/// <summary>Stores one optional cover image per property under an application-owned filename.</summary>
public sealed class PropertyImageService(IWebHostEnvironment environment, ILogger<PropertyImageService> logger)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private const string Prefix = "/uploads/properties/";
    private string DirectoryPath => Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "properties");

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0 || file.Length > MaxBytes)
            throw new ArgumentException("Изберете снимка до 5 MB.");

        await using var source = file.OpenReadStream();
        var header = new byte[12];
        var count = await source.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var valid = extension switch
        {
            ".jpg" or ".jpeg" => count >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
            ".png" => count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".webp" => count == 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8)
                       && BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4)) + 8L == file.Length,
            _ => false
        };
        if (!valid) throw new ArgumentException("Изберете валидна JPG, PNG или WebP снимка.");

        Directory.CreateDirectory(DirectoryPath);
        var name = Guid.NewGuid().ToString("N") + (extension == ".jpeg" ? ".jpg" : extension);
        var path = Path.Combine(DirectoryPath, name);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await output.WriteAsync(header.AsMemory(0, count), cancellationToken);
            var buffer = new byte[81920];
            long written = count;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) != 0)
            {
                written += read;
                if (written > MaxBytes) throw new ArgumentException("Изберете снимка до 5 MB.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            if (written != file.Length) throw new ArgumentException("Снимката не беше качена изцяло. Опитайте отново.");
            return Prefix + name;
        }
        catch
        {
            Delete(Prefix + name);
            throw;
        }
    }

    public void Delete(string? imagePath)
    {
        if (!IsManagedPath(imagePath)) return;
        try { File.Delete(Path.Combine(DirectoryPath, imagePath![Prefix.Length..])); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not remove an unused property cover image.");
        }
    }

    public static bool IsManagedPath(string? path)
    {
        if (path is null || !path.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var name = path[Prefix.Length..];
        var extension = Path.GetExtension(name);
        return extension is ".jpg" or ".png" or ".webp"
            && Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _)
            && name == Path.GetFileName(name);
    }

    public static string DisplayPath(string? path) => IsManagedPath(path) || EstateFlow.Data.DemoPropertyCatalog.IsImage(path)
        ? path! : "/images/property-placeholder.svg";
}
