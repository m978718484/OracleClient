/* MewUI.DBClient — Oracle 数据库管理客户端
 *
 * 基于 MewUI 框架的 Oracle 数据库管理客户端：
 * - 菜单栏: 文件(F) / 编辑(E) / 视图(V) / 帮助(H)，含下拉菜单、快捷键、分隔线
 * - 工具栏: 新建连接 / 新建查询 / 执行 / 执行选中 / 停止 / 刷新 / 主题切换
 * - 侧边栏: Oracle 连接卡片 + 对象浏览器树
 * - 分割面板: 可拖拽分割 sidebar 和编辑区
 * - 选项卡: 查询标签页，支持新建/关闭
 * - SQL 编辑器: 等宽字体，支持 Tab 缩进
 * - 执行栏: 执行(主色) / 执行选中 / 停止 + 耗时显示
 * - 结果网格: 动态列 GridView
 * - 结果工具栏: 刷新 / 导出 / 编辑模式开关
 * - 分页栏: 总行数 / 翻页控件 / 每页行数选择
 * - 状态栏: 强调色背景，连接信息 / 状态文本 / 查询计数
 * - 连接对话框: Oracle 专用表单 + 测试/保存/取消
 * - 关于对话框
 * - 右键菜单: 树节点右键
 * - Toast 通知
 * - 暗色/亮色主题切换
 */

using System.Data;
using System.Diagnostics;
using System.Linq;

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Oracle.ManagedDataAccess.Client;

#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Aprillz.MewUI.HotReload.MewUiMetadataUpdateHandler))]
#endif

var stopwatch = Stopwatch.StartNew();
Startup();

Window window = null!;
IconSource appIcon;
using (var rs = typeof(Program).Assembly.GetManifestResourceStream("OracleClient.assets.icon.appicon.ico")!)
    appIcon = IconSource.FromStream(rs);

// ── 应用状态 ─────────────────────────────────────────
var isDarkMode          = new ObservableValue<bool>(true);
var oracle              = new OracleService();
var connectionStatus    = new ObservableValue<string>("未连接");
var statusText          = new ObservableValue<string>("就绪");
var queryCount       = new ObservableValue<int>(0);
var isEditing        = new ObservableValue<bool>(false);
var currentPage      = new ObservableValue<int>(1);
var totalRows        = new ObservableValue<int>(0);
var isConnected      = new ObservableValue<bool>(false);
var currentTabId     = 0;
var queryTabs        = new List<QueryTab>();
TabControl queryTabControl = new();
TreeView schemaTree  = null!;

// ── 主题切换 ─────────────────────────────────────────
isDarkMode.Changed += () =>
{
    Application.Current.SetTheme(isDarkMode.Value ? ThemeVariant.Dark : ThemeVariant.Light);
};

// ── 连接状态变化时刷新树 ─────────────────────────────
isConnected.Changed += () =>
{
    RefreshSchemaTree();
};

// ── 应用入口 ─────────────────────────────────────────
Application
    .Create()
    .UseAccent(Accent.Blue)
    .BuildMainWindow(() =>
        new Window()
            .Icon(appIcon)
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
                window.ShowToast("欢迎使用 Oracle Developer — 数据库管理客户端");
                stopwatch.Stop();
                statusText.Value = $"就绪  |  加载耗时 {stopwatch.Elapsed.TotalSeconds:0.00}s";
            })
    )
    .Run();

// ═══════════════════════════════════════════════════════
//  菜单栏 — 文件(F) / 编辑(E) / 视图(V) / 帮助(H)
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
//  工具栏
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
                            MkToolBtn("刷新", RefreshSchemaTree)
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
//  侧边栏 — 连接卡片 + 对象浏览器
// ═══════════════════════════════════════════════════════
FrameworkElement CreateSidebar()
{
    // 对象浏览器树
    schemaTree = new TreeView()
        .ItemsSource(BuildTreeData())
        .ExpandTrigger(TreeViewExpandTrigger.DoubleClickNode)
        .OnSelectedNodeChanged(node =>
        {
            if (node is not null) statusText.Value = $"选中: {node.Text}";
        });

    // 右键菜单
    schemaTree.ContextMenu = new ContextMenu()
        .Item("查看数据", () =>
        {
            if (schemaTree.SelectedNode is { } n) OpenTableQuery(n.Text);
        })
        .Item("编辑结构", () =>
        {
            if (schemaTree.SelectedNode is { } n) ShowToast($"编辑 {n.Text} 结构");
        })
        .Separator()
        .Item("导出", () => ShowToast("已导出数据到 CSV"))
        .Separator()
        .Item("删除", () =>
        {
            if (schemaTree.SelectedNode is { } n) ShowToast($"删除表 {n.Text}（模拟）");
        });

    return new Border()
        .BorderThickness(1)
        .WithTheme((t, b) => b.Background(t.Palette.ContainerBackground).BorderBrush(t.Palette.ControlBorder))
        .Child(
            new DockPanel()
                .Children(
                    // ── 连接 Header
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

                    // ── 连接卡片
                    new Border()
                        .DockTop()
                        .BorderThickness(1)
                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                        .Child(
                            MkConnectionCard()
                        ),

                    // ── 对象浏览器 Header
                    new Border()
                        .DockTop()
                        .Padding(10, 8)
                        .BorderThickness(1)
                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                        .Child(
                            new TextBlock().Text("对象浏览器").Bold().FontSize(11.5)
                                .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText))
                        ),

                    // ── 树
                    new ScrollViewer()
                        .VerticalScroll(ScrollMode.Auto)
                        .Content(schemaTree)
                )
        );
}

// Oracle 连接卡片
FrameworkElement MkConnectionCard()
{
    var oracleColor = new Color(192, 0, 0);
    var card = new Border()
        .Padding(10, 6)
        .Child(
            new DockPanel()
                .Children(
                    new Border()
                        .DockRight().Width(7).Height(7).CornerRadius(4).CenterVertical()
                        .WithTheme((t, b) => b.Background(isConnected.Value ? new Color(34, 197, 94) : t.Palette.DisabledText)),
                    new Border()
                        .Width(20).Height(20).CornerRadius(3).CenterVertical().Margin(0, 0, 8, 0)
                        .WithTheme((t, b) => b.Background(oracleColor.WithAlpha(34)))
                        .Child(
                            new TextBlock().Text("O").FontSize(11).Bold()
                                .CenterHorizontal().CenterVertical()
                                .WithTheme((t, tb) => tb.Foreground(oracleColor))
                        ),
                    new StackPanel()
                        .Vertical().Spacing(1).CenterVertical()
                        .Children(
                            new TextBlock().Text("Oracle DB").FontSize(12.5).Bold(),
                            new TextBlock().BindText(connectionStatus).FontSize(11)
                                .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText))
                        )
                )
        );

    // 连接状态变化时更新状态点颜色
    isConnected.Changed += () =>
    {
        var t = Application.Current.Theme;
        card.Child = new DockPanel()
            .Children(
                new Border()
                    .DockRight().Width(7).Height(7).CornerRadius(4).CenterVertical()
                    .WithTheme((_, b) => b.Background(isConnected.Value ? new Color(34, 197, 94) : t.Palette.DisabledText)),
                new Border()
                    .Width(20).Height(20).CornerRadius(3).CenterVertical().Margin(0, 0, 8, 0)
                    .WithTheme((_, b) => b.Background(oracleColor.WithAlpha(34)))
                    .Child(
                        new TextBlock().Text("O").FontSize(11).Bold()
                            .CenterHorizontal().CenterVertical()
                            .WithTheme((_, tb) => tb.Foreground(oracleColor))
                    ),
                new StackPanel()
                    .Vertical().Spacing(1).CenterVertical()
                    .Children(
                        new TextBlock().Text("Oracle DB").FontSize(12.5).Bold(),
                        new TextBlock().BindText(connectionStatus).FontSize(11)
                            .WithTheme((_, tb2) => tb2.Foreground(t.Palette.DisabledText))
                    )
            );
    };

    return card;
}

// ═══════════════════════════════════════════════════════
//  对象树数据
// ═══════════════════════════════════════════════════════
IReadOnlyList<Aprillz.MewUI.Controls.TreeViewNode> BuildTreeData()
{
    if (!oracle.IsConnected)
        return Array.Empty<Aprillz.MewUI.Controls.TreeViewNode>();

    try
    {
        return oracle.GetSchemaObjects()
            .Select(o => new Aprillz.MewUI.Controls.TreeViewNode(
                o.Name,
                o.Children.Select(c => new Aprillz.MewUI.Controls.TreeViewNode(c)).ToArray()
            ))
            .ToArray();
    }
    catch
    {
        return Array.Empty<Aprillz.MewUI.Controls.TreeViewNode>();
    }
}

void RefreshSchemaTree()
{
    if (schemaTree is null) return;
    schemaTree.ItemsSource = TreeItemsView.Create(
        BuildTreeData(), n => n.Children, textSelector: n => n.Text, keySelector: n => n);
    statusText.Value = "对象树已刷新";
}

// ═══════════════════════════════════════════════════════
//  编辑区 — TabControl + SQL编辑器 + 执行栏 + 结果网格 + 分页
// ═══════════════════════════════════════════════════════
FrameworkElement CreateEditorArea()
{
    AddQueryTab("SELECT * FROM emp");

    // 标签页右键菜单
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

    // ── SQL 编辑器 ──
    var editor = new MultiLineTextBox()
        .FontFamily("Consolas, Courier New, monospace")
        .FontSize(13)
        .AcceptTab(true)
        .Text(initialSql ?? string.Empty);

    var results    = new ObservableValue<List<Dictionary<string, object?>>?>(null);
    var elapsed    = new ObservableValue<string>("耗时: 0ms");
    var rowCount   = new ObservableValue<string>("0 行");

    // ── 结果网格 — 动态列 ──
    var grid = new GridView()
        .ZebraStriping(true)
        .ShowGridLines(true)
        .RowHeight(24);

    // ── 结果区布局 ──
    var resultArea = new DockPanel()
        .Children(
            // 结果工具栏
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
                                    new ComboBox()
                                        .Items("导出 CSV", "导出 JSON", "导出 SQL INSERT", "导出 Excel")
                                        .Placeholder("导出")
                                        .FontSize(12)
                                        .OnSelectionChanged(item => ShowToast($"已导出数据到 {item}")),
                                    new TextBlock().BindText(rowCount).FontSize(11).CenterVertical().Margin(8, 0, 0, 0),
                                    new TextBlock().BindText(elapsed).FontSize(11).CenterVertical()
                                        .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText))
                                ),
                            // 编辑模式区域
                            new StackPanel()
                                .DockRight().Horizontal().Spacing(6).CenterVertical()
                                .Children(
                                    new TextBlock().Text("编辑模式").FontSize(12)
                                        .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText)),
                                    new ToggleSwitch().BindIsChecked(isEditing),
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

            // 分页栏
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

            // 网格滚动区
            new ScrollViewer()
                .VerticalScroll(ScrollMode.Auto)
                .HorizontalScroll(ScrollMode.Auto)
                .Content(grid)
        );

    // ── 执行栏 ──
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

    // ── 编辑器+结果 垂直分割 ──
    var split = new SplitPanel
    {
        Orientation = Orientation.Vertical,
        SplitterThickness = 4,
        First = new DockPanel().Children(execBar.DockBottom(), editor),
        Second = resultArea
    };

    // ── 标签页头 ──
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

    queryTabs.Add(new QueryTab
    {
        Id = tabId, Title = tabTitle, Editor = editor,
        Results = results, ElapsedText = elapsed, RowCountText = rowCount,
        ResultGrid = grid
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
//  状态栏
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
//  查询执行
// ═══════════════════════════════════════════════════════
void ExecuteQuery()
{
    var idx = queryTabControl.SelectedIndex;
    if (idx < 0 || idx >= queryTabs.Count) return;

    var tab = queryTabs[idx];
    var sql = tab.Editor.Text?.Trim();
    if (string.IsNullOrWhiteSpace(sql))
    {
        ShowToast("查询内容为空");
        return;
    }

    if (!oracle.IsConnected)
    {
        ShowToast("未连接数据库，请先建立连接");
        return;
    }

    statusText.Value = "正在执行查询...";
    queryCount.Value++;

    Task.Run(() => oracle.ExecuteQuery(sql)).ContinueWith(t =>
    {
        var (columns, rows, error) = t.Result;
        if (error is not null)
        {
            tab.ElapsedText.Value = "耗时: -";
            tab.RowCountText.Value = "0 行";
            statusText.Value = "就绪";
            ShowToast($"查询错误: {error}");
            return;
        }

        // 转换为 Dictionary 行
        var dictRows = rows.Select(r =>
        {
            var d = new Dictionary<string, object?>();
            for (int i = 0; i < columns.Count; i++)
                d[columns[i].Name] = i < r.Length ? r[i] : null;
            return d;
        }).ToList();

        tab.Results.Value = dictRows;
        tab.RowCountText.Value = $"{dictRows.Count} 行";
        totalRows.Value = dictRows.Count;

        // 动态构建 GridView 列
        var gridColumns = new List<GridViewColumn<Dictionary<string, object?>>>();
        foreach (var col in columns)
        {
            var colName = col.Name;
            var colWidth = col.Width;
            var gridColumn = new GridViewColumn<Dictionary<string, object?>>()
            {
                Header = colName,
                Width = colWidth,
                CellTemplate = new DelegateTemplate<Dictionary<string, object?>>(
                    build: _ => new TextBlock().CenterVertical(),
                    bind: (FrameworkElement el, Dictionary<string, object?> item, int _, TemplateContext __) =>
                    {
                        var tb = (TextBlock)el;
                        if (!item.TryGetValue(colName, out var val) || val is null)
                            tb.Text = "(null)";
                        else
                            tb.Text = val.ToString() ?? "";
                    })
            };
            gridColumns.Add(gridColumn);
        }

        tab.ResultGrid.SetColumns(gridColumns);
        tab.ResultGrid.SetItemsSource(dictRows);

        statusText.Value = "就绪";
        ShowToast($"查询完成，返回 {dictRows.Count} 行");
    });
}

// 双击树节点打开表查询
void OpenTableQuery(string tableName)
{
    var idx = queryTabControl.SelectedIndex;
    if (idx >= 0 && idx < queryTabs.Count)
    {
        queryTabs[idx].Editor.Text = $"SELECT * FROM {tableName} WHERE ROWNUM <= 100";
    }
    ShowToast($"查看 {tableName} 数据");
}

// ═══════════════════════════════════════════════════════
//  连接对话框 — Oracle 专用
// ═══════════════════════════════════════════════════════
void ShowConnectionDialog()
{
    var connName     = new ObservableValue<string>("Oracle 连接");
    var connHost     = new ObservableValue<string>("100.66.115.67");
    var connPort     = new ObservableValue<string>("1521");
    var connService  = new ObservableValue<string>("orcl");
    var connUser     = new ObservableValue<string>("scott");
    var connPass     = new ObservableValue<string>("tiger");
    var connRole     = new ObservableValue<int>(0);
    var connStatus   = new ObservableValue<string>("");
    var canClick     = new ObservableValue<bool>(true);

    Window dlg = null!;
    dlg = new Window()
        .Title("新建连接 — Oracle")
        .Resizable(480, 520)
        .StartCenterScreen()
        .Content(
            new DockPanel()
                .Children(
                    // ── modal-footer ──
                    new Border()
                        .DockBottom()
                        .Padding(12, 18)
                        .BorderThickness(1)
                        .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                        .Child(
                            new DockPanel()
                                .Children(
                                    new TextBlock()
                                        .BindText(connStatus).FontSize(11.5).CenterVertical()
                                        .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText)),
                                    new StackPanel()
                                        .DockRight().Horizontal().Spacing(8)
                                        .Children(
                                            // 测试连接
                                            new Button()
                                                .Content("测试连接")
                                                .Padding(16, 6)
                                                .BindIsEnabled(canClick)
                                                .WithTheme((t, b) => b.Foreground(new Color(34, 197, 94)))
                                                .OnClick(() =>
                                                {
                                                    if (!int.TryParse(connPort.Value, out var port)) port = 1521;
                                                    var role = connRole.Value switch { 1 => "SYSDBA", 2 => "SYSOPER", _ => "Normal" };
                                                    var h = connHost.Value; var s = connService.Value; var u = connUser.Value; var p = connPass.Value;
                                                    canClick.Value = false;
                                                    connStatus.Value = "正在连接...";
                                                    Task.Run(() => oracle.Connect(h, port, s, u, p, role)).ContinueWith(t =>
                                                    {
                                                        var (ok, err) = t.Result;
                                                        canClick.Value = true;
                                                        connStatus.Value = ok ? "连接测试成功" : $"连接失败: {err}";
                                                    });
                                                }),
                                            // 保存并连接
                                            new Button()
                                                .Content("保存并连接")
                                                .Padding(16, 6)
                                                .BindIsEnabled(canClick)
                                                .StyleName(BuiltInStyles.AccentButton)
                                                .OnClick(() =>
                                                {
                                                    if (!int.TryParse(connPort.Value, out var port)) port = 1521;
                                                    var role = connRole.Value switch { 1 => "SYSDBA", 2 => "SYSOPER", _ => "Normal" };
                                                    var h = connHost.Value; var s = connService.Value; var u = connUser.Value; var p = connPass.Value; var n = connName.Value;
                                                    canClick.Value = false;
                                                    connStatus.Value = "正在连接...";
                                                    Task.Run(() => oracle.Connect(h, port, s, u, p, role)).ContinueWith(t =>
                                                    {
                                                        var (ok, err) = t.Result;
                                                        canClick.Value = true;
                                                        if (ok)
                                                        {
                                                            isConnected.Value = true;
                                                            connectionStatus.Value = $"{h}:{port}";
                                                            RefreshSchemaTree();
                                                            dlg.Close();
                                                        }
                                                        else
                                                        {
                                                            connStatus.Value = $"连接失败: {err}";
                                                        }
                                                    });
                                                }),
                                            // 取消
                                            new Button()
                                                .Content("取消")
                                                .Padding(16, 6)
                                                .BorderThickness(1)
                                                .WithTheme((t, b) => b.BorderBrush(t.Palette.ControlBorder))
                                                .OnClick(() => dlg.Close())
                                        )
                                )
                        ),

                    // ── modal-body ──
                    new ScrollViewer()
                        .VerticalScroll(ScrollMode.Auto)
                        .Content(
                            new Border()
                                .Padding(18)
                                .Child(
                                    new StackPanel()
                                        .Vertical().Spacing(14)
                                        .Children(
                                            // Oracle 图标
                                            new Border()
                                                .CenterHorizontal()
                                                .Padding(12, 8).CornerRadius(6).BorderThickness(2)
                                                .WithTheme((t, b) => b.Background(new Color(192, 0, 0).WithAlpha(20)).BorderBrush(new Color(192, 0, 0)))
                                                .Child(
                                                    new TextBlock().Text("O  Oracle Database").Bold().FontSize(16)
                                                        .WithTheme((t, tb) => tb.Foreground(new Color(192, 0, 0)))
                                                ),

                                            // 表单字段
                                            new Grid()
                                                .Columns("90,*")
                                                .Rows("Auto,Auto,Auto,Auto,Auto,Auto,Auto")
                                                .Spacing(10)
                                                .AutoIndexing()
                                                .Children(
                                                    MkLabel("连接名称"),
                                                    new TextBox().BindText(connName).Placeholder("输入连接名称"),
                                                    MkLabel("主机地址"),
                                                    new TextBox().BindText(connHost).Placeholder("输入主机地址"),
                                                    MkLabel("端口"),
                                                    new TextBox().BindText(connPort).Placeholder("端口号"),
                                                    MkLabel("服务名"),
                                                    new TextBox().BindText(connService).Placeholder("服务名"),
                                                    MkLabel("用户名"),
                                                    new TextBox().BindText(connUser).Placeholder("用户名"),
                                                    MkLabel("密码"),
                                                    new PasswordBox().BindPassword(connPass).Placeholder("密码"),
                                                    MkLabel("角色"),
                                                    new ComboBox().Items("Normal", "SYSDBA", "SYSOPER").BindSelectedIndex(connRole)
                                                )
                                        )
                                )
                        )
                )
        );

    _ = dlg.ShowDialogAsync(window);
}

// 表单标签
FrameworkElement MkLabel(string text) => new TextBlock()
    .Text(text).FontSize(12.5).Right().CenterVertical()
    .WithTheme((t, tb) => tb.Foreground(t.Palette.DisabledText));

// ═══════════════════════════════════════════════════════
//  关于对话框
// ═══════════════════════════════════════════════════════
void ShowAboutDialog()
{
    Window dlg = null!;
    dlg = new Window()
        .Title("关于 Oracle Developer")
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
                            new TextBlock().Text("Oracle Developer").FontSize(28).Bold().CenterHorizontal()
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
//  Toast
// ═══════════════════════════════════════════════════════
void ShowToast(string message) => window.ShowToast(message);

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
//  Oracle 数据库服务
// ═══════════════════════════════════════════════════════
sealed class OracleService
{
    private OracleConnection? _connection;
    public string ConnectionString { get; private set; } = "";
    public bool IsConnected { get; private set; }
    public event Action<bool>? ConnectionStateChanged;

    public (bool Success, string? Error) Connect(string host, int port, string serviceName, string username, string password, string role = "Normal")
    {
        try
        {
            Disconnect();
            var roleStr = role.ToUpperInvariant() is "SYSDBA" or "SYSOPER" ? $";DBA Privilege={role.ToUpperInvariant()}" : "";
            var connStr = $"Data Source=(DESCRIPTION=(CONNECT_TIMEOUT=30)(TRANSPORT_CONNECT_TIMEOUT=10)(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME={serviceName})));User Id={username};Password={password}{roleStr};";
            _connection = new OracleConnection(connStr);
            _connection.Open();
            ConnectionString = $"{host}:{port}/{serviceName}";
            IsConnected = true;
            ConnectionStateChanged?.Invoke(true);
            return (true, null);
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionString = "";
            ConnectionStateChanged?.Invoke(false);
            return (false, ex.Message);
        }
    }

    public void Disconnect()
    {
        if (_connection != null)
        {
            try { _connection.Close(); } catch { }
            try { _connection.Dispose(); } catch { }
            _connection = null;
        }
        IsConnected = false;
        ConnectionString = "";
        ConnectionStateChanged?.Invoke(false);
    }

    public (List<ResultColumn> Columns, List<string?[]> Rows, string? Error) ExecuteQuery(string sql, int maxRows = 1000)
    {
        try
        {
            if (_connection is null) return ([], [], "未连接数据库");
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            using var adapter = new OracleDataAdapter(cmd);
            var dt = new DataTable();
            adapter.Fill(dt);

            var columns = new List<ResultColumn>();
            foreach (DataColumn dc in dt.Columns)
                columns.Add(new() { Name = dc.ColumnName, Width = Math.Max(80, dc.ColumnName.Length * 10) });

            var rows = new List<string?[]>();
            var count = 0;
            foreach (DataRow dr in dt.Rows)
            {
                if (count++ >= maxRows) break;
                var vals = new string?[dt.Columns.Count];
                for (int i = 0; i < vals.Length; i++)
                    vals[i] = dr.IsNull(i) ? null : dr[i]?.ToString();
                rows.Add(vals);
            }

            return (columns, rows, null);
        }
        catch (Exception ex) { return ([], [], ex.Message); }
    }

    public List<TreeViewNode> GetSchemaObjects()
    {
        try
        {
            if (_connection is null) return [];
            var objects = new List<TreeViewNode>();

            // 表
            var tables = new List<string>();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT table_name FROM user_tables ORDER BY table_name";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) tables.Add(reader.GetString(0));
            }
            objects.Add(new() { Name = "表", Children = tables });

            // 视图
            var views = new List<string>();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT view_name FROM user_views ORDER BY view_name";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) views.Add(reader.GetString(0));
            }
            objects.Add(new() { Name = "视图", Children = views });

            // 索引
            var indexes = new List<string>();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT index_name FROM user_indexes ORDER BY index_name";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) indexes.Add(reader.GetString(0));
            }
            objects.Add(new() { Name = "索引", Children = indexes });

            return objects;
        }
        catch { return []; }
    }

    public List<TableColumnInfo> GetTableColumns(string tableName)
    {
        try
        {
            if (_connection is null) return [];
            var columns = new List<TableColumnInfo>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"SELECT c.column_name, c.data_type, c.data_length, c.nullable, cc.comments FROM user_tab_columns c LEFT JOIN user_col_comments cc ON c.table_name = cc.table_name AND c.column_name = cc.column_name WHERE c.table_name = '{tableName.ToUpper()}' ORDER BY c.column_id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(new()
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    DataLength = reader.GetInt32(2),
                    Nullable = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Comments = reader.IsDBNull(4) ? "" : reader.GetString(4)
                });
            }
            return columns;
        }
        catch { return []; }
    }
}

// ═══════════════════════════════════════════════════════
//  数据模型
// ═══════════════════════════════════════════════════════
sealed class TreeViewNode
{
    public string Name { get; set; } = "";
    public List<string> Children { get; set; } = [];
    public string Text => Name;
}

sealed class ResultColumn
{
    public string Name { get; set; } = "";
    public double Width { get; set; }
}

sealed class TableColumnInfo
{
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public int DataLength { get; set; }
    public string? Nullable { get; set; }
    public string? Comments { get; set; }
}

sealed class QueryTab
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public MultiLineTextBox Editor { get; set; } = null!;
    public ObservableValue<List<Dictionary<string, object?>>?> Results { get; set; } = new(null);
    public ObservableValue<string> ElapsedText { get; set; } = new("耗时: 0ms");
    public ObservableValue<string> RowCountText { get; set; } = new("0 行");
    public GridView ResultGrid { get; set; } = null!;
}
