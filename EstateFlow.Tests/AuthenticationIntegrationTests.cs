
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EstateFlow.Tests;

/// <summary>
/// Интеграционни тестове за достъпа
/// до публичните и защитените страници.
/// </summary>
public class AuthenticationIntegrationTests
{
    [Theory]
    [InlineData("/Properties/Create")]
    [InlineData("/Properties/Edit/1")]
    [InlineData("/Properties/Delete/1")]
    public async Task Guest_ProtectedPage_RedirectsToLogin(
        string url)
    {
        using var factory = new EstateFlowWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.Redirect,
            response.StatusCode);

        Assert.NotNull(response.Headers.Location);


        var redirectUrl = response.Headers.Location!;

        // Проверяваме пътя независимо дали URL адресът
        // е абсолютен или относителен.
        var redirectPath = redirectUrl.IsAbsoluteUri
            ? redirectUrl.AbsolutePath
            : redirectUrl.OriginalString.Split('?')[0];

        Assert.Equal("/Account/Login", redirectPath);

    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    public async Task Guest_AccountPages_AreAccessible(
        string url)
    {
        using var factory = new EstateFlowWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Register_ValidUser_CreatesAccount()
    {
        using var factory = new EstateFlowWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Зареждаме формата, за да получим antiforgery cookie.
        var getResponse = await client.GetAsync("/Account/Register");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var html = await getResponse.Content.ReadAsStringAsync();

        // Извличаме antiforgery токена от формата.
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        Assert.True(match.Success, "Antiforgery токенът не е намерен.");

        var token = System.Net.WebUtility.HtmlDecode(
            match.Groups[1].Value);

        // Използваме уникален имейл за всеки тест.
        var email = $"test-{Guid.NewGuid():N}@example.com";

        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "TestPassword123",
            ["ConfirmPassword"] = "TestPassword123",
            ["__RequestVerificationToken"] = token
        };

        using var response = await client.PostAsync(
            "/Account/Register",
            new FormUrlEncodedContent(form));

        // Успешната регистрация пренасочва към Index.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location;
        Assert.NotNull(location);

        var path = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString.Split('?')[0];

        Assert.Contains(path, new[] { "/", "/Properties", "/Properties/Index" });

        // Проверяваме дали потребителят е записан в Identity.
        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<
                Microsoft.AspNetCore.Identity.UserManager<
                    Microsoft.AspNetCore.Identity.IdentityUser>>();

        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public async Task Login_ValidCredentials_CreatesSession()
    {
        using var factory = new EstateFlowWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Създаваме тестов потребител директно чрез Identity.
        var email = $"login-{Guid.NewGuid():N}@example.com";
        const string password = "TestPassword123";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();

            var user = new IdentityUser
            {
                UserName = email,
                Email = email
            };

            var result = await userManager.CreateAsync(user, password);

            Assert.True(
                result.Succeeded,
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // Зареждаме формата за Login и antiforgery токена.
        var getResponse = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var html = await getResponse.Content.ReadAsStringAsync();

        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        Assert.True(match.Success, "Antiforgery токенът не е намерен.");

        var token = System.Net.WebUtility.HtmlDecode(
            match.Groups[1].Value);

        // Изпращаме данните към Login POST метода.
        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = token
        };

        using var response = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(form));

        // Успешният вход трябва да пренасочи към Index.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location;
        Assert.NotNull(location);

        var path = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString.Split('?')[0];

        Assert.Contains(
            path,
            new[] { "/", "/Properties", "/Properties/Index" });

        // Проверяваме дали Identity е създал cookie за вход.
        Assert.True(
            response.Headers.TryGetValues(
                "Set-Cookie",
                out var cookies));

        Assert.Contains(
            cookies!,
            cookie => cookie.StartsWith(
                ".AspNetCore.Identity.Application=",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logout_AuthenticatedUser_CannotAccessProtectedPage()
    {
        using var factory = new EstateFlowWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var email = $"logout-{Guid.NewGuid():N}@example.com";
        const string password = "TestPassword123";

        // Създаваме тестов потребител.
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();

            var user = new IdentityUser
            {
                UserName = email,
                Email = email
            };

            var result = await userManager.CreateAsync(user, password);

            Assert.True(result.Succeeded);
        }

        // Зареждаме Login формата.
        var loginPage = await client.GetAsync("/Account/Login");
        var loginHtml = await loginPage.Content.ReadAsStringAsync();

        var loginMatch = System.Text.RegularExpressions.Regex.Match(
            loginHtml,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        Assert.True(loginMatch.Success);

        var loginToken = System.Net.WebUtility.HtmlDecode(
            loginMatch.Groups[1].Value);

        // Влизаме в профила.
        var loginForm = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = loginToken
        };

        using var loginResponse = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(loginForm));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        // Проверяваме дали потребителят има достъп след Login.
        using var protectedResponse = await client.GetAsync("/Properties/Create");

        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);

        // Зареждаме страницата, за да вземем нов antiforgery токен.
        var indexPage = await client.GetAsync("/");
        var indexHtml = await indexPage.Content.ReadAsStringAsync();

        var logoutMatch = System.Text.RegularExpressions.Regex.Match(
            indexHtml,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        Assert.True(logoutMatch.Success);

        var logoutToken = System.Net.WebUtility.HtmlDecode(
            logoutMatch.Groups[1].Value);

        // Изпращаме POST заявка за Logout.
        var logoutForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = logoutToken
        };

        using var logoutResponse = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(logoutForm));

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);

        // След Logout защитената страница отново
        // трябва да пренасочва към Login.
        using var afterLogout = await client.GetAsync("/Properties/Create");

        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);

        var location = afterLogout.Headers.Location;
        Assert.NotNull(location);

        var redirectPath = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString.Split('?')[0];

        Assert.Equal("/Account/Login", redirectPath);
    }

}
