using Microsoft.AspNetCore.DataProtection;

namespace AdHocReporting.Infrastructure.Services;

public sealed class SettingsSecretProtectionService
{
    private const string EncryptedPrefix = "enc::";
    private readonly IDataProtector _protector;

    public SettingsSecretProtectionService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("AdHocReporting.SettingsSecretProtection.v1");
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        return EncryptedPrefix + _protector.Protect(plainText);
    }

    public bool IsProtected(string? storedValue)
    {
        return !string.IsNullOrWhiteSpace(storedValue)
            && storedValue.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
    }

    public string? Unprotect(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return null;
        }

        if (!storedValue.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            // Backward compatibility for previously stored plain text values.
            return storedValue;
        }

        try
        {
            var payload = storedValue[EncryptedPrefix.Length..];
            return _protector.Unprotect(payload);
        }
        catch
        {
            return null;
        }
    }

    public string? MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var (server, database) = TryExtractConnectionEndpoint(connectionString);

        if (string.IsNullOrWhiteSpace(server) && string.IsNullOrWhiteSpace(database))
        {
            return "Configured (credentials hidden)";
        }

        if (string.IsNullOrWhiteSpace(database))
        {
            return $"Server={server}; Credentials hidden";
        }

        return $"Server={server}; Database={database}; Credentials hidden";
    }

    public string? MaskSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        if (secret.Length <= 8)
        {
            return new string('*', secret.Length);
        }

        return $"{secret[..4]}{new string('*', Math.Max(4, secret.Length - 8))}{secret[^4..]}";
    }

    public (string? Server, string? Database) TryExtractConnectionEndpoint(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (null, null);
        }

        string? server = null;
        string? database = null;

        var segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim().ToLowerInvariant();
            var value = segment[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (key is "data source" or "server" or "address" or "addr" or "network address")
            {
                server ??= value;
                continue;
            }

            if (key is "initial catalog" or "database")
            {
                database ??= value;
            }
        }

        return (server, database);
    }
}