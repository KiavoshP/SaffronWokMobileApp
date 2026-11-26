using System.Net;
using System.Net.Http.Json;
using Microsoft.Maui.Storage;
using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public sealed partial class PhoneVerificationPage : ContentPage
{
    private readonly HttpClient api;
    private readonly Action<PhoneVerificationReceipt>? verified;
    private Guid? challenge;

    public PhoneVerificationPage(HttpClient api, Action<PhoneVerificationReceipt>? verified = null)
    {
        this.api = api;
        this.verified = verified;
        InitializeComponent();
        cancel.IsVisible = verified is not null;
        request.Clicked += async (_, _) => await SubmitAsync(RequestCodeAsync);
        confirm.Clicked += async (_, _) => await SubmitAsync(ConfirmCodeAsync);
        phone.TextChanged += (_, _) =>
        {
            challenge = null;
            code.Text = "";
            codeFields.IsVisible = false;
            request.Text = "Request code";
            status.Text = "";
        };
    }

    private async void CancelClicked(object? sender, EventArgs args) => await Navigation.PopModalAsync();

    private async Task SubmitAsync(Func<Task> submit)
    {
        phone.IsEnabled = request.IsEnabled = confirm.IsEnabled = cancel.IsEnabled = false;
        try
        {
            await submit();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            status.Text = "Couldn’t reach the verification service. Try again.";
        }
        finally
        {
            phone.IsEnabled = request.IsEnabled = confirm.IsEnabled = cancel.IsEnabled = true;
        }
    }

    private async Task RequestCodeAsync()
    {
        var enteredPhone = string.Concat((phone.Text ?? "").Where(character => !char.IsWhiteSpace(character)));
        using var response = await api.PostAsJsonAsync("api/phone-verification/request",
            new RequestPhoneCode(enteredPhone));
        if (!response.IsSuccessStatusCode)
        {
            status.Text = response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => "Wait a minute before requesting another code.",
                HttpStatusCode.BadRequest => "Enter a valid international phone number.",
                _ => "Phone code delivery is currently unavailable."
            };
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<PhoneCodeChallenge>();
        if (result is null)
            throw new HttpRequestException("Missing challenge.");

        challenge = result.ChallengeId;
        code.Text = "";
        codeFields.IsVisible = true;
        request.Text = "Request a new code";
        status.Text = "Enter your six-digit verification code.";
    }

    private async Task ConfirmCodeAsync()
    {
        if (challenge is not { } id)
            return;

        using var response = await api.PostAsJsonAsync("api/phone-verification/confirm",
            new ConfirmPhoneCode(id, code.Text ?? ""));
        if (!response.IsSuccessStatusCode)
        {
            status.Text = response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => "Too many requests. Try again in a minute.",
                HttpStatusCode.BadRequest => "Code invalid or expired. Try again or request a new code.",
                _ => "Verification is temporarily unavailable."
            };
            return;
        }

        var receipt = await response.Content.ReadFromJsonAsync<PhoneVerificationReceipt>();
        if (receipt is null)
            throw new HttpRequestException("Missing verification receipt.");

        challenge = null;
        code.Text = "";
        codeFields.IsVisible = false;
        if (verified is not null)
        {
            verified(receipt);
            await Navigation.PopModalAsync();
            return;
        }

        await SecureStorage.Default.SetAsync("phone-verification-receipt", receipt.Token);
        request.Text = "Request code";
        status.Text = "Phone number verified.";
    }
}
