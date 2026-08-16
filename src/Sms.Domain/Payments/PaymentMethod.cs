namespace Sms.Domain.Payments
{
    public enum PaymentMethod : short
    {
        Cash = 1,
        Card = 2,
        BankTransfer = 3,
        Cheque = 4,
        Pdc = 5,
    }
}
