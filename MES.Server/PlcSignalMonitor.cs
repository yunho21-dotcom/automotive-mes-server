public class PlcSignalMonitor : IPlcSignalMonitor
{
    private readonly IPlcClient _plcClient;
    private readonly IPlcSignalProcessor _signalProcessor;
    private Timer? _timer;

    public PlcSignalMonitor(IPlcClient plcClient, IPlcSignalProcessor signalProcessor)
    {
        _plcClient = plcClient;
        _signalProcessor = signalProcessor;
    }

    public void Start()
    {
        if (_timer != null)
        {
            return;
        }

        _timer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Log.Information("PLC 모니터링을 시작합니다. (M120~M135, M140~M141, 1초 간격)");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        Log.Information("PLC 모니터링을 중지했습니다.");
    }

    private void OnTimerTick(object? state)
    {
        try
        {
            if (!_plcClient.EnsureConnected())
            {
                return;
            }

            _signalProcessor.ProcessAllSignals();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PLC 모니터링 중 예외가 발생했습니다.");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
