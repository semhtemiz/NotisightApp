using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.AI.Services;
using Notisight.Api.Features.Ingestion.Contracts;
using Notisight.Api.Features.Ingestion.Services;
using Notisight.Api.Infrastructure.Auth;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.Ingestion;

[ApiController]
[Route("notes")]
[Authorize]
public sealed class NotesUploadController(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IPdfIngestionService pdfIngestionService,
    IAudioTranscriptionService audioTranscriptionService,
    IVectorSyncQueue vectorSyncQueue,
    IFileStorageService fileStorageService,
    ILogger<NotesUploadController> logger) : ControllerBase
{
    [HttpPost("upload-pdf")]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType(typeof(UploadedNoteResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<UploadedNoteResponse>> UploadPdf(
        IFormFile file,
        [FromForm] Guid? folderId,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Uploaded PDF is empty.");
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only PDF files are supported.");
        }

        var userId = currentUser.GetRequiredUserId();
        await EnsureFolderOwnershipAsync(userId, folderId, cancellationToken);

        var noteId = Guid.NewGuid();

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        using var extractStream = new MemoryStream(fileBytes);
        var content = await pdfIngestionService.ExtractTextAsync(extractStream, cancellationToken);

        using var uploadStream = new MemoryStream(fileBytes);
        var fileUrl = await fileStorageService.UploadFileAsync(uploadStream, file.FileName, "application/pdf", cancellationToken);
        
        var note = new Note
        {
            Id = noteId,
            UserId = userId,
            FolderId = folderId,
            Title = Path.GetFileNameWithoutExtension(file.FileName),
            Content = content,
            FileUrl = fileUrl,
            FileType = "pdf",
            VectorSyncStatus = VectorSyncStatus.Pending
        };

        dbContext.Notes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);
        vectorSyncQueue.EnqueueUpsert(note.Id);

        return Created(
            $"/notes/{note.Id}",
            new UploadedNoteResponse(
                note.Id,
                note.Title,
                "pdf",
                note.Content.Length,
                fileUrl,
                "pdf",
                note.VectorSyncStatus,
                note.VectorSyncError,
                note.VectorSyncedAtUtc));
    }

    [HttpPost("upload-audio")]
    [RequestSizeLimit(25_000_000)]
    [ProducesResponseType(typeof(UploadedNoteResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<UploadedNoteResponse>> UploadAudio(
        IFormFile file,
        [FromForm] Guid? folderId,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Uploaded audio file is empty.");
        }

        var extension = Path.GetExtension(file.FileName);
        var allowed = new[] { ".wav", ".webm", ".m4a", ".mp3" };
        if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only WAV, WEBM, M4A, and MP3 audio files are supported.");
        }

        var userId = currentUser.GetRequiredUserId();
        await EnsureFolderOwnershipAsync(userId, folderId, cancellationToken);

        var noteId = Guid.NewGuid();

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        using var transcribeStream = new MemoryStream(fileBytes);
        var transcript = await TryTranscribeAudioAsync(transcribeStream, file.FileName, cancellationToken);

        using var uploadStream = new MemoryStream(fileBytes);
        var fileUrl = await fileStorageService.UploadFileAsync(uploadStream, file.FileName, file.ContentType, cancellationToken);

        var note = new Note
        {
            Id = noteId,
            UserId = userId,
            FolderId = folderId,
            Title = Path.GetFileNameWithoutExtension(file.FileName),
            Content = transcript,
            FileUrl = fileUrl,
            FileType = "audio",
            VectorSyncStatus = VectorSyncStatus.Pending
        };

        dbContext.Notes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);
        vectorSyncQueue.EnqueueUpsert(note.Id);

        return Created(
            $"/notes/{note.Id}",
            new UploadedNoteResponse(
                note.Id,
                note.Title,
                "audio",
                note.Content.Length,
                fileUrl,
                "audio",
                note.VectorSyncStatus,
                note.VectorSyncError,
                note.VectorSyncedAtUtc));
    }

    [HttpPost("{noteId:guid}/attachments")]
    [RequestSizeLimit(15_000_000)]
    [ProducesResponseType(typeof(NoteAttachmentResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<NoteAttachmentResponse>> UploadImage(
        Guid noteId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Uploaded image is empty.");
        }

        var extension = Path.GetExtension(file.FileName);
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only JPG, PNG, GIF, and WEBP images are supported.");
        }

        var userId = currentUser.GetRequiredUserId();
        
        var note = await dbContext.Notes
            .SingleOrDefaultAsync(x => x.Id == noteId && x.UserId == userId, cancellationToken);
            
        if (note == null)
        {
            throw new KeyNotFoundException("Note was not found.");
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        using var uploadStream = new MemoryStream(fileBytes);
        var fileUrl = await fileStorageService.UploadFileAsync(uploadStream, file.FileName, file.ContentType, cancellationToken);

        var attachment = new NoteAttachment
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileUrl = fileUrl,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.NoteAttachments.Add(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/notes/attachments/{attachment.Id}/file", 
            new NoteAttachmentResponse(attachment.Id, noteId, attachment.FileName, attachment.ContentType, $"/notes/attachments/{attachment.Id}/file"));
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

    private async Task<string> TryTranscribeAudioAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await audioTranscriptionService.TranscribeAsync(audioStream, fileName, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Audio transcription failed for {FileName}; saving the audio note without transcript.",
                fileName);

            return $"""
                <p>Ses kaydı kaydedildi ancak transkripsiyon tamamlanamadı.</p>
                <p><strong>Hata:</strong> {System.Net.WebUtility.HtmlEncode(exception.Message)}</p>
                """;
        }
    }
}
