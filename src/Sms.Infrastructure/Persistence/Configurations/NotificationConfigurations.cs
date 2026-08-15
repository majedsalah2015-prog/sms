using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Notifications;
using Sms.Domain.Security;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // msg schema per docs/Database/03 §A ("msg (14)"). Only the notification-
    // engine's 6 tables live here — Announcement/Thread/OfficialLetter/etc.
    // belong to the Messaging module (32), out of E-007's scope (doc 09 §0).

    public class TemplateConfiguration : IEntityTypeConfiguration<Template>
    {
        public void Configure(EntityTypeBuilder<Template> builder)
        {
            builder.ToTable("Template", "msg");
            builder.Property(x => x.EventCode).HasMaxLength(60).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.EventCode, x.Channel }).IsUnique();
            builder.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.TemplateId);
        }
    }

    public class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
    {
        public void Configure(EntityTypeBuilder<TemplateVersion> builder)
        {
            builder.ToTable("TemplateVersion", "msg");
            builder.Property(x => x.SubjectAr).HasMaxLength(200);
            builder.Property(x => x.SubjectEn).HasMaxLength(200);
            builder.Property(x => x.BodyAr).IsRequired();
            builder.Property(x => x.BodyEn).IsRequired();
            // The Template ↔ Versions relationship is configured from the Template side (HasMany(x => x.Versions)).
            builder.HasIndex(x => new { x.TemplateId, x.VersionNumber }).IsUnique();
        }
    }

    public class SubscriptionRuleConfiguration : IEntityTypeConfiguration<SubscriptionRule>
    {
        public void Configure(EntityTypeBuilder<SubscriptionRule> builder)
        {
            builder.ToTable("SubscriptionRule", "msg");
            builder.Property(x => x.EventCode).HasMaxLength(60).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.EventCode, x.Channel }).IsUnique();
        }
    }

    public class ProviderConfiguration : IEntityTypeConfiguration<Provider>
    {
        public void Configure(EntityTypeBuilder<Provider> builder)
        {
            builder.ToTable("Provider", "msg");
            builder.Property(x => x.ProviderCode).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Channel });
        }
    }

    public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
    {
        public void Configure(EntityTypeBuilder<Delivery> builder)
        {
            builder.ToTable("Delivery", "msg");
            builder.Property(x => x.EventCode).HasMaxLength(60).IsRequired();
            builder.Property(x => x.RenderedSubject).HasMaxLength(200);
            builder.Property(x => x.RenderedBody).IsRequired();
            builder.Property(x => x.FailureReason).HasMaxLength(500);
            builder.HasOne<TemplateVersion>().WithMany().HasForeignKey(x => x.TemplateVersionId);
            builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecipientUserId);
            builder.HasIndex(x => new { x.RecipientUserId, x.Status });
            builder.HasIndex(x => x.Status);
        }
    }

    public class BudgetCounterConfiguration : IEntityTypeConfiguration<BudgetCounter>
    {
        public void Configure(EntityTypeBuilder<BudgetCounter> builder)
        {
            builder.ToTable("BudgetCounter", "msg");
            builder.Property(x => x.PeriodKey).HasMaxLength(7).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Channel, x.PeriodKey }).IsUnique();
        }
    }
}
