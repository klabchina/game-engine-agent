# Unreal

## 环境

- UE 5.8，开发 / 测试工程 `unreal/SeedanceExample/`（纯蓝图，启用 ModelContextProtocol、AllToolsets 插件）。
- `unreal/` 下的二进制资源走 Git LFS（见根目录 `.gitattributes`）。
- 编辑器打开工程时自动在 `http://127.0.0.1:8001/mcp` 启动 MCP 服务器（`Config/DefaultEditorPerProjectUserSettings.ini` 的 `bAutoStartServer` / `ServerPortNumber`）；地址也写在工程的 `.mcp.json`。没起来就在编辑器控制台敲 `ModelContextProtocol.StartServer 8001`。
- 本机 Windows：`python3` 是应用商店占位程序（退出码 49），用 `python`；脚本里两个都试。
- Git Bash 会把 `/Game/...` 这样的参数改写成 `C:/Program Files/Git/Game/...`，给命令传内容路径时加 `MSYS_NO_PATHCONV=1`。

## 规则

- 每次调用前确认 8001 端口属于目标工程的编辑器（`netstat -ano` 的 PID 对应 `UnrealEditor.exe` 窗口标题），本机可能同时开着别的工程。
- 出错（连不上、超时、PIE 中）如实报告，不要循环重试、换别的编辑器，也不要手改 `.uasset`。
- 工程里不留 Seedance 代码：导入逻辑放在 skill 目录，经 MCP 沙箱执行。

## MCP 工具速查（2026-11-10 实测）

### HTTP 调用

1. `POST /mcp`，头 `Content-Type: application/json`、`Accept: application/json, text/event-stream`，体 `initialize`（`protocolVersion: "2025-06-18"`）。响应头 `Mcp-Session-Id`。
2. 带 `Mcp-Session-Id` 发 `notifications/initialized`，返回 202、空体。
3. `tools/call`，`name: "call_tool"`，`arguments: {toolset_name, tool_name, arguments}`。响应是普通 JSON（不是 SSE）：`result.content[0].text`。
   - 成功：`text` 是 `{"returnValue": ...}` 的 JSON 串。
   - 失败：`result.isError = true`，`text` 是错误文本。

### 沙箱脚本（`editor_toolset.toolsets.programmatic.ProgrammaticToolset.execute_tool_script`）

- 参数 `{"script": "<源码>"}`，源码须定义 `run() -> dict`；返回值在 `returnValue` 里是 **JSON 字符串**，要再解析一层。
- 只能 import `json`、`re`、`math`、`datetime`、`copy`、`time`。
- 调工具：`execute_tool("<完整工具名>", json.dumps(参数))`，返回 dict，取 `["returnValue"]`。
- **工具报错无法 try/except 捕获，整个脚本中止**；`run()` 里自己 `raise` 的异常会以 `"line N: <类型>: <消息>\nTraceback..."` 返回。所以检查一律用不会报错的查询（`exists`、`can_edit_asset`、`IsPIERunning`）。
- 性能：UMG 工具与 `ObjectTools` 的每次调用约 0.25～0.3 秒（读写一样），`AssetTools.exists` 几乎为 0。130 个控件、约 390 次调用约 100 秒；编译 + 保存约 1.5 秒。

### 工具与参数写法

| 用途 | 工具 | 参数 / 返回 |
|---|---|---|
| PIE 中？ | `EditorToolset.EditorAppToolset.IsPIERunning` | `{}` → bool |
| 资产在不在 | `editor_toolset.toolsets.asset.AssetTools.exists` | `{"path": "/Game/X/Y"}` → bool |
| 能否编辑 | `...AssetTools.can_edit_asset` | `{"asset_path"}` → bool |
| 引用者 | `...AssetTools.get_referencers` | `{"asset_path"}` → `["/Game/..."]`（包路径） |
| 删除 | `...AssetTools.delete` | `{"path"}` → bool；**强删，内存中的引用被清空** |
| 列资产 | `...AssetTools.find_assets` | `{"folder_path", "name": ""}` → 路径列表 |
| 保存 | `...AssetTools.save_assets` | `{"asset_paths": ["/Game/X/WBP_X"]}` → bool |
| 读写文本 | `...AssetTools.read_file` / `write_file` | `file_path` 用 `/Game/...`（只限 Content 与 Saved） |
| 导入贴图 | `editor_toolset.toolsets.texture.TextureTools.import_file` | `{"folder_path", "asset_name", "source_file": 绝对路径}`；**同名已存在直接报错** |
| 贴图尺寸 | `...TextureTools.get_size` | `{"texture": {"refPath": "/Game/X/T.T"}}` → `{x, y}` |
| 建 WBP | `UMGToolSet.UMGToolSet.CreateWidgetBlueprint` | `{"folderPath", "assetName", "parentClass": {"refPath": "/Script/UMG.UserWidget"}}` → `{"refPath"}` |
| 加控件 | `UMGToolSet.UMGToolSet.AddWidget` | `{"widgetBlueprint", "widgetClass": {"refPath": "/Script/UMG.Image"}, "widgetDisplayName", "parentWidget": {...}|省略=根, "childIndex": -1}` → `{widget, slot, ...}` |
| 挪控件 | `UMGToolSet.UMGToolSet.MoveWidget` | `{"widgetBlueprint", "widget", "newParent", "childIndex"}` |
| 读树 | `UMGToolSet.UMGToolSet.GetWidgets` | → `{"info": {widgetCount...}, "widgets": [{widget, parent, slot, widgetName, widgetClassPath}]}`，深度优先、同级按槽位顺序；根的 `parent` / `slot` 是字符串 `"None"` |
| 编译 | `UMGToolSet.UMGToolSet.CompileWidgetBlueprint` | → bool |
| 读 / 写属性 | `editor_toolset.toolsets.object.ObjectTools.get_properties` / `set_properties` | `get`: `{"instance", "properties": [...]}` → JSON 串；`set`: `{"instance", "values": "<JSON 串>"}` → bool。**嵌套结构按字段合并**，没写的字段保持原值 |

控件引用路径：`/Game/X/WBP_X.WBP_X:WidgetTree.<控件名>`；槽：`AddWidget` / `GetWidgets` 返回的 `slot.refPath`。

### 属性写法（`set_properties` 的 `values`，属性名小驼峰）

- CanvasPanelSlot：`{"layoutData": {"anchors": {"minimum": {"x":0,"y":0}, "maximum": {"x":0,"y":0}}, "offsets": {"left":L,"top":T,"right":W,"bottom":H}, "alignment": {"x":0,"y":0}}, "bAutoSize": false}`（锚点不拉伸时 right / bottom 就是宽高；四边拉伸时 anchors 0～1、offsets 全 0）
- Widget 可见性：`{"visibility": "SelfHitTestInvisible" | "HitTestInvisible" | "Collapsed" | "Visible"}`
- ScaleBox：`{"stretch": "ScaleToFit"}`；SizeBox：`{"widthOverride": 2560, "heightOverride": 1440, "bOverride_WidthOverride": true, "bOverride_HeightOverride": true}`
- Image：`{"brush": {"resourceObject": {"refPath": "/Game/X/T.T"}, "imageSize": {"x":w,"y":h}}}`
- 笔刷着色：`"tintColor": {"specifiedColor": {"r":k,"g":k,"b":k,"a":1}, "colorUseRule": "UseColor_Specified"}`
- Button：`{"widgetStyle": {"normal": 笔刷, "hovered": 笔刷, "pressed": 笔刷}}`
- CheckBox：`{"widgetStyle": {"checkBoxType": "ToggleButton", "uncheckedImage", "uncheckedHoveredImage", "uncheckedPressedImage", "checkedImage", "checkedHoveredImage", "checkedPressedImage"}}`
- ProgressBar：`{"widgetStyle": {"fillImage": 笔刷, "backgroundImage": {"drawAs": "NoDrawType"}}, "percent": 1.0, "barFillType": "LeftToRight"}`
- TextBlock：`{"text": "...", "justification": "Left|Center|Right", "font": {"size": S, "typefaceFontName": "Bold", "fontObject": {"refPath": ...}}, "colorAndOpacity": {"specifiedColor": {r,g,b,a}, "colorUseRule": "UseColor_Specified"}}`；默认字体 `/Engine/EngineFonts/Roboto.Roboto`
- 字体字形名：`get_properties(字体, ["compositeFont"])` → `compositeFont.defaultTypeface.fonts[].name`（Roboto：Regular、Bold、Italic、Bold Italic、Light）

### 截图

- `CaptureAssetImage` 不支持 Widget Blueprint。
- 做法：`EditorAppToolset.OpenEditorForAsset {"assetPath"}` → `SlateInspectorToolset.Windows {"action": "list"}` 找标题为 WBP 名的窗口 → `Windows {"action": "select", "index"}` → `SlateInspectorToolset.Screenshot {"ref": ""}`（返回 `returnValue: {mimeType, data(base64)}`，是整个 WBP 编辑器窗口）→ 用完 `Windows {"action": "close", "index"}`。
- 这一步要直接调工具（不经沙箱），因为沙箱写不了二进制文件。
