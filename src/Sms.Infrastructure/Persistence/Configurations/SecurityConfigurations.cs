using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Security;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // sec schema per docs/Database/01 §3 and 03 §A11. Schema/table explicit,
    // enums SMALLINT via the enum's underlying short, unique keys per spec.

    public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
    {
        public void Configure(EntityTypeBuilder<UserAccount> builder)
        {
            builder.ToTable("UserAccount", "sec");
            builder.Property(x => x.UserName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.PasswordHash).HasMaxLength(200);
            builder.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.UserName }).IsUnique();
        }
    }

    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("Role", "sec");
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
            });
        }
    }

    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("Permission", "sec");
            builder.Property(x => x.ModuleCode).HasMaxLength(30).IsRequired();
            builder.Property(x => x.ScreenCode).HasMaxLength(60).IsRequired();
            builder.HasIndex(x => new { x.ModuleCode, x.ScreenCode, x.Action }).IsUnique();
        }
    }

    public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
    {
        public void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            builder.ToTable("RolePermission", "sec");
            builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
            builder.HasOne<Role>().WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId);
            builder.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId);
        }
    }

    public class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
    {
        public void Configure(EntityTypeBuilder<RoleAssignment> builder)
        {
            builder.ToTable("RoleAssignment", "sec");
            builder.HasIndex(x => new { x.UserAccountId, x.RoleId }).IsUnique();
            builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
            builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId);
        }
    }

    public class ScopeGrantConfiguration : IEntityTypeConfiguration<ScopeGrant>
    {
        public void Configure(EntityTypeBuilder<ScopeGrant> builder)
        {
            builder.ToTable("ScopeGrant", "sec");
            builder.HasOne<RoleAssignment>().WithMany(a => a.ScopeGrants).HasForeignKey(x => x.RoleAssignmentId);
        }
    }

    // E-003 authentication slice (doc 06 §3, BR-SEC-001..004).

    public class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
    {
        public void Configure(EntityTypeBuilder<PasswordHistory> builder)
        {
            builder.ToTable("PasswordHistory", "sec");
            builder.Property(x => x.PasswordHash).IsRequired();
            builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId);
            builder.HasIndex(x => new { x.UserAccountId, x.CreatedAtUtc });
        }
    }

    public class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
    {
        public void Configure(EntityTypeBuilder<LoginAttempt> builder)
        {
            builder.ToTable("LoginAttempt", "sec");
            builder.Property(x => x.UserNameAttempted).HasMaxLength(100).IsRequired();
            builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId);
            builder.HasIndex(x => new { x.SchoolId, x.UserNameAttempted, x.CreatedAtUtc });
        }
    }

    public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
    {
        public void Configure(EntityTypeBuilder<UserSession> builder)
        {
            builder.ToTable("UserSession", "sec");
            builder.Property(x => x.SessionToken).HasMaxLength(64).IsRequired();
            builder.HasIndex(x => x.SessionToken).IsUnique();
            builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId);
            builder.HasIndex(x => new { x.UserAccountId, x.RevokedAtUtc });
        }
    }

    public class TwoFactorEnrollmentConfiguration : IEntityTypeConfiguration<TwoFactorEnrollment>
    {
        public void Configure(EntityTypeBuilder<TwoFactorEnrollment> builder)
        {
            builder.ToTable("TwoFactorEnrollment", "sec");
            builder.Property(x => x.SecretKey).IsRequired();
            builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId);
            builder.HasIndex(x => new { x.UserAccountId, x.Method }).IsUnique();
        }
    }
}
