using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace OracleClient.UI;

/// <summary>
/// 主窗口 - PL/SQL Developer风格布局
/// 
/// ┌──────────────────────────────────────────────────────┐
/// │ Menu Bar (File, Edit, View, Session, Tools, Help)     │
/// ├──────────────────────────────────────────────────────┤
/// │ Tool Bar (Connect, Execute, New SQL, etc.)             │
/// ├──────────┬───────────────────────────────────────────┤
/// │ Object   │ SQL Editor                                 │
/// │ Browser  │ ┌───────────────────────────────────────┐ │
/// │          │ │ SQL Input Area                         │ │
/// │ Tables   │ │                                       │ │
/// │ Views    │ ├───────────────────────────────────────┤ │
/// │ Procs    │ │ Results Grid / Output                  │ │
/// │ Funcs    │ │                                       │ │
/// │ Packages │ └───────────────────────────────────────┘ │
/// │ Triggers │                                           │
/// │ Sequences│                                           │
/// │ Indexes  │                                           │
/// ├──────────┴───────────────────────────────────────────┤
/// │ Status Bar (Connection | DB | Rows | Time)            │
/// └──────────────────────────────────────────────────────┘
/// </summary>
public sealed class MainWindow
{
    private readonly Services.OracleService _oracleService = new();
    private readonly ObservableValue<bool> _isConnected = new(false);
    private readonly ObservableValue<string> _statusConnection = new("Not Connected");
    private readonly ObservableValue<string> _statusDatabase = new("");
    private readonly ObservableValue<string> _statusRows = new("");

    private ObjectBrowser? _objectBrowser;
    private SqlEditorPanel? _sqlEditor;

    // 用于控制连接按钮的启用/禁用
    private readonly ObservableValue<bool> _canConnect = new(true);
    private readonly ObservableValue<bool> _canDisconnect = new(false);
    private readonly ObservableValue<bool> _canExecute = new(false);

    public Window Build()
    {
        return new Window()
            .Resizable(1280, 800)
            .OnBuild(w => w
                .Title("Oracle Developer")
                .Content(CreateMainLayout())
            );
    }

    private Element CreateMainLayout()
    {
        return new DockPanel()
            .LastChildFill()
            .Children(
                CreateMenuBar().DockTop(),
                CreateToolBar().DockTop(),
                CreateStatusBar().DockBottom(),
                CreateMainContent()
            );
    }

    #region Menu Bar

    private Element CreateMenuBar()
    {
        var p = ModifierKeys.Primary;

        var fileMenu = new Menu()
            .Item("_New SQL Window", () => { }, shortcut: new KeyGesture(Key.N, p))
            .Item("_Open File...", () => { }, shortcut: new KeyGesture(Key.O, p))
            .Item("_Save", () => { }, shortcut: new KeyGesture(Key.S, p))
            .Separator()
            .Item("_Connect...", OnConnect)
            .Item("_Disconnect", OnDisconnect)
            .Separator()
            .Item("E_xit", () => Application.Quit());

        var editMenu = new Menu()
            .Item("_Undo", () => { }, shortcut: new KeyGesture(Key.Z, p))
            .Item("_Redo", () => { }, shortcut: new KeyGesture(Key.Y, p))
            .Separator()
            .Item("Cu_t", () => { }, shortcut: new KeyGesture(Key.X, p))
            .Item("_Copy", () => { }, shortcut: new KeyGesture(Key.C, p))
            .Item("_Paste", () => { }, shortcut: new KeyGesture(Key.V, p))
            .Separator()
            .Item("Select _All", () => { }, shortcut: new KeyGesture(Key.A, p));

        var sessionMenu = new Menu()
            .Item("_Connect...", OnConnect)
            .Item("_Disconnect", OnDisconnect)
            .Separator()
            .Item("Session _Browser", () => { });

        var viewMenu = new Menu()
            .Item("_Refresh All", OnRefreshAll)
            .Separator()
            .Item("_Font Size +", () => { })
            .Item("Font _Size -", () => { });

        var toolsMenu = new Menu()
            .Item("_Export Data...", () => { })
            .Item("_Import Data...", () => { })
            .Separator()
            .Item("_Table Editor", () => { })
            .Item("_Session Monitor", () => { })
            .Separator()
            .Item("_Preferences...", () => { });

        var helpMenu = new Menu()
            .Item("_About Oracle Developer", () => { });

        var menuBar = new MenuBar()
            .Height(28)
            .Items(
                new MenuItem("_File").Menu(fileMenu),
                new MenuItem("_Edit").Menu(editMenu),
                new MenuItem("_View").Menu(viewMenu),
                new MenuItem("_Session").Menu(sessionMenu),
                new MenuItem("_Tools").Menu(toolsMenu),
                new MenuItem("_Help").Menu(helpMenu)
            );
        menuBar.DrawBottomSeparator = true;
        return menuBar;
    }

    #endregion

    #region Tool Bar

    private Element CreateToolBar()
    {
        return new Border()
            .DockTop()
            .Padding(4, 3)
            .BorderThickness(1)
            .WithTheme((t, b) => b.Background(t.Palette.ButtonFace).BorderBrush(t.Palette.ControlBorder))
            .Child(
                new StackPanel()
                    .Horizontal()
                    .Spacing(4)
                    .Children(
                        new Button()
                            .Content("Connect")
                            .OnClick(OnConnect)
                            .BindIsEnabled(_canConnect)
                            .ToolTip("Connect to Oracle database"),

                        new Button()
                            .Content("Disconnect")
                            .OnClick(OnDisconnect)
                            .BindIsEnabled(_canDisconnect)
                            .ToolTip("Disconnect from database"),

                        // 分隔线
                        new Border()
                            .Width(1)
                            .Height(24)
                            .WithTheme((t, b) => b.Background(t.Palette.ControlBorder)),

                        new Button()
                            .Content("Execute")
                            .OnClick(OnExecute)
                            .BindIsEnabled(_canExecute)
                            .ToolTip("Execute SQL (F8)"),

                        new Button()
                            .Content("New SQL")
                            .OnClick(OnNewSql)
                            .BindIsEnabled(_canExecute)
                            .ToolTip("Open new SQL window"),

                        // 分隔线
                        new Border()
                            .Width(1)
                            .Height(24)
                            .WithTheme((t, b) => b.Background(t.Palette.ControlBorder)),

                        new Button()
                            .Content("Explain")
                            .OnClick(() => { })
                            .BindIsEnabled(_canExecute)
                            .ToolTip("Explain Plan"),

                        new Button()
                            .Content("Refresh")
                            .OnClick(OnRefreshAll)
                            .BindIsEnabled(_canExecute)
                            .ToolTip("Refresh object browser"),

                        new Button()
                            .Content("Commit")
                            .OnClick(() => { })
                            .BindIsEnabled(_canExecute)
                            .ToolTip("Commit transaction"),

                        new Button()
                            .Content("Rollback")
                            .OnClick(() => { })
                            .BindIsEnabled(_canExecute)
                            .ToolTip("Rollback transaction"),

                        // 分隔线
                        new Border()
                            .Width(1)
                            .Height(24)
                            .WithTheme((t, b) => b.Background(t.Palette.ControlBorder)),

                        new Button()
                            .Content("Export")
                            .OnClick(() => { })
                            .BindIsEnabled(_canExecute)
                            .ToolTip("Export data"),

                        new Button()
                            .Content("Settings")
                            .OnClick(() => { })
                    )
            );
    }

    #endregion

    #region Status Bar

    private Element CreateStatusBar()
    {
        return new Border()
            .DockBottom()
            .Padding(8, 4)
            .BorderThickness(1)
            .WithTheme((t, b) => b.Background(t.Palette.ButtonFace).BorderBrush(t.Palette.ControlBorder))
            .Child(
                new DockPanel()
                    .Children(
                        new StackPanel()
                            .Horizontal()
                            .Spacing(8)
                            .DockLeft()
                            .Children(
                                new TextBlock()
                                    .BindText(_statusConnection)
                                    .FontSize(11)
                            ),

                        new TextBlock()
                            .BindText(_statusDatabase)
                            .FontSize(11)
                            .CenterHorizontal(),

                        new TextBlock()
                            .BindText(_statusRows)
                            .FontSize(11)
                            .DockRight()
                    )
            );
    }

    #endregion

    #region Main Content

    private Element CreateMainContent()
    {
        _objectBrowser = new ObjectBrowser(_oracleService);
        _sqlEditor = new SqlEditorPanel(_oracleService);

        // PL/SQL Developer 风格：左侧对象浏览器 + 右侧编辑器/结果
        return new SplitPanel()
            .Horizontal()
            .SplitterThickness(6)
            .MinFirst(160)
            .MinSecond(400)
            .FirstLength(GridLength.Pixels(240))
            .SecondLength(GridLength.Star)
            .First(_objectBrowser.Build())
            .Second(_sqlEditor.Build());
    }

    #endregion

    #region Event Handlers

    private void OnConnect()
    {
        var dialog = new ConnectionDialog();
        var window = dialog.Build();
        window.Show();

        _oracleService.ConnectionStateChanged += (connected) =>
        {
            _isConnected.Value = connected;
            _canConnect.Value = !connected;
            _canDisconnect.Value = connected;
            _canExecute.Value = connected;

            if (connected)
            {
                _statusConnection.Value = "Connected";
                _ = LoadDatabaseInfo();
            }
            else
            {
                _statusConnection.Value = "Not Connected";
                _statusDatabase.Value = "";
            }
        };
    }

    private void OnDisconnect()
    {
        _oracleService.Disconnect();
    }

    private void OnExecute()
    {
        // SQL编辑器自己处理执行
    }

    private void OnNewSql()
    {
        _sqlEditor?.SetSqlText("");
    }

    private void OnRefreshAll()
    {
        // 对象浏览器刷新
    }

    private async Task LoadDatabaseInfo()
    {
        try
        {
            var overview = await _oracleService.GetDatabaseOverviewAsync();
            _statusDatabase.Value = $"{overview.InstanceName} @ {overview.HostName} ({overview.Version})";
        }
        catch
        {
            _statusDatabase.Value = "DB info unavailable";
        }
    }

    #endregion
}
