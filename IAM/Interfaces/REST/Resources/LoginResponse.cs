namespace foll_iam.IAM.Interfaces.REST.Resources;

public record LoginResponse(
    long UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Token);
