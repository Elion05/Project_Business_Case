using Microsoft.Data.Sqlite;
using System.Data;

namespace BestelApp_Cons.Services;

public class IdempotencyService
{
    private readonly string _connectionString;

    public IdempotencyService(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync()
    {
        var dbFile = _connectionString.Replace("Data Source=", "");
        var directory = Path.GetDirectoryName(dbFile);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS message_state (
                MessageId TEXT PRIMARY KEY,
                Status TEXT NOT NULL,
                RetryCount INTEGER DEFAULT 0,
                LastError TEXT,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                PayloadJson TEXT
            );
        ";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<MessageState?> GetStateAsync(string messageId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT MessageId, Status, RetryCount FROM message_state WHERE MessageId = $id";
        command.Parameters.AddWithValue("$id", messageId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new MessageState
            {
                MessageId = reader.GetString(0),
                Status = reader.GetString(1),
                RetryCount = reader.GetInt32(2)
            };
        }
        return null;
    }

    public async Task MarkProcessingAsync(string messageId, string? payloadJson)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO message_state (MessageId, Status, PayloadJson, CreatedAt, UpdatedAt)
            VALUES ($id, 'Processing', $json, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            ON CONFLICT(MessageId) DO UPDATE SET
                Status = 'Processing',
                UpdatedAt = CURRENT_TIMESTAMP;
        ";
        command.Parameters.AddWithValue("$id", messageId);
        command.Parameters.AddWithValue("$json", payloadJson ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task MarkProcessedAsync(string messageId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE message_state
            SET Status = 'Processed', UpdatedAt = CURRENT_TIMESTAMP
            WHERE MessageId = $id;
        ";
        command.Parameters.AddWithValue("$id", messageId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task MarkFailedAsync(string messageId, string error)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE message_state
            SET Status = 'Failed', 
                RetryCount = RetryCount + 1,
                LastError = $error,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE MessageId = $id;
        ";
        command.Parameters.AddWithValue("$id", messageId);
        command.Parameters.AddWithValue("$error", error);
        await command.ExecuteNonQueryAsync();
    }
}

public class MessageState
{
    public required string MessageId { get; set; }
    public required string Status { get; set; }
    public int RetryCount { get; set; }
}
