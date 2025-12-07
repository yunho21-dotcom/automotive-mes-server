public class PlcSignalProcessor : IPlcSignalProcessor
{
    private readonly IPlcClient _plcClient;
    private readonly IOrderService _orderService;
    private readonly IProductionService _productionService;

    private static readonly string[] _devices =
    {
        "M120", "M121", "M122", "M123", "M124",
        "M125", "M126", "M127", "M128", "M129",
        "M130", "M131", "M132", "M133", "M134",
        "M135",
        "M140", "M141"
    };

    private enum PlcSignal
    {
        ProductionStart,
        FrontEndCompleted,
        Paused,
        Resumed,
        CancelRequested,
        EmergencyStop,
        EmergencyStopReleased,
        UpperVisionOk,
        UpperVisionNg,
        LowerVisionOk,
        LowerVisionNg,
        AbnormalLineReset,
        UpperProcessCompleted,
        UpperProcessNg,
        LowerProcessCompleted,
        LowerProcessNg,
        AutoMode,
        ManualMode
    }

    private static readonly Dictionary<string, PlcSignal> _deviceToSignal = new()
    {
        ["M120"] = PlcSignal.ProductionStart,
        ["M121"] = PlcSignal.FrontEndCompleted,
        ["M122"] = PlcSignal.Paused,
        ["M123"] = PlcSignal.Resumed,
        ["M124"] = PlcSignal.CancelRequested,
        ["M125"] = PlcSignal.EmergencyStop,
        ["M126"] = PlcSignal.EmergencyStopReleased,
        ["M127"] = PlcSignal.UpperVisionOk,
        ["M128"] = PlcSignal.UpperVisionNg,
        ["M129"] = PlcSignal.LowerVisionOk,
        ["M130"] = PlcSignal.LowerVisionNg,
        ["M131"] = PlcSignal.AbnormalLineReset,
        ["M132"] = PlcSignal.UpperProcessCompleted,
        ["M133"] = PlcSignal.UpperProcessNg,
        ["M134"] = PlcSignal.LowerProcessCompleted,
        ["M135"] = PlcSignal.LowerProcessNg,
        ["M140"] = PlcSignal.AutoMode,
        ["M141"] = PlcSignal.ManualMode
    };

    private static readonly Dictionary<PlcSignal, Action> _logActions = new()
    {
        [PlcSignal.ProductionStart] = () =>
            Log.Information("[M120] 생산 라인 가동을 시작합니다."),
        [PlcSignal.FrontEndCompleted] = () =>
            Log.Information("[M121] 전공정(Front-End) 작업이 완료되었습니다."),
        [PlcSignal.Paused] = () =>
            Log.Warning("[M122] 일시정지 상태입니다. 현장의 설비 상태를 확인하십시오."),
        [PlcSignal.Resumed] = () =>
            Log.Information("[M123] 일시정지 상태에서 생산이 재개되었습니다."),
        [PlcSignal.CancelRequested] = () =>
            Log.Warning("[M124] 작업 취소 요청이 접수되었습니다. 현재 공정을 중단합니다."),
        [PlcSignal.EmergencyStop] = () =>
            Log.Error("[M125] 비상정지(EMG)가 감지되었습니다. 모든 설비를 즉시 정지하십시오."),
        [PlcSignal.EmergencyStopReleased] = () =>
            Log.Information("[M126] 비상정지(EMG) 상태가 해제되었습니다."),
        [PlcSignal.UpperVisionOk] = () =>
            Log.Information("[M127] 상부(Upper) 비전 검사 결과: 양품(OK)"),
        [PlcSignal.UpperVisionNg] = () =>
            Log.Warning("[M128] 상부(Upper) 비전 검사 결과: 불량(NG)"),
        [PlcSignal.LowerVisionOk] = () =>
            Log.Information("[M129] 하부(Lower) 비전 검사 결과: 양품(OK)"),
        [PlcSignal.LowerVisionNg] = () =>
            Log.Warning("[M130] 하부(Lower) 비전 검사 결과: 불량(NG)"),
        [PlcSignal.AbnormalLineReset] = () =>
            Log.Fatal("[M131] 생산 라인 가동 중 PLC의 비정상적인 종료 또는 리셋이 감지되었습니다."),
        [PlcSignal.UpperProcessCompleted] = () =>
            Log.Information("[M132] 상부(Upper) 공정 완료: 흰색 양품치수로 공정이 진행됩니다."),
        [PlcSignal.UpperProcessNg] = () =>
            Log.Warning("[M133] 상부(Upper) 공정 NG: 흰색 불량치를 리젝 컨베이어로 배출합니다."),
        [PlcSignal.LowerProcessCompleted] = () =>
            Log.Information("[M134] 하부(Lower) 공정 완료: 배터리팩 양품치수로 공정이 진행됩니다."),
        [PlcSignal.LowerProcessNg] = () =>
            Log.Warning("[M135] 하부(Lower) 공정 NG: 배터리팩 불량치를 리젝 컨베이어로 배출합니다."),
        [PlcSignal.AutoMode] = () =>
            Log.Information("[M140] 설비 운전 모드가 [AUTO] 모드로 변경되었습니다."),
        [PlcSignal.ManualMode] = () =>
            Log.Information("[M141] 설비 운전 모드가 [MANUAL] 모드로 변경되었습니다.")
    };

    public PlcSignalProcessor(IPlcClient plcClient, IOrderService orderService, IProductionService productionService)
    {
        _plcClient = plcClient;
        _orderService = orderService;
        _productionService = productionService;
    }

    public void ProcessAllSignals()
    {
        foreach (var device in _devices)
        {
            ProcessSignal(device);
        }
    }

    private void ProcessSignal(string deviceName)
    {
        if (!_deviceToSignal.TryGetValue(deviceName, out var signal))
        {
            return;
        }

        int value;

        try
        {
            value = _plcClient.ReadDevice(deviceName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PLC 장치 읽기 오류. Device={Device}", deviceName);
            return;
        }

        if (value == 0)
        {
            return;
        }

        HandleBusiness(signal);
        LogSignal(signal);

        try
        {
            _plcClient.WriteDevice(deviceName, 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PLC 디바이스 리셋(OFF) 중 예외 발생. Device={Device}", deviceName);
        }
    }

    private void HandleBusiness(PlcSignal signal)
    {
        if (signal is PlcSignal.ProductionStart or PlcSignal.FrontEndCompleted or PlcSignal.Paused
            or PlcSignal.Resumed or PlcSignal.CancelRequested)
        {
            OrderStatus? newStatus = signal switch
            {
                PlcSignal.ProductionStart => OrderStatus.Processing,
                PlcSignal.FrontEndCompleted => OrderStatus.Completed,
                PlcSignal.Paused => OrderStatus.Paused,
                PlcSignal.Resumed => OrderStatus.Processing,
                PlcSignal.CancelRequested => OrderStatus.Cancelled,
                _ => null
            };

            if (newStatus.HasValue)
            {
                try
                {
                    _orderService.UpdateLatestOrderStatus(newStatus.Value);

                    if (signal == PlcSignal.ProductionStart)
                    {
                        _productionService.CreateProductionForLatestOrder();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex,
                        "PLC 신호에 대한 주문 상태/생산 처리 중 오류. Signal={Signal}, Status={Status}",
                        signal, newStatus.Value.ToDbString());
                }
            }
        }
        else if (signal == PlcSignal.AbnormalLineReset)
        {
            try
            {
                _orderService.CancelLatestOrderIfProcessingOrPaused();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "M131에서 최신 주문을 취소하는 동안 오류가 발생했습니다.");
            }
        }
    }

    private static void LogSignal(PlcSignal signal)
    {
        if (_logActions.TryGetValue(signal, out var action))
        {
            action();
        }
    }
}
