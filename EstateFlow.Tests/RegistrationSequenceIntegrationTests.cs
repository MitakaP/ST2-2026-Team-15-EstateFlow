using System.Net;
using System.Text.RegularExpressions;
using EstateFlow.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EstateFlow.Tests;

public sealed class RegistrationSequenceIntegrationTests
{
    [Fact]
    public async Task ConsecutiveRegistrations_WorkEvenIfLegacyConnectionLostBuiltInLower()
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var nickname = "Sequence" + Guid.NewGuid().ToString("N")[..8];
        // Emulate the stale pooled connection left by older versions of Database.Search.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            using var connection = new SqliteConnection(context.Database.GetConnectionString());
            await connection.OpenAsync();
            connection.CreateFunction("LOWER", (string? text) => text?.ToLowerInvariant());
        }
        using var first = await RegisterAsync(client, nickname);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        await LogoutAsync(client);
        using var second = await RegisterAsync(client, nickname + "Next");
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        await LogoutAsync(client);
        using var duplicate = await RegisterAsync(client, nickname.ToUpperInvariant());
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("Nickname", await duplicate.Content.ReadAsStringAsync());
        using var verification = factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.UserProfiles.CountAsync());
    }

    [Fact]
    public async Task EditNickname_RejectsDifferentCaseOfAnotherUsersNickname()
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var firstNickname = "First" + Guid.NewGuid().ToString("N")[..8];
        var secondNickname = "Second" + Guid.NewGuid().ToString("N")[..8];
        using var first = await RegisterAsync(client, firstNickname);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        await LogoutAsync(client);
        using var second = await RegisterAsync(client, secondNickname);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        var token = await TokenAsync(client, "/Account/EditProfile");
        using var response = await client.PostAsync("/Account/EditProfile", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nickname"] = firstNickname.ToUpperInvariant(), ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verification = factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.UserProfiles.AnyAsync(p => p.Nickname == secondNickname));
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string nickname)
    {
        var token = await TokenAsync(client, "/Account/Register");
        return await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nickname"] = nickname, ["Email"] = Guid.NewGuid().ToString("N") + "@example.com",
            ["Password"] = "TestPassword123", ["ConfirmPassword"] = "TestPassword123", ["__RequestVerificationToken"] = token
        }));
    }

    private static async Task LogoutAsync(HttpClient client)
    {
        var token = await TokenAsync(client, "/");
        using var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> TokenAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var match = Regex.Match(await response.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
