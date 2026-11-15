namespace foll_iam.IAM.Infrastructure.Messaging.Configuration;

public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "foll.integration";
    public string QueueName { get; set; } = "care.account-deletion-requested.v1";
    public int PublishTimeoutSeconds { get; set; } = 10;
}