using System.Data;
using Oracle.ManagedDataAccess.Client;
using OracleClient.Models;

namespace OracleClient.Services;

/// <summary>
/// Oracle数据库服务 - 使用原生ADO.NET查询（AOT兼容）
/// </summary>
public sealed class OracleService
{
    private string _connectionString = "";
    private bool _isConnected;

    public bool IsConnected => _isConnected;

    public event Action<bool>? ConnectionStateChanged;

    /// <summary>
    /// 测试连接
    /// </summary>
    public async Task<bool> TestConnectionAsync(ConnectionInfo info)
    {
        try
        {
            var connStr = info.BuildConnectionString();
            using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 连接数据库
    /// </summary>
    public async Task<bool> ConnectAsync(ConnectionInfo info)
    {
        try
        {
            _connectionString = info.BuildConnectionString();
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();
            _isConnected = true;
            ConnectionStateChanged?.Invoke(true);
            return true;
        }
        catch
        {
            _isConnected = false;
            ConnectionStateChanged?.Invoke(false);
            return false;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        _isConnected = false;
        _connectionString = "";
        ConnectionStateChanged?.Invoke(false);
    }

    /// <summary>
    /// 执行查询并返回结果
    /// </summary>
    public async Task<(List<ResultColumn> Columns, List<ResultRow> Rows, string? Error)> ExecuteQueryAsync(string sql, int maxRows = 1000)
    {
        if (!_isConnected)
            return ([], [], "Not connected to database");

        try
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 300;

            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default);

            var columns = new List<ResultColumn>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new ResultColumn
                {
                    Name = reader.GetName(i),
                    DataType = reader.GetDataTypeName(i),
                    Width = Math.Max(80, Math.Min(300, reader.GetName(i).Length * 10 + 40))
                });
            }

            var rows = new List<ResultRow>();
            while (await reader.ReadAsync() && rows.Count < maxRows)
            {
                var values = new object?[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                        values[i] = null;
                    else
                        values[i] = reader.GetValue(i);
                }
                rows.Add(new ResultRow { Values = values });
            }

            return (columns, rows, null);
        }
        catch (Exception ex)
        {
            return ([], [], ex.Message);
        }
    }

    /// <summary>
    /// 执行非查询语句
    /// </summary>
    public async Task<(int AffectedRows, string? Error)> ExecuteNonQueryAsync(string sql)
    {
        if (!_isConnected)
            return (0, "Not connected to database");

        try
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var affected = await cmd.ExecuteNonQueryAsync();
            return (affected, null);
        }
        catch (Exception ex)
        {
            return (0, ex.Message);
        }
    }

    /// <summary>
    /// 获取Schema下的对象列表
    /// </summary>
    public async Task<List<ObjectNode>> GetSchemaObjectsAsync()
    {
        if (!_isConnected) return [];

        try
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            var objects = new List<ObjectNode>();

            // 表
            var tableNames = await ReadObjectNamesAsync(conn, "TABLE", "user_tables", "table_name");
            objects.Add(new ObjectNode
            {
                Name = "Tables",
                NodeType = ObjectNodeType.TableFolder,
                HasChildren = true,
                Children = tableNames.Select(n => new ObjectNode
                {
                    Name = n,
                    NodeType = ObjectNodeType.Table,
                    HasChildren = true
                }).ToList()
            });

            // 视图
            var viewNames = await ReadObjectNamesAsync(conn, "VIEW", "user_views", "view_name");
            objects.Add(new ObjectNode
            {
                Name = "Views",
                NodeType = ObjectNodeType.ViewFolder,
                HasChildren = true,
                Children = viewNames.Select(n => new ObjectNode
                {
                    Name = n,
                    NodeType = ObjectNodeType.View,
                    HasChildren = true
                }).ToList()
            });

            // 存储过程
            var procNames = await ReadObjectNamesByTypeAsync(conn, "PROCEDURE");
            objects.Add(new ObjectNode
            {
                Name = "Procedures",
                NodeType = ObjectNodeType.ProcedureFolder,
                HasChildren = true,
                Children = procNames.Select(n => new ObjectNode
                {
                    Name = n,
                    NodeType = ObjectNodeType.Procedure,
                    HasChildren = false
                }).ToList()
            });

            // 函数
            var funcNames = await ReadObjectNamesByTypeAsync(conn, "FUNCTION");
            objects.Add(new ObjectNode
            {
                Name = "Functions",
                NodeType = ObjectNodeType.FunctionFolder,
                HasChildren = true,
                Children = funcNames.Select(n => new ObjectNode
                {
                    Name = n,
                    NodeType = ObjectNodeType.Function,
                    HasChildren = false
                }).ToList()
            });

            // 包
            var pkgNames = await ReadObjectNamesByTypeAsync(conn, "PACKAGE");
            objects.Add(new ObjectNode
            {
                Name = "Packages",
                NodeType = ObjectNodeType.PackageFolder,
                HasChildren = true,
                Children = pkgNames.Select(n => new ObjectNode
                {
                    Name = n,
                    NodeType = ObjectNodeType.Package,
                    HasChildren = false
                }).ToList()
            });

            // 触发器
            var trigNames = await ReadObjectNamesByTypeAsync(conn, "TRIGGER");
            objects.Add(new ObjectNode
            {
                Name = "Triggers",
                NodeType = ObjectNodeType.TriggerFolder,
                HasChildren = true,
                Children = trigNames.Select(n => new ObjectNode
                {
                    Name = n,
                    NodeType = ObjectNodeType.Trigger,
                    HasChildren = false
                }).ToList()
            });

            // 序列
            var seqNames = await ReadObjectNamesAsync(conn, "SEQUENCE", "user_sequences", "sequence_name");
            objects.Add(new ObjectNode
            {
                Name = "Sequences",
                NodeType = ObjectNodeType.SequenceFolder,
                HasChildren = true,
                Children = seqNames.Select(n => new ObjectNode
                {
                    Name = n,
                    NodeType = ObjectNodeType.Sequence,
                    HasChildren = false
                }).ToList()
            });

            // 索引
            var idxNames = await ReadObjectNamesAsync(conn, "INDEX", "user_indexes", "index_name");
            objects.Add(new ObjectNode
            {
                Name = "Indexes",
                NodeType = ObjectNodeType.IndexFolder,
                HasChildren = true,
                Children = idxNames.Select(n => new ObjectNode
                {
                    Name = n,
                    NodeType = ObjectNodeType.Index,
                    HasChildren = false
                }).ToList()
            });

            // 同义词
            var synNames = await ReadObjectNamesAsync(conn, "SYNONYM", "user_synonyms", "synonym_name");
            objects.Add(new ObjectNode
            {
                Name = "Synonyms",
                NodeType = ObjectNodeType.SynonymFolder,
                HasChildren = true,
                Children = synNames.Select(n => new ObjectNode
                {
                    Name = n,
                    NodeType = ObjectNodeType.Synonym,
                    HasChildren = false
                }).ToList()
            });

            return objects;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 获取表列信息
    /// </summary>
    public async Task<List<TableColumnInfo>> GetTableColumnsAsync(string tableName)
    {
        if (!_isConnected) return [];

        try
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT c.column_name, c.data_type, c.data_length, c.data_precision, c.data_scale,
                       c.nullable, c.data_default, cc.comments
                FROM user_tab_columns c
                LEFT JOIN user_col_comments cc ON c.table_name = cc.table_name AND c.column_name = cc.column_name
                WHERE c.table_name = :tableName
                ORDER BY c.column_id";
            cmd.Parameters.Add(new OracleParameter("tableName", tableName.ToUpper()));

            var columns = new List<TableColumnInfo>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new TableColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    DataLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    DataPrecision = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    DataScale = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Nullable = reader.GetString(5) == "Y",
                    DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Comments = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }

            return columns;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 获取数据库概览
    /// </summary>
    public async Task<DatabaseOverview> GetDatabaseOverviewAsync()
    {
        if (!_isConnected)
            return new DatabaseOverview();

        try
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            var overview = new DatabaseOverview();

            // 版本信息
            overview.Version = await ExecuteScalarAsync(conn, 
                "SELECT version FROM product_component_version WHERE product LIKE 'Oracle%'") ?? "Unknown";

            // 实例名
            overview.InstanceName = await ExecuteScalarAsync(conn,
                "SELECT instance_name FROM v$instance") ?? "";

            // 主机名
            overview.HostName = await ExecuteScalarAsync(conn,
                "SELECT host_name FROM v$instance") ?? "";

            // 对象计数
            overview.TableCount = await ExecuteScalarIntAsync(conn, "SELECT COUNT(*) FROM user_tables");
            overview.ViewCount = await ExecuteScalarIntAsync(conn, "SELECT COUNT(*) FROM user_views");
            overview.ProcedureCount = await ExecuteScalarIntAsync(conn,
                "SELECT COUNT(*) FROM user_objects WHERE object_type = 'PROCEDURE'");

            // 活跃会话
            try
            {
                overview.ActiveSessions = await ExecuteScalarIntAsync(conn,
                    "SELECT COUNT(*) FROM v$session WHERE status = 'ACTIVE' AND username IS NOT NULL");
            }
            catch { overview.ActiveSessions = -1; }

            return overview;
        }
        catch
        {
            return new DatabaseOverview();
        }
    }

    /// <summary>
    /// 获取对象DDL
    /// </summary>
    public async Task<string> GetObjectDdlAsync(string objectType, string objectName)
    {
        if (!_isConnected) return "";

        try
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DBMS_METADATA.GET_DDL(:type, :name) FROM dual";
            cmd.Parameters.Add(new OracleParameter("type", objectType.ToUpper()));
            cmd.Parameters.Add(new OracleParameter("name", objectName.ToUpper()));

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "";
        }
        catch (Exception ex)
        {
            return $"-- Error getting DDL: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取源代码
    /// </summary>
    public async Task<string> GetSourceCodeAsync(string objectType, string objectName)
    {
        if (!_isConnected) return "";

        try
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT text FROM user_source WHERE type = :type AND name = :name ORDER BY line";
            cmd.Parameters.Add(new OracleParameter("type", objectType.ToUpper()));
            cmd.Parameters.Add(new OracleParameter("name", objectName.ToUpper()));

            var sb = new System.Text.StringBuilder();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sb.Append(reader.GetString(0));
            }

            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// 获取表数据
    /// </summary>
    public async Task<(List<ResultColumn> Columns, List<ResultRow> Rows, string? Error)> GetTableDataAsync(string tableName, int maxRows = 500)
    {
        return await ExecuteQueryAsync($"SELECT * FROM \"{tableName}\" WHERE ROWNUM <= {maxRows}", maxRows);
    }

    #region Helper Methods (ADO.NET, AOT-safe)

    private static async Task<List<string>> ReadObjectNamesByTypeAsync(OracleConnection conn, string objectType)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT object_name FROM user_objects WHERE object_type = :type ORDER BY object_name";
        cmd.Parameters.Add(new OracleParameter("type", objectType));

        var names = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    private static async Task<List<string>> ReadObjectNamesAsync(OracleConnection conn, string type, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {column} FROM {table} ORDER BY {column}";

        var names = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    private static async Task<string?> ExecuteScalarAsync(OracleConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    private static async Task<int> ExecuteScalarIntAsync(OracleConnection conn, string sql)
    {
        var result = await ExecuteScalarAsync(conn, sql);
        return int.TryParse(result, out var v) ? v : 0;
    }

    #endregion
}
