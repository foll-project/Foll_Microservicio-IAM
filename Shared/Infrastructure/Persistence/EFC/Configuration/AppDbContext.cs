using foll_iam.IAM.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<AccountDeletionRequest> AccountDeletionRequests { get; set; }
    public DbSet<foll_iam.IAM.Infrastructure.Messaging.Outbox.IntegrationOutboxMessage> IntegrationOutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

