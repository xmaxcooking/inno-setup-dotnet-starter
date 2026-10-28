namespace App.Abstractions.FileSystem;

internal interface IFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    void CreateDirectory(string path);
    Stream CreateFile(string path);
    void DeleteFile(string path);
}