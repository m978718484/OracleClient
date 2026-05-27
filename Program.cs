using Aprillz.MewUI;
using OracleClient.Models;
using OracleClient.Services;
using OracleClient.UI;

namespace OracleClient;

/// <summary>
/// Oracle Developer - PL/SQL Developer风格的Oracle客户端管理器
/// 基于 MewUI (Aprillz.MewUI 0.15.1) + 原生 ADO.NET
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test")
        {
            await RunIntegrationTest();
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Win32Platform.Register();
            Direct2DBackend.Register();
        }
        else if (OperatingSystem.IsLinux())
        {
            X11Platform.Register();
            MewVGX11Backend.Register();
        }
        else if (OperatingSystem.IsMacOS())
        {
            MacOSPlatform.Register();
            MewVGMacOSBackend.Register();
        }

        // 构建并运行主窗口
        var mainWindow = new MainWindow();
        Application.Run(mainWindow.Build());
    }

    private static async Task RunIntegrationTest()
    {
        using var log = new StreamWriter("test_result.txt", false, System.Text.Encoding.UTF8);
        Action<string> write = msg => { Console.WriteLine(msg); log.WriteLine(msg); };

        var service = new OracleService();
        var info = new ConnectionInfo
        {
            Name = "Test",
            Host = "100.66.115.67",
            Port = 1521,
            ServiceName = "ORCL",
            UseServiceName = true,
            Username = "SCOTT",
            Password = "tiger",
            Role = "Normal"
        };

        write("=== Oracle Developer 集成测试 ===\n");

        // 1. 测试连接
        write("[1/6] TestConnection... ");
        try
        {
            var testOk = await service.TestConnectionAsync(info);
            write(testOk ? "OK" : "FAILED");
            if (!testOk) { log.Close(); return; }
        }
        catch (Exception ex)
        {
            write($"ERROR: {ex.Message}");
            log.Close(); return;
        }

        // 2. 正式连接
        write("[2/6] Connect... ");
        var connected = await service.ConnectAsync(info);
        write(connected ? "OK" : "FAILED");
        if (!connected) { log.Close(); return; }

        // 3. 数据库概览
        write("[3/6] GetDatabaseOverview... ");
        try
        {
            var overview = await service.GetDatabaseOverviewAsync();
            write("OK");
            write($"       Version: {overview.Version}");
            write($"       Instance: {overview.InstanceName}");
            write($"       Host: {overview.HostName}");
            write($"       Tables: {overview.TableCount}, Views: {overview.ViewCount}, Procs: {overview.ProcedureCount}");
        }
        catch (Exception ex)
        {
            write($"ERROR: {ex.Message}");
        }

        // 4. Schema 对象
        write("[4/6] GetSchemaObjects... ");
        try
        {
            var objects = await service.GetSchemaObjectsAsync();
            write("OK");
            foreach (var folder in objects)
            {
                var names = folder.Children.Select(c => c.Name).ToList();
                write($"       {folder.Name}: [{string.Join(", ", names.Take(5))}{(names.Count > 5 ? "..." : "")}] ({names.Count})");
            }
        }
        catch (Exception ex)
        {
            write($"ERROR: {ex.Message}");
        }

        // 5. 查询
        write("[5/6] ExecuteQuery (SELECT * FROM emp)... ");
        try
        {
            var (columns, rows, error) = await service.ExecuteQueryAsync("SELECT * FROM emp");
            if (error != null)
            {
                write($"ERROR: {error}");
            }
            else
            {
                write("OK");
                write($"       Columns: [{string.Join(", ", columns.Select(c => c.Name))}]");
                write($"       Rows: {rows.Count}");
                if (rows.Count > 0)
                    write($"       First row: [{string.Join(", ", rows[0].Values.Select(v => v?.ToString() ?? "NULL"))}]");
            }
        }
        catch (Exception ex)
        {
            write($"ERROR: {ex.Message}");
        }

        // 6. 表列信息
        write("[6/6] GetTableColumns (EMP)... ");
        try
        {
            var cols = await service.GetTableColumnsAsync("EMP");
            write("OK");
            foreach (var col in cols)
                write($"       {col.ColumnName} {col.DataType}({col.DataLength}) Nullable={col.Nullable}");
        }
        catch (Exception ex)
        {
            write($"ERROR: {ex.Message}");
        }

        // 断开
        service.Disconnect();
        write("\n=== 测试完成 ===");
    }
}
