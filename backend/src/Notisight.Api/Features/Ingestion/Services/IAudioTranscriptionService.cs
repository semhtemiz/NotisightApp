namespace Notisight.Api.Features.Ingestion.Services;

public interface IAudioTranscriptionService
{
    Task<string> TranscribeAsync(Stream audioStream, string fileName, CancellationToken cancellationToken);
}
