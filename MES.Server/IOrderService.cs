public interface IOrderService
{
    void UpdateLatestOrderStatus(OrderStatus newStatus);
    void CancelLatestOrderIfProcessingOrPaused();
}
