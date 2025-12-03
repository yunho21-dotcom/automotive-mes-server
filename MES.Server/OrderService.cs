public class OrderService
{
    private readonly PlcConnector _plc;

    public OrderService(PlcConnector plc)
    {
        _plc = plc;
    }

    /// <summary>
    /// 웹 주문을 생성하고, 설비가 정지(M101 = 0) 상태일 때만 PLC로 전송한다.
    /// 설비가 동작 중이거나, 기타 오류가 있을 경우 false 를 반환하고 errorMessage 에 사유를 전달한다.
    /// </summary>
    /// <param name="modelCode">모델 코드 (KIA_CARNIVAL, KIA_SORENTO, KIA_SPORTAGE)</param>
    /// <param name="orderQuantity">주문 수량</param>
    /// <param name="errorMessage">오류 발생 시 사용자에게 표시할 메시지</param>
    /// <returns>성공 여부</returns>
    public bool CreateWebOrder(string modelCode, int orderQuantity, out string? errorMessage)
    {
        errorMessage = null;

        if (orderQuantity <= 0)
        {
            errorMessage = "주문 수량은 1 이상이어야 합니다.";
            return false;
        }

        if (!IsSupportedModel(modelCode))
        {
            errorMessage = "지원하지 않는 모델입니다.";
            return false;
        }

        const string machineStatusDevice = "M101";

        int machineStatus = _plc.ReadDevice(machineStatusDevice);
        if (machineStatus != 0)
        {
            errorMessage = "설비가 동작 중일 때는 주문을 넣을 수 없습니다.";
            Log.Warning("주문 거부: 설비 동작 중 (M101={MachineStatus}, Model={ModelCode}, Qty={Qty})",
                machineStatus, modelCode, orderQuantity);
            return false;
        }

        try
        {
            int orderId;

            // 1. DB에 주문 저장
            using (var connection = new OdbcConnection(
                       "Driver={MySQL ODBC 5.3 ANSI Driver};Server=127.0.0.1;Database=cimon;UID=cimonedu;PWD=cimonedu1234;"))
            {
                connection.Open();

                orderId = GenerateNewOrderId(connection);

                const string insertSql =
                    "INSERT INTO `cimon`.`order` (`order_id`, `model_code`, `order_quantity`, `order_date`, `order_status`) " +
                    "VALUES (?, ?, ?, ?, ?)";

                using var command = new OdbcCommand(insertSql, connection);
                command.Parameters.AddWithValue("@p1", orderId);
                command.Parameters.AddWithValue("@p2", modelCode);
                command.Parameters.AddWithValue("@p3", orderQuantity);
                command.Parameters.AddWithValue("@p4", DateTime.Now);
                command.Parameters.AddWithValue("@p5", "WAITING");

                int rows = command.ExecuteNonQuery();
                if (rows != 1)
                {
                    Log.Warning(
                        "웹 주문 DB 저장 영향 받은 행 수가 1이 아닙니다. RowsAffected={Rows}, OrderId={OrderId}, Model={ModelCode}, Qty={Qty}",
                        rows, orderId, modelCode, orderQuantity);
                    errorMessage = "주문 정보를 DB에 저장하지 못했습니다.";
                    return false;
                }
            }

            // 2. PLC로 주문 정보 전송 (모델명은 전송하지 않음)
            const string orderSignal = "M310";
            const string requestQuantityDevice = "D310";
            const string workOrderDevice = "D315";
            const string completionSignal = "M311";

            // 완료 신호 초기화
            _plc.WriteDevice(completionSignal, 0);

            // 주문 수량 및 작업 지시 수량 설정
            _plc.WriteDevice(requestQuantityDevice, orderQuantity);
            _plc.WriteDevice(workOrderDevice, orderQuantity);

            // 주문 요청 신호 ON
            _plc.WriteDevice(orderSignal, 1);

            Log.Information(
                "웹 주문 생성 및 PLC 전송 완료. OrderId={OrderId}, Model={ModelCode}, Qty={Qty}",
                orderId, modelCode, orderQuantity);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "웹 주문 처리 중 예외가 발생했습니다. Model={ModelCode}, Qty={Qty}", modelCode, orderQuantity);
            errorMessage = "주문 처리 중 오류가 발생했습니다.";
            return false;
        }
    }

    private static bool IsSupportedModel(string modelCode)
    {
        return modelCode == "KIA_CARNIVAL"
               || modelCode == "KIA_SORENTO"
               || modelCode == "KIA_SPORTAGE";
    }

    /// <summary>
    /// 신규 웹 주문용 주문ID를 1YYMMDDNNN 형식(총 10자리)으로 생성한다.
    /// - 1 : 맨 앞 자리는 항상 1
    /// - YYMMDD : 주문 일자(두 자리 연도, 월, 일)
    /// - NNN : 해당 일자의 순번(001부터 시작)
    /// </summary>
    private int GenerateNewOrderId(OdbcConnection connection)
    {
        string datePart = DateTime.Now.ToString("yyMMdd"); // 예: 250101

        int dayBase = int.Parse("1" + datePart + "000"); // 1YYMMDD000
        int dayEnd = int.Parse("1" + datePart + "999");  // 1YYMMDD999

        const string selectSql =
            "SELECT COALESCE(MAX(`order_id`), 0) FROM `cimon`.`order` WHERE `order_id` BETWEEN ? AND ?";

        using var command = new OdbcCommand(selectSql, connection);
        command.Parameters.AddWithValue("@p1", dayBase);
        command.Parameters.AddWithValue("@p2", dayEnd);

        object? result = command.ExecuteScalar();
        int currentMax = 0;

        if (result != null && result != DBNull.Value)
        {
            currentMax = Convert.ToInt32(result);
        }

        int nextSequence;
        if (currentMax == 0)
        {
            nextSequence = 1;
        }
        else
        {
            nextSequence = (currentMax % 1000) + 1;
        }

        if (nextSequence > 999)
        {
            throw new InvalidOperationException("하루에 생성 가능한 최대 주문 건수(999건)를 초과했습니다.");
        }

        int orderId = dayBase + nextSequence; // 1YYMMDDNNN
        return orderId;
    }
}
