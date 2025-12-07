public class OrderDbService : IOrderService
{
    private readonly IProductionService _productionService;

    public OrderDbService(IProductionService productionService)
    {
        _productionService = productionService;
    }

    public void UpdateLatestOrderStatus(OrderStatus newStatus)
    {
        using var connection = new OdbcConnection(DbConstants.OdbcConnectionString);
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
            Log.Warning("주문 상태를 {Status}(으)로 갱신하려 했으나, order 테이블에 데이터가 없습니다.",
                newStatus.ToDbString());
            return;
        }

        int orderId = Convert.ToInt32(result);

        const string updateSql =
            "UPDATE `cimon`.`order` " +
            "SET `order_status` = ? " +
            "WHERE `order_id` = ?";

        using var updateCommand = new OdbcCommand(updateSql, connection);
        updateCommand.Parameters.AddWithValue("@p1", newStatus.ToDbString());
        updateCommand.Parameters.AddWithValue("@p2", orderId);

        int rows = updateCommand.ExecuteNonQuery();
        if (rows != 1)
        {
            Log.Warning(
                "주문 상태 업데이트가 {Rows}개 행에 영향을 미쳤습니다. (예상: 1) OrderId={OrderId}, Status={Status}",
                rows, orderId, newStatus.ToDbString());
            return;
        }

        if (newStatus is OrderStatus.Completed or OrderStatus.Cancelled)
        {
            try
            {
                _productionService.UpdateLatestProductionEndDate();
            }
            catch (Exception ex)
            {
                Log.Error(ex,
                    "주문 상태 {Status}에 대한 생산 종료 날짜를 업데이트하는 동안 오류가 발생했습니다.",
                    newStatus.ToDbString());
            }
        }
    }

    public void CancelLatestOrderIfProcessingOrPaused()
    {
        using var connection = new OdbcConnection(DbConstants.OdbcConnectionString);
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
            Log.Warning(
                "[M131] 최신 주문 상태를 CANCELLED 로 변경했으나, 영향 행 수가 1이 아닙니다. Rows={Rows}, OrderId={OrderId}",
                rows, orderId);
            return;
        }

        Log.Information(
            "[M131] 최신 주문 상태가 CANCELLED로 변경되었습니다. OrderId={OrderId}, PreviousStatus={Status}",
            orderId, currentStatus);

        try
        {
            _productionService.UpdateLatestProductionEndDate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[M131] 주문 취소 후 생산 종료 날짜 업데이트 중 오류 발생.");
        }
    }
}
