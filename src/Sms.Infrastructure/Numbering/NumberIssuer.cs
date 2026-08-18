using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Numbering;
using Sms.Domain.Numbering;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Numbering
{
    /// <summary>
    /// Resolves the active series, bumps its <see cref="SeriesState"/> for the
    /// policy's reset scope, and renders the number. Never calls SaveChanges
    /// (see <see cref="INumberIssuer"/>) — the caller's own save commits it
    /// atomically with the business row. School short code and academic year
    /// label stand in as their raw ids until the School/Academic Year
    /// entities land (S1, E-101/E-102) — same stub posture as
    /// StaticTenantContext.
    /// </summary>
    public class NumberIssuer : INumberIssuer
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IWorkingYearContext _workingYear;
        private readonly IClock _clock;

        public NumberIssuer(AppDbContext db, ITenantContext tenant, IWorkingYearContext workingYear, IClock clock)
        {
            _db = db;
            _tenant = tenant;
            _workingYear = workingYear;
            _clock = clock;
        }

        public async Task<string> IssueAsync(string seriesCode, CancellationToken cancellationToken = default)
        {
            var series = await _db.NumberingSeries.SingleOrDefaultAsync(s => s.Code == seriesCode && s.IsActive, cancellationToken);
            if (series == null)
            {
                throw new NoActiveNumberingSeriesException(seriesCode);
            }

            var academicYearLabel = _workingYear.AcademicYearId.ToString(CultureInfo.InvariantCulture);
            var gregorianYear = _clock.UtcNow.Year;
            var resetKey = ResetKeyResolver.Resolve(series.ResetPolicy, academicYearLabel, gregorianYear);

            // A second issue in the same unit of work must reuse the state added
            // by the first (bulk registration issues several numbers before saving).
            var state = _db.SeriesStates.Local.FirstOrDefault(s => s.NumberingSeriesId == series.Id && s.ResetKey == resetKey)
                ?? await _db.SeriesStates.SingleOrDefaultAsync(
                    s => s.NumberingSeriesId == series.Id && s.ResetKey == resetKey, cancellationToken);
            if (state == null)
            {
                state = new SeriesState { NumberingSeriesId = series.Id, ResetKey = resetKey, LastIssuedSequence = 0 };
                _db.SeriesStates.Add(state);
            }

            state.LastIssuedSequence += 1;

            // BR-NUM-001/doc 08 §3: the first issuance locks the format; further
            // edits must go through a cutover (INumberingSeriesAdmin) instead.
            series.IsLocked = true;

            var context = new NumberFormatContext(
                schoolCode: _tenant.SchoolId.ToString(CultureInfo.InvariantCulture),
                academicYearLabel: academicYearLabel,
                gregorianYear: gregorianYear,
                sequence: state.LastIssuedSequence);

            return NumberFormatEngine.Render(series.FormatTemplate, context);
        }
    }
}
