using System;
using System.Collections.Generic;
using System.IO;

namespace GameProject.Core.Ports;

/// <summary>
/// In-memory virtual storage implementation for hermetic persistence testing.
/// Provides fault injection methods (corruption, truncation, I/O exceptions).
/// </summary>
public class InMemorySaveStorage : ISaveStorage
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _writeTimes = new(StringComparer.OrdinalIgnoreCase);
    private bool _throwNextIoException;

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public void CreateDirectory(string path)
    {
        _directories.Add(Normalize(path));
    }

    public void EnsureDirectoryExists(string directoryPath) => CreateDirectory(directoryPath);

    public string ReadAllText(string path)
    {
        CheckThrowIo();
        var norm = Normalize(path);
        if (!_files.TryGetValue(norm, out var content))
        {
            throw new FileNotFoundException($"Virtual file not found: {path}");
        }
        return content;
    }

    public void WriteAllText(string path, string content)
    {
        CheckThrowIo();
        var norm = Normalize(path);
        _files[norm] = content;
        _writeTimes[norm] = DateTime.UtcNow;
    }

    public void DeleteFile(string path)
    {
        CheckThrowIo();
        var norm = Normalize(path);
        _files.Remove(norm);
        _writeTimes.Remove(norm);
    }

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite = true)
    {
        CheckThrowIo();
        var srcNorm = Normalize(sourcePath);
        var destNorm = Normalize(destinationPath);

        if (!_files.TryGetValue(srcNorm, out var content))
        {
            throw new FileNotFoundException($"Virtual source file not found: {sourcePath}");
        }

        if (_files.ContainsKey(destNorm) && !overwrite)
        {
            throw new IOException($"Virtual destination file already exists: {destinationPath}");
        }

        _files[destNorm] = content;
        _writeTimes[destNorm] = DateTime.UtcNow;
        _files.Remove(srcNorm);
        _writeTimes.Remove(srcNorm);
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite = true)
    {
        CheckThrowIo();
        var srcNorm = Normalize(sourcePath);
        var destNorm = Normalize(destinationPath);

        if (!_files.TryGetValue(srcNorm, out var content))
        {
            throw new FileNotFoundException($"Virtual source file not found: {sourcePath}");
        }

        if (_files.ContainsKey(destNorm) && !overwrite)
        {
            throw new IOException($"Virtual destination file already exists: {destinationPath}");
        }

        _files[destNorm] = content;
        _writeTimes[destNorm] = DateTime.UtcNow;
    }

    public void ReplaceFile(string sourcePath, string destinationPath, string? backupPath = null)
    {
        CheckThrowIo();
        var srcNorm = Normalize(sourcePath);
        var destNorm = Normalize(destinationPath);

        if (!_files.TryGetValue(srcNorm, out var content))
        {
            throw new FileNotFoundException($"Virtual source file not found: {sourcePath}");
        }

        if (!string.IsNullOrEmpty(backupPath) && _files.ContainsKey(destNorm))
        {
            var bakNorm = Normalize(backupPath);
            _files[bakNorm] = _files[destNorm];
            _writeTimes[bakNorm] = DateTime.UtcNow;
        }

        _files[destNorm] = content;
        _writeTimes[destNorm] = DateTime.UtcNow;
        _files.Remove(srcNorm);
        _writeTimes.Remove(srcNorm);
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        var norm = Normalize(path);
        return _writeTimes.TryGetValue(norm, out var dt) ? dt : DateTime.UtcNow;
    }

    // --- Fault Injection & Testing Helpers ---

    public void InjectCorruption(string path, string corruptJson = "{ invalid_json: true, [[")
    {
        WriteAllText(path, corruptJson);
    }

    public void SimulateTruncation(string path)
    {
        WriteAllText(path, "{\"saveVersion\": \"1.0.0\", \"playerProfile\":");
    }

    public void SimulateIoException()
    {
        _throwNextIoException = true;
    }

    public void Clear()
    {
        _files.Clear();
        _directories.Clear();
        _writeTimes.Clear();
        _throwNextIoException = false;
    }

    private void CheckThrowIo()
    {
        if (_throwNextIoException)
        {
            _throwNextIoException = false;
            throw new IOException("Simulated I/O disk failure");
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/');
}

/// <summary>
/// Alias for InMemorySaveStorage.
/// </summary>
public class MemorySaveStorage : InMemorySaveStorage { }

/// <summary>
/// Alias for InMemorySaveStorage.
/// </summary>
public class MockSaveStorage : InMemorySaveStorage { }
