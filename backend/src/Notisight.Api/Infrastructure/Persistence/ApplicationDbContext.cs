using Microsoft.EntityFrameworkCore;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.Settings.Models;

namespace Notisight.Api.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<NoteTag> NoteTags => Set<NoteTag>();
    public DbSet<NoteAttachment> NoteAttachments => Set<NoteAttachment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AiProviderSettings> AiProviderSettings => Set<AiProviderSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Folder>(entity =>
        {
            entity.ToTable("folders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.HasOne(x => x.User)
                .WithMany(x => x.Folders)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ParentFolder)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.UserId, x.ParentFolderId });
        });

        modelBuilder.Entity<Note>(entity =>
        {
            entity.ToTable("notes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Content);
            entity.Property(x => x.DurationSeconds);
            entity.Property(x => x.VectorSyncStatus)
                .HasMaxLength(32)
                .HasDefaultValue(VectorSyncStatus.Pending)
                .IsRequired();
            entity.Property(x => x.VectorSyncError).HasMaxLength(1000);
            entity.HasIndex(x => x.VectorSyncStatus);
            entity.HasIndex(x => new { x.UserId, x.UpdatedAtUtc });
            entity.HasIndex(x => new { x.UserId, x.FolderId });
            entity.HasOne(x => x.User)
                .WithMany(x => x.Notes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Folder)
                .WithMany(x => x.Notes)
                .HasForeignKey(x => x.FolderId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<NoteAttachment>(entity =>
        {
            entity.ToTable("note_attachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FileUrl).HasMaxLength(1000).IsRequired();
            entity.HasOne(x => x.Note)
                .WithMany(x => x.NoteAttachments)
                .HasForeignKey(x => x.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.NoteId);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("tags");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.HasOne(x => x.User)
                .WithMany(x => x.Tags)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<NoteTag>(entity =>
        {
            entity.ToTable("note_tags");
            entity.HasKey(x => new { x.NoteId, x.TagId });
            entity.HasOne(x => x.Note)
                .WithMany(x => x.NoteTags)
                .HasForeignKey(x => x.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tag)
                .WithMany(x => x.NoteTags)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.TagId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Token).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.ToTable("chat_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.MetadataJson);
            entity.Property(x => x.Mode).HasMaxLength(20);
            entity.HasOne(x => x.ChatSession)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.ChatSessionId);
        });

        modelBuilder.Entity<AiProviderSettings>(entity =>
        {
            entity.ToTable("ai_provider_settings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProviderType).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(e => new { e.UserId, e.ProviderType }).IsUnique();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditing();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditing();
        return base.SaveChanges();
    }

    private void ApplyAuditing()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added)
            {
                switch (entry.Entity)
                {
                    case User user:
                        user.CreatedAtUtc = now;
                        user.UpdatedAtUtc = now;
                        break;
                    case Folder folder:
                        folder.CreatedAtUtc = now;
                        folder.UpdatedAtUtc = now;
                        break;
                    case Note note:
                        note.CreatedAtUtc = now;
                        note.UpdatedAtUtc = now;
                        break;
                    case Tag tag:
                        tag.CreatedAtUtc = now;
                        break;
                    case RefreshToken refreshToken:
                        refreshToken.CreatedAtUtc = refreshToken.CreatedAtUtc == default ? now : refreshToken.CreatedAtUtc;
                        break;
                    case ChatSession chatSession:
                        chatSession.CreatedAtUtc = now;
                        chatSession.UpdatedAtUtc = now;
                        break;
                    case ChatMessage chatMessage:
                        chatMessage.CreatedAtUtc = now;
                        break;
                    case AiProviderSettings aiSettings:
                        aiSettings.CreatedAtUtc = now;
                        aiSettings.UpdatedAtUtc = now;
                        break;
                }
            }

            if (entry.State is EntityState.Modified)
            {
                switch (entry.Entity)
                {
                    case User user:
                        user.UpdatedAtUtc = now;
                        break;
                    case Folder folder:
                        folder.UpdatedAtUtc = now;
                        break;
                    case Note note:
                        note.UpdatedAtUtc = now;
                        break;
                    case ChatSession chatSession:
                        chatSession.UpdatedAtUtc = now;
                        break;
                    case AiProviderSettings aiSettings:
                        aiSettings.UpdatedAtUtc = now;
                        break;
                }
            }
        }
    }
}
