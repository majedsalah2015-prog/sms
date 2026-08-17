namespace Sms.Domain.Messaging
{
    /// <summary>doc/Modules/32 §7's audience builder, simplified to a scope level rather than a full ad-hoc builder (school/stage/grade/section/route/activity/custom lists) — the level is enough to gate BR-MSG-001's approval requirement.</summary>
    public enum AudienceScope : short
    {
        Section = 1,
        Grade = 2,
        Stage = 3,
        SchoolWide = 4,
    }
}
