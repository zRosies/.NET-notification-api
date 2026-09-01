using Npgsql;

namespace MyApi.Services;

public static class DatabaseUrlParser
{
    public static string ToConnectionString(string databaseUrl)
    {
        var withoutScheme = databaseUrl[(databaseUrl.IndexOf("://", StringComparison.Ordinal) + 3)..];
        var authorityEnd = withoutScheme.IndexOf('/');
        var authority = authorityEnd >= 0 ? withoutScheme[..authorityEnd] : withoutScheme;
        var path = authorityEnd >= 0 ? withoutScheme[authorityEnd..] : "/postgres";
        var separator = authority.LastIndexOf('@');

        if (separator < 0)
        {
            throw new InvalidOperationException("DATABASE_URL must include a username and password.");
        }

        var userInfo = authority[..separator].Split(':', 2);
        var hostAndPort = authority[(separator + 1)..];

        if (userInfo.Length != 2)
        {
            throw new InvalidOperationException("DATABASE_URL must include a username and password.");
        }

        var portSeparator = hostAndPort.LastIndexOf(':');
        var host = portSeparator >= 0 ? hostAndPort[..portSeparator] : hostAndPort;
        var port = portSeparator >= 0 && int.TryParse(hostAndPort[(portSeparator + 1)..], out var parsedPort)
            ? parsedPort
            : 5432;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = path.TrimStart('/').Split('?', 2)[0],
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }
}
