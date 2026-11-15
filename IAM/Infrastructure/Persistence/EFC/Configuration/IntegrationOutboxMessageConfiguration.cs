using foll_iam.IAM.Domain.Model.Entities;
using foll_iam.IAM.Infrastructure.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace foll_iam.IAM.Infrastructure.Persistence.EFC.Configuration;

public class IntegrationOutboxMessageConfiguration : IEntityTypeConfiguration<IntegrationOutboxMessage>
{
    public void Configure(EntityTypeBuilder<IntegrationOutboxMessage> builder)
    {
        builder.ToTable("integration_outbox_messages", "iam");
        builder.HasKey(m => m.EventId);
        builder.Property(m => m.EventId).ValueGeneratedNever();
        builder.Property(m => m.EventType).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.HasIndex(m => m.OperationId).IsUnique();
        builder.HasIndex(m => m.NextAttemptAtUtc).HasFilter("published_at_utc IS NULL");
        builder.HasOne<AccountDeletionRequest>().WithMany().HasForeignKey(m => m.OperationId).OnDelete(DeleteBehavior.Restrict);
    }
}
