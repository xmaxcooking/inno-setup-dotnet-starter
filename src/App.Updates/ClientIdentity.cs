using App.Abstractions.FileSystem;
using App.Updates.FileSystem;
using Microsoft.Extensions.Logging;

namespace App.Updates;

public sealed class ClientIdentity : IClientIdentity
{
    private readonly IFileSystem _fs;
    private readonly ILogger<ClientIdentity> _logger;

    public ClientIdentity(IFileSystem fileSystem, ILogger<ClientIdentity> logger)
    {
        _fs = fileSystem;
        _logger = logger;
    }

    public string GetOrCreate()
    {
        try
        {
            if (_fs.FileExists(SpecialFiles.ClientId))
            {
                var stored = _fs.ReadAllText(SpecialFiles.ClientId);
                if (Guid.TryParse(stored, out _))
                {
                    return stored;
                }
            }

            var id = Guid.NewGuid().ToString();
            _fs.CreateDirectory(ApplicationDataDirectory.Path);
            _fs.WriteAllText(SpecialFiles.ClientId, id);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read or persist the client id; using a one-run-only id instead.");
            return Guid.NewGuid().ToString();
        }
    }
}