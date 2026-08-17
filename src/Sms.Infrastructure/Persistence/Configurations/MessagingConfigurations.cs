using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Messaging;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // msg schema per doc/Modules/32 — same schema group as E-007's Notifications entities.

    public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
    {
        public void Configure(EntityTypeBuilder<Announcement> builder)
        {
            builder.ToTable("Announcement", "msg");
            builder.Property(x => x.TitleAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();
            builder.Property(x => x.BodyAr).IsRequired();
            builder.Property(x => x.BodyEn).IsRequired();
        }
    }

    public class CommunicationMatrixConfiguration : IEntityTypeConfiguration<CommunicationMatrix>
    {
        public void Configure(EntityTypeBuilder<CommunicationMatrix> builder)
        {
            builder.ToTable("CommunicationMatrix", "msg");
            builder.Property(x => x.TopicCode).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.TopicCode }).IsUnique();
        }
    }

    public class MessageThreadConfiguration : IEntityTypeConfiguration<MessageThread>
    {
        public void Configure(EntityTypeBuilder<MessageThread> builder)
        {
            builder.ToTable("Thread", "msg");
            builder.Property(x => x.TopicCode).HasMaxLength(50).IsRequired();
        }
    }

    public class ThreadMessageConfiguration : IEntityTypeConfiguration<ThreadMessage>
    {
        public void Configure(EntityTypeBuilder<ThreadMessage> builder)
        {
            builder.ToTable("ThreadMessage", "msg");
            builder.Property(x => x.Body).IsRequired();
            builder.HasOne<MessageThread>().WithMany().HasForeignKey(x => x.ThreadId);
        }
    }

    public class OfficialLetterConfiguration : IEntityTypeConfiguration<OfficialLetter>
    {
        public void Configure(EntityTypeBuilder<OfficialLetter> builder)
        {
            builder.ToTable("OfficialLetter", "msg");
            builder.Property(x => x.LetterNo).HasMaxLength(20).IsRequired();
            builder.Property(x => x.TemplateCode).HasMaxLength(50).IsRequired();
            builder.Property(x => x.BodySnapshot).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.LetterNo }).IsUnique();
        }
    }
}
