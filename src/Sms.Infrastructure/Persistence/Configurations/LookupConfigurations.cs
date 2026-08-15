using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Lookups;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/03 §A (core (34)) — LookupCategory/LookupValue.

    public class LookupCategoryConfiguration : IEntityTypeConfiguration<LookupCategory>
    {
        public void Configure(EntityTypeBuilder<LookupCategory> builder)
        {
            builder.ToTable("LookupCategory", "core");
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
            });
        }
    }

    public class LookupValueConfiguration : IEntityTypeConfiguration<LookupValue>
    {
        public void Configure(EntityTypeBuilder<LookupValue> builder)
        {
            builder.ToTable("LookupValue", "core");
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.HasOne<LookupCategory>().WithMany().HasForeignKey(x => x.LookupCategoryId);
            builder.HasIndex(x => new { x.LookupCategoryId, x.Code }).IsUnique();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
            });
        }
    }
}
