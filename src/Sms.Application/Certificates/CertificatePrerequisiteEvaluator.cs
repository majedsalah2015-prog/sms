namespace Sms.Application.Certificates
{
    /// <summary>Pure BR-CRT-001/003: a type's required checks must all pass — published results and fee clearance are the two this slice can evaluate for real (WF-03 withdrawal clearance for TC isn't modeled, no consumer exists).</summary>
    public static class CertificatePrerequisiteEvaluator
    {
        public static bool AreMet(bool requiresPublishedResults, bool hasPublishedResults, bool requiresFeeClearance, bool isFeeClear)
        {
            if (requiresPublishedResults && !hasPublishedResults)
            {
                return false;
            }

            if (requiresFeeClearance && !isFeeClear)
            {
                return false;
            }

            return true;
        }
    }
}
