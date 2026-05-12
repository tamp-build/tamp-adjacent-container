using System.Threading;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;

namespace Tamp.AdjacentContainer.Postgres;

/// <summary>
/// Fluent builder for an adjacent-or-local Postgres connection. Default env var is
/// <c>TAMP_PG_CONNECTION</c> — when set, the value is used verbatim as the connection
/// string; when absent, a <c>postgres:16-alpine</c> container is spawned via
/// Testcontainers and its connection string is returned.
/// </summary>
public sealed class PostgresAdjacentBuilder : AdjacentContainerBuilder<PostgresAdjacentBuilder>
{
    private string _databaseName = "tamp_test";
    private string _username = "tamp";
    private string _password = "tamp";
    private string _image = "postgres:16-alpine";

    /// <summary>
    /// Default env var the contract reads — <c>TAMP_PG_CONNECTION</c>. Adopters who already
    /// export a different name use <see cref="AdjacentContainerBuilder{TSelf}.WithEnvironmentOverride"/>.
    /// </summary>
    public const string DefaultEnvironmentVariableKey = "TAMP_PG_CONNECTION";

    internal PostgresAdjacentBuilder() : base("postgres", DefaultEnvironmentVariableKey) { }

    /// <summary>Database name created in the local-fallback spawn. Ignored in adjacent mode.</summary>
    public PostgresAdjacentBuilder WithDatabase(string databaseName)
    {
        _databaseName = databaseName;
        return this;
    }

    /// <summary>Username for the local-fallback spawn. Ignored in adjacent mode.</summary>
    public PostgresAdjacentBuilder WithUsername(string username)
    {
        _username = username;
        return this;
    }

    /// <summary>Password for the local-fallback spawn. Ignored in adjacent mode.</summary>
    public PostgresAdjacentBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    /// <summary>
    /// Docker image used by the local-fallback spawn. Default <c>postgres:16-alpine</c>.
    /// Pin a different tag here for project-specific schema-version testing.
    /// </summary>
    public PostgresAdjacentBuilder WithLocalFallback(string image = "postgres:16-alpine")
    {
        _image = image;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<TampConnection> SpawnLocalAsync(CancellationToken cancellationToken)
    {
        var container = new PostgreSqlBuilder(_image)
            .WithDatabase(_databaseName)
            .WithUsername(_username)
            .WithPassword(_password)
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);

        return new TampConnection(
            container.GetConnectionString(),
            AdjacentMode.LocalSpawned,
            dispose: async () => await container.DisposeAsync().ConfigureAwait(false));
    }
}
