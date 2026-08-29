using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attachments;
using Sms.Infrastructure.Persistence;

namespace Sms.Web.Services
{
    /// <summary>
    /// The school's logo as the shell wears it — beside the product name in the sidebar and beside
    /// the school's own name on the landing page (BR-SCH-006, doc/Modules/02 §8.1).
    /// <para>
    /// Separate from <see cref="SchoolBrandingService"/>, which owns the slot: that one answers
    /// "what may be stored and what is stored", this one answers the only two questions the chrome
    /// has — is there a mark worth drawing, and which version of it. The chrome must not fetch
    /// bytes to decide whether to draw an image, and it must not draw one that the scan gate is
    /// still holding back (BR-ATT-009); a broken image on every page of the product is worse than
    /// the generic mark it replaced.
    /// </para>
    /// <para>
    /// Scoped, and memoised in a field for that scope: the layout asks once per request and the
    /// landing page asks again for the same page, and the answer cannot change between the two.
    /// </para>
    /// </summary>
    public sealed class SchoolBrandMark
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly SchoolBrandingService _branding;

        private int? _version;
        private bool _asked;

        public SchoolBrandMark(AppDbContext db, ITenantContext tenant, SchoolBrandingService branding)
        {
            _db = db;
            _tenant = tenant;
            _branding = branding;
        }

        /// <summary>
        /// The current version number of a servable logo, or null when this school has none — no
        /// pointer, a retired attachment, or a version the scan gate has not cleared.
        /// <para>
        /// The number is the chrome's cache key: it goes on the image URL so that replacing a logo
        /// changes the address the browser asks for. Without it a school that fixes a wrong mark
        /// would keep seeing the wrong one until every reader cleared their cache.
        /// </para>
        /// </summary>
        public async Task<int?> VersionAsync(CancellationToken cancellationToken = default)
        {
            if (_asked) { return _version; }

            // School is the row that defines the tenant scope, so it carries no filter of its own
            // (ISchoolScoped is deliberately absent) — name the working school explicitly.
            _version = await (
                from s in _db.Schools.AsNoTracking()
                where s.Id == _tenant.SchoolId && s.LogoAttachmentId != null
                join a in _db.Attachments.AsNoTracking() on s.LogoAttachmentId equals a.Id
                where a.Status == AttachmentStatus.Active
                join v in _db.AttachmentVersions.AsNoTracking() on a.Id equals v.AttachmentId
                where v.VersionNumber == a.CurrentVersionNumber && v.ScanStatus == ScanStatus.Clean
                select (int?)v.VersionNumber)
                .SingleOrDefaultAsync(cancellationToken);

            _asked = true;
            return _version;
        }

        /// <summary>
        /// The logo's bytes for the chrome to serve, or null when there is nothing servable —
        /// the same verdict <see cref="VersionAsync"/> reports, reached by actually reading.
        /// </summary>
        public async Task<AttachmentIntake.StoredFile?> ReadAsync(CancellationToken cancellationToken = default)
        {
            var school = await _db.Schools.AsNoTracking()
                .Where(s => s.Id == _tenant.SchoolId)
                .Select(s => new { s.LogoAttachmentId })
                .SingleOrDefaultAsync(cancellationToken);

            return school == null ? null : await _branding.ReadAsync(school.LogoAttachmentId, cancellationToken);
        }
    }
}
