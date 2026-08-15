using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Numbering;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/04 §3 ("core.NumberingSeries").

    public class NumberingSeriesConfiguration : IEntityTypeConfiguration<NumberingSeries>
    {
        public void Configure(EntityTypeBuilder<NumberingSeries> builder)
        {
            builder.ToTable("NumberingSeries", "core");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.FormatTemplate).HasMaxLength(100).IsRequired();
            // Old, deactivated versions stay behind for continuity reporting (doc 08 §3/§7).
            builder.HasIndex(x => new { x.SchoolId, x.Code, x.Version }).IsUnique();
        }
    }

    public class SeriesStateConfiguration : IEntityTypeConfiguration<SeriesState>
    {
        public void Configure(EntityTypeBuilder<SeriesState> builder)
        {
            builder.ToTable("SeriesState", "core");
            builder.Property(x => x.ResetKey).HasMaxLength(20).IsRequired();
            builder.HasOne<NumberingSeries>().WithMany().HasForeignKey(x => x.NumberingSeriesId);
            builder.HasIndex(x => new { x.NumberingSeriesId, x.ResetKey }).IsUnique();

            // BR-NUM-003: EF includes the original value in the UPDATE's WHERE
            // clause, so a losing concurrent writer's whole SaveChanges (its
            // business row included) fails atomically instead of silently
            // overwriting or skipping a sequence value — gap-free without a
            // SQL Server-specific rowversion type or sp_getapplock. See
            // NumberIssuer for why the loser must retry its whole posting.
            builder.Property(x => x.LastIssuedSequence).IsConcurrencyToken();
        }
    }
}
