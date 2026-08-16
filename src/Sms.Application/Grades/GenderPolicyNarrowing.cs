using Sms.Domain.Grades;

namespace Sms.Application.Grades
{
    /// <summary>
    /// Pure BR-GRD-004: a grade (and later a section) may only narrow its
    /// stage's gender policy, never widen it. Mixed can narrow to anything;
    /// Boys/Girls can only stay themselves.
    /// </summary>
    public static class GenderPolicyNarrowing
    {
        public static bool IsValidNarrowing(GenderPolicy broader, GenderPolicy narrower)
        {
            if (broader == GenderPolicy.Mixed)
            {
                return true;
            }

            return narrower == broader;
        }
    }
}
