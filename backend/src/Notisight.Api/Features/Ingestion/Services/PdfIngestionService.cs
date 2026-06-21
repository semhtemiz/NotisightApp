using UglyToad.PdfPig;

namespace Notisight.Api.Features.Ingestion.Services;

public sealed class PdfIngestionService : IPdfIngestionService
{
    public Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(pdfStream);
        var pages = document.GetPages().Select(page => page.Text);
        var text = string.Join(Environment.NewLine + Environment.NewLine, pages);
        return Task.FromResult(text.Trim());
    }

    public Task<IReadOnlyList<(int PageNumber, string Text)>> ExtractPageTextsAsync(Stream pdfStream, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(pdfStream);
        var pages = document.GetPages()
            .Select(page => (page.Number, page.Text?.Trim() ?? string.Empty))
            .Where(p => !string.IsNullOrWhiteSpace(p.Item2))
            .ToList();
        return Task.FromResult<IReadOnlyList<(int PageNumber, string Text)>>(pages);
    }
}
