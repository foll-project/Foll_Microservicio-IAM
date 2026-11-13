namespace foll_iam.IAM.Domain.Exceptions;

public sealed class AccountDeletionConflictException : Exception
{
    public AccountDeletionConflictException(Exception innerException)
        : base("Ya existe una solicitud de eliminacion para esta cuenta.", innerException) { }
}
