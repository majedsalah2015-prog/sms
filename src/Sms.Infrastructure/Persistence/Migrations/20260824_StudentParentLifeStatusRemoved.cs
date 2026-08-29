using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The father's and mother's life status stop being two columns on every child (owner request,
    /// 2026-08-24). They were the last thing about a parent that ppl.Student still carried, and the
    /// same fact is already on ppl.Parent as <c>LifeStatus</c> — one row per person, reached from the
    /// student's Parents &amp; guardians tab, where a correction lands on every sibling at once.
    /// <para>
    /// Companion to <c>20260824_ParentQualificationAndMotherAsGuardian</c>, which moved the mother's
    /// name, ID number, mobile, occupation and qualification the same way and for the same reason.
    /// </para>
    /// <para>
    /// <strong>This drops data.</strong> A status already recorded on a student is gone with the
    /// columns and is not moved anywhere automatically: the student row does not know which Parent
    /// row its "father" value was about, and guessing from the guardian links would write a
    /// martyr or a missing person onto the wrong file. Re-enter it on the parent's own file.
    /// </para>
    /// <para>
    /// The case this gives up is a parent with no file at all, whose status could previously be
    /// recorded against the child alone. The school records it by opening a parent file, which is
    /// where the rest of that parent's data has to go regardless.
    /// </para>
    /// </summary>
    public partial class StudentParentLifeStatusRemoved : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FatherStatus",
                schema: "ppl",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "MotherStatus",
                schema: "ppl",
                table: "Student");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "FatherStatus",
                schema: "ppl",
                table: "Student",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "MotherStatus",
                schema: "ppl",
                table: "Student",
                type: "smallint",
                nullable: true);
        }
    }
}
