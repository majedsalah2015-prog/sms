using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Dashboards;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per doc/Modules/31 — same schema group as Security/Timetable.

    public class WidgetDefinitionConfiguration : IEntityTypeConfiguration<WidgetDefinition>
    {
        public void Configure(EntityTypeBuilder<WidgetDefinition> builder)
        {
            builder.ToTable("WidgetDefinition", "core");
            builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
            builder.Property(x => x.OwningModuleCode).HasMaxLength(20).IsRequired();
            builder.Property(x => x.TitleAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.TitleEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.DrillTargetCode).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
        }
    }

    public class LayoutTemplateConfiguration : IEntityTypeConfiguration<LayoutTemplate>
    {
        public void Configure(EntityTypeBuilder<LayoutTemplate> builder)
        {
            builder.ToTable("LayoutTemplate", "core");
            builder.HasIndex(x => new { x.SchoolId, x.RoleId }).IsUnique();
        }
    }

    public class LayoutTemplateWidgetConfiguration : IEntityTypeConfiguration<LayoutTemplateWidget>
    {
        public void Configure(EntityTypeBuilder<LayoutTemplateWidget> builder)
        {
            builder.ToTable("LayoutTemplateWidget", "core");
            builder.HasOne<LayoutTemplate>().WithMany().HasForeignKey(x => x.LayoutTemplateId);
            builder.HasOne<WidgetDefinition>().WithMany().HasForeignKey(x => x.WidgetDefinitionId);
            builder.HasIndex(x => new { x.LayoutTemplateId, x.WidgetDefinitionId }).IsUnique();
        }
    }

    public class UserLayoutConfiguration : IEntityTypeConfiguration<UserLayout>
    {
        public void Configure(EntityTypeBuilder<UserLayout> builder)
        {
            builder.ToTable("UserLayout", "core");
            builder.HasOne<WidgetDefinition>().WithMany().HasForeignKey(x => x.WidgetDefinitionId);
            builder.HasIndex(x => new { x.UserAccountId, x.WidgetDefinitionId }).IsUnique();
        }
    }
}
