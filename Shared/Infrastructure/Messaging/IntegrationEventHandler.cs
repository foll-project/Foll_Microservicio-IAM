using foll_iam.IAM.Domain.Model.Enums;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;
namespace foll_iam.Shared.Infrastructure.Messaging;

public sealed class AccountDeletionCompletedHandler(AppDbContext db) : IBrokerMessageHandler
{
    public async Task HandleAsync(BrokerEnvelope envelope, CancellationToken ct)
    {
        if (envelope.EventType != AccountDeletionCompleted.EventType) throw new InvalidDataException("Evento no soportado.");
        var message = envelope.Read<AccountDeletionCompleted>();
        if (message.UserId <= 0 || message.DeletedPatientIds is null || message.DeletedPatientIds.Any(id => id <= 0))
            throw new InvalidDataException("Confirmacion de Care invalida.");
        // Serialize with the outbox publisher, including confirmations that arrive before its commit.
        var request = await db.AccountDeletionRequests
            .FromSqlInterpolated($"SELECT * FROM iam.account_deletion_requests WHERE operation_id = {message.OperationId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (request is null || request.UserId != message.UserId)
            throw new InvalidDataException("Confirmacion sin solicitud de IAM correspondiente.");
        if (request.Status == AccountDeletionStatus.Completed) return;
        var user = await db.Users.FindAsync(new object[] { message.UserId }, ct);
        if (user is not null) db.Users.Remove(user);
        request.MarkAsCompleted(DateTime.UtcNow);
    }
}

