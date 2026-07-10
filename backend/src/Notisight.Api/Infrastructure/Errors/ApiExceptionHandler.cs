using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Notisight.Api.Infrastructure.Errors;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    private static readonly HashSet<string> SafeBadRequestMessages = new(StringComparer.Ordinal)
    {
        "A folder cannot be its own parent.",
        "A folder cannot be moved under one of its descendants.",
        "Uploaded PDF is empty.",
        "Only PDF files are supported.",
        "Uploaded audio file is empty.",
        "Only WAV, WEBM, M4A, and MP3 audio files are supported.",
        "Audio file is too large for transcription. Use a file smaller than 25 MB.",
        "Deepgram did not return an audio transcript.",
        "Uploaded image is empty.",
        "Only JPG, PNG, GIF, and WEBP images are supported.",
        "Bu e-posta adresi zaten kullanılıyor.",
        "Bu kullanıcı adı zaten kullanılıyor."
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Request failed for {Path}", httpContext.Request.Path);

        var statusCode = exception switch
        {
            ApiHttpException apiHttpException => apiHttpException.StatusCode,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException invalidOperationException
                when SafeBadRequestMessages.Contains(invalidOperationException.Message) =>
                    StatusCodes.Status400BadRequest,
            DbUpdateException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = GetTitle(statusCode),
                Detail = GetDetail(exception, statusCode)
            },
            Exception = exception
        });
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        StatusCodes.Status502BadGateway => "Bad Gateway",
        StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
        _ => "Server Error"
    };

    private string GetDetail(Exception exception, int statusCode)
    {
        if (environment.IsDevelopment())
        {
            return exception.Message;
        }

        if (exception is ApiHttpException)
        {
            return exception.Message;
        }

        return statusCode switch
        {
            StatusCodes.Status400BadRequest when SafeBadRequestMessages.Contains(exception.Message) =>
                exception.Message,
            StatusCodes.Status401Unauthorized =>
                "Oturum doğrulanamadı. Lütfen tekrar giriş yapın.",
            StatusCodes.Status404NotFound =>
                "İstenen kaynak bulunamadı.",
            StatusCodes.Status409Conflict =>
                "İstek mevcut kayıtlarla çakıştı.",
            StatusCodes.Status429TooManyRequests =>
                "Çok fazla istek gönderildi. Lütfen biraz sonra tekrar deneyin.",
            _ =>
                "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin."
        };
    }
}
