namespace Sms.Domain.Security
{
    /// <summary>Account model of doc 06 §2.</summary>
    public enum AccountType : short
    {
        Staff = 1,
        Parent = 2,
        Student = 3,
        System = 4,
    }
}
