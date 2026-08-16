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
            builder.HasIndex(x => new { x.SchoolId, x.ParentFileNo }).IsUnique();
        }
    }
}
