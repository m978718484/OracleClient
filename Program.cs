using Aprillz.MewUI;
using OracleClient.Services;
using OracleClient.UI;

namespace OracleClient;

/// <summary>
/// Oracle Developer - PL/SQL Developer风格的Oracle客户端管理器
/// 基于 MewUI (Aprillz.MewUI 0.15.1) + Dapper.AOT
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        // 注册MewUI平台和渲染后端
        // 跨平台：根据操作系统自动选择
#if WINDOWS
        Win32Platform.Register();
        Direct2DBackend.Register();     // Windows推荐: Direct2D高性能渲染
#elif LINUX
        X11Platform.Register();
        MewVGX11Backend.Register();     // Linux: MewVG + X11
#elif MACOS
        MacOSPlatform.Register();
        MewVGMacOSBackend.Register();   // macOS: MewVG + Metal
#else
        // 自动检测或回退
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
#endif

        // 构建并运行主窗口
        var mainWindow = new MainWindow();
        Application.Run(mainWindow.Build());
    }
}
