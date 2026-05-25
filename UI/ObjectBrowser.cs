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

    // 各类对象列表
    private readonly ObservableValue<string> _tablesText = new("0 items");
    private readonly ObservableValue<string> _viewsText = new("0 items");
    private readonly ObservableValue<string> _procsText = new("0 items");
    private readonly ObservableValue<string> _funcsText = new("0 items");
    private readonly ObservableValue<string> _pkgsText = new("0 items");
    private readonly ObservableValue<string> _trigsText = new("0 items");
    private readonly ObservableValue<string> _seqsText = new("0 items");
    private readonly ObservableValue<string> _idxsText = new("0 items");
    private readonly ObservableValue<string> _synsText = new("0 items");

    // 列表数据源
    private List<string> _tables = [];
    private List<string> _views = [];
    private List<string> _procs = [];
    private List<string> _funcs = [];
    private List<string> _pkgs = [];
    private List<string> _trigs = [];
    private List<string> _seqs = [];
    private List<string> _idxs = [];
    private List<string> _syns = [];

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
                                CreateObjectGroup("Tables", _tablesText, _tables, "TABLE"),
                                CreateObjectGroup("Views", _viewsText, _views, "VIEW"),
                                CreateObjectGroup("Procedures", _procsText, _procs, "PROCEDURE"),
                                CreateObjectGroup("Functions", _funcsText, _funcs, "FUNCTION"),
                                CreateObjectGroup("Packages", _pkgsText, _pkgs, "PACKAGE"),
                                CreateObjectGroup("Triggers", _trigsText, _trigs, "TRIGGER"),
                                CreateObjectGroup("Sequences", _seqsText, _seqs, "SEQUENCE"),
                                CreateObjectGroup("Indexes", _idxsText, _idxs, "INDEX"),
                                CreateObjectGroup("Synonyms", _synsText, _syns, "SYNONYM")
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

    private Element CreateObjectGroup(string title, ObservableValue<string> countText, List<string> items, string objectType)
    {
        var header = new DockPanel()
            .Children(
                new TextBlock()
                    .Text(title)
                    .Bold()
                    .FontSize(12)
                    .CenterVertical()
                    .DockLeft(),
                new TextBlock()
                    .BindText(countText)
                    .FontSize(11)
                    .WithTheme((t, e) => e.Foreground(t.Palette.DisabledText))
                    .DockRight()
            );

        // 创建列表内容 - 使用简单的文本显示
        var listContent = new Border()
            .Padding(12, 4)
            .Child(
                new TextBlock()
                    .Text(items.Count == 0 ? "(empty)" : string.Join("\n", items))
                    .FontSize(12)
                    .FontFamily("Consolas")
            );

        return new Expander()
            .Header(title)
            .Content(listContent);
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

                switch (obj.NodeType)
                {
                    case Models.ObjectNodeType.TableFolder:
                        _tables = names;
                        _tablesText.Value = countStr;
                        break;
                    case Models.ObjectNodeType.ViewFolder:
                        _views = names;
                        _viewsText.Value = countStr;
                        break;
                    case Models.ObjectNodeType.ProcedureFolder:
                        _procs = names;
                        _procsText.Value = countStr;
                        break;
                    case Models.ObjectNodeType.FunctionFolder:
                        _funcs = names;
                        _funcsText.Value = countStr;
                        break;
                    case Models.ObjectNodeType.PackageFolder:
                        _pkgs = names;
                        _pkgsText.Value = countStr;
                        break;
                    case Models.ObjectNodeType.TriggerFolder:
                        _trigs = names;
                        _trigsText.Value = countStr;
                        break;
                    case Models.ObjectNodeType.SequenceFolder:
                        _seqs = names;
                        _seqsText.Value = countStr;
                        break;
                    case Models.ObjectNodeType.IndexFolder:
                        _idxs = names;
                        _idxsText.Value = countStr;
                        break;
                    case Models.ObjectNodeType.SynonymFolder:
                        _syns = names;
                        _synsText.Value = countStr;
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
