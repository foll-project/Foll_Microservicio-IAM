namespace foll_iam.IAM.Domain.Model.Commands;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber);
