using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.AI.Services;
using Notisight.Api.Features.Notes.Contracts;
using Notisight.Api.Infrastructure.Auth;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.Notes;

[ApiController]
[Route("notes")]
[Authorize]
public sealed class NotesController(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IVectorSyncQueue vectorSyncQueue) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NoteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NoteResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();

        var query = dbContext.Notes
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.NoteTags)
            .ThenInclude(x => x.Tag)
            .AsSplitQuery();

        var notes = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? (await query.ToListAsync(cancellationToken))
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ToList()
            : await query
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ToListAsync(cancellationToken);

        return Ok(notes
            .Select(ToResponse)
            .ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NoteResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var note = await FindOwnedNoteAsync(id, cancellationToken);
        return Ok(ToResponse(note));
    }

    [HttpPost]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<NoteResponse>> Create(
        [FromBody] NoteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        await EnsureFolderOwnershipAsync(userId, request.FolderId, cancellationToken);

        var tagIds = request.TagIds?.Distinct().ToArray() ?? [];
        var tags = await dbContext.Tags
            .Where(x => x.UserId == userId && tagIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (tags.Count != tagIds.Length)
        {
            throw new KeyNotFoundException("One or more tags were not found.");
        }

        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FolderId = request.FolderId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            VectorSyncStatus = VectorSyncStatus.Pending
        };

        note.NoteTags = tags.Select(tag => new NoteTag
        {
            NoteId = note.Id,
            TagId = tag.Id,
            Tag = tag,
            Note = note
        }).ToList();

        dbContext.Notes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);
        vectorSyncQueue.EnqueueUpsert(note.Id);

        return Created($"/notes/{note.Id}", ToResponse(note));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NoteResponse>> Update(
        Guid id,
        [FromBody] NoteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var note = await FindOwnedNoteAsync(id, cancellationToken);
        await EnsureFolderOwnershipAsync(userId, request.FolderId, cancellationToken);

        var requestedTagIds = request.TagIds?.Distinct().ToArray() ?? [];
        var tags = await dbContext.Tags
            .Where(x => x.UserId == userId && requestedTagIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (tags.Count != requestedTagIds.Length)
        {
            throw new KeyNotFoundException("One or more tags were not found.");
        }

        note.Title = request.Title.Trim();
        note.Content = request.Content.Trim();
        note.FolderId = request.FolderId;
        note.VectorSyncStatus = VectorSyncStatus.Pending;
        note.VectorSyncError = null;
        note.VectorSyncedAtUtc = null;
        note.NoteTags.Clear();
        foreach (var tag in tags)
        {
            note.NoteTags.Add(new NoteTag
            {
                NoteId = note.Id,
                TagId = tag.Id,
                Note = note,
                Tag = tag
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        vectorSyncQueue.EnqueueUpsert(note.Id);
        return Ok(ToResponse(note));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id, 
        [FromServices] Notisight.Api.Features.Ingestion.Contracts.IFileStorageService fileStorageService,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var note = await dbContext.Notes
            .Include(x => x.NoteAttachments)
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (note is null)
        {
            return NoContent();
        }

        if (!string.IsNullOrEmpty(note.FileUrl))
        {
            try { 
                Console.WriteLine($"Deleting main file: {note.FileUrl}");
                await fileStorageService.DeleteFileAsync(note.FileUrl, cancellationToken); 
                Console.WriteLine($"Successfully deleted main file from R2.");
            } catch (Exception ex) { 
                Console.WriteLine($"Error deleting main file: {ex.Message}"); 
            }
        }

        foreach (var attachment in note.NoteAttachments)
        {
            if (!string.IsNullOrEmpty(attachment.FileUrl))
            {
                try { 
                    Console.WriteLine($"Deleting attachment: {attachment.FileUrl}");
                    await fileStorageService.DeleteFileAsync(attachment.FileUrl, cancellationToken); 
                    Console.WriteLine($"Successfully deleted attachment from R2.");
                } catch (Exception ex) { 
                    Console.WriteLine($"Error deleting attachment: {ex.Message}"); 
                }
            }
        }

        vectorSyncQueue.EnqueueDelete(note.Id);
        dbContext.Notes.Remove(note);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFile(
        Guid id, 
        [FromServices] Notisight.Api.Features.Ingestion.Contracts.IFileStorageService fileStorageService,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var note = await dbContext.Notes.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);
        if (note == null || string.IsNullOrEmpty(note.FileUrl)) return NotFound();

        try
        {
            var (stream, contentType) = await fileStorageService.GetFileStreamAsync(note.FileUrl, cancellationToken);
            if (stream == null) return NotFound();

            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
    }

    [HttpGet("attachments/{id:guid}/file")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachmentFile(
        Guid id, 
        [FromServices] Notisight.Api.Features.Ingestion.Contracts.IFileStorageService fileStorageService,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.NoteAttachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (attachment == null || string.IsNullOrEmpty(attachment.FileUrl)) return NotFound();

        try
        {
            var (stream, contentType) = await fileStorageService.GetFileStreamAsync(attachment.FileUrl, cancellationToken);
            if (stream == null) return NotFound();

            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
    }

    private async Task<Note> FindOwnedNoteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var note = await dbContext.Notes
            .Include(x => x.NoteTags)
            .ThenInclude(x => x.Tag)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        return note ?? throw new KeyNotFoundException("Note was not found.");
    }

    private async Task EnsureFolderOwnershipAsync(Guid userId, Guid? folderId, CancellationToken cancellationToken)
    {
        if (!folderId.HasValue)
        {
            return;
        }

        var exists = await dbContext.Folders.AnyAsync(
            x => x.Id == folderId.Value && x.UserId == userId,
            cancellationToken);

        if (!exists)
        {
            throw new KeyNotFoundException("Folder was not found.");
        }
    }

    private static NoteResponse ToResponse(Note note)
    {
        var tags = note.NoteTags
            .Select(x => new TagSummaryResponse(x.TagId, x.Tag.Name))
            .OrderBy(x => x.Name)
            .ToList();

        return new NoteResponse(
            note.Id,
            note.Title,
            note.Content,
            note.FolderId,
            note.CreatedAtUtc,
            note.UpdatedAtUtc,
            tags,
            note.FileUrl,
            note.FileType,
            note.VectorSyncStatus,
            note.VectorSyncError,
            note.VectorSyncedAtUtc);
    }
}
