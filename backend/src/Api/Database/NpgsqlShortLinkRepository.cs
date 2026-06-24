using Api.Contracts;
using Api.Models;
using Npgsql;

namespace Api.Database;

public sealed class NpgsqlShortLinkRepository : IShortLinkRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public NpgsqlShortLinkRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> CreateAsync(ShortLink link, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO short_links (
                code,
                long_url,
                created_at_utc,
                expires_at_utc,
                is_custom_alias,
                click_count
            )
            VALUES (
                @code,
                @long_url,
                @created_at_utc,
                @expires_at_utc,
                @is_custom_alias,
                @click_count
            )
            ON CONFLICT (code) DO NOTHING;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("code", link.Code);
        command.Parameters.AddWithValue("long_url", link.LongUrl);
        command.Parameters.AddWithValue("created_at_utc", link.CreatedAtUtc);
        command.Parameters.AddWithValue("expires_at_utc", (object?)link.ExpiresAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("is_custom_alias", link.IsCustomAlias);
        command.Parameters.AddWithValue("click_count", link.ClickCount);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows == 1;
    }

    public async Task<ShortLink?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                code,
                long_url,
                created_at_utc,
                expires_at_utc,
                is_custom_alias,
                click_count
            FROM short_links
            WHERE code = @code;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadShortLink(reader);
    }

    public async Task<IReadOnlyList<ShortLink>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                code,
                long_url,
                created_at_utc,
                expires_at_utc,
                is_custom_alias,
                click_count
            FROM short_links
            ORDER BY created_at_utc DESC
            LIMIT @limit;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("limit", limit);

        var links = new List<ShortLink>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            links.Add(ReadShortLink(reader));
        }

        return links;
    }

    private static ShortLink ReadShortLink(NpgsqlDataReader reader)
    {
        return new ShortLink(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetBoolean(4),
            reader.GetInt64(5));
    }
}
