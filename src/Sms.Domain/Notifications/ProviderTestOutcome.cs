namespace Sms.Domain.Notifications
{
    /// <summary>
    /// doc/Modules/33 §8.3 — the result of the provider console's "test"
    /// button, kept on the row so the console can say when the credentials
    /// were last known to work rather than only whether somebody typed some.
    /// </summary>
    public enum ProviderTestOutcome : short
    {
        NeverTested = 1,
        Passed = 2,
        Failed = 3,
    }
}
