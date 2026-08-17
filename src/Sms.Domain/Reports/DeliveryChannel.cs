namespace Sms.Domain.Reports
{
    /// <summary>BR-RPT-003: restricted reports never schedule to Email — Portal-delivery only.</summary>
    public enum DeliveryChannel : short
    {
        Email = 1,
        Portal = 2,
    }
}
