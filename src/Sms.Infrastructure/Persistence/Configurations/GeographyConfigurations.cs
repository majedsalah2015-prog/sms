using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Geography;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema: reference data the school maintains, alongside Building/Floor/LookupValue.

    public class GovernorateConfiguration : IEntityTypeConfiguration<Governorate>
    {
        public void Configure(EntityTypeBuilder<Governorate> builder)
        {
            builder.ToTable("Governorate", "core");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
            builder.HasMany(x => x.Areas).WithOne().HasForeignKey(x => x.GovernorateId);
        }
    }

    public class ResidenceAreaConfiguration : IEntityTypeConfiguration<ResidenceArea>
    {
        public void Configure(EntityTypeBuilder<ResidenceArea> builder)
        {
            builder.ToTable("ResidenceArea", "core");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });

            // Unique per governorate, not per school: two governorates may each have a "Central" area,
            // and forcing globally distinct codes would push the parent's name into the child's code.
            builder.HasIndex(x => new { x.GovernorateId, x.Code }).IsUnique();
            builder.HasMany(x => x.Neighbourhoods).WithOne().HasForeignKey(x => x.ResidenceAreaId);
        }
    }

    public class NeighbourhoodConfiguration : IEntityTypeConfiguration<Neighbourhood>
    {
        public void Configure(EntityTypeBuilder<Neighbourhood> builder)
        {
            builder.ToTable("Neighbourhood", "core");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });
            builder.HasIndex(x => new { x.ResidenceAreaId, x.Code }).IsUnique();
        }
    }
}
