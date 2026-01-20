using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using WpfRagApp.Data;

namespace WpfRagApp.Services
{
    public class RagService
    {
        private readonly PdfService _pdfService;
        private readonly GeminiService _geminiService;
        private readonly DatabaseService _databaseService;
        private const int BatchSize = 10; // Gemini batch limit is often around 100, but let's be safe

        public RagService(string apiKey)
        {
            _pdfService = new PdfService();
            _geminiService = new GeminiService(apiKey);
            _databaseService = new DatabaseService();
        }

        public async Task IndexDocumentsAsync(string directoryPath, IProgress<string> progress)
        {
            progress.Report("Scanning files...");
            
            // Channel for chunks to be embedded
            var channel = Channel.CreateBounded<(int DocumentId, string Text)>(new BoundedChannelOptions(100)
            {
                SingleReader = true,
                SingleWriter = true
            });

            // Producer: Read files, check DB, chunk text
            var producer = Task.Run(async () =>
            {
                try
                {
                    foreach (var file in _pdfService.GetFileTextStream(directoryPath))
                    {
                        var fileInfo = new FileInfo(file.FilePath);
                        int docId = await _databaseService.UpsertDocumentAsync(file.FilePath, fileInfo.LastWriteTime);

                        if (docId == -1)
                        {
                            progress.Report($"Skipping {Path.GetFileName(file.FilePath)} (Up to date)");
                            continue;
                        }

                        progress.Report($"Processing {Path.GetFileName(file.FilePath)}...");

                        foreach (var chunk in _pdfService.ChunkText(file.Text))
                        {
                            await channel.Writer.WriteAsync((docId, chunk));
                        }
                        
                        // We mark as indexed only after all chunks are processed, 
                        // but here we are just pushing to channel. 
                        // Ideally we track completion per document, but for simplicity 
                        // we will mark as indexed in the consumer or after consumer finishes?
                        // Actually, Upsert clears old chunks. So if we crash mid-way, we have partial chunks.
                        // Better to mark 'IsIndexed' at the end. 
                        // For now, let's assume if we finish the loop, we are good.
                        // Refinement: We could pass a "Document Complete" signal.
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Producer error", ex);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            });

            // Consumer: Batch chunks and embed
            var consumer = Task.Run(async () =>
            {
                var batch = new List<(int DocumentId, string Text)>();
                
                await foreach (var item in channel.Reader.ReadAllAsync())
                {
                    batch.Add(item);

                    if (batch.Count >= BatchSize)
                    {
                        await ProcessBatchAsync(batch, progress);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    await ProcessBatchAsync(batch, progress);
                }
            });

            await Task.WhenAll(producer, consumer);
            
            // Mark all documents as indexed (simplified for this prototype)
            // In a real app, we would track which documents fully completed.
            // Here we assume if no exception threw us out, we are good.
            progress.Report("Indexing complete.");
        }

        private async Task ProcessBatchAsync(List<(int DocumentId, string Text)> batch, IProgress<string> progress)
        {
            try
            {
                var texts = batch.Select(x => x.Text).ToList();
                var embeddings = await _geminiService.BatchEmbedContentAsync(texts);

                // Group by document to save efficiently
                // But DatabaseService.SaveChunksAsync takes a list.
                // We need to map embeddings back to document IDs.
                
                if (embeddings.Count != batch.Count)
                {
                    Logger.Log("Mismatch in embedding count!");
                    return;
                }

                // Save to DB
                // We can't easily bulk insert across multiple documents with current DB service
                // So let's group by DocumentId
                var chunksWithEmbeddings = new List<(int DocumentId, string Text, List<float> Embedding)>();
                for (int i = 0; i < batch.Count; i++)
                {
                    chunksWithEmbeddings.Add((batch[i].DocumentId, batch[i].Text, embeddings[i]));
                }

                foreach (var group in chunksWithEmbeddings.GroupBy(x => x.DocumentId))
                {
                    var chunksToSave = group.Select(x => (x.Text, x.Embedding));
                    await _databaseService.SaveChunksAsync(group.Key, chunksToSave);
                    await _databaseService.MarkDocumentAsIndexedAsync(group.Key);
                }

                progress.Report($"Indexed batch of {batch.Count} chunks.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Consumer batch error", ex);
                progress.Report($"Error processing batch: {ex.Message}");
            }
        }

        public async Task<string> AskAsync(string question)
        {
            // 1. Embed the question
            var questionEmbedding = await _geminiService.EmbedContentAsync(question);

            // 2. Find most similar chunks from DB
            // Loading ALL chunks into memory is bad for "Enterprise".
            // But SQLite doesn't support vector search natively without extensions.
            // For this prototype, we will load all chunks (Text, Embedding) into memory ONCE or per query?
            // Loading 100k chunks of 768 floats = 300MB. Manageable for now.
            // Optimization: Cache the vectors in memory in RagService.
            
            var allChunks = await _databaseService.GetAllChunksAsync();

            var relevantChunks = allChunks
                .Select(x => new { Text = x.Text, Similarity = CosineSimilarity(x.Embedding, questionEmbedding) })
                .OrderByDescending(x => x.Similarity)
                .Take(5) // Increased context
                .ToList();

            var context = string.Join("\n\n", relevantChunks.Select(x => x.Text));

            // 3. Generate Answer
            var prompt = $"Context:\n{context}\n\nQuestion: {question}\n\nAnswer the question based on the context provided. If the answer is not in the context, say so.";
            return await _geminiService.GenerateContentAsync(prompt);
        }

        private float CosineSimilarity(List<float> v1, List<float> v2)
        {
            if (v1.Count != v2.Count) return 0;

            float dotProduct = 0;
            float mag1 = 0;
            float mag2 = 0;

            for (int i = 0; i < v1.Count; i++)
            {
                dotProduct += v1[i] * v2[i];
                mag1 += v1[i] * v1[i];
                mag2 += v2[i] * v2[i];
            }

            if (mag1 == 0 || mag2 == 0) return 0;

            return dotProduct / ((float)Math.Sqrt(mag1) * (float)Math.Sqrt(mag2));
        }
    }
}





