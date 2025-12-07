public class PlcConnector : IDisposable
{
    private readonly IPlcClient _plcClient;
    private readonly IPlcSignalMonitor _signalMonitor;

    public PlcConnector(IPlcClient plcClient, IPlcSignalMonitor signalMonitor)
    {
        _plcClient = plcClient;
        _signalMonitor = signalMonitor;

        _plcClient.EnsureConnected();
        _signalMonitor.Start();
    }

    public int ReadDevice(string deviceName)
    {
        return _plcClient.ReadDevice(deviceName);
    }

    public void WriteDevice(string deviceName, int value)
    {
        _plcClient.WriteDevice(deviceName, value);
    }

    public void Dispose()
    {
        try
        {
            _signalMonitor.Dispose();
            _plcClient.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PLC 리소스 정리 중 예외가 발생했습니다.");
        }
    }
}
