using System.IO.Enumeration;

namespace Cocoar.FileSystem;

/// <summary>
/// High-performance file system searcher using FileSystemEnumerable for efficient directory traversal.
/// </summary>
public static class FileSearcher
{
    /// <summary>
    /// Enumerates files in a directory with optional filtering and depth control.
    /// </summary>
    /// <param name="searchPath">The root directory to search.</param>
    /// <param name="searchPattern">The search pattern to match files against (e.g., "*.txt", "*").</param>
    /// <param name="excludedFolders">Optional set of folder names to exclude from the search.</param>
    /// <param name="maxDepth">Optional maximum depth to recurse. If null, recurses through all subdirectories.</param>
    /// <returns>An enumerable collection of full file paths matching the criteria.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the search path does not exist.</exception>
    public static IEnumerable<string> EnumerateFiles(
        DirectoryInfo searchPath,
        string searchPattern,
        HashSet<string>? excludedFolders = null,
        int? maxDepth = null)
    {
        ArgumentNullException.ThrowIfNull(searchPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

        if (!searchPath.Exists)
        {
            throw new DirectoryNotFoundException($"The directory '{searchPath.FullName}' does not exist.");
        }

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = !maxDepth.HasValue
        };

        var excluded = excludedFolders ?? [];

        if (maxDepth.HasValue)
        {
            return EnumerateFilesWithDepth(searchPath.FullName, searchPattern, excluded, maxDepth.Value, 0, options);
        }

        return EnumerateFilesWithoutDepth(searchPath.FullName, searchPattern, excluded, options);
    }

    private static IEnumerable<string> EnumerateFilesWithDepth(
        string root,
        string searchPattern,
        HashSet<string> excludedFolders,
        int maxDepth,
        int currentDepth,
        EnumerationOptions options)
    {
        if (currentDepth > maxDepth)
        {
            yield break;
        }

        foreach (var file in EnumerateCurrentDirectory(root, searchPattern, excludedFolders, options))
        {
            yield return file;
        }

        if (currentDepth < maxDepth)
        {
            foreach (var subDir in SafeEnumerateDirectories(root))
            {
                if (!excludedFolders.Contains(Path.GetFileName(subDir)))
                {
                    foreach (var file in EnumerateFilesWithDepth(subDir, searchPattern, excludedFolders, maxDepth, currentDepth + 1, options))
                    {
                        yield return file;
                    }
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesWithoutDepth(
        string root,
        string searchPattern,
        HashSet<string> excludedFolders,
        EnumerationOptions options)
    {
        var fileEnumerator = new FileSystemEnumerable<string>(
            root,
            (ref FileSystemEntry entry) => entry.ToFullPath(),
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                !entry.IsDirectory && FileSystemName.MatchesSimpleExpression(searchPattern, entry.FileName),
            ShouldRecursePredicate = (ref FileSystemEntry entry) =>
                entry.IsDirectory && !IsExcluded(entry.FileName.ToString(), excludedFolders)
        };

        foreach (var file in fileEnumerator)
        {
            yield return file;
        }
    }

    private static IEnumerable<string> EnumerateCurrentDirectory(
        string root,
        string searchPattern,
        HashSet<string> excludedFolders,
        EnumerationOptions options)
    {
        var fileEnumerator = new FileSystemEnumerable<string>(
            root,
            (ref FileSystemEntry entry) => entry.ToFullPath(),
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                !entry.IsDirectory && FileSystemName.MatchesSimpleExpression(searchPattern, entry.FileName),
            ShouldRecursePredicate = (ref FileSystemEntry entry) =>
                entry.IsDirectory && !IsExcluded(entry.FileName.ToString(), excludedFolders)
        };

        foreach (var file in fileEnumerator)
        {
            yield return file;
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (PathTooLongException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static bool IsExcluded(string directoryName, HashSet<string> excludedFolders)
    {
        return excludedFolders.Contains(directoryName);
    }
}
