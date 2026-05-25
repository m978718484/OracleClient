namespace OracleClient.Models;

/// <summary>
/// Oracle连接信息
/// </summary>
public sealed class ConnectionInfo
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1521;
    public string ServiceName { get; set; } = "ORCL";
    public string Sid { get; set; } = "";
    public bool UseServiceName { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "Normal"; // Normal, SYSDBA, SYSOPER

    public string BuildConnectionString()
    {
        // Oracle.ManagedDataAccess.Core uses EZConnect format: host:port/service_name
        var dataSource = UseServiceName && !string.IsNullOrWhiteSpace(ServiceName)
            ? $"{Host}:{Port}/{ServiceName}"
            : $"{Host}:{Port}/{Sid}";

        var cs = $"Data Source={dataSource};User Id={Username};Password={Password};";

        if (Role == "SYSDBA")
            cs += "DBA Privilege=SYSDBA;";
        else if (Role == "SYSOPER")
            cs += "DBA Privilege=SYSOPER;";

        return cs;
    }
}

/// <summary>
/// 对象浏览器节点类型
/// </summary>
public enum ObjectNodeType
{
    Root,
    TableFolder,
    ViewFolder,
    ProcedureFolder,
    FunctionFolder,
    PackageFolder,
    TriggerFolder,
    SequenceFolder,
    IndexFolder,
    SynonymFolder,
    ObjectType,
    Table,
    View,
    Procedure,
    Function,
    Package,
    PackageBody,
    Trigger,
    Sequence,
    Index,
    Synonym,
    Column,
    Constraint,
}

/// <summary>
/// 对象浏览器节点
/// </summary>
public sealed class ObjectNode
{
    public string Name { get; set; } = "";
    public string Schema { get; set; } = "";
    public ObjectNodeType NodeType { get; set; }
    public List<ObjectNode> Children { get; set; } = [];
    public string Tooltip { get; set; } = "";
    public bool HasChildren { get; set; }
}

/// <summary>
/// 查询结果列
/// </summary>
public sealed class ResultColumn
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public int Width { get; set; } = 120;
}

/// <summary>
/// 查询结果行 (值为object数组)
/// </summary>
public sealed class ResultRow
{
    public object?[] Values { get; set; } = [];
}

/// <summary>
/// SQL编辑器标签页
/// </summary>
public sealed class SqlTab
{
    public string Title { get; set; } = "SQL";
    public string Content { get; set; } = "";
    public bool IsModified { get; set; }
    public string FilePath { get; set; } = "";
}

/// <summary>
/// 表结构信息
/// </summary>
public sealed class TableColumnInfo
{
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public int? DataLength { get; set; }
    public int? DataPrecision { get; set; }
    public int? DataScale { get; set; }
    public bool Nullable { get; set; }
    public string? DefaultValue { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// 数据库概览信息
/// </summary>
public sealed class DatabaseOverview
{
    public string Version { get; set; } = "";
    public string InstanceName { get; set; } = "";
    public string HostName { get; set; } = "";
    public long TotalSize { get; set; }
    public long UsedSize { get; set; }
    public int TableCount { get; set; }
    public int ViewCount { get; set; }
    public int ProcedureCount { get; set; }
    public int ActiveSessions { get; set; }
    public long SgaSize { get; set; }
    public long PgaSize { get; set; }
}
