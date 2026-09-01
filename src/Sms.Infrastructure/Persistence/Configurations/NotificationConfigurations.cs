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
            builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.AccountIdentifier).HasMaxLength(200);

            // The ciphertext, not the token: data protection's payload is base64 and grows
            // with the value, so this is sized for a long API key rather than for a password.
            builder.Property(x => x.SecretCipher).HasMaxLength(2000);
            builder.Property(x => x.SenderId).HasMaxLength(32);
            builder.Property(x => x.ApiBaseUrl).HasMaxLength(300);
            builder.Property(x => x.LastTestDetail).HasMaxLength(500);

            // IsConfigured is computed from three columns and has no setter — EF would
            // otherwise try to map it and fail on the missing set accessor.
            builder.Ignore(x => x.IsConfigured);

            builder.HasIndex(x => new { x.SchoolId, x.Channel });

            // Failover order is only meaningful if two gateways on one channel cannot claim
            // the same rank — otherwise "lowest first" is decided by whatever order the
            // database happens to return, which is not a decision a school made.
            builder.HasIndex(x => new { x.SchoolId, x.Channel, x.FailoverOrder }, "UX_Provider_Channel_Failover").IsUnique();
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

            // 320 is the longest a mailbox may be (RFC 3696); an E.164 number fits in 16.
            builder.Property(x => x.RecipientAddress).HasMaxLength(320);

            builder.HasOne<TemplateVersion>().WithMany().HasForeignKey(x => x.TemplateVersionId);
            builder.HasOne<Sms.Domain.Messaging.Announcement>().WithMany().HasForeignKey(x => x.AnnouncementId);
            builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecipientUserId);
            builder.HasIndex(x => new { x.RecipientUserId, x.Status });
            builder.HasIndex(x => x.Status);
            // DB/04 §1 (S8/E-802): ops queue — pending/failed deliveries in queue order (doc's QueuedAtUtc is CreatedAtUtc here; Queued=1, Failed=4).
            builder.HasIndex(x => new { x.SchoolId, x.Status, x.CreatedAtUtc }, "IX_Delivery_School_Status_Queued")
                .HasFilter("[Status] IN (1, 4)");
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
