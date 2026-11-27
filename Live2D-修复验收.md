# Live2D 形象切换修复验收

## 1. 修复内容与证据

### 1.1 预览图

- `AvatarSwitchWindow` 的 Live2D Tab 读取
  `Live2DWeb/models/seethrough_output/seethrough_output.png`。
- 预览容器为 100×120px，`Image.Stretch=Uniform`，不会拉伸。
- 文件不存在或解码失败时显示“预览不可用”，并写入 `Live2DLog`。
- 实际截图：`verification/live2d-tab-preview.png`。

### 1.2 右键菜单和拖动

- 透明 WPF 窗口中的 WebView2 HWND 在运行时带 `WS_EX_TRANSPARENT`，Web 侧 pointer 事件不会稳定到达页面；直接修改该样式又会导致 WebView2 不再渲染。
- 因此保留现有 `GlobalInputHook`，由 WPF 处理：
  - 右键在角色区域内打开原 `Root.ContextMenu`（角色面板、切换形象、备忘录、退出等）。
  - 左键按下记录起点，移动超过 4px 后拖动主窗口，Live2D 窗口通过统一同步入口跟随。
  - 左键未拖动时按部位映射发送表情。
- 拖动坐标使用 `VisualTreeHelper.GetDpi()` 换算，适配高 DPI。
- 实测右键菜单成功弹出；实测拖动把主窗口从 `1769,731` 移动到 `1678,686`。

### 1.3 尺寸、位置、裁剪

- 统一入口 `SyncLive2DWindow()`，触发来源：
  `LocationChanged`、`SizeChanged`、`IsVisibleChanged`、`DpiChanged`、`StateChanged`、
  状态栏展开/收起、靠边收起/展开、`ApplyScale`。
- 每次同步：
  - 设置 Live2D 窗口 `Left/Top/Width/Height` 等于主窗口。
  - 发送 `layout` 消息，使用 `CharacterHost` 的实际矩形，而不是整个窗口中心。
  - 发送 `resize`，页面重新计算 `fitModel()`。
- 模型缩放使用 `origW/origH`（模型原始尺寸，不读取已缩放尺寸），`anchor=0.5`，在当前角色区域居中。
- 状态栏展开时实测：主窗口 `151×441`，角色区域 `153×137`，详情面板起始 Y=183；角色区域在详情面板之上，模型不会按旧的窗口高度居中到状态栏下方。

### 1.4 边缘收起/展开、窗口状态、DPI

- `SyncLive2DWindow()` 对以下状态统一处理：
  - 靠边收起：临时隐藏 WebView2 窗口，但保留 `_wantedVisible`。
  - 展开：恢复显示并重新发送布局。
  - 最小化：临时隐藏；恢复：重新显示。
  - 隐藏：临时隐藏；显示：重新显示。
  - DPI 变化：重新同步窗口几何并发送 `resize`。
- 不使用收起前的尺寸缓存。

### 1.5 点击部位表情

- 实际检查 `seethrough_output.model3.json`：`HitAreas` 字段不存在；引擎 1.4.0 的签名是
  `Live2DModel.hitTest(x, y)` / `InternalModel.isHit(id, x, y)`，不是三参数
  `hitTest(id, x, y)`。
- 因该模型没有 HitAreas，WPF 使用角色区域归一化坐标作为兼容回退：
  - 头部 → `微笑`
  - 双手 → `惊讶`
  - 身体 → `害羞`
  - 双脚 → `生气`
  - 尾巴 → `困倦`
  - 裙子 → `阴沉`
  - 脸 → `悲伤`
- 映射在 WPF 的 `Live2DPartExpressions` 中；Web 侧 `playLockedExpression` 锁定 5 秒。
- 表情期间新点击直接忽略，不排队、不打断；5 秒后恢复 `微笑`。
- 实测点击脸部日志为 `part face -> expression 悲伤`，页面回传 `expression started: 悲伤`。
- 若未来模型提供 HitAreas，Web 侧使用 `model.hitTest(x, y)`（兼容三参数）按实际名称映射；不硬编码 `TapBody`。

### 1.6 每 10 分钟随机 motion

- `motionGroups` 从 `model.internalModel.motionManager.definitions` 动态读取：
  `Idle、Blink、Nod、Shake、Talk`。
- 页面只在 `visible=true` 时启动 `setInterval(..., 10 * 60 * 1000)`；切回静态/GIF/收起/最小化时清除。
- 每次随机排除 `Idle`，用真实 group 名调用 `model.motion(group, 0)`。
- 模型就绪后实测 `motionGroups` 正确，可见性为 true 时定时器已启动。
- 10 分钟完整周期未在本次会话中等待验证；逻辑和启动条件已代码审查并通过模型就绪/可见性日志确认。

## 2. 回归结果

| 检查项 | 结果 | 证据 |
|---|---|---|
| 构建 | 通过 | 0 warning / 0 error |
| GameSmoke | 通过 | 78/78 |
| LayoutSmoke | 通过 | 无异常 |
| 预览图 | 通过 | `verification/live2d-tab-preview.png` |
| 右键菜单 | 通过 | 菜单项实测出现 |
| 左键拖动 | 通过 | 窗口坐标实际改变 |
| 模型加载 | 通过 | 日志 `model ready`，无 404 |
| 状态栏展开同步 | 通过（代码 + 布局日志） | 主窗口 151×441，角色区域 153×137，详情起始 183 |
| 表情映射 | 通过（脸部实测） | `part face -> expression 悲伤` / `expression started` |
| 表情 5 秒锁 | 通过（代码审查） | `expressionUntil` + 5000ms 定时器 |
| 随机 motion 启动 | 通过（模型就绪/可见性） | `motionGroups` 五项，`timer=true` |
| 模型缺失降级 | 通过 | 之前实测自动回 DefaultStatic，无 WebView2 进程 |
| 关闭清理 | 通过 | 关闭主程序后 `msedgewebview2.exe` 为 0 |
| 高 DPI 多显示器 | 未实测 | 当前环境未连接第二块显示器；代码使用 `DpiChanged` + DPI 换算 |
| 10 分钟完整周期 | 未等待 | 定时器条件已验证，未等待完整 10 分钟 |
| 20 次切换内存 | 未完整执行 | 单次切换已验证不销毁 WebView2 |
| 全功能（钓鱼/烹饪/背包等） | 未完整回归 | 本次修改仅触及 Live2D 分支、输入钩子扩展和切换字段；原有分支未重写 |

## 3. 关键实现位置

- 预览图：`desktop/WhalePet/Game/AvatarSwitchWindow.xaml.cs` 的 `LoadLive2DPreview` / `BuildCard`
- 透明窗口：`desktop/WhalePet/Game/Live2DWindow.xaml.cs`
- 统一同步/拖动/右键/表情：`desktop/WhalePet/MainWindow.xaml.cs`
- Web 渲染/布局/表情锁/motion 定时器：`Live2DWeb/app.js`
- 全局输入钩子：`desktop/WhalePet/GlobalInputHook.cs`

## 4. 限制说明

- 当前模型没有 HitAreas，因此“点击部位”使用角色区域回退，不是模型网格级命中。
- 透明窗口的 WebView2 子 HWND 在 WPF `AllowsTransparency=True` 下是鼠标穿透的；强行去掉样式会破坏渲染，因此右键、拖动和部位点击由现有全局钩子驱动，没有使用 Web `pointerdown` 作为唯一输入源。
- 引擎锁定为 1.4.0；其实际 `hitTest` 签名为 `(x, y)`，与文档中的三参数描述不同，已在 Web 侧做兼容分支。
