// MX Component(ActUtlType)을 감싸는 PLC 커넥터 (Serilog 로깅 포함)
public class PlcConnector : IDisposable
{
    private readonly ActUtlType _actUtlType;
    private readonly int _logicalStationNumber;

    // 기본 논리국번호를 1번으로 사용
    public PlcConnector() : this(1)
    {
    }

    public PlcConnector(int stationNumber)
    {
        _logicalStationNumber = stationNumber;
        _actUtlType = new ActUtlType
        {
            ActLogicalStationNumber = _logicalStationNumber
        };

        Log.Information("PLC 연결을 시도합니다. 논리국번호={Station}", _logicalStationNumber);
        int result = _actUtlType.Open();
        if (result != 0)
        {
            Log.Error("PLC 연결에 실패했습니다. 논리국번호={Station}, 코드={Result}", _logicalStationNumber, result);
            throw new InvalidOperationException($"PLC 연결에 실패했습니다. 오류 코드: {result}");
        }

        Log.Information("PLC 연결이 완료되었습니다. 논리국번호={Station}", _logicalStationNumber);
    }

    public int ReadDevice(string deviceName)
    {
        int value;
        int result = _actUtlType.GetDevice(deviceName, out value);

        if (result != 0)
        {
            Log.Error("PLC 디바이스 읽기에 실패했습니다. 디바이스={Device}, 코드={Result}", deviceName, result);
            throw new InvalidOperationException($"디바이스 '{deviceName}' 값을 읽는 데 실패했습니다. 오류 코드: {result}");
        }

        Log.Debug("PLC 디바이스 읽기 성공. 디바이스={Device}, 값={Value}", deviceName, value);
        return value;
    }

    public void WriteDevice(string deviceName, int value)
    {
        int result = _actUtlType.SetDevice(deviceName, value);

        if (result != 0)
        {
            Log.Error("PLC 디바이스 쓰기에 실패했습니다. 디바이스={Device}, 값={Value}, 코드={Result}", deviceName, value, result);
            throw new InvalidOperationException($"디바이스 '{deviceName}' 값을 쓰는 데 실패했습니다. 오류 코드: {result}");
        }

        Log.Debug("PLC 디바이스 쓰기 성공. 디바이스={Device}, 값={Value}", deviceName, value);
    }

    public void Dispose()
    {
        try
        {
            _actUtlType?.Close();
            Log.Information("PLC 연결을 종료했습니다. 논리국번호={Station}", _logicalStationNumber);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PLC 연결 종료 중 예외가 발생했습니다. 논리국번호={Station}", _logicalStationNumber);
        }
    }
}