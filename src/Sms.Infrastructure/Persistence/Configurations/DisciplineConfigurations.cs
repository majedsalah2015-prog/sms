using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Discipline;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // svc schema per docs/Database/02-ER-Model §6.

    public class BehaviorCodeConfiguration : IEntityTypeConfiguration<BehaviorCode>
    {
        public void Configure(EntityTypeBuilder<BehaviorCode> builder)
        {
            builder.ToTable("BehaviorCode", "svc");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.HasMany(x => x.ViolationTypes).WithOne().HasForeignKey(x => x.BehaviorCodeId);
            builder.HasMany(x => x.MeritTypes).WithOne().HasForeignKey(x => x.BehaviorCodeId);
            builder.HasMany(x => x.ConsequenceTypes).WithOne().HasForeignKey(x => x.BehaviorCodeId);
            builder.HasMany(x => x.Ladder).WithOne().HasForeignKey(x => x.BehaviorCodeId);
            builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.Version }).IsUnique();
        }
    }

    public class ViolationTypeConfiguration : IEntityTypeConfiguration<ViolationType>
    {
        public void Configure(EntityTypeBuilder<ViolationType> builder)
        {
            builder.ToTable("ViolationType", "svc");
            builder.Property(x => x.ArticleRef).HasMaxLength(40).IsRequired();
            builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        }
    }

    public class MeritTypeConfiguration : IEntityTypeConfiguration<MeritType>
    {
        public void Configure(EntityTypeBuilder<MeritType> builder)
        {
            builder.ToTable("MeritType", "svc");
            builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        }
    }

    public class ConsequenceTypeConfiguration : IEntityTypeConfiguration<ConsequenceType>
    {
        public void Configure(EntityTypeBuilder<ConsequenceType> builder)
        {
            builder.ToTable("ConsequenceType", "svc");
            builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        }
    }

    public class LadderStepConfiguration : IEntityTypeConfiguration<LadderStep>
    {
        public void Configure(EntityTypeBuilder<LadderStep> builder)
        {
            builder.ToTable("LadderStep", "svc");
            builder.HasOne<ConsequenceType>().WithMany().HasForeignKey(x => x.ConsequenceTypeId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.BehaviorCodeId, x.Severity, x.RepetitionCount }).IsUnique();
        }
    }

    public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
    {
        public void Configure(EntityTypeBuilder<Incident> builder)
        {
            builder.ToTable("Incident", "svc");
            builder.Property(x => x.IncidentNo).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Narrative).HasMaxLength(4000).IsRequired();
            builder.HasOne<ViolationType>().WithMany().HasForeignKey(x => x.ViolationTypeId);
            builder.HasIndex(x => new { x.SchoolId, x.IncidentNo }).IsUnique();
            builder.HasIndex(x => new { x.StudentId, x.AcademicYearId });
        }
    }

    public class MeritConfiguration : IEntityTypeConfiguration<Merit>
    {
        public void Configure(EntityTypeBuilder<Merit> builder)
        {
            builder.ToTable("Merit", "svc");
            builder.Property(x => x.Note).HasMaxLength(500);
            builder.HasOne<MeritType>().WithMany().HasForeignKey(x => x.MeritTypeId);
        }
    }

    public class DisciplineCaseConfiguration : IEntityTypeConfiguration<DisciplineCase>
    {
        public void Configure(EntityTypeBuilder<DisciplineCase> builder)
        {
            builder.ToTable("Case", "svc");
            builder.Property(x => x.DecisionArticleRef).HasMaxLength(40);
            builder.Property(x => x.DeviationReason).HasMaxLength(500);
            builder.HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId);
            builder.HasMany(x => x.Statements).WithOne().HasForeignKey(x => x.DisciplineCaseId);
        }
    }

    public class CaseStatementConfiguration : IEntityTypeConfiguration<CaseStatement>
    {
        public void Configure(EntityTypeBuilder<CaseStatement> builder)
        {
            builder.ToTable("CaseStatement", "svc");
            builder.Property(x => x.Text).HasMaxLength(4000).IsRequired();
        }
    }

    public class ActionAppliedConfiguration : IEntityTypeConfiguration<ActionApplied>
    {
        public void Configure(EntityTypeBuilder<ActionApplied> builder)
        {
            builder.ToTable("ActionApplied", "svc");
            builder.HasOne<DisciplineCase>().WithMany().HasForeignKey(x => x.DisciplineCaseId);
            builder.HasOne<ConsequenceType>().WithMany().HasForeignKey(x => x.ConsequenceTypeId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class AppealConfiguration : IEntityTypeConfiguration<Appeal>
    {
        public void Configure(EntityTypeBuilder<Appeal> builder)
        {
            builder.ToTable("Appeal", "svc");
            builder.Property(x => x.Grounds).HasMaxLength(4000).IsRequired();
            builder.Property(x => x.DecisionNote).HasMaxLength(2000);
            builder.HasOne<DisciplineCase>().WithMany().HasForeignKey(x => x.DisciplineCaseId);
            builder.HasIndex(x => x.DisciplineCaseId).IsUnique();
        }
    }

    public class PointLedgerEntryConfiguration : IEntityTypeConfiguration<PointLedgerEntry>
    {
        public void Configure(EntityTypeBuilder<PointLedgerEntry> builder)
        {
            builder.ToTable("PointLedger", "svc");
            builder.HasIndex(x => new { x.StudentId, x.AcademicYearId, x.TermId });
        }
    }

    public class BehaviorContractConfiguration : IEntityTypeConfiguration<BehaviorContract>
    {
        public void Configure(EntityTypeBuilder<BehaviorContract> builder)
        {
            builder.ToTable("BehaviorContract", "svc");
            builder.Property(x => x.Terms).HasMaxLength(4000).IsRequired();
        }
    }

    public class KeepApartPairConfiguration : IEntityTypeConfiguration<KeepApartPair>
    {
        public void Configure(EntityTypeBuilder<KeepApartPair> builder)
        {
            builder.ToTable("KeepApartPair", "svc");
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        }
    }

    public class ParentMeetingConfiguration : IEntityTypeConfiguration<ParentMeeting>
    {
        public void Configure(EntityTypeBuilder<ParentMeeting> builder)
        {
            builder.ToTable("ParentMeeting", "svc");
            builder.Property(x => x.Notes).HasMaxLength(2000);
        }
    }
}
