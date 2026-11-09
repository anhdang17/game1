using System;
using System.Collections.Generic;
using System.IO;
using GameProject.Core.Ports;

namespace GameProject.Tests.Mocks;

public sealed class MockSaveStorage : ISaveStorage
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public bool ShouldSimulateIoFailure { get; set; } = false;

    public bool FileExists(string path)
    {
        return _files.ContainsKey(Normalize(path));
    }

    public string ReadAllText(string path)
    {
        if (ShouldSimulateIoFailure)
            throw new IOException("Simulated I/O failure on ReadAllText.");

        string norm = Normalize(path);
        if (_files.TryGetValue(norm, out var content))
        {
            return content;
        }

        throw new FileNotFoundException($"File not found in MockSaveStorage: {path}");
    }

    public void WriteAllText(string path, string contents)
    {
        if (ShouldSimulateIoFailure)
            throw new IOException("Simulated I/O failure on WriteAllText.");

        string norm = Normalize(path);
        _files[norm] = contents;
    }

    public void Move(string sourcePath, string destinationPath, bool overwrite = true)
    {
        if (ShouldSimulateIoFailure)
            throw new IOException("Simulated I/O failure on Move.");

        string src = Normalize(sourcePath);
        string dst = Normalize(destinationPath);

        if (!_files.TryGetValue(src, out var content))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}");
        }

        if (_files.ContainsKey(dst) && !overwrite)
        {
            throw new IOException($"Destination file exists and overwrite is false: {destinationPath}");
        }

        _files[dst] = content;
        _files.Remove(src);
    }

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite = true) =>
        Move(sourcePath, destinationPath, overwrite);

    public void Copy(string sourcePath, string destinationPath, bool overwrite = true)
    {
        if (ShouldSimulateIoFailure)
            throw new IOException("Simulated I/O failure on Copy.");

        string src = Normalize(sourcePath);
        string dst = Normalize(destinationPath);

        if (!_files.TryGetValue(src, out var content))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}");
        }

        if (_files.ContainsKey(dst) && !overwrite)
        {
            throw new IOException($"Destination file exists and overwrite is false: {destinationPath}");
        }

        _files[dst] = content;
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite = true) =>
        Copy(sourcePath, destinationPath, overwrite);

    public void ReplaceFile(string sourcePath, string destinationPath, string? backupPath = null)
    {
        if (ShouldSimulateIoFailure)
            throw new IOException("Simulated I/O failure on ReplaceFile.");

        string src = Normalize(sourcePath);
        string dst = Normalize(destinationPath);

        if (!_files.TryGetValue(src, out var content))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}");
        }

        if (!string.IsNullOrEmpty(backupPath) && _files.TryGetValue(dst, out var existing))
        {
            _files[Normalize(backupPath)] = existing;
        }

        _files[dst] = content;
        _files.Remove(src);
    }

    public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;

    public void Delete(string path)
    {
        string norm = Normalize(path);
        _files.Remove(norm);
    }

    public void DeleteFile(string path) => Delete(path);

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(Normalize(path));
    }

    public void CreateDirectory(string path)
    {
        _directories.Add(Normalize(path));
    }

    public void EnsureDirectoryExists(string directoryPath) => CreateDirectory(directoryPath);

    public void InjectCorruption(string path, string corruptContent)
    {
        WriteAllText(path, corruptContent);
    }

    public void Clear()
    {
        _files.Clear();
        _directories.Clear();
        ShouldSimulateIoFailure = false;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
