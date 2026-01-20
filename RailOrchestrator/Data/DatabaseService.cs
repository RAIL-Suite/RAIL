using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using WpfRagApp.Services;

namespace WpfRagApp.Data
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rag_index.db");
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                
                // Documents Table
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Documents (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FilePath TEXT NOT NULL UNIQUE,
                        LastModified TEXT NOT NULL,
                        IsIndexed INTEGER NOT NULL DEFAULT 0
                    )");

                // Chunks Table
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Chunks (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DocumentId INTEGER NOT NULL,
                        Text TEXT NOT NULL,
                        Embedding BLOB NOT NULL,
                        FOREIGN KEY(DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
                    )");
                
                // Index for faster lookups
                connection.Execute("CREATE INDEX IF NOT EXISTS IX_Chunks_DocumentId ON Chunks(DocumentId)");
            }
        }

        private IDbConnection GetConnection() => new SqliteConnection(_connectionString);

        public async Task<int> UpsertDocumentAsync(string filePath, DateTime lastModified)
        {
            using (var connection = GetConnection())
            {
                var existing = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Id, LastModified FROM Documents WHERE FilePath = @FilePath", new { FilePath = filePath });

                if (existing != null)
                {
                    DateTime storedDate = DateTime.Parse(existing.LastModified);
                    if (storedDate >= lastModified)
                    {
                        return -1; // Already up to date
                    }
                    
                    // Update timestamp and reset indexed status
                    await connection.ExecuteAsync(
                        "UPDATE Documents SET LastModified = @LastModified, IsIndexed = 0 WHERE Id = @Id",
                        new { LastModified = lastModified.ToString("o"), Id = existing.Id });
                    
                    // Clear old chunks
                    await connection.ExecuteAsync("DELETE FROM Chunks WHERE DocumentId = @Id", new { Id = existing.Id });
                    
                    return (int)existing.Id;
                }
                else
                {
                    var id = await connection.QuerySingleAsync<int>(
                        "INSERT INTO Documents (FilePath, LastModified, IsIndexed) VALUES (@FilePath, @LastModified, 0) RETURNING Id",
                        new { FilePath = filePath, LastModified = lastModified.ToString("o") });
                    return id;
                }
            }
        }

        public async Task MarkDocumentAsIndexedAsync(int documentId)
        {
            using (var connection = GetConnection())
            {
                await connection.ExecuteAsync("UPDATE Documents SET IsIndexed = 1 WHERE Id = @Id", new { Id = documentId });
            }
        }

        public async Task SaveChunksAsync(int documentId, IEnumerable<(string Text, List<float> Embedding)> chunks)
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var chunk in chunks)
                    {
                        // Convert List<float> to byte array for BLOB storage
                        var embeddingBytes = new byte[chunk.Embedding.Count * 4];
                        Buffer.BlockCopy(chunk.Embedding.ToArray(), 0, embeddingBytes, 0, embeddingBytes.Length);

                        await connection.ExecuteAsync(
                            "INSERT INTO Chunks (DocumentId, Text, Embedding) VALUES (@DocumentId, @Text, @Embedding)",
                            new { DocumentId = documentId, Text = chunk.Text, Embedding = embeddingBytes },
                            transaction);
                    }
                    transaction.Commit();
                }
            }
        }

        public async Task<List<(string Text, List<float> Embedding)>> GetAllChunksAsync()
        {
            using (var connection = GetConnection())
            {
                var result = await connection.QueryAsync<dynamic>("SELECT Text, Embedding FROM Chunks");
                var chunks = new List<(string Text, List<float> Embedding)>();

                foreach (var row in result)
                {
                    byte[] bytes = (byte[])row.Embedding;
                    var floats = new float[bytes.Length / 4];
                    Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
                    
                    chunks.Add((row.Text, floats.ToList()));
                }
                return chunks;
            }
        }
    }
}





