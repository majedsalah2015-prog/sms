using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Geography;
using Sms.Domain.Common;
using Sms.Domain.Geography;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Geography
{
    /// <summary>
    /// The write side of the residence constants — محافظة → منطقة → حي.
    /// <para>
    /// <b>Every read here goes past the soft-active filter and re-applies the school by hand</b>,
    /// for the reason <c>LookupAdmin</c> records: a deactivated row still owns its code and is
    /// still pointed at by addresses already saved, so the maintenance screen has to be able to
    /// see it. Reading through the filter would make "reactivate" try to insert a second row with
    /// the same code and die on the unique index, and would make "deactivate" throw on a row that
    /// was already retired. Past the filter the tenant predicate is gone too, hence the explicit
    /// <c>SchoolId</c> on every query — dropping it would let one school edit another's geography.
    /// </para>
    /// <para>
    /// A locality is never moved between governorates, and a quarter never between localities. The
    /// parent is what gives a child its meaning here — "Central" means one place under Gaza and
    /// another under Rafah — so re-parenting would silently rewrite the address of every student
    /// already recorded under it. Add the row where it belongs and retire the one that was in the
    /// wrong place.
    /// </para>
    /// </summary>
    public class ResidenceAdmin : IResidenceAdmin
    {
        private readonly AppDbContext _db;

        public ResidenceAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Governorate> SaveGovernorateAsync(
            int? id, string? code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default)
        {
            var siblings = await _db.Governorates.IgnoreQueryFilters()
                .Where(g => g.SchoolId == _db.CurrentSchoolId)
                .ToListAsync(cancellationToken);

            Governorate row;
            if (id is int existingId)
            {
                row = siblings.SingleOrDefault(g => g.Id == existingId)
                    ?? throw new ResidenceRowNotFoundException(ResidenceLevel.Governorate, existingId);
            }
            else
            {
                row = new Governorate { Code = ResolveCode(ResidenceLevel.Governorate, code, nameEn, siblings.Select(g => g.Code)) };
                _db.Governorates.Add(row);
            }

            row.Name = Named(nameAr, nameEn);
            row.SortOrder = sortOrder;
            row.IsActive = true;

            await _db.SaveChangesAsync(cancellationToken);
            return row;
        }

        public async Task<ResidenceArea> SaveLocalityAsync(
            int? id, int governorateId, string? code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default)
        {
            await RequireGovernorateAsync(governorateId, cancellationToken);

            var siblings = await _db.ResidenceAreas.IgnoreQueryFilters()
                .Where(a => a.SchoolId == _db.CurrentSchoolId && a.GovernorateId == governorateId)
                .ToListAsync(cancellationToken);

            ResidenceArea row;
            if (id is int existingId)
            {
                row = siblings.SingleOrDefault(a => a.Id == existingId)
                    ?? throw new ResidenceRowNotFoundException(ResidenceLevel.Locality, existingId);
            }
            else
            {
                row = new ResidenceArea
                {
                    GovernorateId = governorateId,
                    Code = ResolveCode(ResidenceLevel.Locality, code, nameEn, siblings.Select(a => a.Code)),
                };
                _db.ResidenceAreas.Add(row);
            }

            row.Name = Named(nameAr, nameEn);
            row.SortOrder = sortOrder;
            row.IsActive = true;

            await _db.SaveChangesAsync(cancellationToken);
            return row;
        }

        public async Task<Neighbourhood> SaveQuarterAsync(
            int? id, int localityId, string? code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default)
        {
            await RequireLocalityAsync(localityId, cancellationToken);

            var siblings = await _db.Neighbourhoods.IgnoreQueryFilters()
                .Where(n => n.SchoolId == _db.CurrentSchoolId && n.ResidenceAreaId == localityId)
                .ToListAsync(cancellationToken);

            Neighbourhood row;
            if (id is int existingId)
            {
                row = siblings.SingleOrDefault(n => n.Id == existingId)
                    ?? throw new ResidenceRowNotFoundException(ResidenceLevel.Quarter, existingId);
            }
            else
            {
                row = new Neighbourhood
                {
                    ResidenceAreaId = localityId,
                    Code = ResolveCode(ResidenceLevel.Quarter, code, nameEn, siblings.Select(n => n.Code)),
                };
                _db.Neighbourhoods.Add(row);
            }

            row.Name = Named(nameAr, nameEn);
            row.SortOrder = sortOrder;
            row.IsActive = true;

            await _db.SaveChangesAsync(cancellationToken);
            return row;
        }

        public async Task SetGovernorateActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
        {
            var row = await _db.Governorates.IgnoreQueryFilters()
                .SingleOrDefaultAsync(g => g.Id == id && g.SchoolId == _db.CurrentSchoolId, cancellationToken)
                ?? throw new ResidenceRowNotFoundException(ResidenceLevel.Governorate, id);

            row.IsActive = isActive;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetLocalityActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
        {
            var row = await _db.ResidenceAreas.IgnoreQueryFilters()
                .SingleOrDefaultAsync(a => a.Id == id && a.SchoolId == _db.CurrentSchoolId, cancellationToken)
                ?? throw new ResidenceRowNotFoundException(ResidenceLevel.Locality, id);

            row.IsActive = isActive;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetQuarterActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
        {
            var row = await _db.Neighbourhoods.IgnoreQueryFilters()
                .SingleOrDefaultAsync(n => n.Id == id && n.SchoolId == _db.CurrentSchoolId, cancellationToken)
                ?? throw new ResidenceRowNotFoundException(ResidenceLevel.Quarter, id);

            row.IsActive = isActive;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ helpers

        private static LocalizedName Named(string nameAr, string nameEn) => new(nameAr.Trim(), nameEn.Trim());

        /// <summary>
        /// The code the row will carry: the one typed, or one derived from the English name. The
        /// duplicate check covers only the typed one — a collision an operator authored has to be
        /// said out loud, while a derived code simply steps past it.
        /// <para>
        /// An edit never reaches here. <c>Code</c> is the stable key the seeder is idempotent on
        /// and the unique index is built over; letting a rename change it would make the next seed
        /// run insert the row again beside the one that was renamed.
        /// </para>
        /// </summary>
        private static string ResolveCode(ResidenceLevel level, string? code, string nameEn, IEnumerable<string> taken)
        {
            var codes = taken.ToList();
            if (string.IsNullOrWhiteSpace(code)) return ResidenceCodeGenerator.Next(nameEn, codes);

            var typed = code.Trim().ToUpperInvariant();
            if (codes.Any(c => string.Equals(c, typed, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DuplicateResidenceCodeException(level, typed);
            }

            return typed;
        }

        private async Task RequireGovernorateAsync(int governorateId, CancellationToken cancellationToken)
        {
            var exists = await _db.Governorates.IgnoreQueryFilters()
                .AnyAsync(g => g.Id == governorateId && g.SchoolId == _db.CurrentSchoolId, cancellationToken);
            if (!exists) throw new ResidenceRowNotFoundException(ResidenceLevel.Governorate, governorateId);
        }

        private async Task RequireLocalityAsync(int localityId, CancellationToken cancellationToken)
        {
            var exists = await _db.ResidenceAreas.IgnoreQueryFilters()
                .AnyAsync(a => a.Id == localityId && a.SchoolId == _db.CurrentSchoolId, cancellationToken);
            if (!exists) throw new ResidenceRowNotFoundException(ResidenceLevel.Locality, localityId);
        }
    }
}
