using Notisight.Api.Domain.Entities;

namespace Notisight.Api.Features.AI.Services;

public interface INoteVectorSyncService
{
    Task UpsertNoteAsync(Note note, CancellationToken cancellationToken);
    Task DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken);
}
