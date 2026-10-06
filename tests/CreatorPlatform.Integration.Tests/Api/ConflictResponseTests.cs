using System.Text.Json;
using CreatorPlatform.Api.Middlewares;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>The 409 body carries the conflict's code, plus <c>details</c> only when the conflict has some.</summary>
public sealed class ConflictResponseTests
{
    private static async Task<JsonElement> RespondAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(_ => throw exception, NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement.Clone();
    }

    [Fact]
    public async Task AConflictWithDetails_ReturnsThemCamelCased()
    {
        var body = await RespondAsync(new ConflictException("Over the limit.", "PLAN_LIMIT_REACHED", new { Used = 3, Limit = 1 }));

        Assert.Equal("PLAN_LIMIT_REACHED", body.GetProperty("code").GetString());
        Assert.Equal(3, body.GetProperty("details").GetProperty("used").GetInt32());
        Assert.Equal(1, body.GetProperty("details").GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task AConflictWithoutDetails_OmitsTheField()
    {
        var body = await RespondAsync(new ConflictException("Taken.", "PAYOUTS_NOT_READY"));

        Assert.Equal("PAYOUTS_NOT_READY", body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("details", out _));
    }
}
