using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Parents;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per doc/Modules/11 §7.

    public class ParentConfiguration : IEntityTypeConfiguration<Parent>
    {
        public void Configure(EntityTypeBuilder<Parent> builder)
        {
            builder.ToTable("Parent", "ppl");
            builder.Property(x => x.ParentFileNo).HasMaxLength(20).IsRequired();
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.PrimaryMobile).HasMaxLength(20).IsRequired();
            builder.Property(x => x.PrimaryIdNo).HasMaxLength(30);
            builder.Property(x => x.LifeStatusNote).HasMaxLength(200);
            builder.HasIndex(x => new { x.SchoolId, x.ParentFileNo }).IsUnique();

            // BR-PAR-002's strongest match, indexed so the check is cheap enough to run
            // on every creation path. Deliberately NOT unique: the rule is that an exact
            // match blocks creation and links instead, which is the dedup pipeline's job
            // and is still deferred — a unique index would instead make an import die on
            // a DbUpdateException, which is the same refusal delivered as a crash.
            builder.HasIndex(x => new { x.SchoolId, x.PrimaryIdNo }).HasDatabaseName("IX_Parent_PrimaryIdNo")
                .HasFilter("[PrimaryIdNo] IS NOT NULL");

            // No FK to either residence level, matching how Student already points at its neighbourhood:
            // reference data is soft-deactivated rather than deleted, and a hard FK would turn retiring a
            // locality into a delete that a live parent row refuses.
            builder.HasIndex(x => x.ResidenceAreaId).HasDatabaseName("IX_Parent_ResidenceArea")
                .HasFilter("[ResidenceAreaId] IS NOT NULL");
        }
    }
}
