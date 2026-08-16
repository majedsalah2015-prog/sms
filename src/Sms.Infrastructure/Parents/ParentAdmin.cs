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

        public async Task<Parent> RegisterParentAsync(
            string nameAr, string nameEn, string primaryMobile, string? email = null, string? address = null,
            string? occupationEmployer = null, string preferredLanguage = "ar", CancellationToken cancellationToken = default)
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
            };
            _db.Parents.Add(parent);

            await _db.SaveChangesAsync(cancellationToken);
            return parent;
        }
    }
}
