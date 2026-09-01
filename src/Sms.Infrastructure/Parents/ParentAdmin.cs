using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Numbering;
using Sms.Application.Parents;
using Sms.Domain.Parents;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Parents
{
    /// <summary>Standalone admin operation — composes with E-006's INumberIssuer the same way E-202's StudentAdmin does.</summary>
    public class ParentAdmin : IParentAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;

        public ParentAdmin(AppDbContext db, INumberIssuer numberIssuer)
        {
            _db = db;
            _numberIssuer = numberIssuer;
        }

        public async Task<Parent> UpdateParentAsync(
            int parentId, string nameAr, string nameEn, string primaryMobile, string? email = null, string? address = null,
            string? occupationEmployer = null, string preferredLanguage = "ar",
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null,
            ParentLifeStatus lifeStatus = ParentLifeStatus.Alive, string? lifeStatusNote = null,
            int? educationLookupId = null,
            CancellationToken cancellationToken = default)
        {
            var parent = await _db.Parents.SingleAsync(p => p.Id == parentId, cancellationToken);
            parent.NameAr = nameAr; parent.NameEn = nameEn; parent.PrimaryMobile = primaryMobile; parent.Email = email;
            parent.Address = address; parent.OccupationEmployer = occupationEmployer; parent.PreferredLanguage = preferredLanguage;
            parent.PrimaryIdTypeLookupId = primaryIdTypeLookupId;
            parent.EducationLookupId = educationLookupId;
            parent.PrimaryIdNo = Trimmed(primaryIdNo);
            parent.LifeStatus = lifeStatus;

            // The note explains "Other" and nothing else. Keeping it after a status
            // change to Deceased would leave a sentence on the record describing a
            // category the person is no longer filed under.
            parent.LifeStatusNote = lifeStatus == ParentLifeStatus.Other ? Trimmed(lifeStatusNote) : null;

            await _db.SaveChangesAsync(cancellationToken);
            return parent;
        }

        /// <summary>Empty and whitespace both mean "not recorded" — and only null keeps the filtered ID index out of the way.</summary>
        private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public async Task SetResidenceAsync(int parentId, int? residenceAreaId, int? neighbourhoodId, CancellationToken cancellationToken = default)
        {
            var parent = await _db.Parents.SingleAsync(p => p.Id == parentId, cancellationToken);

            // A quarter without its locality is not a place, and a quarter belonging to a different
            // locality is a worse record than none: neither is stored.
            if (neighbourhoodId is int hoodId)
            {
                if (residenceAreaId is not int areaId)
                {
                    throw new System.InvalidOperationException("A neighbourhood cannot be recorded without the locality it belongs to.");
                }

                var belongs = await _db.Neighbourhoods.AnyAsync(n => n.Id == hoodId && n.ResidenceAreaId == areaId, cancellationToken);
                if (!belongs)
                {
                    throw new System.InvalidOperationException("That neighbourhood does not belong to the chosen locality.");
                }
            }

            parent.ResidenceAreaId = residenceAreaId;
            parent.NeighbourhoodId = residenceAreaId == null ? null : neighbourhoodId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Parent> RegisterParentAsync(
            string nameAr, string nameEn, string primaryMobile, string? email = null, string? address = null,
            string? occupationEmployer = null, string preferredLanguage = "ar",
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null,
            ParentLifeStatus lifeStatus = ParentLifeStatus.Alive, string? lifeStatusNote = null,
            int? educationLookupId = null,
            CancellationToken cancellationToken = default)
        {
            var fileNo = await _numberIssuer.IssueAsync("PAR", cancellationToken);

            var parent = new Parent
            {
                ParentFileNo = fileNo,
                NameAr = nameAr,
                NameEn = nameEn,
                PrimaryMobile = primaryMobile,
                Email = email,
                Address = address,
                OccupationEmployer = occupationEmployer,
                PreferredLanguage = preferredLanguage,
                PrimaryIdTypeLookupId = primaryIdTypeLookupId,
                EducationLookupId = educationLookupId,
                PrimaryIdNo = Trimmed(primaryIdNo),
                LifeStatus = lifeStatus,
                LifeStatusNote = lifeStatus == ParentLifeStatus.Other ? Trimmed(lifeStatusNote) : null,
            };
            _db.Parents.Add(parent);

            await _db.SaveChangesAsync(cancellationToken);
            return parent;
        }

        public async Task DeleteParentAsync(int parentId, CancellationToken cancellationToken = default)
        {
            var parent = await _db.Parents.SingleAsync(p => p.Id == parentId, cancellationToken);

            var links = await _db.StudentGuardianLinks.Where(l => l.ParentId == parentId).ToListAsync(cancellationToken);
            if (links.Any(l => l.EffectiveToUtc == null))
            {
                throw new System.InvalidOperationException("Parent is still an active guardian of " + links.Count(l => l.EffectiveToUtc == null) + " student(s); unlink them first.");
            }
            if (await _db.Payers.AnyAsync(p => p.ParentId == parentId, cancellationToken))
            {
                throw new System.InvalidOperationException("Parent is a fee payer and cannot be deleted.");
            }

            foreach (var application in await _db.Applications.Where(a => a.ParentId == parentId).ToListAsync(cancellationToken))
            {
                application.ParentId = null;
            }

            _db.StudentGuardianLinks.RemoveRange(links);
            _db.Parents.Remove(parent);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new System.InvalidOperationException("Parent cannot be deleted: other records still reference it (" + (ex.InnerException?.Message ?? ex.Message) + ").");
            }
        }
    }
}
