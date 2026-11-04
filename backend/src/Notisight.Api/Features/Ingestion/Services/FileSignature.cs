namespace Notisight.Api.Features.Ingestion.Services;

/// <summary>
/// Content types are derived from validated extensions rather than the client's declared
/// MIME type, and file signatures are checked before anything is stored.
/// </summary>
public static class FileSignature
{
    private static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".wav"] = "audio/wav",
        [".webm"] = "audio/webm",
        [".m4a"] = "audio/mp4",
        [".mp3"] = "audio/mpeg"
    };

    private static readonly byte[] PdfMagic = [0x25, 0x50, 0x44, 0x46];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] GifMagic = [0x47, 0x49, 0x46, 0x38];
    private static readonly byte[] RiffMagic = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpTag = [0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] WaveTag = [0x57, 0x41, 0x56, 0x45];
    private static readonly byte[] MatroskaMagic = [0x1A, 0x45, 0xDF, 0xA3];
    private static readonly byte[] FtypTag = [0x66, 0x74, 0x79, 0x70];
    private static readonly byte[] Id3Magic = [0x49, 0x44, 0x33];

    private const int MaxObjectNameLength = 120;

    public static bool HasAllowedExtension(string fileName) =>
        AllowedContentTypes.ContainsKey(Path.GetExtension(SafeFileName(fileName)));

    public static string GetContentType(string fileName, string fallback = "application/octet-stream") =>
        AllowedContentTypes.TryGetValue(Path.GetExtension(SafeFileName(fileName)), out var contentType)
            ? contentType
            : fallback;

    /// <summary>
    /// Stored objects predate the server-derived content types, so anything that is not on
    /// the allowlist is served as a download instead of being rendered by the browser.
    /// </summary>
    public static string ToSafeContentType(string? declaredContentType) =>
        declaredContentType is not null && AllowedContentTypes.ContainsValue(declaredContentType)
            ? declaredContentType
            : "application/octet-stream";

    public static bool MatchesExtension(ReadOnlySpan<byte> bytes, string fileName)
    {
        var extension = Path.GetExtension(SafeFileName(fileName));

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => StartsWith(bytes, PdfMagic),
            ".png" => StartsWith(bytes, PngMagic),
            ".jpg" or ".jpeg" => StartsWith(bytes, JpegMagic),
            ".gif" => StartsWith(bytes, GifMagic),
            ".webp" => StartsWith(bytes, RiffMagic) && TagEquals(bytes, 8, WebpTag),
            ".wav" => StartsWith(bytes, RiffMagic) && TagEquals(bytes, 8, WaveTag),
            ".webm" => StartsWith(bytes, MatroskaMagic),
            ".m4a" => TagEquals(bytes, 4, FtypTag),
            ".mp3" => StartsWith(bytes, Id3Magic) ||
                (bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0),
            _ => false
        };
    }

    /// <summary>
    /// Object keys must not carry traversal sequences, control characters or CRLF from the
    /// uploaded name.
    /// </summary>
    public static string ToObjectName(string fileName)
    {
        var name = SafeFileName(fileName);
        var buffer = new System.Text.StringBuilder(name.Length);

        foreach (var character in name)
        {
            buffer.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '_');
        }

        var slug = buffer.ToString().Trim('.', '_');

        if (slug.Length == 0)
        {
            return "file";
        }

        return slug.Length > MaxObjectNameLength ? slug[..MaxObjectNameLength] : slug;
    }

    private static string SafeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "file";
        }

        var name = Path.GetFileName(fileName.Replace('\\', '/'));

        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> magic) =>
        bytes.Length >= magic.Length && bytes[..magic.Length].SequenceEqual(magic);

    private static bool TagEquals(ReadOnlySpan<byte> bytes, int offset, ReadOnlySpan<byte> tag) =>
        bytes.Length >= offset + tag.Length && bytes.Slice(offset, tag.Length).SequenceEqual(tag);
}
