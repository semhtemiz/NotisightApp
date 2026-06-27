using Notisight.Api.Features.Ingestion.Services;

namespace Notisight.Api.Tests.Infrastructure;

public sealed class RecordingAudioTranscriptionService : IAudioTranscriptionService
{
    private readonly object _gate = new();
    private readonly List<string> _fileNames = [];

    public IReadOnlyList<string> FileNames
    {
        get
        {
            lock (_gate)
            {
                return _fileNames.ToList();
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _fileNames.Clear();
        }
    }

    public Task<string> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _fileNames.Add(fileName);
        }

        return Task.FromResult("Recorded transcript from fake audio service.");
    }
}
