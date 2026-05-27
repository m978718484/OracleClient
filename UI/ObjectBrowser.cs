using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace OracleClient.UI;

/// <summary>
/// 对象浏览器面板 - 左侧显示数据库对象
/// PL/SQL Developer 风格：按类型分组的可展开列表
/// </summary>
public sealed class ObjectBrowser
{
    private readonly Services.OracleService _service;
    private readonly ObservableValue<string> _statusText = new("Ready");

    // 各类对象计数文本（绑定到 Expander header 右侧）
    private readonly ObservableValue<string> _tablesText = new("(0)");
    private readonly ObservableValue<string> _viewsText = new("(0)");
    private readonly ObservableValue<string> _procsText = new("(0)");
    private readonly ObservableValue<string> _funcsText = new("(0)");
    private readonly ObservableValue<string> _pkgsText = new("(0)");
    private readonly ObservableValue<string> _trigsText = new("(0)");
    private readonly ObservableValue<string> _seqsText = new("(0)");
    private readonly ObservableValue<string> _idxsText = new("(0)");
    private readonly ObservableValue<string> _synsText = new("(0)");

    // 各类对象名列表（绑定到 Expander 内容）
    private readonly ObservableValue<string> _tablesDisplay = new("(empty)");
    private readonly ObservableValue<string> _viewsDisplay = new("(empty)");
    private readonly ObservableValue<string> _procsDisplay = new("(empty)");
    private readonly ObservableValue<string> _funcsDisplay = new("(empty)");
    private readonly ObservableValue<string> _pkgsDisplay = new("(empty)");
    private readonly ObservableValue<string> _trigsDisplay = new("(empty)");
    private readonly ObservableValue<string> _seqsDisplay = new("(empty)");
    private readonly ObservableValue<string> _idxsDisplay = new("(empty)");
    private readonly ObservableValue<string> _synsDisplay = new("(empty)");

    public ObjectBrowser(Services.OracleService service)
    {
        _service = service;
    }

    public UIElement Build()
    {
        return new DockPanel()
            .Children(
                // 顶部标题栏
                new Border()
                    .DockTop()
                    .Padding(8, 6)
                    .BorderThickness(1)
                    .WithTheme((t, b) => b.Background(t.Palette.ButtonFace).BorderBrush(t.Palette.ControlBorder))
                    .Child(
                        new DockPanel()
                            .Children(
                                new TextBlock()
                                    .Text("Object Browser")
                                    .Bold()
                                    .FontSize(13)
                                    .CenterVertical()
                                    .DockLeft(),
                                new Button()
                                    .Content("Refresh")
                                    .FontSize(12)
                                    .OnClick(OnRefresh)
                                    .DockRight()
                                    .ToolTip("Refresh objects")
                            )
                    ),

                // 可滚动内容区域
                new ScrollViewer()
                    .Content(
                        new StackPanel()
                            .Vertical()
                            .Spacing(0)
                            .Children(
                                CreateObjectGroup("Tables", _tablesText, _tablesDisplay),
                                CreateObjectGroup("Views", _viewsText, _viewsDisplay),
                                CreateObjectGroup("Procedures", _procsText, _procsDisplay),
                                CreateObjectGroup("Functions", _funcsText, _funcsDisplay),
                                CreateObjectGroup("Packages", _pkgsText, _pkgsDisplay),
                                CreateObjectGroup("Triggers", _trigsText, _trigsDisplay),
                                CreateObjectGroup("Sequences", _seqsText, _seqsDisplay),
                                CreateObjectGroup("Indexes", _idxsText, _idxsDisplay),
                                CreateObjectGroup("Synonyms", _synsText, _synsDisplay)
                            )
                    ),

                // 底部状态
                new Border()
                    .DockBottom()
                    .Padding(6, 3)
                    .WithTheme((t, b) => b.Background(t.Palette.ButtonFace))
                    .Child(
                        new TextBlock()
                            .BindText(_statusText)
                            .FontSize(11)
                    )
            );
    }

    private Element CreateObjectGroup(string title, ObservableValue<string> countText, ObservableValue<string> displayText)
    {
        return new Expander()
            .Header(title)
            .Content(
                new Border()
                    .Padding(12, 4)
                    .Child(
                        new TextBlock()
                            .BindText(displayText)
                            .FontSize(12)
                            .FontFamily("Consolas")
                    )
            );
    }

    /// <summary>
    /// 公共刷新方法，供 MainWindow.OnRefreshAll() 调用
    /// </summary>
    public void Refresh()
    {
        _ = RefreshAsync();
    }

    private void OnRefresh()
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _statusText.Value = "Loading objects...";

        try
        {
            var objects = await _service.GetSchemaObjectsAsync();

            foreach (var obj in objects)
            {
                var names = obj.Children.Select(c => c.Name).ToList();
                var countStr = $"({names.Count})";
                var displayStr = names.Count == 0 ? "(empty)" : string.Join("\n", names);

                switch (obj.NodeType)
                {
                    case Models.ObjectNodeType.TableFolder:
                        _tablesText.Value = countStr;
                        _tablesDisplay.Value = displayStr;
                        break;
                    case Models.ObjectNodeType.ViewFolder:
                        _viewsText.Value = countStr;
                        _viewsDisplay.Value = displayStr;
                        break;
                    case Models.ObjectNodeType.ProcedureFolder:
                        _procsText.Value = countStr;
                        _procsDisplay.Value = displayStr;
                        break;
                    case Models.ObjectNodeType.FunctionFolder:
                        _funcsText.Value = countStr;
                        _funcsDisplay.Value = displayStr;
                        break;
                    case Models.ObjectNodeType.PackageFolder:
                        _pkgsText.Value = countStr;
                        _pkgsDisplay.Value = displayStr;
                        break;
                    case Models.ObjectNodeType.TriggerFolder:
                        _trigsText.Value = countStr;
                        _trigsDisplay.Value = displayStr;
                        break;
                    case Models.ObjectNodeType.SequenceFolder:
                        _seqsText.Value = countStr;
                        _seqsDisplay.Value = displayStr;
                        break;
                    case Models.ObjectNodeType.IndexFolder:
                        _idxsText.Value = countStr;
                        _idxsDisplay.Value = displayStr;
                        break;
                    case Models.ObjectNodeType.SynonymFolder:
                        _synsText.Value = countStr;
                        _synsDisplay.Value = displayStr;
                        break;
                }
            }

            _statusText.Value = $"Loaded {objects.Sum(o => o.Children.Count)} objects";
        }
        catch (Exception ex)
        {
            _statusText.Value = $"Error: {ex.Message}";
        }
    }
}
