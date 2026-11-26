using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Maui.Storage;
using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public sealed partial class AccountPage : ContentPage
{
    private const string SessionKey = "saffronwok-customer-token";
    private readonly HttpClient api;
    private string? token;
    private string? receipt;
    private bool registering;
    private bool busy;

    internal AccountPage(HttpClient api)
    {
        this.api = api;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await WhileBusyAsync(async () =>
        {
            token ??= await SecureStorage.Default.GetAsync(SessionKey);
            if (token is null)
                return;

            using var response = await SendAuthenticatedAsync(HttpMethod.Get, "api/customer/me");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                ClearSession();
                return;
            }

            response.EnsureSuccessStatusCode();
            ShowProfile(await response.Content.ReadFromJsonAsync<CustomerProfile>() ??
                throw new HttpRequestException("Missing customer profile."));
        });
    }

    private void SwitchModeClicked(object? sender, EventArgs args)
    {
        if (busy)
            return;

        registering = !registering;
        receipt = null;
        password.Text = "";
        accountStatus.Text = "";
        heading.Text = registering ? "Create an Account" : "Customer Sign In";
        phoneField.IsVisible = !registering;
        nameField.IsVisible = verify.IsVisible = registering;
        verify.Text = "Verify your phone number";
        submit.Text = registering ? "Create account" : "Sign in";
        switchMode.Text = registering ? "Back to sign in" : "Create an account";
    }

    private async void VerifyClicked(object? sender, EventArgs args)
    {
        if (busy)
            return;

        var page = new PhoneVerificationPage(api, successfulReceipt =>
        {
            receipt = successfulReceipt.Token;
            verify.Text = "Phone verified";
            accountStatus.Text = "";
        });
        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private async void SubmitClicked(object? sender, EventArgs args) => await WhileBusyAsync(async () =>
    {
        if (registering && receipt is null)
        {
            accountStatus.Text = "Verify your phone number first.";
            return;
        }

        var enteredPassword = password.Text ?? "";
        if (registering && (string.IsNullOrWhiteSpace(fullName.Text) || enteredPassword.Length < 6))
        {
            accountStatus.Text = "Enter your name and a password with at least 6 characters.";
            return;
        }

        var enteredPhone = string.Concat((phoneNumber.Text ?? "").Where(character => !char.IsWhiteSpace(character)));
        using var response = registering
            ? await api.PostAsJsonAsync("api/customer/register", new RegisterCustomer(fullName.Text ?? "", enteredPassword, receipt!))
            : await api.PostAsJsonAsync("api/customer/login", new SignInCustomer(enteredPhone, enteredPassword));
        if (!response.IsSuccessStatusCode)
        {
            accountStatus.Text = response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => "Too many attempts. Try again in a minute.",
                HttpStatusCode.Unauthorized => "The phone number or password is incorrect.",
                HttpStatusCode.BadRequest => "Account could not be created. Check your details and verify your phone again.",
                _ => "The account service is unavailable."
            };
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<CustomerSession>() ??
            throw new HttpRequestException("Missing customer session.");
        token = result.Token;
        receipt = null;
        password.Text = "";
        ShowProfile(result.Profile);
        await SecureStorage.Default.SetAsync(SessionKey, token);
    });

    private async void SignOutClicked(object? sender, EventArgs args) => await WhileBusyAsync(async () =>
    {
        using var response = await SendAuthenticatedAsync(HttpMethod.Post, "api/customer/logout");
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            response.EnsureSuccessStatusCode();

        ClearSession();
    });

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(HttpMethod method, string url)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await api.SendAsync(request);
    }

    private async Task WhileBusyAsync(Func<Task> action)
    {
        if (busy)
            return;

        busy = true;
        credentials.IsEnabled = profilePanel.IsEnabled = false;
        accountStatus.Text = "";
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            accountStatus.Text = "Couldn’t reach the account service. Try again.";
        }
        finally
        {
            busy = false;
            credentials.IsEnabled = profilePanel.IsEnabled = true;
        }
    }

    private void ShowProfile(CustomerProfile profile)
    {
        heading.Text = profile.FullName;
        profilePhone.Text = profile.PhoneNumber;
        credentials.IsVisible = false;
        profilePanel.IsVisible = true;
    }

    private void ClearSession()
    {
        token = null;
        SecureStorage.Default.Remove(SessionKey);
        profilePanel.IsVisible = false;
        credentials.IsVisible = true;
        if (registering)
            SwitchModeAfterSignOut();
        heading.Text = "Customer Sign In";
    }

    private void SwitchModeAfterSignOut()
    {
        registering = false;
        phoneField.IsVisible = true;
        nameField.IsVisible = verify.IsVisible = false;
        submit.Text = "Sign in";
        switchMode.Text = "Create an account";
    }
}
