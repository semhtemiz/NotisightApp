namespace Notisight.Api.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<Folder> Folders { get; set; } = [];
    public ICollection<Note> Notes { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
