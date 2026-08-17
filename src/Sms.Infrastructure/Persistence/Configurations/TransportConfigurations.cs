using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Fees;
using Sms.Domain.Students;
using Sms.Domain.Transport;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // svc schema per docs/Database/02-ER-Model §6 (services spine).

    public class BusConfiguration : IEntityTypeConfiguration<Bus>
    {
        public void Configure(EntityTypeBuilder<Bus> builder)
        {
            builder.ToTable("Bus", "svc");
            builder.Property(x => x.PlateNo).HasMaxLength(20).IsRequired();
            builder.HasMany(x => x.Documents).WithOne().HasForeignKey(x => x.BusId);
            builder.HasIndex(x => new { x.SchoolId, x.PlateNo }).IsUnique();
        }
    }

    public class BusDocumentConfiguration : IEntityTypeConfiguration<BusDocument>
    {
        public void Configure(EntityTypeBuilder<BusDocument> builder)
        {
            builder.ToTable("BusDocument", "svc");
            builder.HasIndex(x => new { x.BusId, x.Kind });
        }
    }

    public class TransportStaffConfiguration : IEntityTypeConfiguration<TransportStaff>
    {
        public void Configure(EntityTypeBuilder<TransportStaff> builder)
        {
            builder.ToTable("TransportStaff", "svc");
            builder.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
            builder.Property(x => x.ContractorName).HasMaxLength(150);
            builder.Property(x => x.LicenseNo).HasMaxLength(40);
        }
    }

    public class RouteConfiguration : IEntityTypeConfiguration<Route>
    {
        public void Configure(EntityTypeBuilder<Route> builder)
        {
            builder.ToTable("Route", "svc");
            builder.Property(x => x.RouteNo).HasMaxLength(20).IsRequired();
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.HasOne<Bus>().WithMany().HasForeignKey(x => x.BusId);
            builder.HasOne<TransportStaff>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TransportStaff>().WithMany().HasForeignKey(x => x.AttendantId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Stops).WithOne().HasForeignKey(x => x.RouteId);
            builder.HasIndex(x => new { x.SchoolId, x.RouteNo }).IsUnique();
        }
    }

    public class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
    {
        public void Configure(EntityTypeBuilder<RouteStop> builder)
        {
            builder.ToTable("RouteStop", "svc");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.HasOne<FeeCategory>().WithMany().HasForeignKey(x => x.ZoneFeeCategoryId);
            builder.HasIndex(x => new { x.RouteId, x.SequenceNumber }).IsUnique();
        }
    }

    public class TransportSubscriptionConfiguration : IEntityTypeConfiguration<TransportSubscription>
    {
        public void Configure(EntityTypeBuilder<TransportSubscription> builder)
        {
            builder.ToTable("TransportSubscription", "svc");
            builder.Property(x => x.SuspensionReason).HasMaxLength(500);
            builder.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId);
            builder.HasOne<RouteStop>().WithMany().HasForeignKey(x => x.AmRouteStopId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<RouteStop>().WithMany().HasForeignKey(x => x.PmRouteStopId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
            builder.HasIndex(x => x.EnrollmentId);
        }
    }

    public class RouteWaitlistConfiguration : IEntityTypeConfiguration<RouteWaitlist>
    {
        public void Configure(EntityTypeBuilder<RouteWaitlist> builder)
        {
            builder.ToTable("RouteWaitlist", "svc");
            builder.HasOne<Route>().WithMany().HasForeignKey(x => x.RouteId);
            builder.HasOne<TransportSubscription>().WithMany().HasForeignKey(x => x.TransportSubscriptionId);
        }
    }

    public class TripConfiguration : IEntityTypeConfiguration<Trip>
    {
        public void Configure(EntityTypeBuilder<Trip> builder)
        {
            builder.ToTable("Trip", "svc");
            builder.HasOne<Route>().WithMany().HasForeignKey(x => x.RouteId);
            builder.HasMany(x => x.Logs).WithOne().HasForeignKey(x => x.TripId);
            builder.HasIndex(x => new { x.RouteId, x.Date }).IsUnique();
        }
    }

    public class TripLogConfiguration : IEntityTypeConfiguration<TripLog>
    {
        public void Configure(EntityTypeBuilder<TripLog> builder)
        {
            builder.ToTable("TripLog", "svc");
            builder.Property(x => x.ReceivedByName).HasMaxLength(150);
            builder.HasIndex(x => new { x.TripId, x.StudentId });
        }
    }

    public class SafetyEventConfiguration : IEntityTypeConfiguration<SafetyEvent>
    {
        public void Configure(EntityTypeBuilder<SafetyEvent> builder)
        {
            builder.ToTable("SafetyEvent", "svc");
            builder.Property(x => x.Note).HasMaxLength(1000);
            builder.HasOne<Trip>().WithMany().HasForeignKey(x => x.TripId);
        }
    }
}
