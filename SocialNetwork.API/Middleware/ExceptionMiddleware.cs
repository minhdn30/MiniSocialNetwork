using Npgsql;
using SocialNetwork.Domain.Exceptions;
using System.Text.Json;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.API.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";

                switch (ex)
                {
                    case BadRequestException brEx:
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = brEx.Message }));
                        break;

                    case NotFoundException nfEx:
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = nfEx.Message }));
                        break;

                    case UnauthorizedException uaEx:
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = uaEx.Message }));
                        break;

                    case ForbiddenException fEx:
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = fEx.Message }));
                        break;

                    case InternalServerException isEx:
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = isEx.Message }));
                        break;

                    default:
                        if (IsTransientDatabaseFailure(ex))
                        {
                            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                            await context.Response.WriteAsync(JsonSerializer.Serialize(new
                            {
                                message = "Database is temporarily unavailable. Please try again."
                            }));
                            break;
                        }

                        _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
                        if (ex.InnerException != null)
                        {
                            _logger.LogError(ex.InnerException, "Inner exception: {Message}", ex.InnerException.Message);
                        }
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Internal server error.", details = ex.Message }));
                        break;
                }
            }
        }

        private static bool IsTransientDatabaseFailure(Exception ex)
        {
            if (ex is InvalidOperationException &&
                ex.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var cursor = ex;
            while (cursor != null)
            {
                if (cursor is NpgsqlException)
                {
                    return true;
                }

                if (cursor is TimeoutException ||
                    cursor is IOException ||
                    cursor is EndOfStreamException)
                {
                    return true;
                }

                cursor = cursor.InnerException!;
            }

            return false;
        }
    }
}
