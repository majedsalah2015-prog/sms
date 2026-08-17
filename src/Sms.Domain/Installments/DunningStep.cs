namespace Sms.Domain.Installments
{
    /// <summary>
    /// BR-INS-008 ladder in firing order. Reminders precede the due date;
    /// overdue notices follow it; the flag stages after +30 are the
    /// human-gated ones (portal banner, numbered statement letter,
    /// management escalation list) — the ladder evaluator only proposes
    /// them, a Finance Manager confirms letter batches (screens deferred).
    /// </summary>
    public enum DunningStep : short
    {
        ReminderD7 = 1,
        ReminderD1 = 2,
        Overdue3 = 3,
        Overdue14 = 4,
        Overdue30 = 5,
        PortalBanner = 6,
        StatementLetter = 7,
        Escalation = 8,
    }
}
