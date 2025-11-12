namespace Cocoar.FileSystem;

/// <summary>
/// Secure file reading with shared access. Useful for reading sensitive content as bytes to avoid immutable strings in memory.
/// </summary>
public static class FileReader
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Shared read/write access.
    /// </summary>
    public static byte[] ReadAllBytes(string path, bool stripUtf8Bom = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty or whitespace", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}", path);

        byte[] bytes;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (stream.Length > int.MaxValue)
                throw new IOException($"File too large to read into memory: {path}");

            bytes = new byte[stream.Length];
            var bytesRead = stream.Read(bytes, 0, bytes.Length);
            if (bytesRead != bytes.Length)
                throw new IOException($"Failed to read entire file. Expected {bytes.Length} bytes, read {bytesRead} bytes: {path}");
        }

        if (stripUtf8Bom && HasUtf8Bom(bytes))
            return bytes[3..];

        return bytes;
    }

    /// <summary>
    /// Shared read/write access. Returns null if file does not exist.
    /// </summary>
    public static byte[]? TryReadAllBytes(string path, bool stripUtf8Bom = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty or whitespace", nameof(path));

        if (!File.Exists(path))
            return null;

        return ReadAllBytes(path, stripUtf8Bom);
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 &&
               bytes[0] == Utf8Bom[0] &&
               bytes[1] == Utf8Bom[1] &&
               bytes[2] == Utf8Bom[2];
    }
}
