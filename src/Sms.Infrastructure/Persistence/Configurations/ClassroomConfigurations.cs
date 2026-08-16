using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Classrooms;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/03 §A — Building/Floor/Room/RoomFeature/RoomAvailabilityException/RoomBooking (doc/Modules/08).

    public class BuildingConfiguration : IEntityTypeConfiguration<Building>
    {
        public void Configure(EntityTypeBuilder<Building> builder)
        {
            builder.ToTable("Building", "core");
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });
        }
    }

    public class FloorConfiguration : IEntityTypeConfiguration<Floor>
    {
        public void Configure(EntityTypeBuilder<Floor> builder)
        {
            builder.ToTable("Floor", "core");
            builder.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId);
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });
        }
    }

    public class RoomConfiguration : IEntityTypeConfiguration<Room>
    {
        public void Configure(EntityTypeBuilder<Room> builder)
        {
            builder.ToTable("Room", "core");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
            builder.HasOne<Floor>().WithMany().HasForeignKey(x => x.FloorId);
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });
        }
    }

    public class RoomFeatureConfiguration : IEntityTypeConfiguration<RoomFeature>
    {
        public void Configure(EntityTypeBuilder<RoomFeature> builder)
        {
            builder.ToTable("RoomFeature", "core");
            builder.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId);
            builder.HasIndex(x => new { x.RoomId, x.FeatureLookupId }).IsUnique();
        }
    }

    public class RoomAvailabilityExceptionConfiguration : IEntityTypeConfiguration<RoomAvailabilityException>
    {
        public void Configure(EntityTypeBuilder<RoomAvailabilityException> builder)
        {
            builder.ToTable("RoomAvailabilityException", "core");
            builder.Property(x => x.Notes).HasMaxLength(500);
            builder.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId);
            builder.HasIndex(x => new { x.RoomId, x.StartDate });
        }
    }

    public class RoomBookingConfiguration : IEntityTypeConfiguration<RoomBooking>
    {
        public void Configure(EntityTypeBuilder<RoomBooking> builder)
        {
            builder.ToTable("RoomBooking", "core");
            builder.Property(x => x.Purpose).HasMaxLength(200).IsRequired();
            builder.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId);
            builder.HasIndex(x => new { x.RoomId, x.StartUtc });
        }
    }
}
