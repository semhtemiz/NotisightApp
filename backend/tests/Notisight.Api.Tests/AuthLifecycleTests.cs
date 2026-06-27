using System.Net;
using System.Net.Http.Json;
using Notisight.Api.Features.Auth.Contracts;
using Notisight.Api.Tests.Infrastructure;

namespace Notisight.Api.Tests;

public sealed class AuthLifecycleTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public AuthLifecycleTests(TestApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Login_Refresh_And_Logout_Work_EndToEnd()
    {
        const string email = "auth@example.com";
        const string password = "P@ssw0rd123!";

        var registerResponse = await _client.RegisterAsync(email, password, "Auth User");
        Assert.Equal(email, registerResponse.User.Email);
        Assert.Equal("auth-user-auth", registerResponse.User.Username);
        Assert.NotEmpty(registerResponse.AccessToken);
        Assert.NotEmpty(registerResponse.RefreshToken);

        var loginHttpResponse = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        if (!loginHttpResponse.IsSuccessStatusCode)
        {
            var body = await loginHttpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Login failed with {(int)loginHttpResponse.StatusCode}: {body}");
        }

        var loginResponse = (await loginHttpResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEmpty(loginResponse.AccessToken);
        Assert.NotEqual(registerResponse.RefreshToken, loginResponse.RefreshToken);

        var refreshHttpResponse = await _client.PostAsJsonAsync(
            "/auth/refresh",
            new RefreshTokenRequest(loginResponse.RefreshToken));
        if (!refreshHttpResponse.IsSuccessStatusCode)
        {
            var body = await refreshHttpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Refresh failed with {(int)refreshHttpResponse.StatusCode}: {body}");
        }
        var refreshResponse = (await refreshHttpResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(loginResponse.AccessToken, refreshResponse.AccessToken);
        Assert.NotEqual(loginResponse.RefreshToken, refreshResponse.RefreshToken);

        var logoutResponse = await _client.PostAsJsonAsync(
            "/auth/logout",
            new LogoutRequest(refreshResponse.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var invalidRefreshResponse = await _client.PostAsJsonAsync(
            "/auth/refresh",
            new RefreshTokenRequest(refreshResponse.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, invalidRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task Login_Accepts_Username_Or_Email_And_Returns_Field_Specific_Errors()
    {
        const string email = "identity@example.com";
        const string username = "identity";
        const string password = "P@ssw0rd123!";

        var registerResponse = await _client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest(username, email, password));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var usernameLoginResponse = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, usernameLoginResponse.StatusCode);

        var wrongPasswordResponse = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "Wrong123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Contains("Şifre hatalı.", await wrongPasswordResponse.Content.ReadAsStringAsync());

        var missingIdentifierResponse = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("missing-user", password));
        Assert.Equal(HttpStatusCode.Unauthorized, missingIdentifierResponse.StatusCode);
        Assert.Contains("E-posta veya kullanıcı adı bulunamadı.", await missingIdentifierResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_With_Existing_Email_Or_Username_Returns_BadRequest()
    {
        const string password = "P@ssw0rd123!";
        var registerResponse = await _client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("duplicate", "duplicate@example.com", password));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var duplicateEmailResponse = await _client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("another-user", "duplicate@example.com", password));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateEmailResponse.StatusCode);
        Assert.Contains("Bu e-posta adresi zaten kullanılıyor.", await duplicateEmailResponse.Content.ReadAsStringAsync());

        var duplicateUsernameResponse = await _client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("duplicate", "another@example.com", password));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateUsernameResponse.StatusCode);
        Assert.Contains("Bu kullanıcı adı zaten kullanılıyor.", await duplicateUsernameResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_With_Weak_Password_Returns_BadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("weak-user", "weak@example.com", "password"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_User_Can_Update_Profile_With_Duplicate_Checks()
    {
        const string password = "P@ssw0rd123!";
        var auth = await _client.RegisterAsync("profile-owner@example.com", password, "Profile Owner");
        await _client.RegisterAsync("profile-other@example.com", password, "Profile Other");
        _client.SetBearer(auth.AccessToken);

        var meResponse = await _client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = (await meResponse.Content.ReadFromJsonAsync<AuthUserResponse>())!;
        Assert.Equal(auth.User.Email, me.Email);

        var updateResponse = await _client.PutAsJsonAsync(
            "/auth/profile",
            new UpdateProfileRequest("Updated User", "updated-user", "updated@example.com"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<AuthUserResponse>())!;
        Assert.Equal("Updated User", updated.DisplayName);
        Assert.Equal("updated-user", updated.Username);
        Assert.Equal("updated@example.com", updated.Email);

        var duplicateEmailResponse = await _client.PutAsJsonAsync(
            "/auth/profile",
            new UpdateProfileRequest("Updated User", "updated-user-2", "profile-other@example.com"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateEmailResponse.StatusCode);
        Assert.Contains("Bu e-posta adresi zaten kullanılıyor.", await duplicateEmailResponse.Content.ReadAsStringAsync());

        var duplicateUsernameResponse = await _client.PutAsJsonAsync(
            "/auth/profile",
            new UpdateProfileRequest("Updated User", "profile-other-profile-other", "unique-profile@example.com"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateUsernameResponse.StatusCode);
        Assert.Contains("Bu kullanıcı adı zaten kullanılıyor.", await duplicateUsernameResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Authenticated_User_Can_Change_Password_With_Current_Password()
    {
        const string oldPassword = "P@ssw0rd123!";
        const string newPassword = "NewP@ssw0rd123!";
        var auth = await _client.RegisterAsync("password-change@example.com", oldPassword, "Password User");
        _client.SetBearer(auth.AccessToken);

        var wrongCurrentResponse = await _client.PutAsJsonAsync(
            "/auth/password",
            new ChangePasswordRequest("Wrong123!", newPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCurrentResponse.StatusCode);
        Assert.Contains("Mevcut şifre hatalı.", await wrongCurrentResponse.Content.ReadAsStringAsync());

        var weakPasswordResponse = await _client.PutAsJsonAsync(
            "/auth/password",
            new ChangePasswordRequest(oldPassword, "password"));
        Assert.Equal(HttpStatusCode.BadRequest, weakPasswordResponse.StatusCode);

        var changeResponse = await _client.PutAsJsonAsync(
            "/auth/password",
            new ChangePasswordRequest(oldPassword, newPassword));
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        var oldLoginResponse = await _client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(auth.User.Email, oldPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);

        var newLoginResponse = await _client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(auth.User.Email, newPassword));
        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);
    }
}
