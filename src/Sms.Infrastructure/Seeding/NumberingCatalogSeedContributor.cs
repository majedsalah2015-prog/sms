using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Common.Interfaces;
using Sms.Application.Numbering;
using Sms.Application.Seeding;
using Sms.Domain.Numbering;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// doc 08 §4's standard series catalog (E-006's deferred remaining item).
    /// Only Student No. and Receipt No. have doc-given format templates;
    /// the rest follow the same {PREFIX}-{YEAR|GYEAR}-{SEQ:n} shape as a
    /// starter default (doc 08 §6's series designer is where a school
    /// actually adjusts these). Student No. flat with no year per doc 08 §9
    /// Q2's own stated default recommendation.
    /// </summary>
    public class NumberingCatalogSeedContributor : ISeedContributor
    {
        private readonly INumberingSeriesAdmin _admin;
        private readonly IClock _clock;

        public NumberingCatalogSeedContributor(INumberingSeriesAdmin admin, IClock clock)
        {
            _admin = admin;
            _clock = clock;
        }

        public string Name => "Numbering series catalog (doc 08 §4)";

        public int Order => 30;

        private static readonly (string Code, string Entity, string Format, ResetPolicy Reset, GapPolicy Gap)[] Catalog =
        {
            ("STU", "Student", "STU-{SEQ:6}", ResetPolicy.Never, GapPolicy.Normal),
            ("EMP", "Employee", "EMP-{SEQ:5}", ResetPolicy.Never, GapPolicy.Normal),
            ("PAR", "Parent", "PAR-{SEQ:6}", ResetPolicy.Never, GapPolicy.Normal),
            ("APP", "AdmissionApplication", "APP-{YEAR}-{SEQ:5}", ResetPolicy.PerAcademicYear, GapPolicy.Normal),
            ("INV", "Charge", "INV-{GYEAR}-{SEQ:6}", ResetPolicy.PerCalendarYear, GapPolicy.Strict),
            ("RCP", "Receipt", "RCP/{SCHOOL}/{GYEAR}/{SEQ:6}", ResetPolicy.PerCalendarYear, GapPolicy.Strict),
            ("RFD", "RefundVoucher", "RFD-{GYEAR}-{SEQ:5}", ResetPolicy.PerCalendarYear, GapPolicy.Strict),
            ("CRN", "CreditNote", "CRN-{GYEAR}-{SEQ:5}", ResetPolicy.PerCalendarYear, GapPolicy.Strict),
            ("DSC", "DiscountDocument", "DSC-{GYEAR}-{SEQ:5}", ResetPolicy.PerCalendarYear, GapPolicy.Strict),
            ("STM", "StatementIssue", "STM-{GYEAR}-{SEQ:6}", ResetPolicy.PerCalendarYear, GapPolicy.Normal),
            ("GLX", "GlExportBatch", "GLX-{GYEAR}-{SEQ:4}", ResetPolicy.PerCalendarYear, GapPolicy.Strict),
            ("CERT", "Certificate", "CERT-{YEAR}-{SEQ:5}", ResetPolicy.PerAcademicYear, GapPolicy.Strict),
            ("TC", "TransferCertificate", "TC-{YEAR}-{SEQ:4}", ResetPolicy.PerAcademicYear, GapPolicy.Strict),
            ("INC", "DisciplineIncident", "INC-{YEAR}-{SEQ:4}", ResetPolicy.PerAcademicYear, GapPolicy.Normal),
            ("MED", "ClinicVisit", "MED-{YEAR}-{SEQ:5}", ResetPolicy.PerAcademicYear, GapPolicy.Normal),
            ("LVE", "EmployeeLeave", "LVE-{YEAR}-{SEQ:4}", ResetPolicy.PerAcademicYear, GapPolicy.Normal),

            // Payroll and staff advances (owner request, 2026-08-28). Both reset per calendar year
            // because both are calendar-month documents, not academic-year ones — salaries run
            // through the summer. PAY is strict for the same reason RCP is: a payroll run is a
            // money document and a hole in its sequence is a question somebody has to answer. ADV
            // is Normal because a withdrawn request should not oblige anyone to explain a gap.
            ("PAY", "PayrollRun", "PAY-{GYEAR}-{SEQ:4}", ResetPolicy.PerCalendarYear, GapPolicy.Strict),
            ("ADV", "SalaryAdvance", "ADV-{GYEAR}-{SEQ:5}", ResetPolicy.PerCalendarYear, GapPolicy.Normal),
            // Arrears notices issued by hand from the collection follow-up screen (doc/Modules/20
            // §8.5). Per academic year because arrears are chased within a school year, and Normal
            // gap policy because an officer who starts a batch and abandons it should not oblige
            // anyone to explain a missing number — unlike a receipt, no money moved.
            ("DUN", "CollectionNotice", "DUN-{YEAR}-{SEQ:5}", ResetPolicy.PerAcademicYear, GapPolicy.Normal),

            ("MSG", "OfficialMessage", "MSG-{YEAR}-{SEQ:6}", ResetPolicy.PerAcademicYear, GapPolicy.Normal),
            ("EXM", "Exam", "EXM-{YEAR}-{SEQ:3}", ResetPolicy.PerAcademicYear, GapPolicy.Normal),
            ("RTE", "TransportRoute", "RTE-{SEQ:3}", ResetPolicy.Never, GapPolicy.Normal),
            ("AST", "CatalogItem", "AST-{SEQ:6}", ResetPolicy.Never, GapPolicy.Normal),
        };

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in Catalog)
            {
                await _admin.DefineSeriesAsync(
                    entry.Code, entry.Entity, entry.Format, entry.Reset, entry.Gap, _clock.UtcNow, cancellationToken);
            }
        }
    }
}
