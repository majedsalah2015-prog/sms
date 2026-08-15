namespace Sms.Application.Common.Interfaces
{
    /// <summary>
    /// BR-SEC-001: a modern adaptive hash — the algorithm choice lives in
    /// Infrastructure (ASP.NET Core Identity's PasswordHasher per doc 02 §3).
    /// </summary>
    public interface IPasswordHasher
    {
        string Hash(string password);

        bool Verify(string hash, string password);
    }
}
