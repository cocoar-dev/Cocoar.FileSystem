using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Cocoar.FileSystem;

/// <summary>
/// Platform-specific file system identity tracking.
/// Used to detect when a directory is deleted and recreated (same path, different identity).
/// </summary>
internal static class FileSystemIdentity
{
    /// <summary>
    /// Represents a unique identity of a file system entry.
    /// </summary>
    public readonly struct Identity : IEquatable<Identity>
    {
        public readonly ulong DeviceId;
        public readonly ulong FileId;
        
        public Identity(ulong deviceId, ulong fileId)
        {
            DeviceId = deviceId;
            FileId = fileId;
        }
        
        public bool Equals(Identity other) => DeviceId == other.DeviceId && FileId == other.FileId;
        public override bool Equals(object? obj) => obj is Identity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(DeviceId, FileId);
        public static bool operator ==(Identity left, Identity right) => left.Equals(right);
        public static bool operator !=(Identity left, Identity right) => !left.Equals(right);
        
        public override string ToString() => $"Dev={DeviceId}, FileId={FileId}";
    }
    
    /// <summary>
    /// Try to get the unique identity of a directory.
    /// Returns false if the directory doesn't exist or identity cannot be determined.
    /// </summary>
    public static bool TryGetIdentity(string path, out Identity identity)
    {
        identity = default;
        
        if (!Directory.Exists(path))
            return false;
            
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return TryGetIdentityWindows(path, out identity);
            }
            else
            {
                // Unix-like (Linux, macOS, etc.)
                return TryGetIdentityUnix(path, out identity);
            }
        }
        catch
        {
            return false;
        }
    }
    
    private static bool TryGetIdentityWindows(string path, out Identity identity)
    {
        identity = default;
        
        try
        {
            using var handle = CreateFileW(
                path,
                0, // No access needed, just get info
                FileShare.ReadWrite | FileShare.Delete,
                IntPtr.Zero,
                FileMode.Open,
                FILE_FLAG_BACKUP_SEMANTICS, // Required for directories
                IntPtr.Zero);
                
            if (handle.IsInvalid)
                return false;
                
            if (!GetFileInformationByHandle(handle, out var info))
                return false;
                
            ulong fileId = ((ulong)info.nFileIndexHigh << 32) | info.nFileIndexLow;
            identity = new Identity(info.dwVolumeSerialNumber, fileId);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static bool TryGetIdentityUnix(string path, out Identity identity)
    {
        identity = default;
        
        try
        {
            // Use stat to get inode information
            if (stat(path, out var statbuf) != 0)
                return false;
                
            identity = new Identity(statbuf.st_dev, statbuf.st_ino);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    #region Windows P/Invoke
    
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);
        
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation);
        
    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;
    }
    
    #endregion
    
    #region Unix P/Invoke
    
    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA2101:Specify marshaling for P/Invoke string arguments",
        Justification = "Unix paths are UTF-8/ASCII, CharSet.Ansi is correct")]
    private static extern int stat([MarshalAs(UnmanagedType.LPStr)] string pathname, out StatBuffer statbuf);
    
    // Simplified stat structure - only fields we need
    [StructLayout(LayoutKind.Sequential)]
    private struct StatBuffer
    {
        public ulong st_dev;     // Device ID
        public ulong st_ino;     // Inode number
        // ... other fields we don't need
    }
    
    #endregion
}
