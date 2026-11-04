using EstateFlow.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace EstateFlow.Tests;

public sealed class PropertyImageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "estateflow-images-" + Guid.NewGuid().ToString("N"));
    private PropertyImageService Service => new(new TestEnvironment(_root), NullLogger<PropertyImageService>.Instance);

    [Fact]
    public async Task Upload_UsesGeneratedName_AndDeletesOnlyManagedFile()
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZKFAAAAAASUVORK5CYII=");
        using var input = new MemoryStream(bytes);
        var file = new FormFile(input, 0, bytes.Length, "Photo", "../../cover.png");
        var path = await Service.SaveAsync(file);
        Assert.True(PropertyImageService.IsManagedPath(path));
        var stored = Path.Combine(_root, "wwwroot", path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(stored));

        var outside = Path.Combine(_root, "keep.txt");
        await File.WriteAllTextAsync(outside, "keep");
        Service.Delete("/uploads/properties/../../../keep.txt");
        Service.Delete(outside);
        Assert.True(File.Exists(outside));
        Service.Delete(path);
        Assert.False(File.Exists(stored));
    }

    [Theory]
    [InlineData("cover.jpg", "<html>not an image</html>")]
    [InlineData("cover.svg", "<svg></svg>")]
    [InlineData("cover.png", "not a PNG")]
    public async Task Upload_RejectsDisallowedOrMismatchedContent(string name, string content)
    {
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.SaveAsync(new FormFile(input, 0, input.Length, "Photo", name)));
        Assert.False(Directory.Exists(Path.Combine(_root, "wwwroot", "uploads", "properties")));
    }

    [Fact]
    public async Task Upload_RejectsOversizedFileBeforeCreatingDirectory()
    {
        var file = new FormFile(Stream.Null, 0, PropertyImageService.MaxBytes + 1, "Photo", "huge.jpg");
        await Assert.ThrowsAsync<ArgumentException>(() => Service.SaveAsync(file));
        Assert.False(Directory.Exists(_root));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "EstateFlow.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
