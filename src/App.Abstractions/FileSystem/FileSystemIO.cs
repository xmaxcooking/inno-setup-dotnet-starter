namespace App.Abstractions.FileSystem;

public sealed class FileSystemIO : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public Stream CreateFile(string path) => File.Create(path);
    public void DeleteFile(string path) => File.Delete(path);
}