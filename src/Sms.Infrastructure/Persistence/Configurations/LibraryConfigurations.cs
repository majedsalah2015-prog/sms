using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Fees;
using Sms.Domain.Library;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // svc schema per docs/Database/02-ER-Model §6.

    public class TitleConfiguration : IEntityTypeConfiguration<Title>
    {
        public void Configure(EntityTypeBuilder<Title> builder)
        {
            builder.ToTable("Title", "svc");
            builder.Property(x => x.TitleAr).HasMaxLength(300);
            builder.Property(x => x.TitleEn).HasMaxLength(300);
            builder.Property(x => x.Transliteration).HasMaxLength(300);
            builder.Property(x => x.Author).HasMaxLength(200);
            builder.Property(x => x.Isbn).HasMaxLength(20);
            builder.Property(x => x.DeweyClass).HasMaxLength(20);
            builder.Property(x => x.SubjectTags).HasMaxLength(500);
            builder.HasMany(x => x.Copies).WithOne().HasForeignKey(x => x.TitleId);
        }
    }

    public class CopyConfiguration : IEntityTypeConfiguration<Copy>
    {
        public void Configure(EntityTypeBuilder<Copy> builder)
        {
            builder.ToTable("Copy", "svc");
            builder.Property(x => x.Barcode).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Cost).HasColumnType("decimal(18,2)");
            builder.Property(x => x.ShelfLocation).HasMaxLength(50);
            builder.HasIndex(x => new { x.SchoolId, x.Barcode }).IsUnique();
        }
    }

    public class MemberPolicyConfiguration : IEntityTypeConfiguration<MemberPolicy>
    {
        public void Configure(EntityTypeBuilder<MemberPolicy> builder)
        {
            builder.ToTable("MemberPolicy", "svc");
            builder.Property(x => x.FinePerDay).HasColumnType("decimal(9,2)");
            builder.Property(x => x.FineCap).HasColumnType("decimal(9,2)");
            builder.HasIndex(x => new { x.SchoolId, x.MemberKind, x.StageId }).IsUnique();
        }
    }

    public class LoanConfiguration : IEntityTypeConfiguration<Loan>
    {
        public void Configure(EntityTypeBuilder<Loan> builder)
        {
            builder.ToTable("Loan", "svc");
            builder.HasOne<Copy>().WithMany().HasForeignKey(x => x.CopyId);
            builder.HasIndex(x => new { x.MemberKind, x.MemberId });
            builder.HasIndex(x => new { x.CopyId, x.ReturnedAtUtc });
        }
    }

    public class CirculationEventConfiguration : IEntityTypeConfiguration<CirculationEvent>
    {
        public void Configure(EntityTypeBuilder<CirculationEvent> builder)
        {
            builder.ToTable("CirculationEvent", "svc");
            builder.Property(x => x.Note).HasMaxLength(500);
            builder.HasOne<Loan>().WithMany().HasForeignKey(x => x.LoanId);
        }
    }

    public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
    {
        public void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.ToTable("Reservation", "svc");
            builder.HasOne<Title>().WithMany().HasForeignKey(x => x.TitleId);
            builder.HasOne<Copy>().WithMany().HasForeignKey(x => x.HeldCopyId);
            builder.HasIndex(x => new { x.TitleId, x.Status });
        }
    }

    public class FineProposalConfiguration : IEntityTypeConfiguration<FineProposal>
    {
        public void Configure(EntityTypeBuilder<FineProposal> builder)
        {
            builder.ToTable("FineProposal", "svc");
            builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            builder.HasOne<Loan>().WithMany().HasForeignKey(x => x.LoanId);
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
            builder.HasOne<CreditNote>().WithMany().HasForeignKey(x => x.CreditNoteId);
        }
    }

    public class StocktakeSessionConfiguration : IEntityTypeConfiguration<StocktakeSession>
    {
        public void Configure(EntityTypeBuilder<StocktakeSession> builder)
        {
            builder.ToTable("StocktakeSession", "svc");
            builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.StocktakeSessionId);
        }
    }

    public class StocktakeLineConfiguration : IEntityTypeConfiguration<StocktakeLine>
    {
        public void Configure(EntityTypeBuilder<StocktakeLine> builder)
        {
            builder.ToTable("StocktakeLine", "svc");
            builder.Property(x => x.Resolution).HasMaxLength(500);
            builder.HasOne<Copy>().WithMany().HasForeignKey(x => x.CopyId);
            builder.HasIndex(x => new { x.StocktakeSessionId, x.CopyId }).IsUnique();
        }
    }

    public class ReadingLogConfiguration : IEntityTypeConfiguration<ReadingLog>
    {
        public void Configure(EntityTypeBuilder<ReadingLog> builder)
        {
            builder.ToTable("ReadingLog", "svc");
            builder.Property(x => x.Note).HasMaxLength(500);
            builder.HasOne<Title>().WithMany().HasForeignKey(x => x.TitleId);
        }
    }
}
