public class PlcConnector : IDisposable
{
    private readonly ActUtlType _actUtlType;
    private readonly int _logicalStationNumber;
    private readonly object _syncRoot = new();
    private volatile bool _isConnected;
    private Timer? _monitorTimer;

    private const string OdbcConnectionString =
        "Driver={MySQL ODBC 5.3 ANSI Driver};Server=127.0.0.1;Database=cimon;UID=cimonedu;PWD=cimonedu1234;";

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

        Log.Information("PLC 연결을 시도합니다. Station={Station}", _logicalStationNumber);

        // 초기 연결 시도 (예외 발생 안 함). 실패 시, 백그라운드 타이머가
        // PLC가 사용 가능해질 때까지 계속 재시도합니다.
        EnsureConnected();

        // 매초 모니터링 및 재연결
        _monitorTimer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Log.Information("PLC 모니터링을 시작합니다. (M120~M131, 1초 간격, 자동 재연결 활성화)");
    }

    public int ReadDevice(string deviceName)
    {
        // 읽기를 시도하기 전에 PLC 연결을 확인합니다
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
        // 쓰기를 시도하기 전에 PLC 연결을 확인합니다
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

    /// <summary>
    /// 타이머 콜백: PLC 연결이 끊어지면 재연결을 시도합니다.
    /// 연결된 경우 M120~M131 신호를 모니터링합니다.
    /// </summary>
    private void OnTimerTick(object? state)
    {
        try
        {
            if (!EnsureConnected())
            {
                // 아직 연결되지 않음; 이번 틱에서는 신호 모니터링을 건너뜁니다.
                return;
            }

            MonitorSignals();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PLC 모니터링 중 예외가 발생했습니다.");
        }
    }

    /// <summary>
    /// PLC 연결이 열려 있는지 확인합니다.
    /// 연결된 경우 true, 그렇지 않은 경우 false를 반환합니다.
    /// </summary>
    private bool EnsureConnected()
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
                // 이전 세션이 반쯤 열려 있는 경우를 대비하여 먼저 닫습니다.
                _actUtlType.Close();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "재연결 전 PLC를 닫는 동안 오류 발생. Station={Station}", _logicalStationNumber);
            }

            result = _actUtlType.Open();
            _isConnected = result == 0;
        }

        if (result == 0)
        {
            Log.Information("PLC 연결이 열렸습니다. Station={Station}", _logicalStationNumber);
            return true;
        }

        Log.Warning("PLC 연결을 열지 못했습니다. Station={Station}, Code={Result}. 재시도합니다.",
            _logicalStationNumber, result);
        return false;
    }

    private void MonitorSignals()
    {
        try
        {
            string[] devices =
            {
                "M120", "M121", "M122", "M123", "M124",
                "M125", "M126", "M127", "M128", "M129",
                "M130", "M131"
            };

            foreach (var device in devices)
            {
                CheckAndHandleSignal(device);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PLC 신호 모니터링 중 오류 발생.");
        }
    }

    private void CheckAndHandleSignal(string deviceName)
    {
        int value;

        try
        {
            value = ReadDevice(deviceName);
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

        // 주문 상태 및 생산 처리
        if (deviceName is "M120" or "M121" or "M122" or "M123" or "M124")
        {
            string? newOrderStatus = deviceName switch
            {
                "M120" => "PROCESSING",
                "M121" => "COMPLETED",
                "M122" => "PAUSED",
                "M123" => "PROCESSING",
                "M124" => "CANCELLED",
                _ => null
            };

            if (!string.IsNullOrEmpty(newOrderStatus))
            {
                try
                {
                    // 1) 최신 주문 상태 업데이트
                    UpdateLatestOrderStatus(newOrderStatus);

                    // 2) M120이 켜지면 새 생산 행 생성
                    if (deviceName == "M120")
                    {
                        CreateProductionForLatestOrder();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex,
                        "PLC 장치에 대한 주문 상태/생산 처리 중 오류. Device={Device}, Status={Status}",
                        deviceName, newOrderStatus);
                }
            }
        }
        else if (deviceName == "M131")
        {
            // M131: 처리 중이거나 일시 중지된 경우 현재 주문 강제 취소
            try
            {
                CancelLatestOrderIfProcessingOrPaused();
            }
            catch (Exception ex)
            {
                Log.Error(ex,
                    "M131에서 최신 주문을 취소하는 동안 오류가 발생했습니다.");
            }
        }

        // 신호별 로그 출력
        switch (deviceName)
        {
            case "M120":
                Log.Information("[M120] 생산 라인 가동을 시작합니다.");
                break;
            case "M121":
                Log.Information("[M121] 전공정(Front-End) 작업이 완료되었습니다.");
                break;
            case "M122":
                Log.Warning("[M122] 일시정지 상태입니다. 현장의 설비 상태를 확인하십시오.");
                break;
            case "M123":
                Log.Information("[M123] 일시정지 상태에서 생산이 재개되었습니다.");
                break;
            case "M124":
                Log.Warning("[M124] 작업 취소 요청이 접수되었습니다. 현재 공정을 중단합니다.");
                break;
            case "M125":
                Log.Error("[M125] 비상정지(EMG)가 감지되었습니다. 모든 설비를 즉시 정지하십시오.");
                break;
            case "M126":
                Log.Information("[M126] 비상정지(EMG) 상태가 해제되었습니다.");
                break;
            case "M127":
                Log.Information("[M127] 상부(Upper) 비전 검사 결과: 양품(OK)");
                break;
            case "M128":
                Log.Warning("[M128] 상부(Upper) 비전 검사 결과: 불량(NG)");
                break;
            case "M129":
                Log.Information("[M129] 하부(Lower) 비전 검사 결과: 양품(OK)");
                break;
            case "M130":
                Log.Warning("[M130] 하부(Lower) 비전 검사 결과: 불량(NG)");
                break;
            case "M131":
                Log.Error("[M131] 생산 라인 가동중에 PLC의 비정상적인 종료 또는 리셋이 감지되었습니다.");
                break;
        }

        // 처리 후 비트를 OFF로 리셋
        try
        {
            WriteDevice(deviceName, 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PLC의 디바이스 리셋(OFF) 중 예외 발생 (Device={Device})", deviceName);
        }
    }

    /// <summary>
    /// 최신 주문 상태를 업데이트합니다. COMPLETED 또는 CANCELLED인 경우 생산 종료 날짜도 업데이트합니다.
    /// </summary>
    private void UpdateLatestOrderStatus(string newStatus)
    {
        using var connection = new OdbcConnection(OdbcConnectionString);
        connection.Open();

        const string selectSql =
            "SELECT `order_id` " +
            "FROM `cimon`.`order` " +
            "ORDER BY `order_date` DESC " +
            "LIMIT 1";

        using var selectCommand = new OdbcCommand(selectSql, connection);
        object? result = selectCommand.ExecuteScalar();

        if (result == null || result == DBNull.Value)
        {
            Log.Warning("주문 상태를 {Status}(으)로 갱신하려 했으나, order 테이블에 데이터가 없습니다.", newStatus);
            return;
        }

        int orderId = Convert.ToInt32(result);

        const string updateSql =
            "UPDATE `cimon`.`order` " +
            "SET `order_status` = ? " +
            "WHERE `order_id` = ?";

        using var updateCommand = new OdbcCommand(updateSql, connection);
        updateCommand.Parameters.AddWithValue("@p1", newStatus);
        updateCommand.Parameters.AddWithValue("@p2", orderId);

        int rows = updateCommand.ExecuteNonQuery();
        if (rows != 1)
        {
            Log.Warning("주문 상태 업데이트가 {Rows}개 행에 영향을 미쳤습니다. (예상: 1) OrderId={OrderId}, Status={Status}",
                rows, orderId, newStatus);
            return;
        }

        // 주문이 완료되거나 취소되면 생산 종료 날짜도 설정합니다
        if (newStatus == "COMPLETED" || newStatus == "CANCELLED")
        {
            try
            {
                UpdateLatestProductionEndDate();
            }
            catch (Exception ex)
            {
                Log.Error(ex,
                    "주문 상태 {Status}에 대한 생산 종료 날짜를 업데이트하는 동안 오류가 발생했습니다.",
                    newStatus);
            }
        }
    }

    /// <summary>
    /// M120 ON: 최신 주문을 기반으로 새 생산 행을 만듭니다.
    /// </summary>
    private void CreateProductionForLatestOrder()
    {
        using var connection = new OdbcConnection(OdbcConnectionString);
        connection.Open();

        const string selectOrderSql =
            "SELECT `order_id`, `model_code`, `order_quantity` " +
            "FROM `cimon`.`order` " +
            "ORDER BY `order_date` DESC " +
            "LIMIT 1";

        using var selectCommand = new OdbcCommand(selectOrderSql, connection);
        using var reader = selectCommand.ExecuteReader();

        if (!reader.Read())
        {
            Log.Warning("생산 데이터를 생성하려 했으나, order 테이블에 주문 데이터가 없습니다.");
            return;
        }

        string modelCode = reader["model_code"]?.ToString() ?? string.Empty;
        int orderQuantity = Convert.ToInt32(reader["order_quantity"]);

        int productionId = GenerateNewProductionId(connection);

        const string insertSql =
            "INSERT INTO `cimon`.`production` " +
            "(`production_id`, `model_code`, `upper_quantity`, `lower_quantity`, `good_quantity`, `bad_quantity`, `start_date`, `end_date`) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?)";

        using var insertCommand = new OdbcCommand(insertSql, connection);
        insertCommand.Parameters.AddWithValue("@p1", productionId);
        insertCommand.Parameters.AddWithValue("@p2", modelCode);
        insertCommand.Parameters.AddWithValue("@p3", orderQuantity);      // upper_quantity
        insertCommand.Parameters.AddWithValue("@p4", orderQuantity);      // lower_quantity
        insertCommand.Parameters.AddWithValue("@p5", 0);                  // good_quantity
        insertCommand.Parameters.AddWithValue("@p6", 0);                  // bad_quantity
        insertCommand.Parameters.AddWithValue("@p7", DateTime.Now);       // start_date
        insertCommand.Parameters.AddWithValue("@p8", DBNull.Value);       // end_date (NULL 허용)

        int rows = insertCommand.ExecuteNonQuery();
        if (rows != 1)
        {
            Log.Warning(
                "생산 INSERT가 {Rows}개 행에 영향을 미쳤습니다. (예상: 1) ProductionId={ProductionId}, Model={ModelCode}, Qty={Qty}",
                rows, productionId, modelCode, orderQuantity);
        }
        else
        {
            Log.Information(
                "새 생산 행을 만들었습니다. ProductionId={ProductionId}, Model={ModelCode}, Qty={Qty}",
                productionId, modelCode, orderQuantity);
        }

        // 최신 30개의 생산 행만 유지하고 오래된 행은 production_history로 이동합니다
        EnforceProductionRetention(connection);
    }

    /// <summary>
    /// 1YYMMDDNNN 형식(총 10자리)으로 새 production_id를 생성합니다.
    /// NNN은 하루에 001~999입니다(999를 초과하면 예외 발생).
    /// </summary>
    private int GenerateNewProductionId(OdbcConnection connection)
    {
        string datePart = DateTime.Now.ToString("yyMMdd"); // 예: 250101

        int dayBase = int.Parse("1" + datePart + "000"); // 1YYMMDD000
        int dayEnd = int.Parse("1" + datePart + "999");  // 1YYMMDD999

        const string selectSql =
            "SELECT COALESCE(MAX(`production_id`), 0) FROM `cimon`.`production` WHERE `production_id` BETWEEN ? AND ?";

        using var command = new OdbcCommand(selectSql, connection);
        command.Parameters.AddWithValue("@p1", dayBase);
        command.Parameters.AddWithValue("@p2", dayEnd);

        object? result = command.ExecuteScalar();
        int currentMax = 0;

        if (result != null && result != DBNull.Value)
        {
            currentMax = Convert.ToInt32(result);
        }

        int nextSequence = currentMax == 0 ? 1 : (currentMax % 1000) + 1;

        if (nextSequence > 999)
        {
            throw new InvalidOperationException("하루에 생성 가능한 최대 생산 건수(999건)를 초과했습니다.");
        }

        int productionId = dayBase + nextSequence; // 1YYMMDDNNN
        return productionId;
    }

    /// <summary>
    /// production 테이블을 최신 30개 행으로 유지하고 가장 오래된 행을 production_history로 이동합니다.
    /// </summary>
    private void EnforceProductionRetention(OdbcConnection connection)
    {
        const string countSql = "SELECT COUNT(*) FROM `cimon`.`production`";

        using var countCommand = new OdbcCommand(countSql, connection);
        object? countResult = countCommand.ExecuteScalar();

        int totalCount = 0;
        if (countResult != null && countResult != DBNull.Value)
        {
            totalCount = Convert.ToInt32(countResult);
        }

        if (totalCount <= 30)
        {
            return;
        }

        int toArchive = totalCount - 30;

        for (int i = 0; i < toArchive; i++)
        {
            const string selectOldestSql =
                "SELECT `production_id`, `model_code`, `upper_quantity`, `lower_quantity`, `good_quantity`, `bad_quantity`, `start_date`, `end_date` " +
                "FROM `cimon`.`production` " +
                "ORDER BY `start_date` ASC " +
                "LIMIT 1";

            using var selectCommand = new OdbcCommand(selectOldestSql, connection);
            using var reader = selectCommand.ExecuteReader();

            if (!reader.Read())
            {
                return;
            }

            int productionId = Convert.ToInt32(reader["production_id"]);
            string modelCode = reader["model_code"]?.ToString() ?? string.Empty;
            int upperQuantity = Convert.ToInt32(reader["upper_quantity"]);
            int lowerQuantity = Convert.ToInt32(reader["lower_quantity"]);
            int goodQuantity = Convert.ToInt32(reader["good_quantity"]);
            int badQuantity = Convert.ToInt32(reader["bad_quantity"]);
            DateTime startDate = (DateTime)reader["start_date"];
            object endDateObj = reader["end_date"];
            DateTime? endDate = endDateObj == DBNull.Value ? (DateTime?)null : (DateTime)endDateObj;

            int backupId = GetNextProductionHistoryBackupId(connection);

            const string insertHistorySql =
                "INSERT INTO `cimon`.`production_history` " +
                "(`backup_id`, `production_id`, `model_code`, `upper_quantity`, `lower_quantity`, `good_quantity`, `bad_quantity`, `start_date`, `end_date`, `backed_date`) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            using (var insertHistoryCommand = new OdbcCommand(insertHistorySql, connection))
            {
                insertHistoryCommand.Parameters.AddWithValue("@p1", backupId);
                insertHistoryCommand.Parameters.AddWithValue("@p2", productionId);
                insertHistoryCommand.Parameters.AddWithValue("@p3", modelCode);
                insertHistoryCommand.Parameters.AddWithValue("@p4", upperQuantity);
                insertHistoryCommand.Parameters.AddWithValue("@p5", lowerQuantity);
                insertHistoryCommand.Parameters.AddWithValue("@p6", goodQuantity);
                insertHistoryCommand.Parameters.AddWithValue("@p7", badQuantity);
                insertHistoryCommand.Parameters.AddWithValue("@p8", startDate);
                if (endDate.HasValue)
                {
                    insertHistoryCommand.Parameters.AddWithValue("@p9", endDate.Value);
                }
                else
                {
                    insertHistoryCommand.Parameters.AddWithValue("@p9", DBNull.Value);
                }

                insertHistoryCommand.Parameters.AddWithValue("@p10", DateTime.Now);

                int historyRows = insertHistoryCommand.ExecuteNonQuery();
                if (historyRows != 1)
                {
                    Log.Warning(
                        "production_history INSERT가 {Rows}개 행에 영향을 미쳤습니다(예상: 1). ProductionId={ProductionId}, BackupId={BackupId}",
                        historyRows, productionId, backupId);
                }
            }

            const string deleteSql =
                "DELETE FROM `cimon`.`production` " +
                "WHERE `production_id` = ?";

            using (var deleteCommand = new OdbcCommand(deleteSql, connection))
            {
                deleteCommand.Parameters.AddWithValue("@p1", productionId);

                int deleteRows = deleteCommand.ExecuteNonQuery();
                if (deleteRows != 1)
                {
                    Log.Warning(
                        "production DELETE가 {Rows}개 행에 영향을 미쳤습니다(예상: 1). ProductionId={ProductionId}",
                        deleteRows, productionId);
                }
            }
        }
    }

    /// <summary>
    /// production_history의 다음 backup_id를 생성합니다(0,1,2,...).
    /// </summary>
    private int GetNextProductionHistoryBackupId(OdbcConnection connection)
    {
        const string sql = "SELECT COALESCE(MAX(`backup_id`), -1) FROM `cimon`.`production_history`";

        using var command = new OdbcCommand(sql, connection);
        object? result = command.ExecuteScalar();

        int current = -1;
        if (result != null && result != DBNull.Value)
        {
            current = Convert.ToInt32(result);
        }

        return current + 1;
    }

    /// <summary>
    /// 최신 주문이 COMPLETED/CANCELLED가 되면 최신 진행 중인 생산의 end_date를 설정합니다.
    /// </summary>
    private void UpdateLatestProductionEndDate()
    {
        using var connection = new OdbcConnection(OdbcConnectionString);
        connection.Open();

        const string selectSql =
            "SELECT `production_id` " +
            "FROM `cimon`.`production` " +
            "WHERE `end_date` IS NULL " +
            "ORDER BY `start_date` DESC " +
            "LIMIT 1";

        using var selectCommand = new OdbcCommand(selectSql, connection);
        object? result = selectCommand.ExecuteScalar();

        if (result == null || result == DBNull.Value)
        {
            Log.Warning("생산 종료 날짜를 설정하려고 했지만 진행 중인 생산 데이터가 없습니다(end_date IS NULL).");
            return;
        }

        int productionId = Convert.ToInt32(result);

        const string updateSql =
            "UPDATE `cimon`.`production` " +
            "SET `end_date` = ? " +
            "WHERE `production_id` = ?";

        using var updateCommand = new OdbcCommand(updateSql, connection);
        updateCommand.Parameters.AddWithValue("@p1", DateTime.Now);
        updateCommand.Parameters.AddWithValue("@p2", productionId);

        int rows = updateCommand.ExecuteNonQuery();
        if (rows != 1)
        {
            Log.Warning(
                "production 종료 시간(end_date) 갱신 영향 행 수가 1이 아닙니다. Rows={Rows}, ProductionId={ProductionId}",
                rows, productionId);
        }
        else
        {
            Log.Information("생산 종료 날짜를 업데이트했습니다. ProductionId={ProductionId}", productionId);
        }
    }

    /// <summary>
    /// M131: 최신 주문이 PROCESSING 또는 PAUSED 상태이면 CANCELLED로 변경합니다.
    /// 또한 생산 종료 날짜도 업데이트합니다.
    /// </summary>
    private void CancelLatestOrderIfProcessingOrPaused()
    {
        using var connection = new OdbcConnection(OdbcConnectionString);
        connection.Open();

        const string selectSql =
            "SELECT `order_id`, `order_status` " +
            "FROM `cimon`.`order` " +
            "ORDER BY `order_date` DESC " +
            "LIMIT 1";

        using var selectCommand = new OdbcCommand(selectSql, connection);
        using var reader = selectCommand.ExecuteReader();

        if (!reader.Read())
        {
            Log.Warning("[M131] order 테이블에 최신 주문 데이터가 없습니다.");
            return;
        }

        string? currentStatus = reader["order_status"]?.ToString();

        if (currentStatus != "PROCESSING" && currentStatus != "PAUSED")
        {
            // 최신 주문이 '처리 중'/'일시 중지' 상태가 아니면 아무 작업도 하지 않습니다.
            return;
        }

        int orderId = Convert.ToInt32(reader["order_id"]);

        const string updateSql =
            "UPDATE `cimon`.`order` " +
            "SET `order_status` = 'CANCELLED' " +
            "WHERE `order_id` = ?";

        using var updateCommand = new OdbcCommand(updateSql, connection);
        updateCommand.Parameters.AddWithValue("@p1", orderId);

        int rows = updateCommand.ExecuteNonQuery();
        if (rows != 1)
        {
            Log.Warning("[M131] 최신 주문 상태를 CANCELLED 로 변경했으나, 영향 행 수가 1이 아닙니다. Rows={Rows}, OrderId={OrderId}",
                rows, orderId);
            return;
        }

        Log.Information("[M131] 최신 주문 상태가 CANCELLED로 변경되었습니다. OrderId={OrderId}, PreviousStatus={Status}",
            orderId, currentStatus);

        try
        {
            UpdateLatestProductionEndDate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[M131] 주문 취소 후 생산 종료 날짜 업데이트 중 오류 발생.");
        }
    }

    public void Dispose()
    {
        try
        {
            _monitorTimer?.Dispose();
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
