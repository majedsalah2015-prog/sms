using System;

namespace Sms.Application.ReadModels
{
    /// <summary>The five RPT-FEE-004 aging buckets.</summary>
    public enum AgingBucket : short
    {
        Current = 1,
        Days1To30 = 2,
        Days31To60 = 3,
        Days61To90 = 4,
        Over90 = 5,
    }

    /// <summary>
    /// RPT-FEE-004 aged receivables: buckets an open remainder by how many days past its reference date it is.
    /// The reference date is the charge's posting date — Module 19's Charge carries no due date (E-303/E-501:
    /// due dates live on Installments, which not every charge has), so "current" means posted within the
    /// grace window. Pure and shared so the snapshot job and any ad-hoc aging report agree (BR-FEE-008 single math).
    /// </summary>
    public static class ReceivablesAgingBucketer
    {
        /// <summary>Default grace: a charge is "current" for its first 30 days (doc RPT-FEE-004 leaves the window to finance policy).</summary>
        public const int DefaultCurrentDays = 30;

        public static AgingBucket Bucket(DateTime referenceUtc, DateTime asOfUtc, int currentDays = DefaultCurrentDays)
        {
            var age = (asOfUtc.Date - referenceUtc.Date).Days - currentDays;
            if (age <= 0)
            {
                return AgingBucket.Current;
            }

            if (age <= 30)
            {
                return AgingBucket.Days1To30;
            }

            if (age <= 60)
            {
                return AgingBucket.Days31To60;
            }

            return age <= 90 ? AgingBucket.Days61To90 : AgingBucket.Over90;
        }
    }
}
