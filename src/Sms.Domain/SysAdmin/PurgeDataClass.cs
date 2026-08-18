namespace Sms.Domain.SysAdmin
{
    /// <summary>The data classes this build has a documented retention horizon for.</summary>
    public enum PurgeDataClass : short
    {
        Audit = 1,
        Attachment = 2,
        Student = 3,
    }
}
