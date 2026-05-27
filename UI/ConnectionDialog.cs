using System.Text.Json;
using System.Text.Json.Serialization;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace OracleClient.UI;

/// <summary>
/// 连接对话框 - PL/SQL Developer风格的Oracle登录界面
/// </summary>
public sealed partial class ConnectionDialog
{
    private const string ConfigFile = "connections.json";

    private readonly Services.OracleService _service;
    private Window? _window;
    private readonly ObservableValue<string> _name = new("Oracle Dev");
    private readonly ObservableValue<string> _host = new("localhost");
    private readonly ObservableValue<string> _port = new("1521");
    private readonly ObservableValue<string> _serviceName = new("ORCL");
    private readonly ObservableValue<string> _username = new("system");
    private readonly ObservableValue<string> _password = new("");
    private readonly ObservableValue<string> _role = new("Normal");
    private readonly ObservableValue<bool> _useServiceName = new(true);
    private readonly ObservableValue<bool> _isConnecting = new(false);
    private readonly ObservableValue<bool> _canClick = new(true);
    private readonly ObservableValue<string> _statusText = new("");
    private readonly ObservableValue<bool> _hasStatus = new(false);

    public ConnectionDialog(Services.OracleService service)
    {
        _service = service;
        LoadLastConnection();
    }

    public Window Build()
    {
        _window = new Window()
            .Fixed(480, 520)
            .Title("Oracle Developer - Connect")
            .OnBuild(w => w.Content(CreateContent()));
        _window.StartupLocation = WindowStartupLocation.CenterScreen;
        return _window;
    }

    private Element CreateContent()
    {
        return new Border()
            .Padding(24)
            .Child(
                new StackPanel()
                    .Vertical()
                    .Spacing(16)
                    .Children(
                        CreateTitle(),
                        CreateConnectionFields(),
                        CreateCredentialFields(),
                        CreateRoleSection(),
                        CreateStatus(),
                        CreateButtons()
                    )
            );
    }

    private Element CreateTitle()
    {
        return new StackPanel()
            .Vertical()
            .Spacing(4)
            .Children(
                new TextBlock()
                    .Text("Oracle Developer")
                    .FontSize(22)
                    .Bold(),
                new TextBlock()
                    .Text("Connect to Oracle Database")
                    .FontSize(12)
                    .WithTheme((t, e) => e.Foreground(t.Palette.DisabledText))
            );
    }

    private Element CreateConnectionFields()
    {
        return new GroupBox()
            .Header("Connection")
            .Content(
                new Grid()
                    .Columns("80,*")
                    .Rows("Auto,Auto,Auto,Auto")
                    .Spacing(8)
                    .AutoIndexing()
                    .Children(
                        new Label().Text("Name:"),
                        new TextBox().BindText(_name),

                        new Label().Text("Host:"),
                        new TextBox().BindText(_host),

                        new Label().Text("Port:"),
                        new TextBox().BindText(_port).Width(100),

                        new Label().Text("Service:"),
                        new StackPanel()
                            .Horizontal()
                            .Spacing(8)
                            .Children(
                                new TextBox()
                                    .BindText(_serviceName)
                                    .Width(180),
                                new CheckBox()
                                    .BindIsChecked(_useServiceName),
                                new Label().Text("Use Service Name")
                            )
                    )
            );
    }

    private Element CreateCredentialFields()
    {
        return new GroupBox()
            .Header("Credentials")
            .Content(
                new Grid()
                    .Columns("80,*")
                    .Rows("Auto,Auto")
                    .Spacing(8)
                    .AutoIndexing()
                    .Children(
                        new Label().Text("Username:"),
                        new TextBox().BindText(_username),

                        new Label().Text("Password:"),
                        new PasswordBox().BindPassword(_password)
                    )
            );
    }

    private Element CreateRoleSection()
    {
        var rbNormal = new RadioButton() { Content = new Label().Text("Normal") };
        rbNormal.BindIsChecked(_role, v => v == "Normal");

        var rbSysdba = new RadioButton() { Content = new Label().Text("SYSDBA") };
        rbSysdba.BindIsChecked(_role, v => v == "SYSDBA");

        var rbSysoper = new RadioButton() { Content = new Label().Text("SYSOPER") };
        rbSysoper.BindIsChecked(_role, v => v == "SYSOPER");

        return new StackPanel()
            .Horizontal()
            .Spacing(12)
            .Children(
                new Label().Text("Role:").CenterVertical(),
                rbNormal,
                rbSysdba,
                rbSysoper
            );
    }

    private Element CreateStatus()
    {
        return new Border()
            .Padding(8, 4)
            .CornerRadius(4)
            .BindIsVisible(_hasStatus)
            .WithTheme((t, b) => b.Background(t.Palette.ContainerBackground))
            .Child(
                new TextBlock()
                    .BindText(_statusText)
                    .FontSize(12)
            );
    }

    private Element CreateButtons()
    {
        return new DockPanel()
            .Children(
                new Button()
                    .Content("Help")
                    .DockLeft(),
                new StackPanel()
                    .Horizontal()
                    .Spacing(8)
                    .DockRight()
                    .Children(
                        new Button()
                            .Content("Test")
                            .OnClick(OnTestConnection)
                            .BindIsEnabled(_canClick),
                        new Button()
                            .Content("Connect")
                            .OnClick(OnConnect)
                            .BindIsEnabled(_canClick)
                    )
            );
    }

    // Note: BindIsEnabled binds to a bool source. _canClick is the inverse of _isConnecting.
    // When connecting, _canClick=false disables buttons; when idle, _canClick=true enables them.

    private void OnTestConnection()
    {
        _ = TestConnectionAsync();
    }

    private void OnConnect()
    {
        _ = ConnectAsync();
    }

    private async Task TestConnectionAsync()
    {
        _isConnecting.Value = true;
        _canClick.Value = false;
        _hasStatus.Value = true;
        _statusText.Value = "Testing connection...";

        var info = BuildConnectionInfo();
        var success = await _service.TestConnectionAsync(info);

        _statusText.Value = success ? "Connection successful!" : "Connection failed!";
        _isConnecting.Value = false;
        _canClick.Value = true;
    }

    private async Task ConnectAsync()
    {
        _isConnecting.Value = true;
        _canClick.Value = false;
        _hasStatus.Value = true;
        _statusText.Value = "Connecting...";

        var info = BuildConnectionInfo();
        var success = await _service.ConnectAsync(info);

        if (success)
        {
            SaveConnection(info);
            _statusText.Value = "Connected!";
            _window?.Close();
        }
        else
        {
            _statusText.Value = "Connection failed!";
        }

        _isConnecting.Value = false;
        _canClick.Value = true;
    }

    private Models.ConnectionInfo BuildConnectionInfo()
    {
        return new Models.ConnectionInfo
        {
            Name = _name.Value,
            Host = _host.Value,
            Port = int.TryParse(_port.Value, out var p) ? p : 1521,
            ServiceName = _serviceName.Value,
            UseServiceName = _useServiceName.Value,
            Username = _username.Value,
            Password = _password.Value,
            Role = _role.Value
        };
    }

    private void LoadLastConnection()
    {
        try
        {
            if (!File.Exists(ConfigFile)) return;
            var json = File.ReadAllText(ConfigFile);
            var saved = JsonSerializer.Deserialize(json, SavedConnectionContext.Default.SavedConnection);
            if (saved == null) return;

            _name.Value = saved.Name ?? "Oracle Dev";
            _host.Value = saved.Host ?? "localhost";
            _port.Value = saved.Port > 0 ? saved.Port.ToString() : "1521";
            _serviceName.Value = saved.ServiceName ?? "ORCL";
            _useServiceName.Value = saved.UseServiceName;
            _username.Value = saved.Username ?? "";
            _password.Value = saved.Password ?? "";
            _role.Value = saved.Role ?? "Normal";
        }
        catch
        {
            // 文件损坏则忽略，使用默认值
        }
    }

    private void SaveConnection(Models.ConnectionInfo info)
    {
        try
        {
            var saved = new SavedConnection
            {
                Name = info.Name,
                Host = info.Host,
                Port = info.Port,
                ServiceName = info.ServiceName,
                UseServiceName = info.UseServiceName,
                Username = info.Username,
                Password = info.Password,
                Role = info.Role
            };
            var json = JsonSerializer.Serialize(saved, SavedConnectionContext.Default.SavedConnection);
            File.WriteAllText(ConfigFile, json);
        }
        catch
        {
            // 保存失败不影响连接
        }
    }

    private sealed class SavedConnection
    {
        public string? Name { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? ServiceName { get; set; }
        public bool UseServiceName { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
    }

    [JsonSerializable(typeof(SavedConnection))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    private sealed partial class SavedConnectionContext : JsonSerializerContext;
}
