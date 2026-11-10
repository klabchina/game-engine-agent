# seedance-ui-to-unreal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新 skill `seedance-ui-to-unreal`：把 ui-maker 产物经 unreal-mcp 导成 UMG Widget Blueprint，支持保留程序改动的重导，工程里不留代码。

**Architecture:** 导入逻辑是 `editor/seedance_ui/` 下的纯 Python 包（只用 `json` / `re` / `math`），分成「纯逻辑」（解析、几何、命名、记录、生成操作计划）与「工具层」（包装沙箱的 `execute_tool`）。本地启动器 `scripts/run_import.py` 解压产物、做本地检查、把包**拼接成一个脚本**并在开头嵌入 `REQUEST`，经 MCP HTTP 调 `ProgrammaticToolset.execute_tool_script` 执行，打印 ImportReport。离线测试用 `unittest` 直接 import 这个包，工具层换成内存里的假编辑器。

**Tech Stack:** UE 5.8（`unreal/SeedanceExample`，插件 ModelContextProtocol + AllToolsets），MCP Streamable HTTP（`http://127.0.0.1:8001/mcp`），Python 3（本地 ≥3.10，只用标准库；编辑器沙箱 3.11），`unittest`，bash。

**Spec:** `docs/superpowers/specs/2026-11-10-seedance-ui-to-unreal-design.md`

## Global Constraints

- 沙箱代码（`editor/seedance_ui/*.py`）只许 `import json, re, math`；包内互相引用一律写成单独一行 `from .<模块> import <名字>`（拼接时整行删除），模块间不许有同名顶层函数 / 常量。
- 启动器与测试只用 Python 标准库（本机没有 pytest，用 `python -m unittest`）。
- Widget Blueprint `<IMPORT_DIR>/WBP_<WBP_NAME>`，`WBP_NAME` 只能 `[A-Za-z0-9_-]`，不给取 `IMPORT_DIR` 最后一级；`IMPORT_DIR` 必须以 `/Game/` 开头。
- 贴图 `<IMPORT_DIR>/Textures/T_<type>_<id>`（`sameAs` 取主元素的 type 与 id）。
- 控件名：根 `Root` / `ScaleBox` / `SizeBox` / `Design`；容器 `<type>_<id>`；显示控件 `<type>_<id>_<Image|Button|CheckBox|ProgressBar|Text>`。
- 导入记录：`<工程>/Content/SeedanceKlab/UI/<WBP 路径去掉 /Game/>.json`，`formatVersion = 1`。
- 字号 `UMG = 像素 × 0.75`，缺字号用 27 并警告；颜色解析失败用白色并警告；Hovered 着色 1.1、Pressed 0.85。
- 报告字段：`widgetBlueprint`、`created`、`updated`、`rebuilt`、`deactivated`、`missing`、`moved`、`typeChanged`、`orphanTextures`、`warnings`；列表项 `"<id> <名字>"`。
- 所有给用户看的错误是中文；检查阶段失败时工程里什么都没写。
- 文档、注释、提交信息用中文，提交前缀 `feat:` / `fix:` / `docs:` / `refactor:`。

## Review Focus

- **元素名带空格 / 中文、产物里切图路径带中文**：`slices/格子_1.png` 这类路径从 zip 解出、交给 `TextureTools.import_file` 时必须原样可用 → Task 8 的 zip 测试用中文文件名条目。
- **嵌套深度与父在子后**：`layout.json` 若出现子元素排在父元素前面，首次导入仍要建对层级 → Task 4 测试 `test_child_before_parent_still_nested`。
- **重导时上游删掉了一个父元素、子元素改挂根**：子容器要跟到根下（程序没挪过），父容器 Collapsed → Task 5 测试 `test_parent_removed_child_follows_to_root`。
- **两个产物导进同一个导入目录 / 同名 WBP**：第二次是重导而不是另建；记录对应的是同一个 WBP → Task 5 已覆盖（重导入口按 WBP 路径）。
- **编辑器里 WBP 正被打开编辑**：MCP 改树后编译、保存仍要成功 → Task 9 端到端在 WBP 编辑器打开状态下跑一次重导。

---

## 文件结构

```
.claude/skills/seedance-ui-to-unreal/
  SKILL.md
  editor/seedance_ui/
    __init__.py        # 空
    errors.py          # UiImportError
    layout.py          # layout.json 解析与校验 → LayoutDoc
    naming.py          # 资产名、控件名
    geometry.py        # 坐标、字号、颜色换算
    record.py          # 导入记录的数据结构、序列化、路径
    planner.py         # 生成操作计划（首次 + 重导），产出 Plan 与 report
    tools.py           # 工具层：包装 execute_tool 的函数（唯一碰 MCP 的地方）
    apply.py           # 执行 Plan：检查 → 贴图 → 控件树 → 编译保存 → 写记录
    main.py            # run()：读 REQUEST，串起上面
  scripts/run_import.py  # 本地启动器
unreal/SeedanceExample/
  Tests/SeedanceUI/      # unittest：test_*.py、fake_editor.py
  TestFixtures/ui-popup.zip, ui-status.zip
  run-tests.sh           # 离线单测
  run-e2e.sh             # 端到端（编辑器开着）
docs/engine/unreal.md
```

`fake_editor.py` 实现与 `tools.py` 同名同签名的函数，测试里用 `unittest.mock.patch` 把 `seedance_ui.tools` 的函数换成它。

---

### Task 1：验证 V1～V5，定下工具层的确切调用

不写产品代码。结论写进 `docs/engine/unreal.md`（新建），后续 Task 7 照它实现。

**Files:**
- Create: `docs/engine/unreal.md`
- Create（一次性，不提交）：`$SCRATCHPAD/probe/*.py`

**Interfaces:**
- Produces：`docs/engine/unreal.md` 的「MCP 工具速查」一节，至少写明：每个要用的工具全名、参数 JSON 示例、返回结构；CanvasPanelSlot `LayoutData`、Image `Brush`、Button / CheckBox `WidgetStyle`、ProgressBar `WidgetStyle`、ScaleBox `Stretch`、SizeBox `WidthOverride/HeightOverride`、TextBlock `Font/ColorAndOpacity/Justification`、Widget `Visibility` 的 `set_properties` 值写法；MCP HTTP 握手步骤（`initialize` → 会话头 → `notifications/initialized` → `tools/call`）。

- [ ] **Step 1：V4 先做**：写一个 30 行的探针脚本，用 `urllib` 完成 MCP 握手并调 `call_tool`（`toolset_name = editor_toolset.toolsets.programmatic.ProgrammaticToolset`、`tool_name = execute_tool_script`，脚本 `def run(): return {"ok": 1}`）。期望打印 `{"ok": 1}`。记下会话头名、响应是 JSON 还是 SSE、超时表现。
- [ ] **Step 2：V1**：在 `/Game/Tests/Probe/` 用 `TextureTools.import_file` 导 `slices/面板.png` 为 `T_probe`，建一个 WBP 用 Image 引用它；再用另一张图对同一 `asset_name` 再导一次。期望：资产路径不变，WBP 里 Image 的 Brush 仍指向它且尺寸变成新图的。不符合就停下回报（设计要改）。
- [ ] **Step 3：V3**：在探针 WBP 里加 CanvasPanel → Button / CheckBox / ProgressBar / TextBlock / ScaleBox / SizeBox，逐个 `list_properties` 后 `set_properties` 写上表的属性，`CompileWidgetBlueprint` + `save_assets`，再 `GetWidgetDescription` 读回确认值保持。把可用的 JSON 写法逐条记下。
- [ ] **Step 4：V5**：TextBlock 写「角色属性」，默认字体，`CaptureAssetImage` 或打开设计器截图看是否方块。
- [ ] **Step 5：V2**：试 `OpenEditorForAsset` + `SlateInspectorToolset` 截设计器画布，不行再用 `CaptureEditorImage` 裁剪。记下最终做法与需要的缩放。
- [ ] **Step 6：在一次 `execute_tool_script` 里建 65 个容器 + 65 个显示控件并设属性，计时**。期望 < 2 分钟；超出就记下实际耗时并回报。
- [ ] **Step 7：删 `/Game/Tests/Probe/`，写 `docs/engine/unreal.md`**（含「MCP 工具速查」、环境、MCP 自动启动配置、规则：每次调用前确认端口属于目标工程、不重试、不手改 `.uasset`），提交

```bash
git add docs/engine/unreal.md
git commit -m "docs: Unreal 引擎文档与 MCP 工具速查（seedance-ui-to-unreal 前置验证）"
```

---

### Task 2：包骨架、测试接线、layout 解析

**Files:**
- Create: `.claude/skills/seedance-ui-to-unreal/editor/seedance_ui/{__init__,errors,layout}.py`
- Create: `unreal/SeedanceExample/run-tests.sh`、`unreal/SeedanceExample/Tests/SeedanceUI/test_layout.py`

**Interfaces:**
- Produces：
  - `class UiImportError(Exception)`（`errors.py`）
  - `layout.py`：`parse_layout(text: str) -> dict`，返回 `{"version", "width", "height", "elements": [Element...], "by_id": {id: Element}}`；`Element` 为 dict：`id, name, type, image|None, parent|None, same_as|None, rect: (x,y,w,h), missing: bool, text: dict|None`（`rect` 已取 `drawnRect` 当 missing；text 元素 `text = {content, font_size, color, align, bold}`）。
  - `run-tests.sh`：`cd` 到脚本目录，`PYTHONPATH=<skill>/editor python -m unittest discover -s Tests/SeedanceUI -v`，透传退出码；可选参数是测试模块名。

- [ ] **Step 1：写失败测试** `test_layout.py`，用 `TestFixtures` 里两个 zip 的 `layout.json`（`zipfile` 读）与手写的小 JSON：
  - `test_parses_ui_status`：65 个元素；`e14.same_as == "e11"`；`e5.parent == "e4"`；`e3.text["content"] == "角色属性"`；`width, height == 2560, 1440`
  - `test_missing_uses_drawn_rect`：`missing: true, rect: null` 的元素 `rect` 等于 `drawnRect`
  - `test_rejects_bad_input`：version ≠ 1、缺 resolution、重复 id、parent 不存在、sameAs 不存在、元素没有位置、非 JSON —— 各抛 `UiImportError`，消息为中文且含出错的 id
- [ ] **Step 2：把两个 zip 拷进 `unreal/SeedanceExample/TestFixtures/`，`.gitattributes` 追加 `unreal/**/*.zip filter=lfs diff=lfs merge=lfs -text`**；跑 `bash unreal/SeedanceExample/run-tests.sh`，期望 FAIL（模块不存在）
- [ ] **Step 3：实现 `parse_layout`**
- [ ] **Step 4：跑测试**，期望全部 PASS
- [ ] **Step 5：提交**

```bash
git add .gitattributes .claude/skills/seedance-ui-to-unreal unreal/SeedanceExample/run-tests.sh unreal/SeedanceExample/Tests unreal/SeedanceExample/TestFixtures
git commit -m "feat: seedance-ui-to-unreal 包骨架与 layout.json 解析"
```

---

### Task 3：命名与几何换算

**Files:**
- Create: `editor/seedance_ui/naming.py`、`editor/seedance_ui/geometry.py`
- Test: `Tests/SeedanceUI/test_naming.py`、`test_geometry.py`

**Interfaces:**
- Consumes：Task 2 的 Element dict
- Produces：
  - `naming.py`：`is_valid_wbp_name(s) -> bool`；`wbp_path(import_dir, wbp_name) -> str`（`import_dir` 不以 `/Game/` 开头或名字非法抛 `UiImportError`）；`texture_name(doc, el) -> str`（`T_<type>_<id>`）；`container_name(el)`；`view_name(el) -> str|None`（missing 为 None）；`VIEW_CLASS = {type: (后缀, UMG 类路径)}`；`record_file_path(wbp_path) -> str`（`/Game/SeedanceKlab/UI/...json`，供 `AssetTools.write_file`）
  - `geometry.py`：`slot_offsets(el, parent_el|None) -> (left, top, w, h)`；`font_size(px|None) -> (size, warning|None)`；`parse_color(s) -> ((r,g,b,a) 线性, warning|None)`；`text_anchor(align) -> (ax, ay)`（left 0 / center 0.5 / right 1，ay 恒 0.5）

- [ ] **Step 1：写失败测试**
  - `test_texture_name_uses_same_as_master`：ui-status 的 `e14` → `T_bar_e11`
  - `test_names`：`container_name(e62) == "button_e62"`、`view_name(e62) == "button_e62_Button"`、`view_name(e63) == "text_e63_Text"`、missing 元素 `view_name is None`
  - `test_wbp_path`：(`/Game/UI/X`, None) → `/Game/UI/X/WBP_X`；`/Game/UI/角色` 不给名字抛错；`/Content/X` 抛错
  - `test_record_file_path`：`/Game/UI/X/WBP_X` → `/Game/SeedanceKlab/UI/UI/X/WBP_X.json`
  - `test_slot_offsets`：ui-status `e2`（父 e1）→ `(2213-237, 134-127, 105, 106)`；根下元素 → 画布绝对坐标
  - `test_font_size`：`48 → (36.0, None)`；`None → (27.0, 警告)`
  - `test_parse_color`：`#ffffff → (1,1,1,1)`；`#80808080` 的 a ≈ 0.502、rgb 为 sRGB→线性（0.2158）；`"red"` → 白色 + 警告
- [ ] **Step 2：跑，期望 FAIL**
- [ ] **Step 3：实现**（sRGB→线性用标准分段公式：`c<=0.04045 ? c/12.92 : ((c+0.055)/1.055)^2.4`）
- [ ] **Step 4：跑，期望 PASS**
- [ ] **Step 5：提交** `feat: seedance-ui-to-unreal 命名与几何换算`

---

### Task 4：导入记录与首次导入计划

**Files:**
- Create: `editor/seedance_ui/record.py`、`editor/seedance_ui/planner.py`
- Test: `test_record.py`、`test_planner_create.py`

**Interfaces:**
- Consumes：Task 2、3
- Produces：
  - `record.py`：`new_record(wbp_path, doc) -> dict`（含 `textures: {贴图名: sha256}`，见 Task 1 后的贴图规则）；`dump_record(rec) -> str`（`json.dumps(indent=2, ensure_ascii=False, sort_keys=True)`，元素按 layout 顺序再接已不在产物里的）；`load_record(text, wbp_path) -> dict`（解析失败、`formatVersion` 不是 1、`widgetBlueprint` 不符都抛中文 `UiImportError`）。元素条目字段：`elementId, container, view, sourceName, type, layoutParentId`（根为 `"<root>"`）、`missingAtCreate: bool`。
  - `planner.py`：`plan(doc, record|None, tree|None, font|None) -> Plan`。`Plan = {"ops": [Op...], "report": {...}, "record": dict}`。`tree` 是 Task 7 `read_tree()` 的结果：`{name: {"class": str, "parent": str|None, "index": int}}`。`Op` 为 dict，`op` 字段取值：`ensure_root`、`add_container`（`name, parent, index, offsets`）、`add_view`（`name, container, cls, element`）、`set_offsets`（`name, offsets`）、`set_size`（`name, w, h`）、`set_texture`（`view, texture, w, h`）、`move`（`name, parent, index`）、`set_visibility`（`name, value`）。贴图导入不进 ops，由 `plan["textures"] = [{name, source_image, hash, elements:[id]}]` 给出；`plan(...)` 多一个参数 `hashes: {source_image: sha256}`（启动器算好放进 REQUEST）。哪些贴图要删重导 / 跳过由 Task 7 的 `apply` 结合引用者决定：记录里哈希相同 → 不动；不同或缺失 → 只被本 WBP 引用（或无人引用）则 `delete` + `import_texture` + `set_texture_settings` + 对用它的显示控件补 `set_texture`；有外部引用则跳过并警告、记录保留旧哈希。

- [ ] **Step 1：写失败测试**
  - `test_record_roundtrip`、`test_record_rejects_unknown_format_version`、`test_record_rejects_other_wbp`、`test_record_rejects_garbage`
  - `test_create_plan_ui_status`：`plan(doc, None, None, None)`：第一个 op 是 `ensure_root`；`add_container` 65 个、`add_view` 65 个（ui-status 没有 missing）；`button_e62` 的 parent 是 `panel_e1`，同级顺序与数组一致；`textures` 共 8 张（与 slices 数一致）；`report.created` 65 项；`record.elements` 65 项
  - `test_child_before_parent_still_nested`：手写 layout 把子元素排在父前，容器 parent 仍正确，且父的 `add_container` 在子之前
  - `test_missing_element_has_no_view`：missing 元素只有 `add_container`，`report.missing` 含它，记录 `missingAtCreate = true`
- [ ] **Step 2：跑，期望 FAIL**
- [ ] **Step 3：实现 `record.py` 与 `plan` 的首次导入分支**（`record is None`）
- [ ] **Step 4：跑，期望 PASS**
- [ ] **Step 5：提交** `feat: seedance-ui-to-unreal 导入记录与首次导入计划`

---

### Task 5：重导计划

**Files:**
- Modify: `editor/seedance_ui/planner.py`
- Test: `test_planner_update.py`

**Interfaces:**
- Consumes：Task 4 的 `plan` / Op / 记录格式
- Produces：`plan(doc, record, tree, font)` 的重导分支；`report` 填 `updated / rebuilt / deactivated / moved / typeChanged / missing`；新增 op 不超出 Task 4 定义的集合。

测试用 Task 4 首次计划生成的「树」作为起点（写一个 `tree_from_plan(plan)` 测试辅助），再改它模拟程序操作。

- [ ] **Step 1：写失败测试**（全用 ui-status）
  - `test_reimport_unchanged`：同一 layout 重导 → `updated` 65 项，无 `add_*`、无 `move`
  - `test_find_by_view_when_container_renamed`：把 `button_e62` 改名 `UpgradeRoot` → 仍认得，`plan.record` 里 container 写回 `UpgradeRoot`，无 `rebuilt`
  - `test_find_by_container_when_view_renamed`：`button_e62_Button` 改名 `UpgradeButton` → 认得，记录写回
  - `test_both_renamed_rebuilds`：两者都改 → `rebuilt` 含 `e62`，有对应 `add_container` / `add_view`
  - `test_moved_by_program`：把 `bar_e11` 挪到 `panel_e26` 下 → `moved` 含 e11，只有 `set_size`，无 `set_offsets`、无 `move`
  - `test_upstream_reparent_follows`：layout 里把 e11 的 parent 改成 e26（树未动）→ 有 `move` 到 `panel_e26`、记录 `layoutParentId = e26`
  - `test_parent_removed_child_follows_to_root`：layout 删掉 e4、e5 改挂根 → e4 `set_visibility Collapsed` 进 `deactivated`；e5 `move` 到 `Design`
  - `test_deactivated_restored`：先删 e62 再加回 → 恢复 `SelfHitTestInvisible`，`warnings` 有一条说明
  - `test_type_changed_reported_only`：layout 把 e8 type 改成 `frame` → `typeChanged` 含 e8，无 view 替换
  - `test_missing_filled_by_record_flag`：首次 missing、重导出图 → 有 `add_view`；首次非 missing、程序删了 view → **不**补（判断看 `missingAtCreate`）
  - `test_manual_widgets_untouched`：树里多一个不在记录的 `MyButton` → 没有任何 op 指向它
  - `test_texture_changes_size`：layout 改 e2 的 rect 尺寸 → `set_texture` 带新 w/h、`set_offsets` 新值
- [ ] **Step 2：跑，期望 FAIL**
- [ ] **Step 3：实现重导分支**（认节点顺序：容器名 → 显示控件名的父 → 重建；「程序没挪过」= 容器当前父等于记录里 `layoutParentId` 对应容器的当前名字）
- [ ] **Step 4：跑全部测试，期望 PASS**
- [ ] **Step 5：提交** `feat: seedance-ui-to-unreal 重导计划`

---

### Task 6：工具层与假编辑器

**Files:**
- Create: `editor/seedance_ui/tools.py`
- Create: `unreal/SeedanceExample/Tests/SeedanceUI/fake_editor.py`
- Test: `test_fake_editor.py`

**Interfaces:**
- Consumes：`docs/engine/unreal.md` 的「MCP 工具速查」（Task 1）
- Produces（`tools.py`，每个函数只做一次或少量 `execute_tool` 调用，返回 Python 值；失败抛 `UiImportError` 并带工具名）：
  - `is_pie_running() -> bool`、`asset_exists(path) -> bool`、`can_edit(path) -> bool`、`load_font(path) -> bool`、`font_typefaces(path|None) -> [str]`（None = 引擎默认字体）
  - `read_text(path) -> str|None`、`write_text(path, text)`
  - `import_texture(folder, name, source_file) -> str`（返回资产路径）、`set_texture_settings(path)`、`texture_size(path) -> (w, h)`、`list_textures(folder) -> [path]`、`referencers(path) -> [包路径]`、`delete_asset(path) -> None`
  - `create_wbp(folder, name) -> str`、`read_tree(wbp) -> {name: {class, parent, index}}`
  - `add_widget(wbp, cls, name, parent|None, index) -> None`、`move_widget(wbp, name, parent, index)`
  - `set_slot(wbp, name, slot_values: dict)`、`set_widget(wbp, name, values: dict)`
  - `compile_wbp(wbp) -> None`（失败抛错，含编译信息）、`save(paths: list)`
- `fake_editor.FakeEditor`：同名方法，内存里维护资产集合、WBP 控件树、文件；带开关模拟 `pie=True`、`locked={path}`、`fail_on={tool名}`；`install(test_case)` 用 `mock.patch.object` 替换 `seedance_ui.tools` 的全部函数。

- [ ] **Step 1：写失败测试** `test_fake_editor.py`：`FakeEditor` 的 `read_tree` 在 `add_widget` / `move_widget` 后结构正确；`fail_on` 让对应函数抛 `UiImportError`（保证后续测试的故障注入可用）
- [ ] **Step 2：跑，期望 FAIL**
- [ ] **Step 3：实现 `tools.py`（按 Task 1 速查）与 `FakeEditor`**
- [ ] **Step 4：跑，期望 PASS**
- [ ] **Step 5：提交** `feat: seedance-ui-to-unreal 工具层与测试用假编辑器`

---

### Task 7：执行计划与入口 run()

**Files:**
- Create: `editor/seedance_ui/apply.py`、`editor/seedance_ui/main.py`
- Test: `test_import_flow.py`

**Interfaces:**
- Consumes：Task 2～6
- Produces：
  - `main.run() -> dict`：读全局 `REQUEST = {"importDir", "wbpName", "font", "layoutJson", "sliceRoot", "hashes"}`（`hashes` = `{layout 里的 image 相对路径: sha256}`，启动器算）（`sliceRoot` 是本地解压目录的绝对路径，用 `/` 分隔），返回 ImportReport dict；`UiImportError` 不吞，由沙箱原样抛出。
  - `apply.preflight(...)`：设计「先检查后写入」一节全部条目（PIE、记录存在 / 可解析 / 与 WBP 存在性一致、`FONT` 可加载、WBP 与已存在贴图 `can_edit`）。
  - `apply.execute(plan, wbp, ...)`：顺序 = 导入贴图并设置 → 建 / 读 WBP → 执行 ops → `compile_wbp` → `save` → 写记录；记录写失败抛「Widget Blueprint 已保存，但导入记录没写成……不要直接重导」。

- [ ] **Step 1：写失败测试**（`FakeEditor` + ui-status / ui-popup）
  - `test_first_import_ui_popup` / `test_first_import_ui_status`：报告 `created` 数正确；假编辑器里 WBP 树有 `Root/ScaleBox/SizeBox/Design`；`SizeBox` 宽高 = 2560×1440；记录文件写在 `/Game/SeedanceKlab/UI/Tests/X/WBP_X.json`；容器 `Visibility = SelfHitTestInvisible`、Image / TextBlock 为 `HitTestInvisible`、Button 不设；`button_e62_Button` 的 Hovered 着色 1.1、Pressed 0.85
  - `test_bold_without_bold_face_warns`：`FakeEditor` 的 `font_typefaces` 不含 `Bold` → 粗体文字用默认字形，`warnings` 有一条
  - `test_texture_unchanged_not_touched`：重导时哈希相同 → 假编辑器的 `delete_asset` / `import_texture` 调用数为 0
  - `test_texture_changed_reimported`：哈希变了、只被本 WBP 引用 → 先 `delete_asset` 再 `import_texture` 同名，用它的显示控件笔刷重设，记录哈希更新
  - `test_texture_changed_external_ref_skipped`：哈希变了、`referencers` 还含 `/Game/Other/WBP_Other` → 不删不导，`warnings` 含 `T_xxx` 与 `WBP_Other`，记录保留旧哈希
  - `test_orphan_textures`：`Textures/` 下预置一张不在产物里的 `T_old_e99` → 出现在 `orphanTextures`，未被删除
  - `test_reimport_keeps_manual_widget`：首导 → 手加 `MyButton` → 重导 → `MyButton` 还在、无重复容器
  - `test_preflight_blocks_writes`：分别打开 `pie`、记录损坏、WBP 在但记录缺失、记录在但 WBP 缺失、WBP 被锁、`font` 加载失败 → 抛中文错误，且假编辑器的写操作计数为 0
  - `test_record_write_failure_message`：`fail_on={"write_text"}` → 错误消息含「不要直接重导」
  - `test_compile_failure_not_saved`：`fail_on={"compile_wbp"}` → `save` 未被调用
- [ ] **Step 2：跑，期望 FAIL**
- [ ] **Step 3：实现**
- [ ] **Step 4：跑全部测试，期望 PASS**
- [ ] **Step 5：提交** `feat: seedance-ui-to-unreal 导入流程（检查、执行、写记录）`

---

### Task 8：本地启动器

**Files:**
- Create: `.claude/skills/seedance-ui-to-unreal/scripts/run_import.py`
- Test: `test_launcher.py`

**Interfaces:**
- Consumes：Task 7 的 `REQUEST` 字段；Task 1 记下的 MCP 握手
- Produces：
  - CLI：`python run_import.py --source <zip|dir> --project <dir> --import-dir /Game/... [--wbp-name N] [--font /Game/...] [--capture <png>]`；stdout 只打印 ImportReport JSON；错误打印到 stderr，退出码 1（导入错误）/ 2（连接或环境错误）。
  - 函数：`extract_source(src, project) -> (root_dir, cleanup)`（zip 按 UTF-8 解文件名，越界条目抛错；目录直接用）；`check_slices(layout, root) -> None`（任一切图不存在抛中文错误）；`slice_hashes(layout, root) -> {image: sha256 hex}`；`bundle_script(editor_dir, request: dict) -> str`（按 `errors, layout, naming, geometry, record, planner, tools, apply, main` 顺序拼接，删 `from .` 行，开头 `REQUEST = json.loads(<字面量>)`）；`mcp_endpoint(project) -> str`（读 `$PROJECT/.mcp.json`）；`McpClient(url).call_tool(toolset, tool, args, timeout) -> dict`；`check_project(client, project)`（编辑器工程名 ≠ `.uproject` 名就报错，具体工具按 Task 1）。

- [ ] **Step 1：写失败测试**
  - `test_extract_zip_utf8_names`：用 `zipfile` 现造一个不带 UTF-8 标志、含 `slices/格子_1.png` 的 zip → 解出文件存在；含 `../evil.png` 的 zip 抛错；`cleanup()` 后目录不存在
  - `test_check_slices_missing`：删掉一张切图 → 抛错并含该路径
  - `test_bundle_compiles_and_runs`：`bundle_script` 产物 `compile()` 通过；在一个只放 `json/re/math` 与假 `execute_tool`（转发到 `FakeEditor`）的命名空间里 `exec` 后调 `run()`，返回报告含 65 个 `created`（证明拼接后的单脚本与包行为一致）
  - `test_bundle_imports_only_allowed_modules`：用 `re` 扫拼接结果的 `import` 行，只许 `json` / `re` / `math`
  - `test_mcp_client_against_stub`：`http.server` 在线程里起一个按 Task 1 记录的握手协议应答的桩，`call_tool` 拿回 `{"ok": 1}`
- [ ] **Step 2：跑，期望 FAIL**
- [ ] **Step 3：实现**
- [ ] **Step 4：跑全部测试，期望 PASS**
- [ ] **Step 5：提交** `feat: seedance-ui-to-unreal 本地启动器`

---

### Task 9：端到端（真实编辑器）

**Files:**
- Create: `unreal/SeedanceExample/run-e2e.sh`、`unreal/SeedanceExample/Tests/SeedanceUI/e2e_check.py`

**Interfaces:**
- Consumes：Task 8 的 CLI 与 `McpClient`（读回控件树、改名、加控件）
- Produces：`run-e2e.sh`（编辑器开着时运行；退出码 0 全过）

`e2e_check.py` 依次：

1. 两个 fixture 分别导入 `/Game/Tests/SeedanceUI/Popup`、`/Game/Tests/SeedanceUI/Status`，断言报告 `created` 数（popup = layout 元素数、status = 65），`warnings` 不含「方块」类字体问题。
2. 经 MCP `GetWidgetDescription` 读回 Status：`button_e62` 的 slot 偏移 = `(1367-237, 1157-127, 286, 105)`；`text_e3_Text` 文字为「角色属性」、字号 = 原像素 × 0.75；`SizeBox` 2560×1440。
3. 改名 `button_e62_Button` → `UpgradeButton`，经 MCP 加一个 `MyButton` 到 `panel_e1`，**打开 WBP 编辑器**后重导 Status：`UpgradeButton` 与 `MyButton` 都在，控件总数 = 首导数 + 1，报告无 `rebuilt`。
4. 截图（按 Task 1 的方式）写到 `$SCRATCHPAD/e2e/status.png`，打印路径供人眼与 `preview.png` 对照。
5. 清理：删 `/Game/Tests/SeedanceUI/` 与 `Content/SeedanceKlab/UI/Tests/`。

- [ ] **Step 1：写 `e2e_check.py` 与 `run-e2e.sh`**
- [ ] **Step 2：在 SeedanceExample 编辑器开着时跑 `bash unreal/SeedanceExample/run-e2e.sh`**，期望退出码 0；用 Read 打开截图与 `preview.png` 并排目测，错位 / 方块 / 叠放错误都算失败，回到对应任务修
- [ ] **Step 3：提交** `feat: seedance-ui-to-unreal 端到端测试`

---

### Task 10：SKILL.md、文档与插件清单

**Files:**
- Create: `.claude/skills/seedance-ui-to-unreal/SKILL.md`
- Modify: `docs/engine/unreal.md`、`CLAUDE.md`（Unreal 一行指向 `docs/engine/unreal.md`）、`.claude-plugin/marketplace.json`（新增插件，`version: "1.0.0"`、`source: "./.claude/skills/seedance-ui-to-unreal"`、`keywords` 含 `unreal`、`umg`、`seedance`、`ui-maker`、`mcp`）、`README.md`（勾 UMG 目标、补安装与用法一段）

- [ ] **Step 1：写 SKILL.md**：frontmatter `name: seedance-ui-to-unreal`，description 触发词含「导入 UI 到 UE / Unreal / UMG、Widget Blueprint、ui-maker、ui.zip、重导」；正文：参数表、步骤（前置检查 → `run_import.py` → 截图对照 → 汇报）、Report 字段表（同设计）、出错时、Common Mistakes（至少：容器与显示控件同时改名会重复；挪 WBP 要连同记录一起挪；不要手改 `.uasset`；编辑器打开的不是目标工程）
- [ ] **Step 2：更新 `docs/engine/unreal.md`**（本 skill 的约定、`run-tests.sh` / `run-e2e.sh` 用法）、CLAUDE.md、marketplace.json、README
- [ ] **Step 3：验证**：`python -c "import json;json.load(open('.claude-plugin/marketplace.json',encoding='utf-8'))"` 通过；`bash unreal/SeedanceExample/run-tests.sh` 全过
- [ ] **Step 4：用 skill 走一遍**：按 SKILL.md 把 `ui-popup.zip` 导入 `/Game/UI/Generated/Popup`，汇报与截图符合 SKILL.md 描述；然后删掉这次导入的产物与记录（测试工程不留手动导入结果）
- [ ] **Step 5：提交** `docs: seedance-ui-to-unreal 的 SKILL.md、引擎文档与插件清单`
