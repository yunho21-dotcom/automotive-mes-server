public class PlcOrderWorker : BackgroundService
{
    private readonly OrderService _orderService;

    public PlcOrderWorker(OrderService orderService)
    {
        _orderService = orderService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // PLC 주문 처리 시도 (M310이 1이면 처리)
            _orderService.ProcessPlcOrder();

            // 폴링 주기 (예: 500ms ~ 1초 등)
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}