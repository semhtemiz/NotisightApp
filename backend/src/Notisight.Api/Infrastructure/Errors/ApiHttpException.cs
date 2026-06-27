namespace Notisight.Api.Infrastructure.Errors;

public sealed class ApiHttpException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
