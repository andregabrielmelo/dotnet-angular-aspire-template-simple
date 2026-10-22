using System.Text.Json;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;
using Ardalis.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AppTemplate.FunctionalTests.Errors;

/// <summary>
/// <see cref="ResultExtensions.ToProblem"/> on its own, for every result status, including ones
/// no current endpoint produces. Writes each response into a bare HttpContext; no Docker.
/// </summary>
public class ResultMappingTests
{
    private static async Task<(int Status, JsonElement Body)> ExecuteAsync(
        Microsoft.AspNetCore.Http.IResult httpResult
    )
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetailsConfigurations(NullLogger.Instance)
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        await httpResult.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        return (context.Response.StatusCode, body);
    }

    public static TheoryData<Result, int> StatusCodes() =>
        new()
        {
            { Result.Unauthorized(), 401 },
            { Result.Forbidden(), 403 },
            { Result.NotFound(), 404 },
            { Result.Conflict(), 409 },
            { Result.Unavailable(), 503 },
            { Result.Error(), 500 },
            { Result.CriticalError(), 500 },
        };

    [Theory]
    [MemberData(nameof(StatusCodes))]
    public async Task FailedResult_MapsToItsStatusCodeWithATraceId(Result result, int expected)
    {
        var (status, body) = await ExecuteAsync(result.ToProblem());

        Assert.Equal(expected, status);
        Assert.Equal(expected, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Invalid_ListsErrorsByCamelCaseField()
    {
        var result = Result.Invalid(
            new ValidationError("PhoneNumber", "Invalid phone number"),
            new ValidationError("PhoneNumber", "Too long"),
            new ValidationError("Name", "Required")
        );

        var (status, body) = await ExecuteAsync(result.ToProblem());

        Assert.Equal(400, status);
        var errors = body.GetProperty("errors");
        Assert.Equal(
            ["Invalid phone number", "Too long"],
            errors.GetProperty("phoneNumber").EnumerateArray().Select(e => e.GetString())
        );
        Assert.Equal("Required", errors.GetProperty("name")[0].GetString());
    }

    [Theory]
    [InlineData(ResultStatus.Error)]
    [InlineData(ResultStatus.CriticalError)]
    [InlineData(ResultStatus.Unavailable)]
    public async Task InternalFailures_NeverEchoTheirMessages(ResultStatus status)
    {
        const string Internal = "SELECT * FROM users failed: password=hunter2";
        var result = status switch
        {
            ResultStatus.Error => Result.Error(Internal),
            ResultStatus.CriticalError => Result.CriticalError(Internal),
            _ => Result.Unavailable(Internal),
        };

        var (_, body) = await ExecuteAsync(result.ToProblem());

        Assert.DoesNotContain("hunter2", body.GetRawText());
    }

    [Fact]
    public async Task ConflictAndNotFound_PassOnTheUseCaseMessage()
    {
        var (_, conflict) = await ExecuteAsync(Result.Conflict("Name already taken").ToProblem());
        var (_, notFound) = await ExecuteAsync(Result.NotFound("No such job").ToProblem());

        Assert.Equal("Name already taken", conflict.GetProperty("detail").GetString());
        Assert.Equal("No such job", notFound.GetProperty("detail").GetString());
    }

    [Fact]
    public void SuccessfulResult_HasNoProblem() =>
        Assert.Throws<InvalidOperationException>(() => Result.Success().ToProblem());
}
