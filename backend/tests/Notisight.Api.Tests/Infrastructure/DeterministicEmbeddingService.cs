using Notisight.Api.Features.AI.Services;

namespace Notisight.Api.Tests.Infrastructure;

public sealed class DeterministicEmbeddingService : IEmbeddingService
{
    public Task<IReadOnlyList<float>> EmbedDocumentAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<float>>(CreateVector(text));

    public Task<IReadOnlyList<float>> EmbedQueryAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<float>>(CreateVector(text));

    private static float[] CreateVector(string text)
    {
        var seed = text.Aggregate(17, (current, character) => (current * 31) + character);
        var vector = new float[768];

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = ((seed + index) % 97) / 97f;
        }

        return vector;
    }
}
