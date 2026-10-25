using App.Abstractions.FileSystem;

namespace App.Updates.Tests;

internal sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _contentsByPath = new();
    private bool _defaultExists;
    private string? _defaultContent;

    public List<string> CreatedDirectories { get; } = new();
    public List<string> CreatedFilePaths { get; } = new();
    public List<string> DeletedFilePaths { get; } = new();
    public string? WrittenContent { get; private set; }
    public bool ThrowOnReadAllText { get; set; }
    public bool ThrowOnWriteAllText { get; set; }

    public void SeedExistingFile(string content)
    {
        _defaultExists = true;
        _defaultContent = content;
    }

    public bool FileExists(string path) => _contentsByPath.ContainsKey(path) || _defaultExists;

    public string ReadAllText(string path)
    {
        if (ThrowOnReadAllText)
        {
            throw new IOException("Simulated read failure.");
        }

        if (_contentsByPath.TryGetValue(path, out var content))
        {
            return content;
        }

        if (_defaultContent is null)
        {
            throw new FileNotFoundException(path);
        }

        return _defaultContent;
    }

    public void WriteAllText(string path, string contents)
    {
        if (ThrowOnWriteAllText)
        {
            throw new IOException("Simulated write failure.");
        }

        _contentsByPath[path] = contents;
        WrittenContent = contents;
    }

    public void CreateDirectory(string path) => CreatedDirectories.Add(path);

    public Stream CreateFile(string path)
    {
        CreatedFilePaths.Add(path);
        return new MemoryStream();
    }

    public void DeleteFile(string path)
    {
        _contentsByPath.Remove(path);
        DeletedFilePaths.Add(path);
    }
}