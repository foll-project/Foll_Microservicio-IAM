using foll_iam.IAM.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace foll_iam.IAM.Infrastructure.Persistence.EFC.Configuration;

public class AccountDeletionRequestConfiguration : IEntityTypeConfiguration<AccountDeletionRequest>
{
    public void Configure(EntityTypeBuilder<AccountDeletionRequest> builder)
    {
        builder.ToTable("account_deletion_requests", "iam");
        builder.HasKey(r => r.OperationId);
        builder.Property(r => r.OperationId).ValueGeneratedNever();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.HasIndex(r => r.UserId).IsUnique();
        // Keep the operation history after deleting the account. UserId is a historical reference.
    }
}
