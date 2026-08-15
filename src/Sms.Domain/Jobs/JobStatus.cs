namespace Sms.Domain.Jobs
{
    public enum JobStatus : short
    {
        Running = 1,
        Succeeded = 2,
        Failed = 3,
    }
}
