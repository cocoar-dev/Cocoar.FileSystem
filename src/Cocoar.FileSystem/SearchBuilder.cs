using System.Collections;

namespace Cocoar.FileSystem;

/// <summary>
/// Fluent builder for configuring and executing file system searches.
/// </summary>
public sealed class SearchBuilder : IEnumerable<string>
{
    private readonly string _searchPath;
    private string _searchPattern = "*";
    private List<string>? _filters;
    private HashSet<string>? _excludedFolders;
    private int? _maxDepth = 0;

    internal SearchBuilder(string searchPath)
    {
        if (string.IsNullOrWhiteSpace(searchPath))
            throw new ArgumentException("Search path cannot be null or whitespace.", nameof(searchPath));
        
        _searchPath = searchPath;
    }

    /// <summary>
    /// Adds file patterns to search for. Can be called multiple times to add more patterns.
    /// Patterns are matched against filenames only (not full paths).
    /// Supports DOS-style wildcards: * (any characters) and ? (single character).
    /// </summary>
    /// <param name="patterns">One or more file patterns (e.g., "*.txt", "test-*.log")</param>
    /// <example>
    /// .WithFilter("*.cs", "*.csproj")
    /// .WithFilter("*.json")  // Adds to existing patterns
    /// </example>
    public SearchBuilder WithFilter(params string[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Length == 0)
            throw new ArgumentException("At least one pattern must be specified.", nameof(patterns));
        if (patterns.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Patterns cannot be null or whitespace.", nameof(patterns));
        
        _filters ??= new List<string>();
        _filters.AddRange(patterns);
        _searchPattern = "*"; // Will filter in enumeration logic
        return this;
    }

    /// <summary>
    /// Sets a single file pattern to search for.
    /// For multiple patterns, use WithFilter(params string[]) instead.
    /// </summary>
    /// <param name="pattern">File pattern (e.g., "*.txt")</param>
    public SearchBuilder WithPattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        _searchPattern = pattern;
        _filters = null; // Clear any multiple filters
        return this;
    }

    /// <summary>
    /// Clears all previously configured file patterns.
    /// </summary>
    public SearchBuilder ClearFilters()
    {
        _filters?.Clear();
        _searchPattern = "*";
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
    /// Controls subdirectory recursion depth.
    /// 0 = current directory only, 1 = direct children, 2+ = specific depth, -1 = unlimited.
    /// </summary>
    /// <param name="maxDepth">Maximum depth to recurse (-1 for unlimited)</param>
    public SearchBuilder IncludeSubdirectories(int maxDepth)
    {
        if (maxDepth < -1)
            throw new ArgumentException("Max depth must be -1 (unlimited) or greater than or equal to 0.", nameof(maxDepth));
        
        _maxDepth = maxDepth == -1 ? null : maxDepth;
        return this;
    }

    /// <summary>
    /// Enables or disables subdirectory recursion.
    /// True = unlimited depth, False = current directory only.
    /// </summary>
    /// <param name="include">Whether to include subdirectories</param>
    public SearchBuilder IncludeSubdirectories(bool include = true)
    {
        _maxDepth = include ? null : 0;
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

    /// <summary>
    /// Recursively search all subdirectories (unlimited depth).
    /// </summary>
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
        // If multiple filters configured, search for each pattern
        if (_filters != null && _filters.Count > 0)
        {
            var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pattern in _filters)
            {
                foreach (var file in FileSearcher.EnumerateFiles(
                    new DirectoryInfo(_searchPath),
                    pattern,
                    _excludedFolders,
                    _maxDepth))
                {
                    if (allFiles.Add(file))
                        yield return file;
                }
            }
        }
        else
        {
            // Single pattern
            foreach (var file in FileSearcher.EnumerateFiles(
                new DirectoryInfo(_searchPath),
                _searchPattern,
                _excludedFolders,
                _maxDepth))
            {
                yield return file;
            }
        }
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
