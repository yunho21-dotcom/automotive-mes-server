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
            Log.Information("[M120] 생산 라인이 가동을 시작합니다."),
        [PlcSignal.FrontEndCompleted] = () =>
            Log.Information("[M121] 전공정(Front-End) 작업이 완료되었습니다."),
        [PlcSignal.Paused] = () =>
            Log.Warning("[M122] 일시정지 상태입니다. 현장 설비 점검을 진행해 주세요."),
        [PlcSignal.Resumed] = () =>
            Log.Information("[M123] 일시정지 상태에서 생산이 재개되었습니다."),
        [PlcSignal.CancelRequested] = () =>
            Log.Warning("[M124] 작업 취소 요청이 수신되었습니다. 공정을 중단합니다."),
        [PlcSignal.EmergencyStop] = () =>
            Log.Error("[M125] 비상정지(EMG)가 감지되었습니다. 모든 설비를 즉시 정지해 주세요."),
        [PlcSignal.EmergencyStopReleased] = () =>
            Log.Information("[M126] 비상정지(EMG) 해제 신호가 들어왔습니다."),
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
            Log.Information("[M132] 상부(Upper) 공정 완료: 양품 1개가 생산 수량으로 집계됩니다."),
        [PlcSignal.UpperProcessNg] = () =>
            Log.Warning("[M133] 상부(Upper) 공정 NG: 불량 1개가 불량 수량으로 집계됩니다."),
        [PlcSignal.LowerProcessCompleted] = () =>
            Log.Information("[M134] 하부(Lower) 공정 완료: 양품 1개가 생산 수량으로 집계됩니다."),
        [PlcSignal.LowerProcessNg] = () =>
            Log.Warning("[M135] 하부(Lower) 공정 NG: 불량 1개가 불량 수량으로 집계됩니다."),
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
            Log.Error(ex, "PLC 디바이스 값을 읽는 중 오류가 발생했습니다. Device={Device}", deviceName);
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
            Log.Error(ex, "PLC 디바이스를 리셋(OFF) 하는 중 오류가 발생했습니다. Device={Device}", deviceName);
        }
    }

    private void HandleBusiness(PlcSignal signal)
    {
        if (signal is PlcSignal.UpperVisionOk or PlcSignal.UpperVisionNg or PlcSignal.LowerVisionOk or PlcSignal.LowerVisionNg)
        {
            try
            {
                switch (signal)
                {
                    case PlcSignal.UpperVisionOk:
                        _productionService.InsertVisionUpperResult("OK");
                        break;
                    case PlcSignal.UpperVisionNg:
                        _productionService.InsertVisionUpperResult("NG");
                        break;
                    case PlcSignal.LowerVisionOk:
                        _productionService.InsertVisionLowerResult("OK");
                        break;
                    case PlcSignal.LowerVisionNg:
                        _productionService.InsertVisionLowerResult("NG");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[M127~M130] Vision judgement DB insert failed. Signal={Signal}", signal);
            }

            return;
        }

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
                        "PLC 신호에 따른 주문 상태/생산 레코드 처리 중 오류가 발생했습니다. Signal={Signal}, Status={Status}",
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
                Log.Error(ex,
                    "[M131] 비정상 라인 리셋 신호 처리 중 최신 주문을 취소하는 과정에서 오류가 발생했습니다.");
            }
        }
        else if (signal is PlcSignal.UpperProcessCompleted or PlcSignal.LowerProcessCompleted)
        {
            try
            {
                _productionService.IncrementLatestProductionGoodQuantity();
            }
            catch (Exception ex)
            {
                Log.Error(ex,
                    "[M132/M134] 공정 완료 신호 처리 중 production.good_quantity 증가 처리에 실패했습니다. Signal={Signal}",
                    signal);
            }
        }
        else if (signal is PlcSignal.UpperProcessNg or PlcSignal.LowerProcessNg)
        {
            try
            {
                _productionService.IncrementLatestProductionBadQuantity();
            }
            catch (Exception ex)
            {
                Log.Error(ex,
                    "[M133/M135] 공정 NG 신호 처리 중 production.bad_quantity 증가 처리에 실패했습니다. Signal={Signal}",
                    signal);
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
