using System;

namespace GameProject.Core.Ports;

/// <summary>
/// Abstract port for filesystem storage operations. Decouples persistence from System.IO.File,
/// enabling unit tests to verify atomic saving, corruption isolation, and backup rollbacks
/// completely in-memory without touching the host filesystem.
/// </summary>
public interface ISaveStorage
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void EnsureDirectoryExists(string directoryPath) => CreateDirectory(directoryPath);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    void DeleteFile(string path);
    void Delete(string path) => DeleteFile(path);
    void MoveFile(string sourcePath, string destinationPath, bool overwrite = true);
    void Move(string sourcePath, string destinationPath, bool overwrite = true) => MoveFile(sourcePath, destinationPath, overwrite);
    void CopyFile(string sourcePath, string destinationPath, bool overwrite = true);
    void Copy(string sourcePath, string destinationPath, bool overwrite = true) => CopyFile(sourcePath, destinationPath, overwrite);
    void ReplaceFile(string sourcePath, string destinationPath, string? backupPath = null);
    DateTime GetLastWriteTimeUtc(string path);
}
