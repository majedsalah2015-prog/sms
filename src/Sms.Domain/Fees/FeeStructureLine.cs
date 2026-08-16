using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Fees
{
    /// <summary>
    /// ppl.FeeStructureLine (doc/Modules/19 §7, BR-FEE-002): the price for
    /// one grade-year profile x category for a year. Doc's DB concept
    /// separates FeeStructure (header) + FeeStructureLine — collapsed
    /// into a single entity here since nothing in this slice needs a
    /// structure-level rollup beyond its lines (a leaner shape, same
    /// simplification spirit as reusing LookupValue instead of a
    /// dedicated Position entity in E-203). Once a Charge references a
    /// line, its Amount is frozen by convention (BR-FEE-002 "posted
    /// charges never reprice") — not physically locked, since Charge
    /// snapshots the amount at posting time rather than pointing live at
    /// the line.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class FeeStructureLine : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int GradeYearProfileId { get; set; }

        public int FeeCategoryId { get; set; }

        [RequiresAuditReason]
        public decimal Amount { get; set; }

        public FeeStructureLineStatus Status { get; set; } = FeeStructureLineStatus.Draft;
    }
}
