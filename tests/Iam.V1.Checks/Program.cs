using System.Text.Json;
using System.Security.Claims;
using foll_iam.IAM.Application.Internal.QueryServices;
using foll_iam.IAM.Domain.Exceptions;
using foll_iam.IAM.Interfaces.REST;
using foll_iam.IAM.Interfaces.REST.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using foll_iam.IAM.Application.IntegrationEvents;
using foll_iam.IAM.Application.Internal.CommandServices;
using foll_iam.IAM.Application.OutboundServices;
using foll_iam.IAM.Domain.Model.Commands;
using foll_iam.IAM.Domain.Model.Entities;
using foll_iam.IAM.Domain.Model.Enums;
using foll_iam.IAM.Domain.Repositories;
using foll_iam.IAM.Infrastructure.Hashing;
using foll_iam.IAM.Infrastructure.Messaging.Configuration;
using foll_iam.IAM.Infrastructure.Messaging.Outbox;
using foll_iam.Shared.Domain.Repositories;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var count = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL: " + name);
    count++;
    Console.WriteLine("PASS: " + name);
}
async Task Throws<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); } catch (T) { Check(true, name); return; }
    throw new Exception("Expected " + typeof(T).Name + ": " + name);
}
var users = new FakeUsers();
var events = new FakeEvents();
var unit = new FakeUnit();
var hashing = new BcryptHashingService();
var service = new UserCommandService(users, hashing, unit, events);
await Throws<InvalidOperationException>(() => service.Handle(new RequestAccountDeletionCommand(1, "secret")), "unknown account");
users.User = new User("test@example.com", hashing.HashPassword("secret"), "Test", "User", null);
await Throws<InvalidOperationException>(() => service.Handle(new RequestAccountDeletionCommand(1, "wrong")), "wrong password");
Check(users.Request is null && events.Messages.Count == 0 && unit.Saves == 0, "invalid credentials produce no writes");
var request = await service.Handle(new RequestAccountDeletionCommand(1, "secret"));
Check(request.Status == AccountDeletionStatus.PendingPublication && request.PublishedAtUtc is null, "pending publication initially");
Check(events.Messages.Count == 1 && unit.Saves == 1 && users.User is not null, "stage once and commit once, without deleting user");
var repeated = await service.Handle(new RequestAccountDeletionCommand(1, "secret"));
Check(repeated.OperationId == request.OperationId && events.Messages.Count == 1 && unit.Saves == 1, "repeat is idempotent");
await Throws<InvalidOperationException>(() => service.Handle(new RequestAccountDeletionCommand(1, "wrong")), "repeat still checks credentials");
var message = events.Messages.Single();
Check(message.OperationId == request.OperationId && message.EventId != Guid.Empty, "stable correlation identifiers");
var row = IntegrationOutboxMessage.From(message);
Check(JsonSerializer.Deserialize<AccountDeletionRequestedIntegrationEvent>(row.Payload) == message && !row.Payload.Contains("secret"), "message roundtrip without credentials");
row.RecordFailure(DateTime.UtcNow, new string('x', 3000));
Check(row.PublishedAtUtc is null && row.AttemptCount == 1 && row.NextAttemptAtUtc > DateTime.UtcNow && row.LastError!.Length == 2000, "failed publication has backoff and retains pending state");
var now = DateTime.UtcNow;
row.MarkAsPublished(now);
request.MarkAsPublished(now);
request.MarkAsPublished(now.AddMinutes(1));
Check(request.Status == AccountDeletionStatus.WaitingForConfirmations && request.PublishedAtUtc == now && row.LastError is null, "confirmed publication transition is idempotent");
await Throws<ArgumentOutOfRangeException>(() => Task.FromResult(AccountDeletionRequest.Create(0)), "invalid account id");
await Throws<ArgumentException>(() => { AccountDeletionRequest.Create(1).MarkAsPublished(DateTime.UtcNow.AddDays(-1)); return Task.CompletedTask; }, "publication cannot precede request");
using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").UseSnakeCaseNamingConvention().Options);
Check(context.Model.FindEntityType(typeof(AccountDeletionRequest))!.GetIndexes().Any(i => i.IsUnique && i.Properties.Single().Name == "UserId"), "unique database constraint for concurrent requests");
var sql = context.GetService<IMigrator>().GenerateScript();
Check(sql.Contains("account_deletion_requests") && sql.Contains("integration_outbox_messages"), "migration contains both tables");
var staged = AccountDeletionRequest.Create(1);
await context.AccountDeletionRequests.AddAsync(staged);
await new OutboxUserEventService(context).AccountDeletionRequestedAsync(
    new(Guid.NewGuid(), staged.OperationId, staged.UserId, staged.RequestedAtUtc));
Check(context.ChangeTracker.Entries().Count(e => e.State == EntityState.Added) == 2,
    "real outbox adapter stages request and message in the same context without saving");

var concurrentUsers = new FakeUsers { User = users.User };
var concurrentUnit = new FakeUnit { OnSave = () => concurrentUsers.Request = AccountDeletionRequest.Create(1), Conflict = true };
var winner = await new UserCommandService(concurrentUsers, hashing, concurrentUnit, new FakeEvents())
    .Handle(new RequestAccountDeletionCommand(1, "secret"));
Check(winner == concurrentUsers.Request, "unique conflict recovers the winning operation");

var controller = new AuthenticationController(service, new UserQueryService(users, hashing, new FakeTokens()))
{ ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
Check(await controller.GetAccountDeletionRequest() is UnauthorizedObjectResult, "invalid claim rejected");
controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", "2") }, "test"));
Check(await controller.GetAccountDeletionRequest() is NotFoundObjectResult, "status lookup uses authenticated user only");
controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", "1") }, "test"));
Check(await controller.RequestAccountDeletion(new DeleteAccountResource("secret")) is AcceptedAtActionResult { StatusCode: 202 }, "request returns Accepted with status location");
Check(await controller.RequestAccountDeletion(new DeleteAccountResource("wrong")) is BadRequestObjectResult, "wrong password maps to 400");
using var provider = new ServiceCollection().BuildServiceProvider();
using var disabled = new OutboxPublisherBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new IntegrationMessagingOptions()), NullLogger<OutboxPublisherBackgroundService>.Instance);
await disabled.StartAsync(default);
await disabled.StopAsync(default);
Check(true, "disabled publication needs neither transport nor database access");
using var enabled = new OutboxPublisherBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new IntegrationMessagingOptions { Enabled = true }), NullLogger<OutboxPublisherBackgroundService>.Instance);
await Throws<InvalidOperationException>(() => enabled.StartAsync(default), "enabling publication without adapter fails explicitly");
var recipientController = new foll_iam.IAM.Interfaces.Internal.NotificationRecipientController(new UserQueryService(users, hashing, new FakeTokens()));
Check(await recipientController.Get(0) is NotFoundResult, "notification recipient rejects invalid identity");
var recipientResponse = await recipientController.Get(1) as OkObjectResult;
Check(recipientResponse is not null, "notification recipient reads existing IAM user");
var recipientJson = JsonSerializer.SerializeToElement(recipientResponse!.Value);
Check(recipientJson.TryGetProperty("FirstName", out _) && recipientJson.TryGetProperty("LastName", out _) && recipientJson.TryGetProperty("PhoneNumber", out _) &&
    !recipientJson.TryGetProperty("PasswordHash", out _) && !recipientJson.TryGetProperty("Email", out _), "notification recipient exposes only minimal contact fields");
if (args.Contains("--rabbitmq")) await RabbitMqChecks.RunAsync(Check);
Console.WriteLine($"{count} checks passed.");

sealed class FakeUsers : IUserRepository
{
    public User? User;
    public AccountDeletionRequest? Request;
    public Task<AccountDeletionRequest?> FindAccountDeletionRequestByUserIdAsync(long id) => Task.FromResult(Request?.UserId == id ? Request : null);
    public Task AddAccountDeletionRequestAsync(AccountDeletionRequest request) { Request = request; return Task.CompletedTask; }
    public Task<User?> FindByIdAsync(long id) => Task.FromResult(User);
    public Task<User?> FindByEmailAsync(string email) => Task.FromResult(User);
    public Task<bool> ExistsByEmailAsync(string email) => Task.FromResult(User is not null);
    public Task AddAsync(User user) { User = user; return Task.CompletedTask; }
    public void Update(User user) => User = user;
    public void Remove(User user) => throw new Exception("V1 must never delete a user");
    public Task<IEnumerable<User>> ListAsync() => Task.FromResult<IEnumerable<User>>(User is null ? Array.Empty<User>() : new[] { User });
}
sealed class FakeEvents : IUserEventService
{
    public List<AccountDeletionRequestedIntegrationEvent> Messages { get; } = new();
    public Task AccountDeletionRequestedAsync(AccountDeletionRequestedIntegrationEvent message) { Messages.Add(message); return Task.CompletedTask; }
}
sealed class FakeUnit : IUnitOfWork
{
    public int Saves;
    public Action? OnSave;
    public bool Conflict;
    public Task CompleteAsync()
    {
        Saves++;
        OnSave?.Invoke();
        if (Conflict) throw new AccountDeletionConflictException(new Exception("unique conflict"));
        return Task.CompletedTask;
    }
}
sealed class FakeTokens : ITokenService
{
    public string GenerateToken(User user) => "test-token";
}
