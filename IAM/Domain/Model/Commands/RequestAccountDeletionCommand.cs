namespace foll_iam.IAM.Domain.Model.Commands;

public record RequestAccountDeletionCommand(long UserId, string Password);
