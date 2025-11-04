namespace Cocoar.FileSystem;

/// <summary>
/// Fluent builder for configuring and executing file system searches.
/// </summary>
public sealed class SearchBuilder
{
    private readonly string _searchPath;
    private string _searchPattern = "*";
    private HashSet<string>? _excludedFolders;
    private int? _maxDepth;

    internal SearchBuilder(string searchPath)
    {
        if (string.IsNullOrWhiteSpace(searchPath))
            throw new ArgumentException("Search path cannot be null or whitespace.", nameof(searchPath));
        
        _searchPath = searchPath;
    }

    /// <summary>
    /// Sets the search pattern to match files against.
    /// </summary>
    /// <param name="pattern">The search pattern (e.g., "*.txt", "*.json").</param>
    public SearchBuilder WithPattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        _searchPattern = pattern;
        return this;
    }

    /// <summary>
    /// Excludes the specified folder names from the search.
    /// </summary>
    /// <param name="folderNames">The folder names to exclude.</param>
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
    /// Sets the maximum depth to recurse into subdirectories.
    /// </summary>
    /// <param name="depth">The maximum depth (0 = current directory only, 1 = current + one level, etc.).</param>
    public SearchBuilder WithMaxDepth(int depth)
    {
        if (depth < 0)
            throw new ArgumentException("Max depth cannot be negative.", nameof(depth));
        
        _maxDepth = depth;
        return this;
    }

    /// <summary>
    /// Limits the search to only the current directory (does not recurse into subdirectories).
    /// Equivalent to WithMaxDepth(0).
    /// </summary>
    public SearchBuilder InCurrentDirectoryOnly()
    {
        _maxDepth = 0;
        return this;
    }

    /// <summary>
    /// Enumerates files matching the configured criteria.
    /// This is a lazy operation that yields results as they are found.
    /// </summary>
    /// <returns>An enumerable collection of full file paths.</returns>
    public IEnumerable<string> Enumerate()
    {
        return FileSearcher.EnumerateFiles(
            new DirectoryInfo(_searchPath),
            _searchPattern,
            _excludedFolders,
            _maxDepth);
    }

    /// <summary>
    /// Materializes all matching files into a list.
    /// </summary>
    /// <returns>A list of full file paths.</returns>
    public List<string> ToList() => Enumerate().ToList();

    /// <summary>
    /// Materializes all matching files into an array.
    /// </summary>
    /// <returns>An array of full file paths.</returns>
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
}
