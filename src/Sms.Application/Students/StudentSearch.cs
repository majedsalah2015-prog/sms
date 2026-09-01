using System;
using System.Linq;
using Sms.Domain.Students;

namespace Sms.Application.Students
{
    /// <summary>
    /// One student search, shared by every screen that has to find a child before it can act on one
    /// (doc/Modules/23 §8.3 names "student search" as half of the subscription desk; the assignment
    /// console and the discount desk ask the same question).
    /// <para>
    /// Two decisions are the whole of it. First, a student's identity name is four parts in each
    /// language (BR-STU-001) and a picker prints only three, so a search over first and family names
    /// alone answers "no results" for the father's name the clerk can read on screen — every part is
    /// searched, in both languages, alongside the student number.
    /// </para>
    /// <para>
    /// Second, each word narrows further rather than starting over: "محمد أحمد" means the محمد whose
    /// father is أحمد, not every محمد in the school and every child of an أحمد.
    /// </para>
    /// <para>
    /// Matching is case-insensitive by construction rather than by collation. Sqlite translates
    /// <c>Contains</c> to a case-sensitive <c>instr</c> while SQL Server's default collation is
    /// case-insensitive, so a search folded at the provider would behave one way in the tests and
    /// another way in the school. Lowering both sides costs nothing an index would have used —
    /// a contains-search cannot seek anyway — and buys the same answer everywhere.
    /// </para>
    /// </summary>
    public static class StudentSearch
    {
        /// <summary>
        /// Narrows <paramref name="students"/> to those matching every word of <paramref name="term"/>.
        /// A blank term is not a filter and returns the query untouched — the caller decides whether an
        /// unfiltered list is worth showing.
        /// </summary>
        public static IQueryable<Student> Matching(IQueryable<Student> students, string? term)
        {
            if (students is null)
            {
                throw new ArgumentNullException(nameof(students));
            }

            if (string.IsNullOrWhiteSpace(term))
            {
                return students;
            }

            foreach (var word in Words(term))
            {
                // Captured per iteration: the closure must not close over the loop variable, or every
                // predicate ends up searching the last word.
                var w = word;
                students = students.Where(s =>
                    s.StudentNo.ToLower().Contains(w)
                    || s.FirstNameAr.ToLower().Contains(w) || s.FatherNameAr.ToLower().Contains(w)
                    || s.GrandfatherNameAr.ToLower().Contains(w) || s.FamilyNameAr.ToLower().Contains(w)
                    || s.FirstNameEn.ToLower().Contains(w) || s.FatherNameEn.ToLower().Contains(w)
                    || s.GrandfatherNameEn.ToLower().Contains(w) || s.FamilyNameEn.ToLower().Contains(w));
            }

            return students;
        }

        /// <summary>
        /// The search words, lowered and stripped of whitespace. Public because a caller filtering an
        /// already-materialised list must split the term the same way the query does.
        /// </summary>
        public static string[] Words(string? term) =>
            string.IsNullOrWhiteSpace(term)
                ? Array.Empty<string>()
                : term.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }
}
