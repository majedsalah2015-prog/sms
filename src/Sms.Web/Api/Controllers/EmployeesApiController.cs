using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Employees;
using Sms.Application.Payroll;
using Sms.Application.Security;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Api.Models;
using Sms.Web.Security;

namespace Sms.Web.Api.Controllers
{
    /// <summary>
    /// doc/Modules/12 §8 for the app — the staff directory, the employee file,
    /// the contract manager and the payroll statements, over the same
    /// <see cref="IEmployeeAdmin"/> and <see cref="IPayrollStatements"/> the
    /// browser screens use.
    /// <para>
    /// <b>Pay is a restricted category and is kept behind its own permissions.</b>
    /// BR-EMP-003 / BR-EMP-010 make salary HR-and-Principal only, so a contract
    /// never appears on the file response (which needs only
    /// <c>Employees/File/View</c>) and the register, the payslips and the
    /// advances each sit behind <c>Employees/Contracts</c>,
    /// <c>Employees/Payroll</c> and <c>Employees/Advances</c> exactly as they do
    /// in the browser.
    /// </para>
    /// <para>
    /// <b>Stated gap.</b> There is no self-service payslip here — "my own
    /// payslip" would need a permission the catalogue does not define, and
    /// inventing one on a second transport is a security decision made by
    /// accident. An employee reads their payslip today through a role that holds
    /// <c>Employees/Payroll/View</c>, which is the school's whole staff-pay
    /// grant. Narrowing it is a <c>ScreenCatalog</c> change and its own slice.
    /// </para>
    /// </summary>
    [Route(V1)]
    public sealed class EmployeesApiController : ApiControllerBase
    {
        private readonly IEmployeeAdmin _employees;
        private readonly IPayrollStatements _statements;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;

        public EmployeesApiController(
            IEmployeeAdmin employees,
            IPayrollStatements statements,
            AppDbContext db,
            IAuditContext audit,
            IClock clock)
        {
            _employees = employees;
            _statements = statements;
            _db = db;
            _audit = audit;
            _clock = clock;
        }

        // ---------------------------------------------------------------- §8.1 directory

        /// <summary>The staff contact card list. Search reads every name part in both languages plus the employee number.</summary>
        [HttpGet("employees")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.View)]
        public async Task<ActionResult<ApiPage<ApiEmployeeRow>>> Directory(
            string? q = null, string? status = null, int? orgUnitId = null, int? page = null, int? pageSize = null)
        {
            var (p, size) = ApiPaging.Clamp(page, pageSize);

            // IgnoreQueryFilters + an explicit school predicate: a terminated employee is
            // IsActive = false and still belongs in the directory a school searches.
            var query = _db.Employees.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.SchoolId == _db.CurrentSchoolId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                // Lowered on both sides rather than left to the provider: Sqlite's `instr`
                // is case-sensitive and SQL Server's default collation is not, and a search
                // folded at the provider behaves one way in the tests and another in the school.
                foreach (var word in q.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var needle = word.ToLowerInvariant();
                    query = query.Where(e =>
                        e.EmployeeNo.ToLower().Contains(needle)
                        || e.FirstNameAr.ToLower().Contains(needle)
                        || e.FatherNameAr.ToLower().Contains(needle)
                        || e.GrandfatherNameAr.ToLower().Contains(needle)
                        || e.FamilyNameAr.ToLower().Contains(needle)
                        || e.FirstNameEn.ToLower().Contains(needle)
                        || e.FatherNameEn.ToLower().Contains(needle)
                        || e.GrandfatherNameEn.ToLower().Contains(needle)
                        || e.FamilyNameEn.ToLower().Contains(needle));
                }
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<EmployeeStatus>(status, ignoreCase: true, out var wanted))
            {
                query = query.Where(e => e.Status == wanted);
            }

            if (orgUnitId.HasValue)
            {
                var inUnit = await _db.EmployeeAssignments.AsNoTracking()
                    .Where(a => a.OrgUnitId == orgUnitId.Value && a.EffectiveToUtc == null)
                    .Select(a => a.EmployeeId)
                    .ToListAsync(Ct);
                query = query.Where(e => inUnit.Contains(e.Id));
            }

            var total = await query.CountAsync(Ct);
            var employees = await query
                .OrderBy(e => e.EmployeeNo)
                .Skip(ApiPaging.Skip(p, size))
                .Take(size)
                .ToListAsync(Ct);

            var assignments = await AssignmentsAsync(employees.Select(e => e.Id).ToList());

            var rows = employees
                .Select(e =>
                {
                    assignments.TryGetValue(e.Id, out var assignment);
                    return new ApiEmployeeRow
                    {
                        EmployeeId = e.Id,
                        EmployeeNo = e.EmployeeNo,
                        NameAr = Join(e.FirstNameAr, e.FatherNameAr, e.GrandfatherNameAr, e.FamilyNameAr),
                        NameEn = Join(e.FirstNameEn, e.FatherNameEn, e.GrandfatherNameEn, e.FamilyNameEn),
                        Status = e.Status.ToString(),
                        Mobile = e.Mobile,
                        OrgUnitName = assignment?.OrgUnitName,
                        PositionName = assignment?.PositionName,
                    };
                })
                .ToList();

            return Page<ApiEmployeeRow>(rows, p, size, total);
        }

        /// <summary>The employee file — identity, the live position, and the qualifications. No salary (BR-EMP-010).</summary>
        [HttpGet("employees/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.View)]
        public async Task<ActionResult<ApiEmployeeFile>> File(int id)
        {
            var employee = await _db.Employees.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && e.SchoolId == _db.CurrentSchoolId, Ct);
            if (employee == null)
            {
                return NotFoundError();
            }

            var assignments = await AssignmentsAsync(new[] { id });
            assignments.TryGetValue(id, out var assignment);

            var qualifications = await _db.Qualifications.AsNoTracking()
                .Where(qf => qf.EmployeeId == id)
                .OrderByDescending(qf => qf.DateAwarded)
                .ToListAsync(Ct);

            var nationality = await LookupNamesAsync(new[] { employee.NationalityLookupId });

            return new ApiEmployeeFile
            {
                EmployeeId = employee.Id,
                EmployeeNo = employee.EmployeeNo,
                FirstNameAr = employee.FirstNameAr,
                FatherNameAr = employee.FatherNameAr,
                GrandfatherNameAr = employee.GrandfatherNameAr,
                FamilyNameAr = employee.FamilyNameAr,
                FirstNameEn = employee.FirstNameEn,
                FatherNameEn = employee.FatherNameEn,
                GrandfatherNameEn = employee.GrandfatherNameEn,
                FamilyNameEn = employee.FamilyNameEn,
                Gender = employee.Gender.ToString(),
                DateOfBirth = employee.DateOfBirth,
                NationalityLookupId = employee.NationalityLookupId,
                NationalityName = nationality.TryGetValue(employee.NationalityLookupId, out var name) ? name : null,
                PrimaryIdTypeLookupId = employee.PrimaryIdTypeLookupId,
                PrimaryIdNo = employee.PrimaryIdNo,
                PrimaryIdExpiry = employee.PrimaryIdExpiry,
                Status = employee.Status.ToString(),
                Mobile = employee.Mobile,
                WhatsAppNumber = employee.WhatsAppNumber,
                HasPhoto = employee.PhotoAttachmentId != null,
                Assignment = assignment,
                Qualifications = qualifications
                    .Select(qf => new ApiQualification
                    {
                        QualificationId = qf.Id,
                        TitleAr = qf.TitleAr,
                        TitleEn = qf.TitleEn,
                        InstitutionName = qf.InstitutionName,
                        DateAwarded = qf.DateAwarded,
                        IsTeachingRelevant = qf.IsTeachingRelevant,
                        EducationLookupId = qf.EducationLookupId,
                        UniversityLookupId = qf.UniversityLookupId,
                        SpecializationLookupId = qf.SpecializationLookupId,
                        AcademicGradeLookupId = qf.AcademicGradeLookupId,
                        Gpa = qf.Gpa,
                        DocumentAttachmentId = qf.DocumentAttachmentId,
                    })
                    .ToList(),
            };
        }

        /// <summary>Registers a member of staff. The employee number is issued on this call's own commit.</summary>
        [HttpPost("employees")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.Create)]
        public async Task<ActionResult<ApiEmployeeFile>> Register([FromBody] ApiRegisterEmployeeRequest request)
        {
            if (!Enum.TryParse<Gender>(request.Gender, ignoreCase: true, out var gender))
            {
                return Refuse(422, "invalid_gender", "Gender must be Male or Female.", "الجنس يجب أن يكون ذكر أو أنثى.");
            }

            var employee = await _employees.RegisterEmployeeAsync(
                request.FirstNameAr.Trim(), request.FatherNameAr.Trim(), request.GrandfatherNameAr.Trim(), request.FamilyNameAr.Trim(),
                request.FirstNameEn.Trim(), request.FatherNameEn.Trim(), request.GrandfatherNameEn.Trim(), request.FamilyNameEn.Trim(),
                gender, request.DateOfBirth, request.NationalityLookupId, request.UserAccountId,
                request.PrimaryIdTypeLookupId, request.PrimaryIdNo, request.PrimaryIdExpiry,
                request.Mobile, request.WhatsAppNumber, Ct);

            return await File(employee.Id);
        }

        /// <summary>BR-EMP-001: a name change is T1 and the reason is put on the ambient audit context before the save.</summary>
        [HttpPut("employees/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<ActionResult<ApiEmployeeFile>> Update(int id, [FromBody] ApiUpdateEmployeeRequest request)
        {
            if (!Enum.TryParse<Gender>(request.Gender, ignoreCase: true, out var gender))
            {
                return Refuse(422, "invalid_gender", "Gender must be Male or Female.", "الجنس يجب أن يكون ذكر أو أنثى.");
            }

            _audit.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            await _employees.UpdateEmployeeAsync(
                id,
                request.FirstNameAr.Trim(), request.FatherNameAr.Trim(), request.GrandfatherNameAr.Trim(), request.FamilyNameAr.Trim(),
                request.FirstNameEn.Trim(), request.FatherNameEn.Trim(), request.GrandfatherNameEn.Trim(), request.FamilyNameEn.Trim(),
                gender, request.DateOfBirth, request.NationalityLookupId, request.UserAccountId,
                request.PrimaryIdTypeLookupId, request.PrimaryIdNo, request.PrimaryIdExpiry,
                request.Mobile, request.WhatsAppNumber, Ct);

            return await File(id);
        }

        /// <summary>Active / Suspended / Terminated — the engine decides which moves are legal.</summary>
        [HttpPost("employees/{id:int}/status")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Approve)]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] ApiChangeEmployeeStatusRequest request)
        {
            if (!Enum.TryParse<EmployeeStatus>(request.Status, ignoreCase: true, out var status))
            {
                return Refuse(422, "invalid_employee_status", "That is not an employee status.", "هذه ليست حالة موظف.");
            }

            _audit.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            await _employees.ChangeStatusAsync(id, status, Ct);
            return NoContent();
        }

        /// <summary>BR-EMP-002: closes the current assignment and opens the new one.</summary>
        [HttpPost("employees/{id:int}/assignments")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> AssignPosition(int id, [FromBody] ApiAssignPositionRequest request)
        {
            var assignment = await _employees.AssignPositionAsync(
                id, request.OrgUnitId, request.PositionLookupId, request.ManagerEmployeeId,
                request.EffectiveFromUtc ?? _clock.UtcNow, Ct);

            return Ok(new { assignmentId = assignment.Id });
        }

        /// <summary>BR-EMP-004. Either a written title or an education lookup must identify it.</summary>
        [HttpPost("employees/{id:int}/qualifications")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> AddQualification(int id, [FromBody] ApiQualificationRequest request)
        {
            var qualification = await _employees.AddQualificationAsync(
                id, request.TitleAr?.Trim() ?? string.Empty, request.TitleEn?.Trim() ?? string.Empty,
                request.DateAwarded, request.IsTeachingRelevant, request.InstitutionName,
                request.DocumentAttachmentId, request.EducationLookupId, request.UniversityLookupId,
                request.SpecializationLookupId, request.AcademicGradeLookupId, request.Gpa, Ct);

            return Ok(new { qualificationId = qualification.Id });
        }

        // ---------------------------------------------------------------- contracts (restricted)

        /// <summary>
        /// An employee's contracts. Its own permission because this is where the
        /// salary is (BR-EMP-003, BR-EMP-010) — a caller who may read the file
        /// does not thereby read the pay.
        /// </summary>
        [HttpGet("employees/{id:int}/contracts")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Contracts, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiContract>>> Contracts(int id)
        {
            var currency = await CurrencyAsync();
            var contracts = await _db.Contracts.AsNoTracking()
                .Where(c => c.EmployeeId == id)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync(Ct);

            return contracts.Select(c => Describe(c, currency)).ToList();
        }

        /// <summary>BR-EMP-003: a contract overlapping an existing one is refused.</summary>
        [HttpPost("employees/{id:int}/contracts")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Contracts, ActionVerb.Create)]
        public async Task<ActionResult<ApiContract>> DefineContract(int id, [FromBody] ApiContractRequest request)
        {
            if (!Enum.TryParse<ContractType>(request.Type, ignoreCase: true, out var type))
            {
                return Refuse(422, "invalid_contract_type",
                    "Contract type must be FullTime, PartTime or Term.",
                    "نوع العقد يجب أن يكون دواماً كاملاً أو جزئياً أو محدد المدة.");
            }

            var contract = await _employees.DefineContractAsync(
                id, type, request.StartDate, request.EndDate, request.SalaryBasic, request.SalaryAllowances, Ct);

            return Describe(contract, await CurrencyAsync());
        }

        /// <summary>Edits a Draft contract. An active contract is an immutable document.</summary>
        [HttpPut("contracts/{contractId:int}")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Contracts, ActionVerb.Edit)]
        public async Task<ActionResult<ApiContract>> UpdateContract(int contractId, [FromBody] ApiContractRequest request)
        {
            if (!Enum.TryParse<ContractType>(request.Type, ignoreCase: true, out var type))
            {
                return Refuse(422, "invalid_contract_type",
                    "Contract type must be FullTime, PartTime or Term.",
                    "نوع العقد يجب أن يكون دواماً كاملاً أو جزئياً أو محدد المدة.");
            }

            var contract = await _employees.UpdateContractAsync(
                contractId, type, request.StartDate, request.EndDate, request.SalaryBasic, request.SalaryAllowances, Ct);

            return Describe(contract, await CurrencyAsync());
        }

        /// <summary>Draft → Active → Terminated. Natural expiry is derived from the end date, never a stored move.</summary>
        [HttpPost("contracts/{contractId:int}/status")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Contracts, ActionVerb.Approve)]
        public async Task<IActionResult> ChangeContractStatus(int contractId, [FromBody] ApiContractStatusRequest request)
        {
            if (!Enum.TryParse<ContractStatus>(request.Status, ignoreCase: true, out var status))
            {
                return Refuse(422, "invalid_contract_status", "That is not a contract status.", "هذه ليست حالة عقد.");
            }

            await _employees.ChangeContractStatusAsync(contractId, status, Ct);
            return NoContent();
        }

        // ---------------------------------------------------------------- payroll statements (read-only)

        /// <summary>مسير الرواتب — one month, every employee, with the totals a school signs at the bottom.</summary>
        [HttpGet("payroll/runs/{runId:int}/register")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.View)]
        public async Task<ActionResult<ApiPayrollRegister>> Register(int runId)
        {
            var register = await _statements.BuildRegisterAsync(runId, Ct);
            var currency = await CurrencyAsync();

            return new ApiPayrollRegister
            {
                RunId = register.RunId,
                RunNo = register.RunNo,
                PeriodYear = register.PeriodYear,
                PeriodMonth = register.PeriodMonth,
                PaymentDate = register.PaymentDate,
                Status = register.Status.ToString(),
                Currency = currency,
                TotalBasic = register.TotalBasic,
                TotalAllowances = register.TotalAllowances,
                TotalAdditions = register.TotalAdditions,
                TotalDeductions = register.TotalDeductions,
                TotalAdvanceDeduction = register.TotalAdvanceDeduction,
                TotalGross = register.TotalGross,
                TotalNet = register.TotalNet,
                Lines = register.Lines
                    .Select(l => new ApiPayrollRegisterLine
                    {
                        LineId = l.LineId,
                        EmployeeId = l.Employee.EmployeeId,
                        EmployeeNo = l.Employee.EmployeeNo,
                        NameAr = l.Employee.NameAr,
                        NameEn = l.Employee.NameEn,
                        BasicSalary = l.BasicSalary,
                        Allowances = l.Allowances,
                        AdditionsTotal = l.AdditionsTotal,
                        DeductionsTotal = l.DeductionsTotal,
                        AdvanceDeduction = l.AdvanceDeduction,
                        GrossPay = l.GrossPay,
                        NetPay = l.NetPay,
                    })
                    .ToList(),
            };
        }

        /// <summary>قسيمة الراتب — one payslip, with its adjustments and advance instalments broken out.</summary>
        [HttpGet("payroll/lines/{lineId:int}/payslip")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.View)]
        public async Task<ActionResult<ApiPayslip>> Payslip(int lineId)
        {
            var payslip = await _statements.BuildPayslipAsync(lineId, Ct);

            return new ApiPayslip
            {
                LineId = payslip.LineId,
                RunId = payslip.RunId,
                RunNo = payslip.RunNo,
                PeriodYear = payslip.PeriodYear,
                PeriodMonth = payslip.PeriodMonth,
                PaymentDate = payslip.PaymentDate,
                RunStatus = payslip.RunStatus.ToString(),
                EmployeeId = payslip.Employee.EmployeeId,
                EmployeeNo = payslip.Employee.EmployeeNo,
                NameAr = payslip.Employee.NameAr,
                NameEn = payslip.Employee.NameEn,
                // Both languages are carried by the statement; the caller picked one at the
                // Accept-Language header and this layer honours it rather than deciding for them.
                BankName = T(payslip.BankNameEn ?? string.Empty, payslip.BankNameAr ?? string.Empty) is { Length: > 0 } bank ? bank : null,
                BankAccountNo = payslip.BankAccountNo,
                Currency = await CurrencyAsync(),
                BasicSalary = payslip.BasicSalary,
                Allowances = payslip.Allowances,
                AdditionsTotal = payslip.AdditionsTotal,
                DeductionsTotal = payslip.DeductionsTotal,
                AdvanceDeduction = payslip.AdvanceDeduction,
                GrossPay = payslip.GrossPay,
                NetPay = payslip.NetPay,
                Notes = payslip.Notes,
                Adjustments = payslip.Adjustments
                    .Select(a => new ApiPayslipAdjustment
                    {
                        Kind = a.Kind.ToString(),
                        Description = a.Description,
                        Amount = a.Amount,
                    })
                    .ToList(),
                AdvanceInstallments = payslip.AdvanceInstallments
                    .Select(i => new ApiPayslipAdvanceInstallment
                    {
                        AdvanceNo = i.AdvanceNo,
                        SequenceNo = i.SequenceNo,
                        InstallmentCount = i.InstallmentCount,
                        Amount = i.Amount,
                        RemainingAfterThis = i.RemainingAfterThis,
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// The payroll lines that belong to one employee, newest month first —
        /// the list a payslip is opened from. Its ids feed
        /// <see cref="Payslip"/>.
        /// </summary>
        [HttpGet("employees/{id:int}/payslips")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.View)]
        public async Task<ActionResult<ApiPage<ApiPayrollRegisterLine>>> Payslips(int id, int? page = null, int? pageSize = null)
        {
            var (p, size) = ApiPaging.Clamp(page, pageSize);

            var query = _db.PayrollRunLines.AsNoTracking().Where(l => l.EmployeeId == id);
            var total = await query.CountAsync(Ct);

            var lines = await query
                .Join(_db.PayrollRuns.AsNoTracking(), l => l.PayrollRunId, r => r.Id, (l, r) => new { Line = l, Run = r })
                .OrderByDescending(x => x.Run.PeriodYear).ThenByDescending(x => x.Run.PeriodMonth)
                .Skip(ApiPaging.Skip(p, size))
                .Take(size)
                .ToListAsync(Ct);

            var employee = await _db.Employees.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.Id == id && e.SchoolId == _db.CurrentSchoolId)
                .Select(e => new { e.EmployeeNo, e.FirstNameAr, e.FatherNameAr, e.FamilyNameAr, e.FirstNameEn, e.FatherNameEn, e.FamilyNameEn })
                .FirstOrDefaultAsync(Ct);

            var rows = lines
                .Select(x => new ApiPayrollRegisterLine
                {
                    LineId = x.Line.Id,
                    EmployeeId = id,
                    EmployeeNo = employee?.EmployeeNo ?? string.Empty,
                    NameAr = employee == null ? string.Empty : Join(employee.FirstNameAr, employee.FatherNameAr, employee.FamilyNameAr),
                    NameEn = employee == null ? string.Empty : Join(employee.FirstNameEn, employee.FatherNameEn, employee.FamilyNameEn),
                    BasicSalary = x.Line.BasicSalary,
                    Allowances = x.Line.Allowances,
                    AdditionsTotal = x.Line.AdditionsTotal,
                    DeductionsTotal = x.Line.DeductionsTotal,
                    AdvanceDeduction = x.Line.AdvanceDeduction,
                    GrossPay = x.Line.GrossPay,
                    NetPay = x.Line.NetPay,
                })
                .ToList();

            return Page<ApiPayrollRegisterLine>(rows, p, size, total);
        }

        // ------------------------------------------------------------------ helpers

        private static ApiContract Describe(Contract contract, string currency) => new()
        {
            ContractId = contract.Id,
            EmployeeId = contract.EmployeeId,
            Type = contract.Type.ToString(),
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            SalaryBasic = contract.SalaryBasic,
            SalaryAllowances = contract.SalaryAllowances,
            Status = contract.Status.ToString(),
            Currency = currency,
        };

        /// <summary>The live position for a set of employees, in three queries rather than three per row.</summary>
        private async Task<Dictionary<int, ApiEmployeeAssignment>> AssignmentsAsync(IReadOnlyList<int> employeeIds)
        {
            var result = new Dictionary<int, ApiEmployeeAssignment>();
            if (employeeIds.Count == 0)
            {
                return result;
            }

            var assignments = await _db.EmployeeAssignments.AsNoTracking()
                .Where(a => employeeIds.Contains(a.EmployeeId) && a.EffectiveToUtc == null)
                .ToListAsync(Ct);
            if (assignments.Count == 0)
            {
                return result;
            }

            // IgnoreQueryFilters on both lookups: a retired org unit or position still names
            // the assignment already recorded against it (the soft-active lookup trap).
            var unitIds = assignments.Select(a => a.OrgUnitId).Distinct().ToList();
            var units = await _db.OrgUnits.IgnoreQueryFilters().AsNoTracking()
                .Where(u => unitIds.Contains(u.Id) && u.SchoolId == _db.CurrentSchoolId)
                .Select(u => new { u.Id, u.NameAr, u.NameEn })
                .ToListAsync(Ct);

            var positions = await LookupNamesAsync(assignments.Select(a => a.PositionLookupId).Distinct().ToList());

            foreach (var assignment in assignments)
            {
                var unit = units.FirstOrDefault(u => u.Id == assignment.OrgUnitId);
                result[assignment.EmployeeId] = new ApiEmployeeAssignment
                {
                    AssignmentId = assignment.Id,
                    OrgUnitId = assignment.OrgUnitId,
                    OrgUnitName = unit == null ? null : T(unit.NameEn, unit.NameAr),
                    PositionLookupId = assignment.PositionLookupId,
                    PositionName = positions.TryGetValue(assignment.PositionLookupId, out var name) ? name : null,
                    ManagerEmployeeId = assignment.ManagerEmployeeId,
                    EffectiveFromUtc = assignment.EffectiveFromUtc,
                };
            }

            return result;
        }

        private async Task<Dictionary<int, string>> LookupNamesAsync(IReadOnlyList<int> ids)
        {
            if (ids.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var rows = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking()
                .Where(v => ids.Contains(v.Id) && v.SchoolId == _db.CurrentSchoolId)
                .Select(v => new { v.Id, v.Name.NameAr, v.Name.NameEn })
                .ToListAsync(Ct);

            return rows.ToDictionary(r => r.Id, r => T(r.NameEn, r.NameAr));
        }

        private async Task<string> CurrencyAsync()
            => await _db.Schools.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == _db.CurrentSchoolId)
                .Select(s => s.CurrencyCode)
                .SingleOrDefaultAsync(Ct) ?? string.Empty;

        private static string Join(params string[] parts)
            => string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
