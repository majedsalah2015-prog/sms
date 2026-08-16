using System;

namespace Sms.Application.Employees
{
    /// <summary>Pure BR-EMP-003: contract dates non-overlapping per employee. Same half-open-interval shape as AcademicYearValidation.Overlaps.</summary>
    public static class ContractOverlapGuard
    {
        public static bool Overlaps(DateTime candidateStart, DateTime candidateEnd, DateTime existingStart, DateTime existingEnd)
            => candidateStart < existingEnd && existingStart < candidateEnd;
    }
}
