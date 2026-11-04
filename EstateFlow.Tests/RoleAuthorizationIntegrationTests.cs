using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using EstateFlow.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EstateFlow.Tests;

/// <summary>
/// Проверки на разрешенията чрез истинската Identity cookie сесия.
/// Базата с имоти остава блокирана от EstateFlowWebApplicationFactory.
/// </summary>
public class RoleAuthorizationIntegrationTests
{
    [Fact]
    public async Task BrokerCreatePage_HasOneProfileLinkAndMultipartImageForm()
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = await LoginAsAsync(factory, AppRoles.Broker);
        using var response = await client.GetAsync("/Properties/Create");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(Regex.Matches(html, "href=\"/Account/Profile\""));
        Assert.Contains("enctype=\"multipart/form-data\"", html);
        Assert.Contains("name=\"Photo\"", html);
    }

    [Theory]
    [InlineData(AppRoles.User, HttpStatusCode.Redirect)]
    [InlineData(AppRoles.Broker, HttpStatusCode.OK)]
    [InlineData(AppRoles.Admin, HttpStatusCode.OK)]
    public async Task CreatePage_RequiresBrokerOrAdmin(string role, HttpStatusCode expected)
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = await LoginAsAsync(factory, role);

        using var response = await client.GetAsync("/Properties/Create");
        Assert.Equal(expected, response.StatusCode);
        if (role == AppRoles.User)
            Assert.Contains("/Account/AccessDenied", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData(AppRoles.User)]
    [InlineData(AppRoles.Broker)]
    [InlineData(AppRoles.Admin)]
    public async Task FavoritesIds_AuthenticatedRole_CanReadOwnList(string role)
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = await LoginAsAsync(factory, role);

        using var response = await client.GetAsync("/Favorites/Ids");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(AppRoles.User, HttpStatusCode.Redirect)]
    [InlineData(AppRoles.Broker, HttpStatusCode.Redirect)]
    [InlineData(AppRoles.Admin, HttpStatusCode.OK)]
    public async Task AdminUsers_RequiresAdmin(string role, HttpStatusCode expected)
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = await LoginAsAsync(factory, role);

        using var response = await client.GetAsync("/Admin/Users");
        Assert.Equal(expected, response.StatusCode);
        if (role != AppRoles.Admin)
            Assert.Contains("/Account/AccessDenied", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData(AppRoles.User)]
    [InlineData(AppRoles.Broker)]
    [InlineData(AppRoles.Admin)]
    public async Task FavoritesToggle_WithoutAntiforgeryToken_IsRejected(string role)
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = await LoginAsAsync(factory, role);

        using var response = await client.PostAsync(
            "/Favorites/Toggle",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["propertyId"] = "1" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpClient> LoginAsAsync(
        EstateFlowWebApplicationFactory factory, string role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var email = $"role-{Guid.NewGuid():N}@example.com";
        const string password = "TestPassword123";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email };
            var created = await users.CreateAsync(user, password);
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
            var assigned = await users.AddToRoleAsync(user, role);
            Assert.True(assigned.Succeeded, string.Join("; ", assigned.Errors.Select(e => e.Description)));
        }

        using var loginPage = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var html = await loginPage.Content.ReadAsStringAsync();
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Липсва antiforgery токен в Login формата.");
        var token = WebUtility.HtmlDecode(match.Groups[1].Value);

        using var response = await client.PostAsync("/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["__RequestVerificationToken"] = token
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }
}
