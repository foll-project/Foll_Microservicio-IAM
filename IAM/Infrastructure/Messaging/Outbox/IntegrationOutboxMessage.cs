using System.Text.Json;
using foll_iam.IAM.Application.IntegrationEvents;

namespace foll_iam.IAM.Infrastructure.Messaging.Outbox;

public class IntegrationOutboxMessage
{
    public Guid EventId { get; private set; }
    public Guid OperationId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }

    private IntegrationOutboxMessage() { }

    public static IntegrationOutboxMessage From(AccountDeletionRequestedIntegrationEvent message) => new()
    {
        EventId = message.EventId, OperationId = message.OperationId,
        EventType = AccountDeletionRequestedIntegrationEvent.EventType, Version = message.Version,
        Payload = JsonSerializer.Serialize(message), OccurredAtUtc = message.OccurredAtUtc,
        NextAttemptAtUtc = message.OccurredAtUtc
    };

    public void MarkAsPublished(DateTime now)
    {
        AttemptCount++;
        PublishedAtUtc = now;
        LastError = null;
    }

    public void RecordFailure(DateTime now, string error)
    {
        AttemptCount++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        NextAttemptAtUtc = now.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(AttemptCount, 9))));
    }
}
