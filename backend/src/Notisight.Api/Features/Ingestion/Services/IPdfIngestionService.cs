namespace Notisight.Api.Features.Ingestion.Services;

public interface IPdfIngestionService
{
    Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken);
    Task<IReadOnlyList<(int PageNumber, string Text)>> ExtractPageTextsAsync(Stream pdfStream, CancellationToken cancellationToken);
}
