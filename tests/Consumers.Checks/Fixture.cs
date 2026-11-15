using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using foll_iam.Shared.Infrastructure.Messaging;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_iam.IAM.Domain.Model.Entities;
using foll_iam.IAM.Domain.Model.Enums;
static class Fixture
{
    static long userId; static Guid operationId;
    public static IBrokerMessageHandler Handler(AppDbContext db) => new AccountDeletionCompletedHandler(db);
    public static async Task<BrokerEnvelope> SeedAsync(AppDbContext db)
    {
        var user = new User(Guid.NewGuid().ToString("N") + "@checks.local", "test-hash", "Test", "User", null);
        db.Users.Add(user); await db.SaveChangesAsync();
        userId = user.UserId;
        var request = AccountDeletionRequest.Create(userId);
        request.MarkAsPublished(DateTime.UtcNow);
        db.AccountDeletionRequests.Add(request);
        operationId = request.OperationId;
        var e = new AccountDeletionCompleted(Guid.NewGuid(), operationId, userId, new long[] { 100 }, DateTime.UtcNow);
        return new(e.EventId, e.OperationId, AccountDeletionCompleted.EventType, JsonSerializer.Serialize(e));
    }
    public static async Task AssertAsync(AppDbContext db, Action<bool,string> check)
    {
        check(!await db.Users.AnyAsync(x => x.UserId == userId), "IAM user removed only after Care confirmation");
        var request = await db.AccountDeletionRequests.SingleAsync(x => x.OperationId == operationId);
        check(request.Status == AccountDeletionStatus.Completed && request.CompletedAtUtc is not null, "completed history retained after user deletion");
        request.MarkAsPublished(DateTime.UtcNow);
        check(request.Status == AccountDeletionStatus.Completed, "late publisher cannot revert completed status");
    }
}

