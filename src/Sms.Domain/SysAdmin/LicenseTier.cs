namespace Sms.Domain.SysAdmin
{
    /// <summary>
    /// O5 commercial decision (docs/Implementation/01-Entry-Decisions.md
    /// §O5, made concrete for this build): tiers align to the WBS's own
    /// stage boundaries rather than an unrelated scheme — Essentials covers
    /// S0-S3 core (setup, people, attendance, basic grading, fees/payments,
    /// portal essentials); Professional adds S4-S6 (timetable, examinations,
    /// full grading, certificates, installments, discounts, all student
    /// services); Enterprise adds S7 platform (reports long tail,
    /// dashboards, messaging, advanced audit/backup/sysadmin). See
    /// LicenseTierCatalog for the module-group mapping; enforcement
    /// middleware per module is a deferred wiring point, same "engine
    /// built, not wired" precedent as several other epics.
    /// </summary>
    public enum LicenseTier : short
    {
        Essentials = 1,
        Professional = 2,
        Enterprise = 3,
    }
}
