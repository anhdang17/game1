using System;
using System.IO;
using System.Text;

namespace GameProject.Core.Ports;

/// <summary>
/// Standard production implementation of ISaveStorage using System.IO.
/// Enforces UTF-8 encoding and atomic replace semantics.
/// </summary>
public sealed class FileSaveStorage : ISaveStorage
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public void EnsureDirectoryExists(string directoryPath) => CreateDirectory(directoryPath);

    public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);

    public void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite = true)
    {
        if (File.Exists(destinationPath) && overwrite)
        {
            File.Delete(destinationPath);
        }
        File.Move(sourcePath, destinationPath);
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite = true)
    {
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.Copy(sourcePath, destinationPath, overwrite);
    }

    public void ReplaceFile(string sourcePath, string destinationPath, string? backupPath = null)
    {
        if (!string.IsNullOrEmpty(backupPath) && File.Exists(destinationPath))
        {
            CopyFile(destinationPath, backupPath, overwrite: true);
        }

        if (File.Exists(destinationPath))
        {
            // POSIX rename syscall on macOS/Linux: atomic replace
            File.Move(sourcePath, destinationPath, overwrite: true);
        }
        else
        {
            File.Move(sourcePath, destinationPath);
        }
    }

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
}
