public class ProductionService : IProductionService
{
    public void CreateProductionForLatestOrder()
    {
        using var connection = new OdbcConnection(DbConstants.OdbcConnectionString);
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
        insertCommand.Parameters.AddWithValue("@p3", orderQuantity);
        insertCommand.Parameters.AddWithValue("@p4", orderQuantity);
        insertCommand.Parameters.AddWithValue("@p5", 0);
        insertCommand.Parameters.AddWithValue("@p6", 0);
        insertCommand.Parameters.AddWithValue("@p7", DateTime.Now);
        insertCommand.Parameters.AddWithValue("@p8", DBNull.Value);

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

        EnforceProductionRetention(connection);
    }

    public void UpdateLatestProductionEndDate()
    {
        using var connection = new OdbcConnection(DbConstants.OdbcConnectionString);
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
            Log.Warning("생산 종료 날짜를 설정하려고 했지만 진행 중인 생산 데이터가 없습니다. (end_date IS NULL)");
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
                "production 종료 시간(end_date) 갱신 작업에 영향을 받은 행 수가 1이 아닙니다. Rows={Rows}, ProductionId={ProductionId}",
                rows, productionId);
        }
        else
        {
            Log.Information("생산 종료 시간을 업데이트했습니다. ProductionId={ProductionId}", productionId);
        }
    }

    public void EnforceProductionRetention()
    {
        using var connection = new OdbcConnection(DbConstants.OdbcConnectionString);
        connection.Open();
        EnforceProductionRetention(connection);
    }

    public void IncrementLatestProductionGoodQuantity()
    {
        using var connection = new OdbcConnection(DbConstants.OdbcConnectionString);
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
            Log.Warning("good_quantity를 증가시키려고 했으나 진행 중인 production 레코드(end_date IS NULL)를 찾을 수 없습니다.");
            return;
        }

        int productionId = Convert.ToInt32(result);

        const string updateSql =
            "UPDATE `cimon`.`production` " +
            "SET `good_quantity` = `good_quantity` + 1 " +
            "WHERE `production_id` = ?";

        using var updateCommand = new OdbcCommand(updateSql, connection);
        updateCommand.Parameters.AddWithValue("@p1", productionId);

        int rows = updateCommand.ExecuteNonQuery();
        if (rows != 1)
        {
            Log.Warning(
                "good_quantity 증가 UPDATE가 {Rows}건에 대해 실행되었습니다. (예상: 1건) ProductionId={ProductionId}",
                rows, productionId);
        }
        else
        {
            Log.Information("production의 good_quantity를 1 증가시켰습니다. ProductionId={ProductionId}", productionId);
        }
    }

    public void IncrementLatestProductionBadQuantity()
    {
        using var connection = new OdbcConnection(DbConstants.OdbcConnectionString);
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
            Log.Warning("bad_quantity를 증가시키려고 했으나 진행 중인 production 레코드(end_date IS NULL)를 찾을 수 없습니다.");
            return;
        }

        int productionId = Convert.ToInt32(result);

        const string updateSql =
            "UPDATE `cimon`.`production` " +
            "SET `bad_quantity` = `bad_quantity` + 1 " +
            "WHERE `production_id` = ?";

        using var updateCommand = new OdbcCommand(updateSql, connection);
        updateCommand.Parameters.AddWithValue("@p1", productionId);

        int rows = updateCommand.ExecuteNonQuery();
        if (rows != 1)
        {
            Log.Warning(
                "bad_quantity 증가 UPDATE가 {Rows}건에 대해 실행되었습니다. (예상: 1건) ProductionId={ProductionId}",
                rows, productionId);
        }
        else
        {
            Log.Information("production의 bad_quantity를 1 증가시켰습니다. ProductionId={ProductionId}", productionId);
        }
    }

    private int GenerateNewProductionId(OdbcConnection connection)
    {
        string datePart = DateTime.Now.ToString("yyMMdd");

        int dayBase = int.Parse("1" + datePart + "000");
        int dayEnd = int.Parse("1" + datePart + "999");

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

        int productionId = dayBase + nextSequence;
        return productionId;
    }

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
                        "production_history INSERT가 {Rows}개 행에 영향을 미쳤습니다. (예상: 1) ProductionId={ProductionId}, BackupId={BackupId}",
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
                        "production DELETE가 {Rows}개 행에 영향을 미쳤습니다. (예상: 1) ProductionId={ProductionId}",
                        deleteRows, productionId);
                }
            }
        }
    }

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
}
