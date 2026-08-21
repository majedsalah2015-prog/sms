using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Jobs;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ops schema per docs/Database/03 §A ("ops (16)") — JobDefinition/JobRun only; the
    // reporting/import/backup tables in that schema belong to their own later epics.

    public class JobDefinitionConfiguration : IEntityTypeConfiguration<JobDefinition>
    {
        public void Configure(EntityTypeBuilder<JobDefinition> builder)
        {
            builder.ToTable("JobDefinition", "ops");
            builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
            builder.Property(x => x.CronExpression).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => x.Code).IsUnique();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
            });
        }
    }

    public class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
    {
        public void Configure(EntityTypeBuilder<JobRun> builder)
        {
            builder.ToTable("JobRun", "ops");
            builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
            builder.HasOne<JobDefinition>().WithMany().HasForeignKey(x => x.JobDefinitionId);
            builder.HasIndex(x => new { x.JobDefinitionId, x.StartedAtUtc });

            // At most one run of a job in flight at a time, enforced by the database rather than by
            // a read-then-write in the runner. Hangfire enqueues every occurrence missed while the
            // host was down, and ten dispatch runs starting inside the same tenth of a second would
            // all read the same queued notifications and all send them. A check in code cannot stop
            // that; ten readers see "nothing running" before any of them writes.
            builder.HasIndex(x => x.JobDefinitionId)
                .IsUnique()
                .HasFilter("[Status] = 1")
                .HasDatabaseName("UX_JobRun_InFlight");
        }
    }
}
