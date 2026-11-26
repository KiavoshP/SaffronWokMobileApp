namespace SaffronWok.Shared;

public sealed record RegisterCustomer(string FullName, string Password, string VerificationReceipt);
public sealed record SignInCustomer(string PhoneNumber, string Password);
public sealed record CustomerProfile(Guid Id, string FullName, string PhoneNumber);
public sealed record CustomerSession(string Token, CustomerProfile Profile);
