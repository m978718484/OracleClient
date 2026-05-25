# Oracle Developer — 设计方案

## 🎯 项目定位

专为 Oracle 数据库设计的桌面客户端管理器，参考 PL/SQL Developer 的经典交互范式，基于 **MewUI (Aprillz.MewUI 0.15.1)** 代码优先 GUI 框架 + **NativeAOT** 构建。

---

## 🏗️ 架构设计

```
┌─────────────────────────────────────────────────────────┐
│                    Oracle Developer                      │
│                   (MewUI GUI Shell)                      │
├──────────┬──────────┬───────────┬────────────────────────┤
│  Main    │Connection│  Object   │    SQL Editor          │
│  Window  │  Dialog  │  Browser  │    Panel               │
│  (IDE)   │          │           │                        │
├──────────┴──────────┴───────────┴────────────────────────┤
│                    OracleService                          │
│              (ADO.NET 原生，AOT安全)                       │
├──────────────────────────────────────────────────────────┤
│           Oracle.ManagedDataAccess.Core 23.7              │
│                 (Oracle 官方托管驱动)                       │
└──────────────────────────────────────────────────────────┘
```

### 分层说明

| 层 | 职责 | 技术 |
|---|---|---|
| **UI 层** | 窗口/控件/交互/布局 | MewUI Controls + Panels |
| **ViewModel 层** | 状态管理/数据绑定 | `ObservableValue<T>` |
| **Service 层** | 数据库操作/业务逻辑 | ADO.NET (AOT安全) |
| **Driver 层** | Oracle 网络协议 | Oracle.ManagedDataAccess.Core |

### AOT 兼容性策略

```
✅ AOT 安全                        ❌ AOT 不安全（本项目避免使用）
─────────────────────              ─────────────────────
new OracleConnection(connStr)      Type.GetType("OracleConnection")
cmd.ExecuteReaderAsync()           Activator.CreateInstance()
reader.GetString(0)                System.Reflection.Emit
ObservableValue<T>                 Dapper.QueryAsync<T>()（反射版）
手动拼写 SQL                       动态 LINQ 表达式
```

---

## 🖥️ UI 设计

### 主窗口布局（PL/SQL Developer 经典风格）

```
┌──────────────────────────────────────────────────────────────┐
│ File  Edit  View  Session  Tools  Help                        │ ← MenuBar
├──────────────────────────────────────────────────────────────┤
│ [Connect][Disconnect]│[▶ Execute][New SQL]│[Explain][Refresh] │ ← ToolBar
│                      │                │[Commit][Rollback]     │
│                      │                │[Export][Settings]     │
├────────────┬─────────────────────────────────────────────────┤
│            │                                                   │
│  Object    │  SQL Editor (Consolas 14pt, No Wrap)             │ ← SplitPanel
│  Browser   │  ┌─────────────────────────────────────────────┐│   Horizontal
│            │  │ SELECT e.ename, d.dname                      ││
│ ─ Tables ▼ │  │   FROM emp e                                 ││
│   DEPT     │  │   JOIN dept d ON e.deptno = d.deptno         ││
│   EMP      │  │   WHERE e.sal > :min_sal                     ││
│   SALGRADE │  │                                               ││
│            │  ├─────────────────────────────────────────────┤│ ← SplitPanel
│ ─ Views ▼  │  │ Results │ Output │ Statistics               ││   Vertical
│            │  │ ┌──────┬─────────┬────────┐                 ││
│ ─ Procs ▼  │  │ │ ENAME│ DNAME   │ SAL    │                 ││
│            │  │ ├──────┼─────────┼────────┤                 ││
│ ─ Funcs ▼  │  │ │ SCOTT│ RESEARCH│ 3000   │                 ││
│            │  │ │ FORD │ RESEARCH│ 3000   │                 ││
│ ─ Pkgs ▼   │  │ │ KING │ PRESIDENT│5000   │                 ││
│            │  │ └──────┴─────────┴────────┘                 ││
│ ─ Trigs ▼  │  │ 3 rows retrieved in 0.047s                  ││
│            │  └─────────────────────────────────────────────┘│
│ ─ Seqs ▼   │                                                  │
│ ─ Idx ▼    │                                                  │
│ ─ Syns ▼   │                                                  │
├────────────┴─────────────────────────────────────────────────┤
│ ● Connected │ ORCL @ db-server (19c Enterprise) │ 3 rows │    │ ← StatusBar
└──────────────────────────────────────────────────────────────┘
```

### 区域尺寸规范

| 区域 | 宽/高 | 最小值 | 比例 | 可拖拽 |
|------|-------|--------|------|--------|
| 对象浏览器 | 240px | 160px | 固定像素起始 | ✅ 水平拖拽 |
| SQL编辑器 | 60% | 80px | 3* | ✅ 垂直拖拽 |
| 结果面板 | 40% | 60px | 2* | ✅ 垂直拖拽 |
| 菜单栏 | 28px | — | Auto | ❌ |
| 工具栏 | 36px | — | Auto | ❌ |
| 状态栏 | 28px | — | Auto | ❌ |

---

## 📋 功能模块设计

### 1. 连接管理

```
┌─────────────────────────────────┐
│  Oracle Developer - Connect     │
│  Connect to Oracle Database     │
├─────────────────────────────────┤
│  ┌─ Connection ──────────────┐  │
│  │ Name:     [Oracle Dev    ]│  │
│  │ Host:     [localhost     ]│  │
│  │ Port:     [1521          ]│  │
│  │ Service:  [ORCL   ]☑Use  │  │
│  └───────────────────────────┘  │
│  ┌─ Credentials ─────────────┐  │
│  │ Username: [system        ]│  │
│  │ Password: [••••••••      ]│  │
│  └───────────────────────────┘  │
│  Role: ◉Normal ○SYSDBA ○SYSOPER│
│                                  │
│  ℹ Connection successful!       │
│                                  │
│           [Test] [Connect]       │
└─────────────────────────────────┘
```

**连接串格式**：
```
Data Source=host:port/service_name;User Id=username;Password=password;
DBA Privilege=SYSDBA;  // 可选
```

**支持角色**：Normal / SYSDBA / SYSOPER

### 2. 对象浏览器

9 类数据库对象，Expander 分组展开：

| 分类 | 查询来源 | 子项 |
|------|----------|------|
| **Tables** | `user_tables` | 列信息、约束、索引 |
| **Views** | `user_views` | 列信息、源代码 |
| **Procedures** | `user_objects` WHERE type='PROCEDURE' | 源代码 |
| **Functions** | `user_objects` WHERE type='FUNCTION' | 源代码 |
| **Packages** | `user_objects` WHERE type='PACKAGE' | Spec+Body |
| **Triggers** | `user_objects` WHERE type='TRIGGER' | 源代码 |
| **Sequences** | `user_sequences` | 当前值/增量 |
| **Indexes** | `user_indexes` | 列信息 |
| **Synonyms** | `user_synonyms` | 目标对象 |

### 3. SQL 编辑器

```
┌─ Editor ToolBar ───────────────────────────────────────┐
│ [▶ Execute] [■ Stop] [📊 Explain] [🗑 Clear] [💾 Save] [📂 Open] │
└─────────────────────────────────────────────────────────┘
┌─ SQL Input (Consolas, 14pt, No Wrap) ──────────────────┐
│ SELECT e.ename, d.dname, e.sal                         │
│   FROM emp e                                           │
│   JOIN dept d ON e.deptno = d.deptno                   │
│  WHERE e.sal > &min_salary                             │
│  ORDER BY e.sal DESC;                                  │
└─────────────────────────────────────────────────────────┘
```

**编辑器特性**：
- 等宽字体 (Consolas)
- 不自动换行（水平滚动）
- 占位符提示
- 双向绑定 `ObservableValue<string>`

### 4. 结果面板（三标签页）

| 标签 | 内容 | 控件 |
|------|------|------|
| **Results** | 查询结果网格 | GridView (虚拟化, 斑马纹, 网格线) |
| **Output** | 执行日志/错误信息 | MultiLineTextBox (只读) |
| **Statistics** | 执行统计 | TextBlock |

**结果网格特性**：
- 虚拟化渲染（支持 10,000+ 行）
- 斑马纹（`ZebraStriping`）
- 网格线（`ShowGridLines`）
- 动态列生成（根据查询结果）
- 底部状态行（行数/列数）

### 5. 菜单系统

```
File                    Edit                 View
├─ New SQL Window  Ctrl+N  ├─ Undo      Ctrl+Z  ├─ Refresh All
├─ Open File...    Ctrl+O  ├─ Redo      Ctrl+Y  ├─ Font Size +
├─ Save            Ctrl+S  ├─ ────────────────  └─ Font Size -
├─ ────────────────────    ├─ Cut       Ctrl+X
├─ Connect...              ├─ Copy      Ctrl+C  Session
├─ Disconnect              ├─ Paste     Ctrl+V  ├─ Connect...
├─ ────────────────────    ├─ ────────────────  ├─ Disconnect
└─ Exit                    ├─ Select All Ctrl+A └─ Session Browser
                           └─ Find/Replace Ctrl+H

Tools                   Help
├─ Export Data...       └─ About Oracle Developer
├─ Import Data...
├─ ──────────────
├─ Table Editor
├─ Session Monitor
└─ Preferences...
```

### 6. 状态栏

```
┌──────────────────────────────────────────────────────────┐
│ ● Connected │ ORCL @ db01 (19c Enterprise) │ 3 rows │     │
└──────────────────────────────────────────────────────────┘
   ↑ 颜色指示    ↑ 实例名@主机(版本)          ↑ 行数统计
```

---

## 📦 数据模型设计

```csharp
// 连接信息
ConnectionInfo {
    Name, Host, Port, ServiceName, Sid,
    UseServiceName, Username, Password, Role
    → BuildConnectionString() → "Data Source=host:port/svc;User Id=...;Password=...;"
}

// 对象浏览器节点类型
ObjectNodeType { 
    Root, TableFolder, ViewFolder, ..., 
    Table, View, Procedure, Function, Package, ...
}

// 对象节点
ObjectNode { Name, Schema, NodeType, Children, HasChildren }

// 查询结果
ResultColumn { Name, DataType, Width }
ResultRow { Values (object?[]) }

// 表结构
TableColumnInfo { 
    ColumnName, DataType, DataLength, 
    DataPrecision, DataScale, Nullable, 
    DefaultValue, Comments 
}

// 数据库概览
DatabaseOverview { 
    Version, InstanceName, HostName,
    TableCount, ViewCount, ProcedureCount,
    ActiveSessions, TotalSize 
}
```

---

## 🔧 核心服务设计（OracleService）

### AOT 安全查询模式

```csharp
// ✅ 模式1：ExecuteReader（最灵活，动态列）
using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync()) {
    var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
}

// ✅ 模式2：ExecuteScalar（单值查询）
var result = await cmd.ExecuteScalarAsync();

// ✅ 模式3：ExecuteNonQuery（DML/DDL）
var affected = await cmd.ExecuteNonQueryAsync();

// ❌ 避免：Dapper反射版
// var rows = conn.QueryAsync<T>(sql, param);  // AOT不安全
```

### API 列表

| 方法 | 用途 | 返回 |
|------|------|------|
| `TestConnectionAsync(info)` | 测试连接 | `bool` |
| `ConnectAsync(info)` | 建立连接 | `bool` |
| `Disconnect()` | 断开连接 | `void` |
| `ExecuteQueryAsync(sql, maxRows)` | 执行查询 | `(Columns, Rows, Error)` |
| `ExecuteNonQueryAsync(sql)` | 执行DML/DDL | `(AffectedRows, Error)` |
| `GetSchemaObjectsAsync()` | 获取9类对象 | `List<ObjectNode>` |
| `GetTableColumnsAsync(table)` | 获取表列信息 | `List<TableColumnInfo>` |
| `GetDatabaseOverviewAsync()` | 获取概览 | `DatabaseOverview` |
| `GetObjectDdlAsync(type, name)` | 获取DDL | `string` |
| `GetSourceCodeAsync(type, name)` | 获取源代码 | `string` |
| `GetTableDataAsync(table, maxRows)` | 浏览表数据 | `(Columns, Rows, Error)` |

---

## 🎨 MewUI 控件映射

| PL/SQL Developer 组件 | MewUI 实现 |
|---|---|
| 主窗口 | `Window().Resizable(1280,800)` |
| 菜单栏 | `MenuBar` + `Menu` + `MenuItem` |
| 工具栏 | `Border` + `StackPanel.Horizontal` + `Button` |
| 对象浏览器 | `Expander` × 9 + `TextBlock` / `ListBox` |
| SQL编辑器 | `MultiLineTextBox().FontFamily("Consolas")` |
| 结果网格 | `GridView().ZebraStriping().ShowGridLines()` |
| 输出面板 | `MultiLineTextBox().IsReadOnly(true)` |
| 标签页 | `TabControl` + `TabItem` |
| 分割线 | `SplitPanel.Horizontal/Vertical` |
| 状态栏 | `Border` + `DockPanel` + `TextBlock` |
| 连接对话框 | `Window().Fixed(480,520)` |
| 数据绑定 | `ObservableValue<T>` + `BindText/BindIsEnabled/BindIsChecked` |

---

## 🔑 MewUI API 注意事项

本项目开发过程中踩过的坑，记录如下：

| 问题 | 解决方案 |
|------|----------|
| `OracleConnectionStringBuilder` 无 Host/Port 属性 | 改用 `DataSource = "host:port/service"` EZConnect 格式 |
| `Window.StartupLocation` 是属性非方法 | `window.StartupLocation = ...` 不可链式调用 |
| `MenuBar.DrawBottomSeparator` 是属性 | `menuBar.DrawBottomSeparator = true` 不可链式调用 |
| `PasswordBox` 无 `BindText` | 改用 `BindPassword(ObservableValue<string>)` |
| `RadioButton` 无 `Content(string)` 扩展方法 | 直接设 `Content = new Label().Text(...)` |
| `SplitPanel.First/Second` 期望 `UIElement?` | 返回类型需为 `UIElement` 而非 `Element` |
| `BindIsEnabled` 无 `mode` 参数 | 仅接受 `ObservableValue<bool>` |
| `BorderThickness` 仅接受单个 `double` | 统一用 `BorderThickness(1)`，四边等宽 |
| `Palette` 无 Success/Error/CardBackground | 用 `ContainerBackground`/`ButtonFace`/`ControlBorder` 替代 |
| `Dapper.AOT` 的 `QueryAsync` 需源生成器 | 改用原生 ADO.NET `ExecuteReaderAsync`，更可靠 |

---

## 🚀 后续扩展方向

| 优先级 | 功能 | 说明 |
|--------|------|------|
| 🔴 高 | TreeView 替换对象浏览器 | 替代 Expander+TextBlock，支持节点展开/折叠 |
| 🔴 高 | GridView 动态列绑定 | 根据查询结果动态生成 `GridViewColumn<T>` |
| 🟡 中 | SQL 历史记录 | 记录执行过的 SQL，支持回查 |
| 🟡 中 | 表数据编辑器 | 可编辑的 GridView，生成 UPDATE/INSERT 语句 |
| 🟡 中 | 导出功能 | CSV/JSON/SQL INSERT 导出 |
| 🟢 低 | SQL 语法高亮 | MultiLineTextBox 关键字着色 |
| 🟢 低 | 自动补全 | 表名/列名提示弹窗 |
| 🟢 低 | 多连接管理 | 标签页切换不同数据库连接 |
| 🟢 低 | ER 图可视化 | 表关系图 |
| 🟢 低 | AOT 发布优化 | TrimmerRootDescriptor.xml + 发布测试 |

---

## 📂 源码结构

```
OracleClient/
├── OracleClient.csproj        # 项目配置 (net9.0 + PublishAot)
├── Program.cs                 # 跨平台平台注册 + 应用入口
├── Models/
│   └── OracleModels.cs        # 数据模型
├── Services/
│   └── OracleService.cs       # ADO.NET 原生查询服务
└── UI/
    ├── MainWindow.cs          # 主窗口 IDE 布局
    ├── ConnectionDialog.cs    # 连接对话框
    ├── ObjectBrowser.cs       # 对象浏览器
    └── SqlEditorPanel.cs      # SQL编辑器+结果面板
```
