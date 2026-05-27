/* MewUI.DBClient — 完全基于 demo.html 从零构建的数据库管理客户端
 *
 * 复刻 demo.html 的全部 UI 结构和交互逻辑：
 * - 菜单栏: 文件(F) / 编辑(E) / 视图(V) / 帮助(H)，含下拉菜单、快捷键、分隔线
 * - 工具栏: 新建连接 / 新建查询 / 执行 / 执行选中 / 停止 / 刷新 / 主题切换
 * - 侧边栏: 连接列表（MySQL/PostgreSQL/SQLite + 状态指示灯）+ 对象浏览器树
 * - 分割面板: 可拖拽分割 sidebar 和编辑区
 * - 选项卡: 查询标签页，支持新建/关闭
 * - SQL 编辑器: 等宽字体，支持 Tab 缩进
 * - 执行栏: 执行(主色) / 执行选中 / 停止 + 耗时显示
 * - 结果网格: 6列数据表（id/name/email/age/city/created_at）
 * - 结果工具栏: 刷新 / 导出 / 编辑模式开关
 * - 分页栏: 总行数 / 翻页控件 / 每页行数选择
 * - 状态栏: 强调色背景，连接信息 / 状态文本 / 查询计数
 * - 连接对话框: 数据库类型选择器 + 表单字段 + 测试/保存/取消
 * - 关于对话框
 * - 右键菜单: 树节点右键
 * - Toast 通知
 * - 暗色/亮色主题切换
 */

using System.Diagnostics;
using System.Linq;

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Aprillz.MewUI.HotReload.MewUiMetadataUpdateHandler))]
#endif

var stopwatch = Stopwatch.StartNew();
Startup();

Window window = null!;

// ── 应用状态 ─────────────────────────────────────────
var isDarkMode          = new ObservableValue<bool>(true);
var activeConnectionId  = new ObservableValue<int>(1);
var connectionStatus    = new ObservableValue<string>("192.168.1.100:3306");
var statusText          = new ObservableValue<string>("就绪");
var queryCount       = new ObservableValue<int>(0);
var isEditing        = new ObservableValue<bool>(false);
var currentPage      = new ObservableValue<int>(1);
var totalRows        = new ObservableValue<int>(156);
var currentTabId     = 0;
var queryTabs        = new List<QueryTab>();
TabControl queryTabControl = new();

// ── 主题切换 ─────────────────────────────────────────
isDarkMode.Changed += () =>
{
    Application.Current.SetTheme(isDarkMode.Value ? ThemeVariant.Dark : ThemeVariant.Light);
};

// ── 应用入口 ─────────────────────────────────────────
Application
    .Create()
    .UseAccent(Accent.Blue)
    .BuildMainWindow(() =>
        new Window()
            .Resizable(1280, 800, minWidth: 900, minHeight: 600)
            .StartCenterScreen()
            .OnBuild(w => w
                .Ref(out window)
                .Title("Oracle Developer")
                .Content(
                    new DockPanel()
                        .Children(
                            CreateMenuBar().DockTop(),
                            CreateToolbar().DockTop(),
                            CreateStatusBar().DockBottom(),
                            new SplitPanel()
                            {
                                SplitterThickness = 5,
                                FirstLength = GridLength.Pixels(260),
                                MinFirst = 180,
                                First = CreateSidebar(),
                                Second = CreateEditorArea()
                            }
                        )
                )
            )
            .OnLoaded(() =>
            {
                Application.Current.SetTheme(isDarkMode.Value ? ThemeVariant.Dark : ThemeVariant.Light);
                window.ShowToast("欢迎使用 MewDB — 数据库管理客户端");
                stopwatch.Stop();
                statusText.Value = $"就绪  |  加载耗时 {stopwatch.Elapsed.TotalSeconds:0.00}s";
            })
    )
    .Run();

// ═══════════════════════════════════════════════════════
//  菜单栏 — 文件(F) / 编辑(E) / 视图(V) / 帮助(H)
//  对应 demo.html .menubar + .menubar-dropdown
// ═══════════════════════════════════════════════════════
FrameworkElement CreateMenuBar() => new MenuBar()
    .Items(
        new MenuItem("_文件(F)")
        {
            SubMenu = new Menu
            {
                Items =
                {
                    new MenuItem("新建连接...") { Shortcut = new KeyGesture(Key.N, ModifierKeys.Primary), Click = ShowConnectionDialog },
                    new MenuItem("新建查询") { Shortcut = new KeyGesture(Key.N, ModifierKeys.Primary | ModifierKeys.Shift), Click = () => AddQueryTab() },
                    MenuSeparator.Instance,
                    new MenuItem("退出") { Click = () => ShowToast("退出功能仅作演示") }
                }
            }
        },
        new MenuItem("_编辑(E)")
        {
            SubMenu = new Menu
            {
                Items =
                {
                    new MenuItem("复制") { Shortcut = new KeyGesture(Key.C, ModifierKeys.Primary) },
                    new MenuItem("粘贴") { Shortcut = new KeyGesture(Key.V, ModifierKeys.Primary) }
                }
            }
        },
        new MenuItem("_视图(V)")
        {
            SubMenu = new Menu
            {
                Items =
                {
                    new MenuItem("对象浏览器") { Click = () => ShowToast("对象浏览器切换") },
                    new MenuItem("查询历史") { Click = () => ShowToast("查询历史面板切换") },
                    MenuSeparator.Instance,
                    new MenuItem("切换暗色模式")
                    {
                        Shortcut = new KeyGesture(Key.D, ModifierKeys.Primary | ModifierKeys.Shift),
                        Click = () => isDarkMode.Value = !isDarkMode.Value
                    }
                }
            }
        },
        new MenuItem("_帮助(H)")
        {
            SubMenu = new Menu
            {
                Items =
                {
                    new MenuItem("关于") { Click = ShowAboutDialog }
                }
            }
        }
    );

// ═══════════════════════════════════════════════════════
//  工具栏 — 对应 demo.html .toolbar
// ═══════════════════════════════════════════════════════
FrameworkElement CreateToolbar()
{
    var themeToggle = new ToggleSwitch()
        .IsChecked(isDarkMode.Value)
        .DockRight()
        .CenterVertical()
        .ToolTip("切换暗色/亮色模式");
    themeToggle.CheckedChanged += v => isDarkMode.Value = v;
    isDarkMode.Changed += () => themeToggle.IsChecked = isDarkMode.Value;

    return new Border()
        .Padding(6, 4)
        .BorderThickness(1)
        .WithTheme((t, b) => b.Background(t.Palette.ContainerBackground).BorderBrush(t.Palette.ControlBorder))
        .Child(
            new DockPanel()
                .Children(
                    new StackPanel()
                        .Horizontal()
                        .Spacing(2)
                        .Children(
                            MkToolBtn("新建连接", ShowConnectionDialog),
                            MkToolBtn("新建查询", () => AddQueryTab()),
                            MkSep(),
                            MkToolBtn("执行", ExecuteQuery, accent: true),
                            MkToolBtn("执行选中", () => ShowToast("请先选中要执行的SQL语句")),
                            MkToolBtn("停止", () => ShowToast("查询已停止")),
                            MkSep(),
                            MkToolBtn("刷新", () => ShowToast("对象树已刷新"))
                        ),
                    themeToggle
                )
        );
}

Button MkToolBtn(string text, Action onClick, bool accent = false)
{
    var btn = new Button().Content(text).Padding(8, 3).FontSize(12).OnClick(onClick);
    if (accent) btn.StyleName = BuiltInStyles.AccentButton;
    return btn;
}

Border MkSep() => new Border()
    .Width(1).Height(18).Margin(4, 0)
    .WithTheme((t, b) => b.Background(t.Palette.ControlBorder));

// ═══════════════════════════════════════════════════════
//  侧边栏 — 对应 demo.html .sidebar
//  连接列表 + 对象浏览器
// ═══════════════════════════════════════════════════════
FrameworkElement CreateSidebar()
{
    // 对象浏览器树 — 对应 demo.html .object-tree + renderObjectTree()
    var tree = new TreeView()
        .ItemsSource(BuildTreeData(activeConnectionId.Value))
        .ExpandTrigger(TreeViewExpandTrigger.DoubleClickNode)
        .OnSelectedNodeChanged(node =>
        {
            if (node is not null) statusText.Value = $"选中: {node.Text}";
        });

    // 右键菜单 — 对应 demo.html showTreeContextMenu()
    tree.ContextMenu = new ContextMenu()
        .Item("查看数据", () =>
        {
            if (tree.SelectedNode is { } n) OpenTableQuery(n.Text);
        })
        .Item("编辑结构", () =>
        {
            if (tree.SelectedNode is { } n) ShowToast($"编辑 {n.Text} 结构");
        })
        .Separator()
        .Item("导出", () => ShowToast("已导出数据到 CSV"))
        .Separator()
        .Item("删除", () =>
        {
            if (tree.SelectedNode is { } n) ShowToast($"删除表 {n.Text}（模拟）");
        });

    return new Border()
        .BorderThickness(1)
        .WithTheme((t, b) => b.Background(t.Palette.ContainerBackground).BorderBrush(t.Palette.ControlBorder))
        .Child(
            new DockPanel()
                .Children(
                    // ── 连接 Header ── 对应 demo.html .sidebar-header "连接"
                    new Border()
                        .DockTop()
                        .Padding(10, 8)
                        .BorderThickness(1)
                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                        .Child(
                            new DockPanel()
                                .Children(
                                    new TextBlock().Text("连接").Bold().FontSize(11.5).CenterVertical()
                                        .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText)),
                                    new Button()
                                        .DockRight().Content("+").Padding(4, 2).FontSize(12)
                                        .BorderThickness(0).StyleName(BuiltInStyles.FlatButton)
                                        .OnClick(ShowConnectionDialog).ToolTip("新建连接")
                                )
                        ),

                    // ── 连接列表 ── 对应 demo.html .connection-list + renderConnectionList()
                    new Border()
                        .DockTop()
                        .BorderThickness(1)
                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                        .Child(
                            MkConnectionList(tree)
                        ),

                    // ── 对象浏览器 Header ── 对应 demo.html .sidebar-header "对象浏览器"
                    new Border()
                        .DockTop()
                        .Padding(10, 8)
                        .BorderThickness(1)
                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                        .Child(
                            new TextBlock().Text("对象浏览器").Bold().FontSize(11.5)
                                .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText))
                        ),

                    // ── 树 ──
                    new ScrollViewer()
                        .VerticalScroll(ScrollMode.Auto)
                        .Content(tree)
                )
        );
}

// 连接列表 — 对应 demo.html .connection-list + selectConnection()
// 点击选择连接，选中项高亮，更新状态栏连接信息，切换对象树
FrameworkElement MkConnectionList(TreeView tree)
{
    var dbConns = new (int Id, string Name, string Letter, Color Color, string Host, bool Connected)[]
    {
        (1, "生产数据库", "M", new Color(0, 117, 143), "192.168.1.100:3306", true),
        (2, "测试数据库", "P", new Color(51, 103, 145), "localhost:5432", true),
        (3, "本地SQLite", "S", new Color(4, 74, 100), "local", false)
    };

    var cards = new List<Border>();

    StackPanel panel = new StackPanel().Vertical();

    for (int i = 0; i < dbConns.Length; i++)
    {
        var (id, name, letter, color, host, connected) = dbConns[i];
        var card = new Border()
            .Padding(10, 6)
            .Child(
                new DockPanel()
                    .Children(
                        new Border()
                            .DockRight().Width(7).Height(7).CornerRadius(4).CenterVertical()
                            .WithTheme((t, b) => b.Background(connected ? new Color(34, 197, 94) : t.Palette.DisabledText)),
                        new Border()
                            .Width(20).Height(20).CornerRadius(3).CenterVertical().Margin(0, 0, 8, 0)
                            .WithTheme((t, b) => b.Background(color.WithAlpha(34)))
                            .Child(
                                new TextBlock().Text(letter).FontSize(11).Bold()
                                    .CenterHorizontal().CenterVertical()
                                    .WithTheme((t, tb) => tb.Foreground(color))
                            ),
                        new StackPanel()
                            .Vertical().Spacing(1).CenterVertical()
                            .Children(
                                new TextBlock().Text(name).FontSize(12.5).Bold(),
                                new TextBlock().Text(host).FontSize(11)
                                    .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText))
                            )
                    )
            );

        var isSelected = id == activeConnectionId.Value;
        if (isSelected)
            card.WithTheme((t, b) => b.Background(t.Palette.Accent.WithAlpha(20)));
        else
            card.WithTheme((t, b) => b.Background(t.Palette.ContainerBackground));

        card.MouseDown += e =>
        {
            if (e.Button == MouseButton.Left)
            {
                activeConnectionId.Value = id;
                for (int j = 0; j < cards.Count; j++)
                    ApplyConnStyle(cards[j], dbConns[j].Id == id);

                // 对应 demo.html selectConnection(): 更新状态栏连接信息
                var conn = dbConns.FirstOrDefault(c => c.Id == id);
                connectionStatus.Value = conn.Host;

                // 对应 demo.html renderObjectTree(): 切换对象树
                tree.ItemsSource = TreeItemsView.Create(
                    BuildTreeData(id), n => n.Children, textSelector: n => n.Text, keySelector: n => n);

                // 未连接的自动连接 — 对应 demo.html 中 conn.connected = true
                if (!connected)
                    ShowToast($"已连接到 {name}");
            }
        };

        cards.Add(card);
        panel.Children(card);
    }

    return panel;

    void ApplyConnStyle(Border card, bool selected)
    {
        var t = Application.Current.Theme;
        card.Background(selected ? t.Palette.Accent.WithAlpha(20) : t.Palette.ContainerBackground);
    }
}

// ═══════════════════════════════════════════════════════
//  对象树数据 — 对应 demo.html connections 数据结构
// ═══════════════════════════════════════════════════════
IReadOnlyList<TreeViewNode> BuildTreeData(int connectionId)
{
    // ecommerce 数据库 — connections[0].databases[0]
    var ecommerce = new TreeViewNode("ecommerce", new TreeViewNode[]
    {
        new("表", new TreeViewNode[]
        {
            new("users"), new("orders"), new("products"),
            new("categories"), new("reviews"), new("payments"), new("shipping")
        }),
        new("视图", new TreeViewNode[] { new("v_order_summary"), new("v_user_stats") }),
        new("存储过程", new TreeViewNode[] { new("sp_calculate_total"), new("sp_generate_report") }),
        new("函数", new TreeViewNode[] { new("fn_format_date"), new("fn_calc_discount") })
    });

    // analytics 数据库 — connections[0].databases[1]
    var analytics = new TreeViewNode("analytics", new TreeViewNode[]
    {
        new("表", new TreeViewNode[] { new("events"), new("sessions"), new("pageviews"), new("conversions") }),
        new("视图", new TreeViewNode[] { new("v_daily_stats"), new("v_funnel_analysis") })
    });

    // testdb 数据库 — connections[1].databases[0]
    var testdb = new TreeViewNode("testdb", new TreeViewNode[]
    {
        new("表", new TreeViewNode[] { new("test_users"), new("test_orders") }),
        new("视图", new TreeViewNode[] { new("v_test_summary") })
    });

    // 对应 demo.html renderObjectTree(): 按 activeConnection 过滤，直接显示数据库节点
    return connectionId switch
    {
        1 => new TreeViewNode[] { ecommerce, analytics },
        2 => new TreeViewNode[] { testdb },
        _ => Array.Empty<TreeViewNode>()
    };
}

// ═══════════════════════════════════════════════════════
//  编辑区 — 对应 demo.html .query-area
//  TabControl + SQL编辑器 + 执行栏 + 结果网格 + 分页
// ═══════════════════════════════════════════════════════
FrameworkElement CreateEditorArea()
{
    AddQueryTab("SELECT * FROM users WHERE age > 25 ORDER BY name;");

    // 标签页右键菜单 — 对应 demo.html showTabContextMenu()
    queryTabControl.ContextMenu = new ContextMenu()
        .Item("关闭", () =>
        {
            var idx = queryTabControl.SelectedIndex;
            if (idx >= 0 && idx < queryTabs.Count) CloseQueryTab(queryTabs[idx].Id);
        })
        .Item("关闭其他", () =>
        {
            var currentIdx = queryTabControl.SelectedIndex;
            if (currentIdx < 0) return;
            var currentTab = queryTabs[currentIdx];
            var toClose = queryTabs.Where(t => t.Id != currentTab.Id).ToList();
            foreach (var tab in toClose)
            {
                var idx = queryTabs.IndexOf(tab);
                queryTabControl.RemoveTabAt(idx);
            }
            queryTabs.Clear();
            queryTabs.Add(currentTab);
            ShowToast("已关闭其他标签页");
        })
        .Item("关闭所有", () =>
        {
            queryTabControl.ClearTabs();
            queryTabs.Clear();
            AddQueryTab();
            ShowToast("已关闭所有标签页");
        });

    // 鼠标中键关闭 Tab
    queryTabControl.MouseDown += e =>
    {
        if (e.Button == MouseButton.Middle)
        {
            var idx = queryTabControl.SelectedIndex;
            if (idx >= 0 && idx < queryTabs.Count) CloseQueryTab(queryTabs[idx].Id);
        }
    };

    return queryTabControl;
}

void AddQueryTab(string? initialSql = null)
{
    currentTabId++;
    var tabId = currentTabId;
    var tabTitle = $"查询 {queryTabs.Count + 1}";

    // ── SQL 编辑器 — 对应 demo.html .sql-editor ──
    var editor = new MultiLineTextBox()
        .FontFamily("Consolas, Courier New, monospace")
        .FontSize(13)
        .AcceptTab(true)
        .Text(initialSql ?? string.Empty);

    var results    = new ObservableValue<List<UserRow>>(GenerateUsers());
    var elapsed    = new ObservableValue<string>("耗时: 0ms");
    var rowCount   = new ObservableValue<string>($"{results.Value.Count} 行");

    // ── 结果网格 — 对应 demo.html .grid-table + renderGrid() ──
    var grid = new GridView()
        .ItemsSource(results.Value)
        .Columns(
            new GridViewColumn<UserRow>().Header("id").Width(60).Text(r => r.Id.ToString()),
            new GridViewColumn<UserRow>().Header("name").Width(100).Text(r => r.Name),
            new GridViewColumn<UserRow>().Header("email").Width(180).Text(r => r.Email),
            new GridViewColumn<UserRow>().Header("age").Width(60).Text(r => r.Age.ToString()),
            new GridViewColumn<UserRow>().Header("city").Width(80).Text(r => r.City),
            new GridViewColumn<UserRow>().Header("created_at").Width(110).Text(r => r.CreatedAt)
        );

    // ── 结果区布局 — 对应 demo.html .result-area ──
    var resultArea = new DockPanel()
        .Children(
            // 结果工具栏 — 对应 demo.html .result-toolbar
            new Border()
                .DockTop().Padding(8, 4).BorderThickness(1)
                .WithTheme((t, b) => b.Background(t.Palette.ContainerBackground).BorderBrush(t.Palette.ControlBorder))
                .Child(
                    new DockPanel()
                        .Children(
                            new StackPanel()
                                .Horizontal().Spacing(4)
                                .Children(
                                    new Button().Content("刷新").Padding(8, 2).FontSize(12)
                                        .BorderThickness(0).StyleName(BuiltInStyles.FlatButton)
                                        .OnClick(ExecuteQuery),
                                    // 导出下拉 — 对应 demo.html .export-dropdown，用 ComboBox 选择格式
                                    new ComboBox()
                                        .Items("导出 CSV", "导出 JSON", "导出 SQL INSERT", "导出 Excel")
                                        .Placeholder("导出")
                                        .FontSize(12)
                                        .OnSelectionChanged(item => ShowToast($"已导出数据到 {item}")),
                                    new TextBlock().BindText(rowCount).FontSize(11).CenterVertical().Margin(8, 0, 0, 0),
                                    new TextBlock().BindText(elapsed).FontSize(11).CenterVertical()
                                        .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText))
                                ),
                            // 编辑模式区域 — 对应 demo.html .edit-toggle + .edit-actions
                            new StackPanel()
                                .DockRight().Horizontal().Spacing(6).CenterVertical()
                                .Children(
                                    new TextBlock().Text("编辑模式").FontSize(12)
                                        .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText)),
                                    new ToggleSwitch().BindIsChecked(isEditing),
                                    // 提交/回滚按钮 — 对应 demo.html .edit-actions
                                    new Button().Content("提交").Padding(8, 2).FontSize(11)
                                        .StyleName(BuiltInStyles.AccentButton)
                                        .OnClick(() => ShowToast("更改已提交")),
                                    new Button().Content("回滚").Padding(8, 2).FontSize(11)
                                        .BorderThickness(1)
                                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                                        .OnClick(() => ShowToast("更改已回滚"))
                                )
                        )
                ),

            // 分页栏 — 对应 demo.html .pagination-bar + renderPagination()
            new Border()
                .DockBottom().Height(28).Padding(10, 0).BorderThickness(1)
                .WithTheme((t, b) => b.Background(t.Palette.ContainerBackground).BorderBrush(t.Palette.ControlBorder))
                .Child(
                    new DockPanel()
                        .Children(
                            new TextBlock()
                                .BindText(totalRows, v => $"总计 {v} 行")
                                .FontSize(12).CenterVertical()
                                .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText)),
                            new StackPanel()
                                .DockRight().Horizontal().Spacing(6).CenterVertical()
                                .Children(
                                    new TextBlock().Text("每页行数:").FontSize(12).CenterVertical()
                                        .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText)),
                                    new ComboBox().Items("50", "100", "200", "500", "1000").SelectedIndex(1).FontSize(12)
                                ),
                            new StackPanel()
                                .Horizontal().Spacing(6).CenterHorizontal().CenterVertical()
                                .Children(
                                    new Button().Content("<").FontSize(11).Padding(6, 2)
                                        .BorderThickness(0).StyleName(BuiltInStyles.FlatButton)
                                        .OnClick(() => { if (currentPage.Value > 1) currentPage.Value--; }),
                                    new TextBlock().BindText(currentPage, p => $"{p}").FontSize(12).CenterVertical(),
                                    new TextBlock().Text("/ 2").FontSize(12).CenterVertical()
                                        .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText)),
                                    new Button().Content(">").FontSize(11).Padding(6, 2)
                                        .BorderThickness(0).StyleName(BuiltInStyles.FlatButton)
                                        .OnClick(() => currentPage.Value++)
                                )
                        )
                ),

            // 网格滚动区 — 对应 demo.html .grid-container
            new ScrollViewer()
                .VerticalScroll(ScrollMode.Auto)
                .HorizontalScroll(ScrollMode.Auto)
                .Content(grid)
        );

    // ── 执行栏 — 对应 demo.html .exec-bar ──
    var execBar = new Border()
        .Height(32).Padding(10, 0).BorderThickness(1)
        .WithTheme((t, b) => b.Background(t.Palette.ContainerBackground).BorderBrush(t.Palette.ControlBorder))
        .Child(
            new DockPanel()
                .Children(
                    new StackPanel()
                        .Horizontal().Spacing(4).CenterVertical()
                        .Children(
                            new Button().Content("执行").Padding(10, 3).FontSize(12)
                                .StyleName(BuiltInStyles.AccentButton).OnClick(ExecuteQuery),
                            new Button().Content("执行选中").Padding(10, 3).FontSize(12)
                                .OnClick(() => ShowToast("请先选中要执行的SQL语句")),
                            new Button().Content("停止").Padding(10, 3).FontSize(12)
                                .StyleName(BuiltInStyles.FlatButton)
                                .OnClick(() => ShowToast("查询已停止"))
                        ),
                    new TextBlock()
                        .DockRight().BindText(elapsed).FontSize(11.5).CenterVertical()
                        .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText))
                )
        );

    // ── 编辑器+结果 垂直分割 — 对应 demo.html .editor-container + .result-area ──
    var split = new SplitPanel
    {
        Orientation = Orientation.Vertical,
        SplitterThickness = 4,
        First = new DockPanel().Children(execBar.DockBottom(), editor),
        Second = resultArea
    };

    // ── 标签页头 — 对应 demo.html .tab-item ──
    var header = new StackPanel()
        .Horizontal().Spacing(6)
        .Children(
            new TextBlock().Text(tabTitle).FontSize(12).CenterVertical(),
            new Button().Content("x").Padding(4, 2).FontSize(10).CornerRadius(3)
                .BorderThickness(0).StyleName(BuiltInStyles.FlatButton)
                .OnClick(() => CloseQueryTab(tabId))
        );

    queryTabControl.AddTab(new TabItem { Header = header, Content = split });
    queryTabControl.SelectedIndex = queryTabs.Count;

    // 模拟初始查询执行 — 对应 demo.html executeQuery() 中的 setTimeout 模拟
    var rng = new Random();
    elapsed.Value = $"耗时: {rng.Next(50, 300)}ms";
    rowCount.Value = $"{results.Value.Count} 行";

    queryTabs.Add(new QueryTab
    {
        Id = tabId, Title = tabTitle, Editor = editor,
        Results = results, ElapsedText = elapsed, RowCountText = rowCount
    });
}

void CloseQueryTab(int tabId)
{
    if (queryTabs.Count <= 1)
    {
        ShowToast("至少保留一个查询标签");
        return;
    }
    var idx = queryTabs.FindIndex(t => t.Id == tabId);
    if (idx >= 0)
    {
        queryTabControl.RemoveTabAt(idx);
        queryTabs.RemoveAt(idx);
    }
}

// ═══════════════════════════════════════════════════════
//  状态栏 — 对应 demo.html .statusbar
//  强调色背景 + 连接信息 / 状态文本 / 查询计数
// ═══════════════════════════════════════════════════════
FrameworkElement CreateStatusBar() => new Border()
    .Padding(10, 4)
    .WithTheme((t, b) => b.Background(t.Palette.Accent))
    .Child(
        new DockPanel()
            .Spacing(16)
            .Children(
                new StackPanel()
                    .DockLeft().Horizontal().Spacing(6).CenterVertical()
                    .Children(
                        new Border().Width(6).Height(6).CornerRadius(3)
                            .WithTheme((t, b) => b.Background(new Color(255, 255, 255, 200))),
                        new TextBlock().BindText(connectionStatus).FontSize(11.5)
                            .WithTheme((t, tb) => tb.Foreground(t.Palette.WindowText))
                    ),
                new TextBlock()
                    .BindText(statusText).FontSize(11.5).CenterHorizontal()
                    .WithTheme((t, tb) => tb.Foreground(t.Palette.WindowText)),
                new TextBlock()
                    .DockRight().BindText(queryCount, c => $"查询数: {c}").FontSize(11.5)
                    .WithTheme((t, tb) => tb.Foreground(t.Palette.WindowText))
            )
    );

// ═══════════════════════════════════════════════════════
//  查询执行 — 对应 demo.html executeQuery()
// ═══════════════════════════════════════════════════════
void ExecuteQuery()
{
    var idx = queryTabControl.SelectedIndex;
    if (idx < 0 || idx >= queryTabs.Count) return;

    var tab = queryTabs[idx];
    if (string.IsNullOrWhiteSpace(tab.Editor.Text))
    {
        ShowToast("查询内容为空");
        return;
    }

    statusText.Value = "正在执行查询...";
    queryCount.Value++;

    // 模拟执行耗时 — 对应 demo.html 中的 setTimeout(800~2000ms)
    var rng = new Random();
    var ms = rng.Next(100, 800);
    tab.ElapsedText.Value = $"耗时: {ms}ms";
    tab.RowCountText.Value = $"{tab.Results.Value.Count} 行";
    statusText.Value = "就绪";

    ShowToast($"查询完成，返回 {tab.Results.Value.Count} 行");
}

// 双击树节点打开表查询 — 对应 demo.html onObjectDoubleClick()
void OpenTableQuery(string tableName)
{
    var idx = queryTabControl.SelectedIndex;
    if (idx >= 0 && idx < queryTabs.Count)
    {
        queryTabs[idx].Editor.Text = $"SELECT * FROM {tableName} LIMIT 100;";
    }
    ShowToast($"查看 {tableName} 数据");
}

// ═══════════════════════════════════════════════════════
//  连接对话框 — 对应 demo.html openConnectionDialog()
// ═══════════════════════════════════════════════════════
void ShowConnectionDialog()
{
    var connName = new ObservableValue<string>("新连接");
    var connHost = new ObservableValue<string>("localhost");
    var connPort = new ObservableValue<string>("3306");
    var connUser = new ObservableValue<string>("root");
    var connPass = new ObservableValue<string>("");

    Window dlg = null!;
    dlg = new Window()
        .Title("新建连接")
        .Resizable(560, 540)
        .StartCenterScreen()
        .Content(
            new DockPanel()
                .Children(
                    // ── modal-footer — 对应 demo.html .modal-footer ──
                    new Border()
                        .DockBottom()
                        .Padding(12, 18)
                        .BorderThickness(1)
                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                        .Child(
                            new StackPanel()
                                .Horizontal().Spacing(8).Right()
                                .Children(
                                    // 测试连接 — 对应 demo.html btn-success (绿色)
                                    new Button()
                                        .Content("测试连接")
                                        .Padding(16, 6)
                                        .WithTheme((t, b) => b.Foreground(new Color(34, 197, 94)))
                                        .OnClick(() => ShowToast("连接测试成功")),
                                    // 保存 — 对应 demo.html btn-primary (accent蓝)
                                    new Button()
                                        .Content("保存")
                                        .Padding(16, 6)
                                        .StyleName(BuiltInStyles.AccentButton)
                                        .OnClick(() =>
                                        {
                                            ShowToast($"连接 \"{connName.Value}\" 已保存");
                                            dlg.Close();
                                        }),
                                    // 取消 — 对应 demo.html btn-secondary (描边)
                                    new Button()
                                        .Content("取消")
                                        .Padding(16, 6)
                                        .BorderThickness(1)
                                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                                        .OnClick(() => dlg.Close())
                                )
                        ),

                    // ── modal-body — 对应 demo.html .modal-body ──
                    new ScrollViewer()
                        .VerticalScroll(ScrollMode.Auto)
                        .Content(
                            new Border()
                                .Padding(18)
                                .Child(
                                    new StackPanel()
                                        .Vertical().Spacing(14)
                                        .Children(
                                            // 数据库类型选择器 — 对应 demo.html .db-type-selector
                                            // 用 Grid 5列均分，模拟 CSS flex:1
                                            // selectedDbType 跟踪选中索引，点击切换
                                            MkDbCardSelector(),

                                            // 表单字段 — 对应 demo.html .form-grid
                                            new Grid()
                                                .Columns("90,*")
                                                .Rows("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto")
                                                .Spacing(10)
                                                .AutoIndexing()
                                                .Children(
                                                    MkLabel("连接名称"),
                                                    new TextBox().BindText(connName).Placeholder("输入连接名称"),
                                                    MkLabel("主机地址"),
                                                    new TextBox().BindText(connHost).Placeholder("输入主机地址"),
                                                    MkLabel("端口号"),
                                                    new TextBox().BindText(connPort).Placeholder("端口号"),
                                                    MkLabel("用户名"),
                                                    new TextBox().BindText(connUser).Placeholder("用户名"),
                                                    MkLabel("密码"),
                                                    new PasswordBox().BindPassword(connPass).Placeholder("密码"),
                                                    MkLabel("数据库名"),
                                                    new ComboBox().Items("默认", "ecommerce", "analytics", "testdb").SelectedIndex(0),
                                                    MkLabel("字符编码"),
                                                    new ComboBox().Items("UTF-8", "GBK", "Latin1").SelectedIndex(0),
                                                    MkLabel("自动重连"),
                                                    new CheckBox().Content("启用").IsChecked(true),
                                                    MkLabel("SSL模式"),
                                                    new ComboBox().Items("Disable", "Prefer", "Require").SelectedIndex(0)
                                                )
                                        )
                                )
                        )
                )
        );

    _ = dlg.ShowDialogAsync(window);
}

// 表单标签 — 对应 demo.html .form-label
FrameworkElement MkLabel(string text) => new TextBlock()
    .Text(text).FontSize(12.5).Right().CenterVertical()
    .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText));

// 数据库类型选择器 — 对应 demo.html .db-type-selector + selectDbType()
Grid MkDbCardSelector()
{
    var selectedIdx = new ObservableValue<int>(0);
    var dbTypes = new (string Letter, string Name, Color Color)[]
    {
        ("M", "MySQL", new Color(0, 117, 143)),
        ("P", "PostgreSQL", new Color(51, 103, 145)),
        ("S", "SQLite", new Color(4, 74, 100)),
        ("MS", "SQL Server", new Color(204, 41, 39)),
        ("R", "Redis", new Color(220, 56, 45))
    };

    var grid = new Grid()
        .Columns(GridLength.Star, GridLength.Star, GridLength.Star, GridLength.Star, GridLength.Star)
        .Spacing(8)
        .AutoIndexing();

    var cards = new List<Border>();

    for (int i = 0; i < dbTypes.Length; i++)
    {
        var idx = i;
        var (letter, name, color) = dbTypes[i];

        var card = new Border()
            .Padding(10, 8).CornerRadius(6).BorderThickness(2)
            .Child(
                new StackPanel()
                    .Vertical().Spacing(4).CenterHorizontal()
                    .Children(
                        new TextBlock().Text(letter).Bold().FontSize(18).CenterHorizontal()
                            .WithTheme((t, tb) => tb.Foreground(color)),
                        new TextBlock().Text(name).FontSize(11).CenterHorizontal()
                            .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText))
                    )
            );

        // 初始样式
        ApplyCardStyle(card, idx == 0, color);

        card.MouseDown += e =>
        {
            if (e.Button == MouseButton.Left)
            {
                selectedIdx.Value = idx;
                for (int j = 0; j < cards.Count; j++)
                    ApplyCardStyle(cards[j], j == idx, dbTypes[j].Color);
            }
        };

        cards.Add(card);
        grid.Children(card);
    }

    return grid;

    void ApplyCardStyle(Border card, bool selected, Color color)
    {
        var t = Application.Current.Theme;
        card.Background(selected ? color.WithAlpha(20) : t.Palette.ContainerBackground);
        card.BorderBrush(selected ? color : t.Palette.ControlBorder);
    }
}

// ═══════════════════════════════════════════════════════
//  关于对话框 — 对应 demo.html showAboutDialog()
// ═══════════════════════════════════════════════════════
void ShowAboutDialog()
{
    Window dlg = null!;
    dlg = new Window()
        .Title("关于 MewDB")
        .Resizable(400, 340)
        .StartCenterScreen()
        .Content(
            new DockPanel()
                .Children(
                    new Border()
                        .DockBottom().Padding(12, 18).BorderThickness(1)
                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                        .Child(
                            new Button().Content("确定").Padding(16, 6)
                                .StyleName(BuiltInStyles.AccentButton)
                                .CenterHorizontal()
                                .OnClick(() => dlg.Close())
                        ),
                    new StackPanel()
                        .Vertical()
                        .Spacing(8)
                        .CenterHorizontal()
                        .CenterVertical()
                        .Children(
                            new TextBlock().Text("MewDB").FontSize(28).Bold().CenterHorizontal()
                                .WithTheme((t, tb) => tb.Foreground(t.Palette.Accent)),
                            new TextBlock().Text("数据库管理客户端").FontSize(14).CenterHorizontal(),
                            new TextBlock().Text("版本 1.0.0 (Build 20240101)").FontSize(12).CenterHorizontal()
                                .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText)),
                            new TextBlock().Text("基于 MewUI 框架构建").FontSize(12).CenterHorizontal()
                                .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText)),
                            new TextBlock().Text("© 2024 MewUI Team. All rights reserved.").FontSize(11).CenterHorizontal()
                                .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText))
                        )
                )
        );

    _ = dlg.ShowDialogAsync(window);
}

// ═══════════════════════════════════════════════════════
//  Toast — 对应 demo.html showToast()
// ═══════════════════════════════════════════════════════
void ShowToast(string message) => window.ShowToast(message);

// ═══════════════════════════════════════════════════════
//  示例数据 — 对应 demo.html users 数组（20条中文用户）
// ═══════════════════════════════════════════════════════
List<UserRow> GenerateUsers() =>
[
    new(1,  "张伟", "zhangwei@example.com",  28, "北京", "2024-01-15"),
    new(2,  "李娜", "lina@example.com",      32, "上海", "2024-02-20"),
    new(3,  "王强", "wangqiang@example.com",  25, "深圳", "2024-03-10"),
    new(4,  "赵敏", "zhaomin@example.com",    29, "广州", "2024-01-22"),
    new(5,  "陈浩", "chenhao@example.com",    35, "杭州", "2024-04-05"),
    new(6,  "刘洋", "liuyang@example.com",    27, "成都", "2024-02-14"),
    new(7,  "孙丽", "sunli@example.com",      31, "南京", "2024-05-18"),
    new(8,  "周磊", "zhoulei@example.com",    24, "武汉", "2024-03-25"),
    new(9,  "吴芳", "wufang@example.com",     33, "西安", "2024-06-01"),
    new(10, "郑凯", "zhengkai@example.com",   26, "重庆", "2024-04-12"),
    new(11, "黄婷", "huangting@example.com",  30, "天津", "2024-07-08"),
    new(12, "林杰", "linjie@example.com",     22, "苏州", "2024-05-30"),
    new(13, "何雪", "hexue@example.com",      28, "长沙", "2024-08-15"),
    new(14, "马超", "machao@example.com",     36, "郑州", "2024-06-22"),
    new(15, "罗敏", "luomin@example.com",     29, "青岛", "2024-09-03"),
    new(16, "谢宇", "xieyu@example.com",      23, "大连", "2024-07-19"),
    new(17, "韩梅", "hanmei@example.com",     34, "厦门", "2024-10-01"),
    new(18, "唐风", "tangfeng@example.com",   27, "昆明", "2024-08-28"),
    new(19, "冯刚", "fenggang@example.com",   31, "合肥", "2024-11-10"),
    new(20, "曹颖", "caoying@example.com",    26, "福州", "2024-09-15")
];

// ═══════════════════════════════════════════════════════
//  平台与后端注册
// ═══════════════════════════════════════════════════════
static void Startup()
{
    var args = Environment.GetCommandLineArgs();

#if MEWUI_GALLERY_WIN
#pragma warning disable CA1416
    Win32Platform.Register();
    if (args.Any(a => a is "--gdi")) GdiBackend.Register();
    else if (args.Any(a => a is "--vg")) MewVGWin32Backend.Register();
    else Direct2DBackend.Register();
#pragma warning restore CA1416
#elif MEWUI_GALLERY_OSX
    MacOSPlatform.Register();
    MewVGMacOSBackend.Register();
#elif MEWUI_GALLERY_LINUX
    X11Platform.Register();
    MewVGX11Backend.Register();
#else
    if (OperatingSystem.IsWindows())
    {
        Win32Platform.Register();
        if (args.Any(a => a is "--gdi")) GdiBackend.Register();
        else if (args.Any(a => a is "--vg")) MewVGWin32Backend.Register();
        else Direct2DBackend.Register();
    }
    else if (OperatingSystem.IsMacOS())
    {
        MacOSPlatform.Register();
        MewVGMacOSBackend.Register();
    }
    else if (OperatingSystem.IsLinux())
    {
        X11Platform.Register();
        MewVGX11Backend.Register();
    }
#endif

    Application.DispatcherUnhandledException += e =>
    {
        try { NativeMessageBox.Show(e.Exception.ToString(), "Unhandled UI exception"); }
        catch { /* ignore */ }
        e.Handled = true;
    };
}

// ═══════════════════════════════════════════════════════
//  数据模型
// ═══════════════════════════════════════════════════════
sealed class UserRow
{
    public int Id { get; }
    public string Name { get; }
    public string Email { get; }
    public int Age { get; }
    public string City { get; }
    public string CreatedAt { get; }

    public UserRow(int id, string name, string email, int age, string city, string createdAt)
        => (Id, Name, Email, Age, City, CreatedAt) = (id, name, email, age, city, createdAt);
}

sealed class QueryTab
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public MultiLineTextBox Editor { get; set; } = null!;
    public ObservableValue<List<UserRow>> Results { get; set; } = null!;
    public ObservableValue<string> ElapsedText { get; set; } = new("耗时: 0ms");
    public ObservableValue<string> RowCountText { get; set; } = new("0 行");
}
