using Cyber.Client.Api;
using Cyber.Client.Filters;

namespace Cyber.Client.Pipeline;

public sealed class IngestionPipeline
{
    private readonly ContentFilterRegistry _filters;
    private readonly NotebookBatchClient _batchClient;

    public IngestionPipeline(ContentFilterRegistry filters, NotebookBatchClient batchClient)
    {
        _filters = filters;
        _batchClient = batchClient;
    }

    public async Task<IngestionResult> ProcessFilesAsync(
        IReadOnlyList<string> filePaths,
        IProgress<IngestionProgress>? progress = null,
        CancellationToken ct = default)
    {
        var details = new List<FileResult>();
        var entries = new List<BatchEntryRequest>();
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;

        // Phase 1: Filter all files
        foreach (var filePath in filePaths)
        {
            ct.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(filePath);

            progress?.Report(new IngestionProgress
            {
                FileName = fileName,
                Stage = IngestionStage.Detecting,
                Message = "Detecting file type..."
            });

            var filter = _filters.GetFilter(fileName);
            if (filter == null)
            {
                progress?.Report(new IngestionProgress
                {
                    FileName = fileName,
                    Stage = IngestionStage.Skipped,
                    Message = $"Unsupported file type: {Path.GetExtension(fileName)}"
                });
                details.Add(new FileResult { FileName = fileName, Success = false, Error = "Unsupported file type" });
                skipped++;
                continue;
            }

            progress?.Report(new IngestionProgress
            {
                FileName = fileName,
                Stage = IngestionStage.Filtering,
                Message = "Extracting content..."
            });

            try
            {
                await using var stream = File.OpenRead(filePath);
                var result = await filter.FilterAsync(stream, fileName, ct);

                if (string.IsNullOrWhiteSpace(result.Text))
                {
                    progress?.Report(new IngestionProgress
                    {
                        FileName = fileName,
                        Stage = IngestionStage.Skipped,
                        Message = "File produced no content after filtering"
                    });
                    details.Add(new FileResult { FileName = fileName, Success = false, Error = "Empty content" });
                    skipped++;
                    continue;
                }

                entries.Add(new BatchEntryRequest
                {
                    Content = result.Text,
                    ContentType = result.ContentType,
                    Topic = Path.GetFileNameWithoutExtension(fileName)
                });

                details.Add(new FileResult { FileName = fileName, Success = true });
                succeeded++;
            }
            catch (Exception ex)
            {
                progress?.Report(new IngestionProgress
                {
                    FileName = fileName,
                    Stage = IngestionStage.Failed,
                    Error = ex.Message
                });
                details.Add(new FileResult { FileName = fileName, Success = false, Error = ex.Message });
                failed++;
            }
        }

        // Phase 2: Upload
        if (entries.Count > 0)
        {
            progress?.Report(new IngestionProgress
            {
                FileName = "",
                Stage = IngestionStage.Uploading,
                Message = $"Uploading {entries.Count} entries..."
            });

            try
            {
                await _batchClient.BatchWriteAsync(entries, new Progress<string>(msg =>
                {
                    progress?.Report(new IngestionProgress
                    {
                        FileName = "",
                        Stage = IngestionStage.Uploading,
                        Message = msg
                    });
                }), ct);

                progress?.Report(new IngestionProgress
                {
                    FileName = "",
                    Stage = IngestionStage.Completed,
                    Message = $"Successfully uploaded {entries.Count} entries"
                });
            }
            catch (Exception ex)
            {
                progress?.Report(new IngestionProgress
                {
                    FileName = "",
                    Stage = IngestionStage.Failed,
                    Error = $"Upload failed: {ex.Message}"
                });

                // Mark all previously succeeded as failed
                for (var i = 0; i < details.Count; i++)
                {
                    if (details[i].Success)
                    {
                        details[i] = details[i] with { Success = false, Error = $"Upload failed: {ex.Message}" };
                        succeeded--;
                        failed++;
                    }
                }
            }
        }

        return new IngestionResult
        {
            Succeeded = succeeded,
            Failed = failed,
            Skipped = skipped,
            Details = details
        };
    }
}
