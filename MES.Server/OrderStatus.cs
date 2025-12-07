public enum OrderStatus
{
    Waiting,
    Processing,
    Completed,
    Paused,
    Cancelled
}

public static class OrderStatusExtensions
{
    public static string ToDbString(this OrderStatus status)
    {
        return status.ToString().ToUpperInvariant();
    }
}
