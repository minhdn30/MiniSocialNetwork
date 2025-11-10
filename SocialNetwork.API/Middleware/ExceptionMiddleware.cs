using SocialNetwork.Application.Exceptions;
using System.Text.Json;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

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
                        _logger.LogError(ex, "Unhandled exception");
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Internal server error." }));
                        break;
                }
            }
        }
    }
}
