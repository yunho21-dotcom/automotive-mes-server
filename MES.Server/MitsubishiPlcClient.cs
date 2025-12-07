public class MitsubishiPlcClient : IPlcClient
{
    private readonly ActUtlType _actUtlType;
    private readonly int _logicalStationNumber;
    private readonly object _syncRoot = new();
    private volatile bool _isConnected;

    public MitsubishiPlcClient() : this(1)
    {
    }

    public MitsubishiPlcClient(int stationNumber)
    {
        _logicalStationNumber = stationNumber;
        _actUtlType = new ActUtlType
        {
            ActLogicalStationNumber = _logicalStationNumber
        };

        Log.Information("PLC 연결을 시도합니다. Station={Station}", _logicalStationNumber);

        EnsureConnected();
    }

    public int ReadDevice(string deviceName)
    {
        if (!EnsureConnected())
        {
            Log.Error("PLC가 연결되어 있지 않아 PLC 디바이스를 읽을 수 없습니다. Device={Device}", deviceName);
            throw new InvalidOperationException("PLC가 연결되어 있지 않습니다.");
        }

        int value;
        int result;

        lock (_syncRoot)
        {
            try
            {
                result = _actUtlType.GetDevice(deviceName, out value);
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Log.Error(ex, "PLC 디바이스 읽기 중 예외 발생. Device={Device}", deviceName);
                throw new InvalidOperationException("PLC와 통신하는 동안 오류가 발생했습니다.", ex);
            }
        }

        if (result != 0)
        {
            _isConnected = false;
            Log.Error("PLC 디바이스 읽기 실패. Device={Device}, Code={Result}", deviceName, result);
            throw new InvalidOperationException($"PLC 디바이스 '{deviceName}'을(를) 읽지 못했습니다. 코드: {result}");
        }

        Log.Debug("PLC 디바이스 읽기. Device={Device}, Value={Value}", deviceName, value);
        return value;
    }

    public void WriteDevice(string deviceName, int value)
    {
        if (!EnsureConnected())
        {
            Log.Error("PLC가 연결되어 있지 않아 PLC 디바이스에 쓸 수 없습니다. Device={Device}, Value={Value}",
                deviceName, value);
            throw new InvalidOperationException("PLC가 연결되어 있지 않습니다.");
        }

        int result;

        lock (_syncRoot)
        {
            try
            {
                result = _actUtlType.SetDevice(deviceName, value);
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Log.Error(ex, "PLC 디바이스 쓰기 중 예외 발생. Device={Device}, Value={Value}", deviceName, value);
                throw new InvalidOperationException("PLC와 통신하는 동안 오류가 발생했습니다.", ex);
            }
        }

        if (result != 0)
        {
            _isConnected = false;
            Log.Error("PLC 디바이스 쓰기 실패. Device={Device}, Value={Value}, Code={Result}", deviceName, value, result);
            throw new InvalidOperationException($"PLC 디바이스 '{deviceName}'에 쓰지 못했습니다. 코드: {result}");
        }

        Log.Debug("PLC 디바이스 쓰기. Device={Device}, Value={Value}", deviceName, value);
    }

    public bool EnsureConnected()
    {
        if (_isConnected)
        {
            return true;
        }

        Log.Debug("PLC가 연결되지 않았습니다. 연결을 시도합니다. Station={Station}", _logicalStationNumber);

        int result;
        lock (_syncRoot)
        {
            if (_isConnected)
            {
                return true;
            }

            try
            {
                _actUtlType.Close();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "연결 재시도 전 기존 PLC 연결 종료 중 예외 발생. Station={Station}", _logicalStationNumber);
            }

            result = _actUtlType.Open();
            _isConnected = result == 0;
        }

        if (result == 0)
        {
            Log.Information("PLC가 연결되었습니다. Station={Station}", _logicalStationNumber);
            return true;
        }

        Log.Warning("PLC 연결 시도가 실패했습니다. Station={Station}, Code={Result}. 재시도합니다.",
            _logicalStationNumber, result);
        return false;
    }

    public void Dispose()
    {
        try
        {
            _isConnected = false;

            lock (_syncRoot)
            {
                _actUtlType?.Close();
            }

            Log.Information("PLC 연결을 종료했습니다. Station={Station}", _logicalStationNumber);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PLC 연결 종료 중 예외가 발생했습니다. Station={Station}", _logicalStationNumber);
        }
    }
}
