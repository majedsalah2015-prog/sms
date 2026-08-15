using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Attachments;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // doc schema per docs/Database/03 §A ("doc (6)"). Slice 1 covers the
    // upload/version pipeline; ChecklistDefinition/ChecklistItemState/
    // PurgeCertificate are a later E-008 slice.

    public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
    {
        public void Configure(EntityTypeBuilder<DocumentType> builder)
        {
            builder.ToTable("DocumentType", "doc");
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.Property(x => x.ModuleCode).HasMaxLength(30).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
            });
        }
    }

    public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.ToTable("Attachment", "doc");
            builder.Property(x => x.OwningEntityType).HasMaxLength(60).IsRequired();
            builder.Property(x => x.TitleAr).HasMaxLength(200);
            builder.Property(x => x.TitleEn).HasMaxLength(200);
            builder.Property(x => x.VoidReason).HasMaxLength(500);
            builder.HasOne<DocumentType>().WithMany().HasForeignKey(x => x.DocumentTypeId);
            builder.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.AttachmentId);
            builder.HasIndex(x => new { x.OwningEntityType, x.OwningEntityId, x.DocumentTypeId });
        }
    }

    public class AttachmentVersionConfiguration : IEntityTypeConfiguration<AttachmentVersion>
    {
        public void Configure(EntityTypeBuilder<AttachmentVersion> builder)
        {
            builder.ToTable("AttachmentVersion", "doc");
            builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.StorageReference).HasMaxLength(500).IsRequired();
            builder.HasIndex(x => new { x.AttachmentId, x.VersionNumber }).IsUnique();
        }
    }
}
