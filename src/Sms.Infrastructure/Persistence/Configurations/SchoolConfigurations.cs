using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Schools;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/03 §A — School/SchoolGroup/Signatory (doc/Modules/02).

    public class SchoolGroupConfiguration : IEntityTypeConfiguration<SchoolGroup>
    {
        public void Configure(EntityTypeBuilder<SchoolGroup> builder)
        {
            builder.ToTable("SchoolGroup", "core");
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
            });
        }
    }

    public class SchoolConfiguration : IEntityTypeConfiguration<School>
    {
        public void Configure(EntityTypeBuilder<School> builder)
        {
            builder.ToTable("School", "core");
            builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
            builder.Property(x => x.LicenseNumber).HasMaxLength(50).IsRequired();
            builder.Property(x => x.MinistryCode).HasMaxLength(50).IsRequired();
            builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            builder.HasOne<SchoolGroup>().WithMany().HasForeignKey(x => x.SchoolGroupId);
            builder.HasOne<Sms.Domain.Setup.CountryPack>().WithMany().HasForeignKey(x => x.CountryPackId);
        }
    }

    public class SignatoryConfiguration : IEntityTypeConfiguration<Signatory>
    {
        public void Configure(EntityTypeBuilder<Signatory> builder)
        {
            builder.ToTable("Signatory", "core");
            builder.Property(x => x.DocumentClassCode).HasMaxLength(30).IsRequired();
            builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
            builder.Property(x => x.TitleAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.TitleEn).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.DocumentClassCode, x.EffectiveToUtc });
        }
    }
}
