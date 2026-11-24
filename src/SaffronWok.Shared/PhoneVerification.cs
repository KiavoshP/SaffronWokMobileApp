namespace SaffronWok.Shared;

public sealed record RequestPhoneCode(string PhoneNumber);
public sealed record PhoneCodeChallenge(Guid ChallengeId);
public sealed record ConfirmPhoneCode(Guid ChallengeId, string Code);
public sealed record PhoneVerificationReceipt(string Token, DateTimeOffset ExpiresAt);
