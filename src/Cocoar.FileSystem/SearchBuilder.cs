using System.Collections;

namespace Cocoar.FileSystem;

/// <summary>
/// Fluent builder for configuring and executing file system searches.
/// </summary>
public sealed class SearchBuilder : IEnumerable<string>
{
    private readonly string _searchPath;
    private string _searchPattern = "*";
    private HashSet<string>? _excludedFolders;
    private int? _maxDepth = 0;

    internal SearchBuilder(string searchPath)
    {
        if (string.IsNullOrWhiteSpace(searchPath))
            throw new ArgumentException("Search path cannot be null or whitespace.", nameof(searchPath));
        
        _searchPath = searchPath;
    }

    public SearchBuilder WithPattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        _searchPattern = pattern;
        return this;
    }

    public SearchBuilder Excluding(params string[] folderNames)
    {
        ArgumentNullException.ThrowIfNull(folderNames);
        
        _excludedFolders ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folderNames)
        {
            if (!string.IsNullOrWhiteSpace(folder))
                _excludedFolders.Add(folder);
        }
        
        return this;
    }

    /// <summary>
    /// 0 = current directory only, 1 = current + one level, etc. Use null for unlimited depth.
    /// </summary>
    public SearchBuilder WithMaxDepth(int? depth)
    {
        if (depth < 0)
            throw new ArgumentException("Max depth cannot be negative.", nameof(depth));
        
        _maxDepth = depth;
        return this;
    }

    public SearchBuilder Recursively()
    {
        _maxDepth = null;
        return this;
    }

    /// <summary>
    /// Lazy operation that yields results as they are found.
    /// </summary>
    public IEnumerable<string> Enumerate()
    {
        return FileSearcher.EnumerateFiles(
            new DirectoryInfo(_searchPath),
            _searchPattern,
            _excludedFolders,
            _maxDepth);
    }

    public List<string> ToList() => Enumerate().ToList();

    public string[] ToArray() => Enumerate().ToArray();

    /// <summary>
    /// Counts the number of files matching the configured criteria.
    /// </summary>
    /// <returns>The number of matching files.</returns>
    public int Count() => Enumerate().Count();

    /// <summary>
    /// Checks if any files match the configured criteria.
    /// </summary>
    /// <returns>True if at least one file matches; otherwise, false.</returns>
    public bool Any() => Enumerate().Any();

    /// <summary>
    /// Gets the first file matching the configured criteria.
    /// </summary>
    /// <returns>The full path of the first matching file.</returns>
    /// <exception cref="InvalidOperationException">If no files match the criteria.</exception>
    public string First() => Enumerate().First();

    /// <summary>
    /// Gets the first file matching the configured criteria, or null if no files match.
    /// </summary>
    /// <returns>The full path of the first matching file, or null if none found.</returns>
    public string? FirstOrDefault() => Enumerate().FirstOrDefault();

    /// <summary>
    /// Returns an enumerator that iterates through the matching files.
    /// This enables foreach and LINQ support directly on the builder.
    /// </summary>
    public IEnumerator<string> GetEnumerator() => Enumerate().GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the matching files.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
