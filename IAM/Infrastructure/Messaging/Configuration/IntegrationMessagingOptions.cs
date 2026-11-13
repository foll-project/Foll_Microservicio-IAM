namespace foll_iam.IAM.Infrastructure.Messaging.Configuration;

public class IntegrationMessagingOptions
{
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
}
