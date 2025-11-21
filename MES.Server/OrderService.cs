public class OrderService
{
    private readonly PlcConnector _plc;

    public OrderService(PlcConnector plc)
    {
        _plc = plc;
    }

    // PLC 흐름에 따라 주문을 처리한다.
    public void ProcessPlcOrder()
    {
        const string orderSignal = "M310";
        const string requestQuantityDevice = "D310";
        const string workOrderDevice = "D315";
        const string completionSignal = "M311";

        // 1. PLC 신호 확인 (M310): 주문 요청 신호가 ON인지 확인
        if (_plc.ReadDevice(orderSignal) != 1)
        {
            return;
        }

        // 2. D310에서 주문 수량 읽기
        int requestQty = _plc.ReadDevice(requestQuantityDevice);

        // 3. DB 처리: MySQL(cimon.주문1)에 주문 정보 저장
        SaveOrderToDatabase(requestQty);

        // 4. D315에 작업 지시 수량 쓰기 (확정)
        _plc.WriteDevice(workOrderDevice, requestQty);

        // 5. PLC에 "주문 처리 완료" 신호 주기 (M311 ON)
        _plc.WriteDevice(completionSignal, 1);
    }

    /// <summary>
    /// MySQL 데이터베이스(cimon.주문1)에 주문 수량을 저장한다.
    /// 주문ID는 DDNN 형식으로 생성한다. (DD: 일자 01~31, NN: 해당 일자의 순번 01부터 시작)
    /// </summary>
    /// <param name="requestQty">PLC에서 읽어온 주문 수량(D310)</param>
    private void SaveOrderToDatabase(int requestQty)
    {
        // 주문1 테이블 스키마:
        //  - 0번째 컬럼: 주문ID (Primary Key, NOT NULL)
        //  - 1번째 컬럼: 주문수량 (NOT NULL)
        const string connectionString =
            "Driver={MySQL ODBC 5.3 ANSI Driver};Server=127.0.0.1;Database=cimon;UID=cimonedu;PWD=cimonedu1234;";

        const string insertSql = "INSERT INTO cimon.주문1 (`주문ID`, `주문수량`) VALUES (?, ?)";

        try
        {
            using var connection = new OdbcConnection(connectionString);
            connection.Open();

            // 주문ID 생성: Day(DD) + 일별 순번(NN)
            string orderId = GenerateOrderId(connection);

            using var command = new OdbcCommand(insertSql, connection);
            // ODBC에서는 위치 기반 파라미터(?)를 사용한다.
            command.Parameters.AddWithValue("@p1", orderId);
            command.Parameters.AddWithValue("@p2", requestQty);

            int rows = command.ExecuteNonQuery();

            if (rows != 1)
            {
                Log.Warning("주문을 DB에 저장했지만 영향 받은 행 수가 예상과 다릅니다. RowsAffected={Rows}, RequestQty={RequestQty}, OrderId={OrderId}", rows, requestQty, orderId);
            }
            else
            {
                Log.Information("주문을 DB에 저장했습니다. 주문ID={OrderId}, 주문수량={RequestQty}", orderId, requestQty);
            }
        }
        catch (Exception ex)
        {
            // DB 오류는 로그로 남기고 상위에서 처리할 수 있도록 예외를 다시 던진다.
            Log.Error(ex, "주문 DB 저장 중 오류가 발생했습니다. RequestQty={RequestQty}", requestQty);
            throw;
        }
    }

    /// <summary>
    /// 주문ID를 DDNN 형식으로 생성한다.
    /// DD : 일(day, 01~31), NN : 해당 일자의 순번(01부터 시작, 이전 주문ID의 최대 순번 + 1)
    /// </summary>
    private string GenerateOrderId(OdbcConnection connection)
    {
        string dayPart = DateTime.Now.Day.ToString("00"); // 예: 05, 12, 31

        const string selectSql = "SELECT COALESCE(MAX(CAST(SUBSTRING(`주문ID`, 3, 2) AS UNSIGNED)), 0) FROM cimon.주문1 WHERE `주문ID` LIKE ?";

        using var command = new OdbcCommand(selectSql, connection);
        command.Parameters.AddWithValue("@p1", dayPart + "%");

        object? result = command.ExecuteScalar();
        int currentMax = 0;

        if (result != null && result != DBNull.Value)
        {
            currentMax = Convert.ToInt32(result);
        }

        int nextSequence = currentMax + 1;
        string orderId = $"{dayPart}{nextSequence:00}"; // DDNN

        return orderId;
    }
}
