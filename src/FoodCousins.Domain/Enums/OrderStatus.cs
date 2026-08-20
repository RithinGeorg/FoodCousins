namespace FoodCousins.Domain.Enums;
public enum OrderStatus
{
    Pending, AwaitingPayment, Confirmed, AcceptedByCook, Preparing, Ready,
    Completed, Cancelled, Rejected, PaymentFailed, Refunded
}
