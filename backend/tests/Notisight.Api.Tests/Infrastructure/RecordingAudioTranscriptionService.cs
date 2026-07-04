using Notisight.Api.Features.Ingestion.Services;

namespace Notisight.Api.Tests.Infrastructure;

public sealed class RecordingAudioTranscriptionService : IAudioTranscriptionService
{
    private readonly object _gate = new();
    private readonly List<string> _fileNames = [];
    private Exception? _failure;

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
            _failure = null;
        }
    }

    public void FailWith(Exception exception)
    {
        lock (_gate)
        {
            _failure = exception;
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
            if (_failure is not null)
            {
                throw _failure;
            }
        }

        return Task.FromResult("Recorded transcript from fake audio service.");
    }
}
