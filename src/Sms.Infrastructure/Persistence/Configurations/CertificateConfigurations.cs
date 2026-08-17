using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Certificates;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per doc/Modules/18 — same schema group as Student/Application/Charge.

    public class CertificateTypeConfiguration : IEntityTypeConfiguration<CertificateType>
    {
        public void Configure(EntityTypeBuilder<CertificateType> builder)
        {
            builder.ToTable("CertificateType", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NumberingSeriesCode).HasMaxLength(10).IsRequired();
        }
    }

    public class CertificateRequestConfiguration : IEntityTypeConfiguration<CertificateRequest>
    {
        public void Configure(EntityTypeBuilder<CertificateRequest> builder)
        {
            builder.ToTable("CertificateRequest", "ppl");
            builder.HasOne<CertificateType>().WithMany().HasForeignKey(x => x.CertificateTypeId);
            builder.HasIndex(x => x.StudentId);
            builder.Property(x => x.ClearanceOverrideReason).HasMaxLength(500);
        }
    }

    public class CertificateIssueConfiguration : IEntityTypeConfiguration<CertificateIssue>
    {
        public void Configure(EntityTypeBuilder<CertificateIssue> builder)
        {
            builder.ToTable("CertificateIssue", "ppl");
            builder.Property(x => x.CertificateNo).HasMaxLength(20).IsRequired();
            builder.Property(x => x.VerificationCode).HasMaxLength(32).IsRequired();
            builder.Property(x => x.DataSnapshotJson).IsRequired();
            builder.HasOne<CertificateRequest>().WithMany().HasForeignKey(x => x.CertificateRequestId);
            builder.HasIndex(x => new { x.SchoolId, x.CertificateNo }).IsUnique();
            builder.HasIndex(x => x.VerificationCode).IsUnique();
            builder.HasOne<CertificateIssue>().WithMany().HasForeignKey(x => x.ReissuedFromCertificateIssueId);
        }
    }

    public class VerificationLogConfiguration : IEntityTypeConfiguration<VerificationLog>
    {
        public void Configure(EntityTypeBuilder<VerificationLog> builder)
        {
            builder.ToTable("VerificationLog", "ppl");
            builder.Property(x => x.SubmittedCode).HasMaxLength(32).IsRequired();
            builder.HasOne<CertificateIssue>().WithMany().HasForeignKey(x => x.CertificateIssueId);
        }
    }
}
