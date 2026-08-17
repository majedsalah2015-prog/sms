using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Reports;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // doc/Modules/30 — ReportDefinition is a core catalog row; ReportExecution/ReportSubscription are ppl-schema instance rows, same split as other registry-vs-instance pairs in this codebase.

    public class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
    {
        public void Configure(EntityTypeBuilder<ReportDefinition> builder)
        {
            builder.ToTable("ReportDefinition", "core");
            builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
            builder.Property(x => x.TitleAr).HasMaxLength(150).IsRequired();
            builder.Property(x => x.TitleEn).HasMaxLength(150).IsRequired();
            builder.Property(x => x.OwningModuleCode).HasMaxLength(20).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
        }
    }

    public class ReportExecutionConfiguration : IEntityTypeConfiguration<ReportExecution>
    {
        public void Configure(EntityTypeBuilder<ReportExecution> builder)
        {
            builder.ToTable("ReportExecution", "ppl");
            builder.Property(x => x.ParametersJson).IsRequired();
            builder.HasOne<ReportDefinition>().WithMany().HasForeignKey(x => x.ReportDefinitionId);
        }
    }

    public class ReportSubscriptionConfiguration : IEntityTypeConfiguration<ReportSubscription>
    {
        public void Configure(EntityTypeBuilder<ReportSubscription> builder)
        {
            builder.ToTable("ReportSubscription", "ppl");
            builder.Property(x => x.ParametersJson).IsRequired();
            builder.HasOne<ReportDefinition>().WithMany().HasForeignKey(x => x.ReportDefinitionId);
            builder.HasIndex(x => new { x.ReportDefinitionId, x.SubscriberUserId }).IsUnique().HasFilter("[IsActive] = 1");
        }
    }
}
