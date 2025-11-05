namespace Cocoar.FileSystem;

/// <summary>
/// Provides secure file reading operations with shared access support.
/// Particularly useful when reading sensitive content as bytes to avoid immutable strings in memory.
/// </summary>
public static class FileReader
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Reads all bytes from a file with shared read/write access.
    /// </summary>
    /// <param name="path">Path to the file to read</param>
    /// <param name="stripUtf8Bom">If true, removes UTF-8 BOM (EF BB BF) from the beginning if present</param>
    /// <returns>Byte array containing the file contents</returns>
    /// <exception cref="ArgumentNullException">When path is null</exception>
    /// <exception cref="ArgumentException">When path is empty or whitespace</exception>
    /// <exception cref="FileNotFoundException">When file does not exist</exception>
    /// <exception cref="IOException">When file cannot be read completely</exception>
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
    /// Attempts to read all bytes from a file with shared read/write access.
    /// Returns null if the file does not exist.
    /// </summary>
    /// <param name="path">Path to the file to read</param>
    /// <param name="stripUtf8Bom">If true, removes UTF-8 BOM (EF BB BF) from the beginning if present</param>
    /// <returns>Byte array containing the file contents, or null if file does not exist</returns>
    /// <exception cref="ArgumentNullException">When path is null</exception>
    /// <exception cref="ArgumentException">When path is empty or whitespace</exception>
    /// <exception cref="IOException">When file exists but cannot be read completely</exception>
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
