namespace Cocoar.FileSystem.Tests;

public class FileReaderTests : IDisposable
{
    private readonly string _testDir;

    public FileReaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FileReaderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ReadAllBytes_WithValidFile_ShouldReturnContent()
    {
        var path = Path.Combine(_testDir, "test.bin");
        var expected = new byte[] { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(path, expected);

        var result = FileReader.ReadAllBytes(path);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReadAllBytes_WithUtf8Bom_AndStripEnabled_ShouldRemoveBom()
    {
        var path = Path.Combine(_testDir, "test.txt");
        var content = new byte[] { 0xEF, 0xBB, 0xBF, 72, 101, 108, 108, 111 }; // BOM + "Hello"
        File.WriteAllBytes(path, content);

        var result = FileReader.ReadAllBytes(path, stripUtf8Bom: true);

        Assert.Equal(new byte[] { 72, 101, 108, 108, 111 }, result);
    }

    [Fact]
    public void ReadAllBytes_WithUtf8Bom_AndStripDisabled_ShouldKeepBom()
    {
        var path = Path.Combine(_testDir, "test.txt");
        var content = new byte[] { 0xEF, 0xBB, 0xBF, 72, 101, 108, 108, 111 };
        File.WriteAllBytes(path, content);

        var result = FileReader.ReadAllBytes(path, stripUtf8Bom: false);

        Assert.Equal(content, result);
    }

    [Fact]
    public void ReadAllBytes_WithoutBom_AndStripEnabled_ShouldReturnAsIs()
    {
        var path = Path.Combine(_testDir, "test.txt");
        var content = new byte[] { 72, 101, 108, 108, 111 };
        File.WriteAllBytes(path, content);

        var result = FileReader.ReadAllBytes(path, stripUtf8Bom: true);

        Assert.Equal(content, result);
    }

    [Fact]
    public void ReadAllBytes_WithPartialBom_ShouldNotStrip()
    {
        var path = Path.Combine(_testDir, "test.bin");
        var content = new byte[] { 0xEF, 0xBB, 0x00 }; // Not a complete BOM
        File.WriteAllBytes(path, content);

        var result = FileReader.ReadAllBytes(path, stripUtf8Bom: true);

        Assert.Equal(content, result);
    }

    [Fact]
    public void ReadAllBytes_WithEmptyFile_ShouldReturnEmptyArray()
    {
        var path = Path.Combine(_testDir, "empty.bin");
        File.WriteAllBytes(path, Array.Empty<byte>());

        var result = FileReader.ReadAllBytes(path);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadAllBytes_WithSharedAccess_ShouldAllowReading()
    {
        var path = Path.Combine(_testDir, "shared.bin");
        var content = new byte[] { 1, 2, 3 };
        File.WriteAllBytes(path, content);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
        var result = FileReader.ReadAllBytes(path);

        Assert.Equal(content, result);
    }

    [Fact]
    public void ReadAllBytes_WithNullPath_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => FileReader.ReadAllBytes(null!));
    }

    [Fact]
    public void ReadAllBytes_WithEmptyPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FileReader.ReadAllBytes(""));
    }

    [Fact]
    public void ReadAllBytes_WithWhitespacePath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FileReader.ReadAllBytes("   "));
    }

    [Fact]
    public void ReadAllBytes_WithNonExistentFile_ShouldThrow()
    {
        var path = Path.Combine(_testDir, "nonexistent.bin");

        Assert.Throws<FileNotFoundException>(() => FileReader.ReadAllBytes(path));
    }

    [Fact]
    public void TryReadAllBytes_WithValidFile_ShouldReturnContent()
    {
        var path = Path.Combine(_testDir, "test.bin");
        var expected = new byte[] { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(path, expected);

        var result = FileReader.TryReadAllBytes(path);

        Assert.NotNull(result);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryReadAllBytes_WithNonExistentFile_ShouldReturnNull()
    {
        var path = Path.Combine(_testDir, "nonexistent.bin");

        var result = FileReader.TryReadAllBytes(path);

        Assert.Null(result);
    }

    [Fact]
    public void TryReadAllBytes_WithUtf8Bom_AndStripEnabled_ShouldRemoveBom()
    {
        var path = Path.Combine(_testDir, "test.txt");
        var content = new byte[] { 0xEF, 0xBB, 0xBF, 72, 101, 108, 108, 111 };
        File.WriteAllBytes(path, content);

        var result = FileReader.TryReadAllBytes(path, stripUtf8Bom: true);

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 72, 101, 108, 108, 111 }, result);
    }

    [Fact]
    public void TryReadAllBytes_WithNullPath_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => FileReader.TryReadAllBytes(null!));
    }

    [Fact]
    public void TryReadAllBytes_WithEmptyPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FileReader.TryReadAllBytes(""));
    }

    [Fact]
    public void TryReadAllBytes_WithWhitespacePath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FileReader.TryReadAllBytes("   "));
    }

    [Fact]
    public void ReadAllBytes_WithOnlyBom_AndStripEnabled_ShouldReturnEmpty()
    {
        var path = Path.Combine(_testDir, "only-bom.txt");
        var content = new byte[] { 0xEF, 0xBB, 0xBF };
        File.WriteAllBytes(path, content);

        var result = FileReader.ReadAllBytes(path, stripUtf8Bom: true);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadAllBytes_WithLargeFile_ShouldReadCompletely()
    {
        var path = Path.Combine(_testDir, "large.bin");
        var content = new byte[1024 * 1024]; // 1 MB
        new Random(42).NextBytes(content);
        File.WriteAllBytes(path, content);

        var result = FileReader.ReadAllBytes(path);

        Assert.Equal(content, result);
    }
}
