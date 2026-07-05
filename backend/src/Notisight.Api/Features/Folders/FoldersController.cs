using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.AI.Services;
using Notisight.Api.Features.Folders.Contracts;
using Notisight.Api.Infrastructure.Auth;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.Folders;

[ApiController]
[Route("folders")]
[Authorize]
public sealed class FoldersController(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IVectorSyncQueue vectorSyncQueue) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FolderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FolderResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();

        var folders = await dbContext.Folders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new FolderResponse(x.Id, x.Name, x.ParentFolderId, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(folders);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<FolderResponse>> Create(
        [FromBody] FolderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();

        await EnsureParentBelongsToUserAsync(userId, request.ParentFolderId, cancellationToken);

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            ParentFolderId = request.ParentFolderId
        };

        dbContext.Folders.Add(folder);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/folders/{folder.Id}", ToResponse(folder));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FolderResponse>> Update(
        Guid id,
        [FromBody] FolderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var folder = await dbContext.Folders.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);

        if (folder is null)
        {
            throw new KeyNotFoundException("Folder was not found.");
        }

        await EnsureValidParentAsync(userId, id, request.ParentFolderId, cancellationToken);

        folder.Name = request.Name.Trim();
        folder.ParentFolderId = request.ParentFolderId;

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnqueueFolderTreeNotesAsync(userId, folder.Id, cancellationToken);

        return Ok(ToResponse(folder));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var folder = await dbContext.Folders.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);

        if (folder is null)
        {
            return NoContent();
        }

        var affectedFolderIds = await GetFolderTreeIdsAsync(userId, id, cancellationToken);
        var noteIdsToReindex = await dbContext.Notes
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.FolderId.HasValue && affectedFolderIds.Contains(x.FolderId.Value))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var notesInFolder = await dbContext.Notes
            .Where(x => x.FolderId == id && x.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var note in notesInFolder)
        {
            note.FolderId = null;
        }

        var childFolders = await dbContext.Folders
            .Where(x => x.ParentFolderId == id && x.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var childFolder in childFolders)
        {
            childFolder.ParentFolderId = null;
        }

        dbContext.Folders.Remove(folder);
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var noteId in noteIdsToReindex)
        {
            vectorSyncQueue.EnqueueUpsert(noteId);
        }

        return NoContent();
    }

    private async Task EnsureParentBelongsToUserAsync(
        Guid userId,
        Guid? parentFolderId,
        CancellationToken cancellationToken)
    {
        if (!parentFolderId.HasValue)
        {
            return;
        }

        var parentExists = await dbContext.Folders.AnyAsync(
            x => x.Id == parentFolderId.Value && x.UserId == userId,
            cancellationToken);

        if (!parentExists)
        {
            throw new KeyNotFoundException("Parent folder was not found.");
        }
    }

    private async Task EnsureValidParentAsync(
        Guid userId,
        Guid folderId,
        Guid? parentFolderId,
        CancellationToken cancellationToken)
    {
        if (!parentFolderId.HasValue)
        {
            return;
        }

        if (parentFolderId.Value == folderId)
        {
            throw new InvalidOperationException("A folder cannot be its own parent.");
        }

        var folders = await dbContext.Folders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Id, x.ParentFolderId })
            .ToListAsync(cancellationToken);

        if (!folders.Any(x => x.Id == parentFolderId.Value))
        {
            throw new KeyNotFoundException("Parent folder was not found.");
        }

        var visited = new HashSet<Guid> { folderId };
        var currentParentId = parentFolderId.Value;

        while (true)
        {
            if (!visited.Add(currentParentId))
            {
                throw new InvalidOperationException("A folder cannot be moved under one of its descendants.");
            }

            var nextParentId = folders
                .FirstOrDefault(x => x.Id == currentParentId)
                ?.ParentFolderId;

            if (!nextParentId.HasValue)
            {
                return;
            }

            currentParentId = nextParentId.Value;
        }
    }

    private async Task EnqueueFolderTreeNotesAsync(
        Guid userId,
        Guid folderId,
        CancellationToken cancellationToken)
    {
        var affectedFolderIds = await GetFolderTreeIdsAsync(userId, folderId, cancellationToken);

        var noteIds = await dbContext.Notes
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.FolderId.HasValue && affectedFolderIds.Contains(x.FolderId.Value))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var noteId in noteIds)
        {
            vectorSyncQueue.EnqueueUpsert(noteId);
        }
    }

    private async Task<HashSet<Guid>> GetFolderTreeIdsAsync(
        Guid userId,
        Guid folderId,
        CancellationToken cancellationToken)
    {
        var folders = await dbContext.Folders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Id, x.ParentFolderId })
            .ToListAsync(cancellationToken);

        var affectedFolderIds = new HashSet<Guid> { folderId };
        var added = true;
        while (added)
        {
            added = false;
            foreach (var candidate in folders)
            {
                if (candidate.ParentFolderId.HasValue &&
                    affectedFolderIds.Contains(candidate.ParentFolderId.Value) &&
                    affectedFolderIds.Add(candidate.Id))
                {
                    added = true;
                }
            }
        }

        return affectedFolderIds;
    }

    private static FolderResponse ToResponse(Folder folder) =>
        new(folder.Id, folder.Name, folder.ParentFolderId, folder.CreatedAtUtc, folder.UpdatedAtUtc);
}
