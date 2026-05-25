using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace OracleClient.UI;

/// <summary>
/// SQL编辑器面板 - PL/SQL Developer风格：上方编辑器，下方结果面板
/// </summary>
public sealed class SqlEditorPanel
{
    private readonly Services.OracleService _service;
    private readonly ObservableValue<string> _sqlText = new("");
    private readonly ObservableValue<string> _resultStatus = new("Ready");
    private readonly ObservableValue<bool> _isExecuting = new(false);
    private readonly ObservableValue<string> _outputText = new("");

    // 查询结果
    private List<Models.ResultColumn> _columns = [];
    private List<Models.ResultRow> _rows = [];
    private GridView? _resultsGrid;

    public SqlEditorPanel(Services.OracleService service)
    {
        _service = service;
    }

    public UIElement Build()
    {
        // PL/SQL Developer 风格：上下分割面板
        return new SplitPanel()
            .Vertical()
            .SplitterThickness(6)
            .MinFirst(80)
            .MinSecond(60)
            .FirstLength(GridLength.Stars(3))
            .SecondLength(GridLength.Stars(2))
            .First(CreateEditorArea())
            .Second(CreateResultsArea());
    }

    private UIElement CreateEditorArea()
    {
        return new DockPanel()
            .Children(
                CreateEditorToolBar().DockTop(),
                new Border()
                    .Padding(4)
                    .Child(
                        new MultiLineTextBox()
                            .FontFamily("Consolas")
                            .FontSize(14)
                            .Wrap(false)
                            .BindText(_sqlText)
                            .Placeholder("Enter SQL statement here...")
                    )
            );
    }

private UIElement CreateEditorToolBar()
    {
        return new Border()
            .DockTop()
            .Padding(4, 2)
            .BorderThickness(1)
            .WithTheme((t, b) => b.Background(t.Palette.ButtonFace).BorderBrush(t.Palette.ControlBorder))
            .Child(
                new StackPanel()
                    .Horizontal()
                    .Spacing(4)
                    .Children(
                        new Button()
                            .Content("Execute")
                            .OnClick(OnExecute)
                            .BindIsEnabled(_isExecuting)
                            .ToolTip("Execute SQL (F8)"),

                        new Button()
                            .Content("Stop")
                            .ToolTip("Cancel execution"),

                        new Button()
                            .Content("Explain")
                            .OnClick(() => OnExplainPlan())
                            .BindIsEnabled(_isExecuting)
                            .ToolTip("Explain Plan"),

                        new Button()
                            .Content("Clear")
                            .OnClick(() => _sqlText.Value = "")
                            .ToolTip("Clear editor"),

                        new Button()
                            .Content("Save")
                            .ToolTip("Save SQL to file"),

                        new Button()
                            .Content("Open")
                            .ToolTip("Open SQL file")
                    )
            );
    }

    private UIElement CreateResultsArea()
    {
        var tabControl = new TabControl();
        tabControl.AddTabs(
            new TabItem()
                .Header("Results")
                .Content(CreateResultsGrid()),

            new TabItem()
                .Header("Output")
                .Content(
                    new Border()
                        .Padding(8)
                        .Child(
                            new MultiLineTextBox()
                                .FontFamily("Consolas")
                                .FontSize(12)
                                .IsReadOnly(true)
                                .Wrap(true)
                                .BindText(_outputText)
                        )
                ),

            new TabItem()
                .Header("Statistics")
                .Content(
                    new Border()
                        .Padding(8)
                        .Child(
                            new TextBlock()
                                .BindText(_resultStatus)
                                .FontSize(12)
                        )
                )
        );
        return tabControl;
    }

    private UIElement CreateResultsGrid()
    {
        _resultsGrid = new GridView()
            .ZebraStriping()
            .RowHeight(28)
            .ShowGridLines();

        return new DockPanel()
            .Children(
                new Border()
                    .DockBottom()
                    .Padding(6, 3)
                    .BorderThickness(1)
                    .WithTheme((t, b) => b.Background(t.Palette.ButtonFace).BorderBrush(t.Palette.ControlBorder))
                    .Child(
                        new TextBlock()
                            .BindText(_resultStatus)
                            .FontSize(11)
                    ),

                _resultsGrid
            );
    }

    private void OnExecute()
    {
        _ = ExecuteAsync();
    }

    private async Task ExecuteAsync()
    {
        var sql = _sqlText.Value.Trim();
        if (string.IsNullOrWhiteSpace(sql)) return;

        _isExecuting.Value = true;
        _resultStatus.Value = "Executing...";

        try
        {
            var upperSql = sql.TrimStart().ToUpperInvariant();
            if (upperSql.StartsWith("SELECT") || upperSql.StartsWith("WITH") ||
                upperSql.StartsWith("(") || upperSql.StartsWith("EXPLAIN"))
            {
                var (columns, rows, error) = await _service.ExecuteQueryAsync(sql);

                if (error != null)
                {
                    _outputText.Value = $"Error:\n{error}";
                    _resultStatus.Value = $"Error: {error}";
                    _columns = [];
                    _rows = [];
                }
                else
                {
                    _columns = columns;
                    _rows = rows;
                    _resultStatus.Value = $"{rows.Count} rows retrieved in {columns.Count} columns";
                    UpdateResultsGrid();
                }
            }
            else
            {
                var (affected, error) = await _service.ExecuteNonQueryAsync(sql);
                if (error != null)
                {
                    _outputText.Value = $"Error:\n{error}";
                    _resultStatus.Value = $"Error: {error}";
                }
                else
                {
                    _outputText.Value = $"{affected} row(s) affected";
                    _resultStatus.Value = $"{affected} row(s) affected";
                }
            }
        }
        catch (Exception ex)
        {
            _outputText.Value = $"Exception:\n{ex.Message}";
            _resultStatus.Value = $"Error: {ex.Message}";
        }
        finally
        {
            _isExecuting.Value = false;
        }
    }

    private void UpdateResultsGrid()
    {
        if (_resultsGrid == null || _columns.Count == 0) return;

        // 创建文本行数据作为简单展示
        var displayRows = _rows.Select(r =>
            string.Join(" | ", r.Values.Select(v => v?.ToString() ?? "NULL"))
        ).ToArray();

        _resultsGrid.SetItemsSource(displayRows);

        // 添加单列
        _resultsGrid.AddColumns(
            new GridViewColumn<string>
            {
                Header = "Results",
                Width = 800,
            }
        );
    }

    private void OnExplainPlan()
    {
        var sql = _sqlText.Value.Trim();
        if (string.IsNullOrWhiteSpace(sql)) return;

        _sqlText.Value = $"EXPLAIN PLAN FOR\n{sql}";
        _ = ExecuteAsync();
    }

    public void SetSqlText(string sql)
    {
        _sqlText.Value = sql;
    }
}
