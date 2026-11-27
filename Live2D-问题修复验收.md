# Live2D 形象切换问题修复验收报告

- 修复依据：`docs/Live2D形象切换问题修复.md`
- 验收日期：2026-11-27
- 程序版本：桌面版鲸鱼娘 1.1.0（`net10.0-windows`，单文件自包含 `win-x64`）
- 运行环境：Windows 10 专业版 19045 / 16 逻辑核 / NVIDIA RTX 3050 / 主屏 1920×1080（工作区 1920×1040，100% 缩放）
- WebView2 Runtime：133.0.3065.92
- Live2D 引擎：untitled-pixi-live2d-engine 1.4.0；PixiJS 8.23.0；模型 `seethrough_output`
- 测试时角色设置：整体缩放 0.7 × 角色缩放 0.5（角色区域约 150×140 DIP；覆盖窗口裁剪到状态栏上方后约 150×122 DIP）

## 一、修改内容

### 1. 右键菜单与桌面操作隔离

- `CoreWebView2Settings.IsNonClientRegionSupportEnabled = false`，关闭 WebView2 的 non-client 拖拽区，消除系统标题栏右键菜单来源。
- `AreDefaultContextMenusEnabled = false`，并在 Web 侧对 `document`/`canvas` 的 `contextmenu` 统一 `preventDefault()`。
- Live2D 窗口不再覆盖整个桌宠窗口，而是只覆盖角色区域；窗口底边止于状态栏摘要按钮顶部，状态栏保持可点击。
- 右键改为在全局钩子的 **button-up** 时打开桌宠菜单，避免同一次右键的 button-up 落到 WebView 后立刻关闭 WPF 菜单。
- 只有按在模型包围盒内才转发拖拽/部位点击；透明边距不触发桌宠菜单或拖拽。
- 窗口背景改为 1/255 透明度的可命中表面，避免 WPF 分层窗口把右键穿透给桌面；WebView2 仍保持透明渲染。

### 2. 状态栏展开/收起几何同步

- `SyncLive2DWindow()` 仍是唯一几何入口；状态栏变更后在 `DispatcherPriority.Loaded` 同步一次，并在 280ms 后带 token 兜底同步一次，避免布局未完成时留下旧坐标。
- Web 侧 `ResizeObserver`（100ms 节流）在非 WPF 触发的尺寸变化时兜底重算。
- 模型采用 contain + 3.5% 安全边距：`scale = min(availableW / modelW, availableH / modelH)`，始终完整显示。
- 窗口尺寸使用角色设计尺寸 × 两个显式缩放变换，并按整数 DIP 取整，避免拖动/展开过程中 `ActualWidth` 短暂变成 152×138 导致模型缩放跳变。
- 状态栏展开/收起时不再播放角色按压缩放动画（Live2D 模式），避免把字符区整体缩放造成的瞬时偏移带到独立覆盖窗口。

### 3. 窗口移动、边缘吸附与多显示器

- `CurrentAvatarType == Live2D` 时禁用原有自动贴边收起；切回静态图/GIF 后恢复。
- 新增 `MonitorPlacement`：使用 `MonitorFromRect` 获取窗口当前所在显示器的工作区，限制窗口不越出工作区，并在距边缘 15px 内吸附。
- `WM_MOVING` 中处理系统发起的移动；手写拖拽（无标题栏，不产生 `WM_MOVING`）在每次移动后调用同一 `ConstrainPetWindow()`。
- 监听 `SystemEvents.DisplaySettingsChanged`，立即检查并在 600ms/1400ms 后复检；窗口不在任何显示器工作区时移回主屏可见区域。
- 多显示器吸附算法通过可注入工作区的 `ConstrainToWorkArea` 做副屏参数单测。

### 4. 资源占用

- 隐藏/切走时调用 `CoreWebView2.TrySuspendAsync()`；重新显示前 `Resume()`。未手动设置 `MemoryUsageTargetLevel`。
- Web 侧停止 Pixi 自带 ticker，改用 `requestAnimationFrame` + 时间戳跳帧：可见 30 FPS，不可见 15 FPS。
- `textureOptions: { lod: 'single-auto' }`。
- 未设置 `disable-gpu` 等禁用硬件加速参数。

## 二、测试结果

### 第 1 条：右键菜单与桌面隔离

| 测试项 | 结果 | 证据 |
|---|---|---|
| 右键点击 Live2D 角色只弹桌宠菜单，不出现 Windows 系统菜单 | **通过** | 右键后桌宠菜单可见（`verification/live2d-rightclick-stable.png`）；枚举顶层窗口没有 `#32768` 系统菜单；桌宠菜单为桌宠进程自己的 `HwndWrapper` 弹出窗口 |
| 左键按住角色拖动，窗口跟随移动 | **通过** | 从 `1769,241` 拖到 `1769,163`，`delta=0,-78`；到右边缘后横向被工作区限制为 1769，符合“不拖出工作区” |
| 角色之外桌面空白处右键 | **通过** | 在 `(500,300)` 右键，出现 Explorer 的 `#32768` 菜单（pid 3044），桌宠窗口坐标不变 |
| 角色之外桌面空白处框选 | **通过** | `verification/desktop-rubberband-stable.png` 显示 Explorer 框选虚线框；框选前后桌宠窗口 `1769,163` 未移动 |
| 角色之外桌面空白处点击 | **通过** | 点击后桌宠无拖动、无表情；桌面 Explorer 保持前台 |
| 非角色区域不转发拖拽/菜单 | **通过** | 指针按下位置必须同时满足“Live2D 窗口内 + 模型包围盒内 + 不在状态栏”才进入拖拽或表情分支 |

说明：Live2D 覆盖窗口现在只到角色区域顶部到状态栏顶部之间，状态栏不再被覆盖；状态栏展开/收起时该覆盖窗口高度保持稳定。

### 第 2 条：状态栏展开/收起

| 测试项 | 结果 | 证据 |
|---|---|---|
| 反复展开/收起 20 次，模型位置、大小始终正确 | **通过** | 20 次交替后统计：`150x122, dx=0, dy=0` × 20；主窗口高度只在 `147` 与 `406` 之间交替（10/10） |
| 展开/收起过程不闪烁、不跳变、不被裁切 | **通过** | 20 次过程中 Web 几何只在尺寸真正变化时上报一次（`150x122`）；未再出现拖动/折叠时的 `152x138` 过渡值；模型按 contain 完整显示，状态栏在覆盖窗口之外 |

### 第 3 条：移动、吸附、多显示器

| 测试项 | 结果 | 证据 |
|---|---|---|
| Live2D 拖动靠近屏幕四边均有吸附且不抖动 | **通过** | 实测四次拖拽终点：左边 `Left=0`；上边 `Top=0`；右边 `Right=1920`（Left=1769）；下边 `Bottom=1040`（Top=893）。均在 15px 阈值内一次吸附 |
| Live2D 模式不自动收起到屏幕边缘 | **通过** | 贴左边缘后鼠标移开等待 5s，主窗口仍为 `151×147`，未变成 30px 贴边标签；`IsLive2DMode` 分支拦截自动收起 |
| 切回静态图/GIF 后自动收起恢复 | **通过** | 切到 GIF 后拖到左边缘、松开鼠标，窗口立即收起为 `30×76` 的贴边标签 |
| 更改分辨率/拔掉副屏后回到主屏可见区域 | **通过** | 1) 广播 `WM_DISPLAYCHANGE`：窗口从 `1900,500` 自动移到 `1769,500`，日志 `display changed: moved window from 1900,500 to 1769,500`，后续两次复检 `visible=True`；2) 实际切换到 1600×900 再恢复 1920×1080，窗口仍在可见工作区 |
| 多显示器下吸附到副屏边缘而非主屏 | **未实测（硬件限制）** | 当前环境只有 1 个活动工作区（`1920×1040`）。已用副屏工作区 `1920..3840` 做算法单测：左边缘吸附到 1920、右边缘吸附到 3690、越界夹回 3690/893，全部 PASS；`MonitorFromRect` 为实际代码路径 |

### 第 4 条：资源占用

| 测试项 | 结果 | 证据 |
|---|---|---|
| 隐藏时渲染进程挂起 | **通过** | 最小化后日志 `webview suspend=True`；恢复后 `webview resume` |
| 隐藏时 CPU 明显下降 | **通过** | 可见 10s 总 CPU：WebView2 2.265s + 主程序 0.969s = **3.234s**（2.02% × 16 核）；隐藏 10s：WebView2 **0.000s** + 主程序 1.109s = **1.109s**（0.69% × 16 核）；WebView2 渲染进程完全降至 0，总 CPU 下降约 66% |
| 切回后帧率恢复、动作流畅 | **通过** | 可见期间 `fps 30.0 target=30` 连续稳定；恢复后下一次统计 `fps 30.0 target=30`；挂起/恢复瞬间出现一次过渡统计（`fps 10.2 target=30`，更早一次为 `fps 7.5 target=15`），随后回到 30 |
| 显存较优化前明显下降 | **通过** | 同一模型、同一窗口 A/B：移除 `textureOptions` 时专用显存 **264.61 MB**；启用 `lod:'single-auto'` 后 **18.64 MB**（150×140 窗口），下降约 **93%**。最终 150×122 裁剪窗口下可见 15.81 MB、隐藏 15.44 MB |
| GPU 硬件加速未被禁用 | **通过** | 代码未设置任何 `disable-gpu` 参数；GPU 进程持续存在，显存计数可读 |

### 全局功能回归

| 测试项 | 结果 | 证据 |
|---|---|---|
| 静态图、GIF、自定义图、Live2D 四种形象显示/切换 | **通过** | 默认静态图经切换窗口实际选中并显示（`avatar-default-static-pet.png`）；GIF 切换并显示（`pet-gif-mode2.png`）；自定义图通过数据库夹具设置 `AvatarType=CustomImage` + 有效图片路径后显示（`avatar-custom-final.png`，因为本机没有已添加的自定义图 UI 样本）；Live2D 切换并显示正常 |
| Live2D → GIF → Live2D 无残留、无重叠 | **通过** | 切到 GIF 后 `avatar visible=False`、`suspend=True`；切回 Live2D 后 `model ready`、`resume`，主窗口与覆盖窗口对齐，无重叠；`seethrough_output` 源模型文件未改动 |
| 七个部位表情各播放 5 秒且不被打断 | **通过** | 日志：`part head -> 微笑`、`hands -> 惊讶`、`body -> 害羞`、`tail -> 困倦`、`skirt -> 阴沉`、`feet -> 生气`、`face -> 悲伤`；每个 `expression start` 与 `expression end` 间隔 5.0s；在 5 秒内点击另一部位记录 `expression ignored`，不打断当前表情 |
| 每 10 分钟随机动作 | **通过** | 模型 20:17:27 就绪，20:27:27.815 记录 `random motion: Shake`；历史日志同样按 10 分钟间隔出现 `Talk/Nod/Blink/Shake`，未出现 `Idle` |
| 角色面板、任务、签到、抽签、背包、成就墙、钓鱼、烹饪等无报错 | **通过（自动化覆盖）** | `GameSmoke` **81/81 PASS**，包含等级/好感/背包/暂存区/抽签/签到/任务/GIF 解锁/迁移/阅读区/快捷键/Live2D 类型与副屏吸附；`LayoutSmoke` PASS（面板 1080/540/400 布局与位置保存） |
| 状态栏、气泡显示/隐藏不影响 Live2D | **通过** | 20 次状态栏切换几何稳定；覆盖窗口底边在状态栏上方，状态栏点击不会被表情/拖拽拦截；气泡显示时 Live2D 随角色区下移，隐藏后回到原位 |
| 主程序关闭后 msedgewebview2.exe 完全退出 | **通过** | 发送 `WM_CLOSE` 后 6s 统计：`WhalePet=0`、`msedgewebview2=0` |
| 重命名 `seethrough_output.model3.json` 能回退 DefaultStatic 并提示，恢复后正常 | **通过** | 重命名后启动，数据库 `AvatarType=DefaultStatic`，静态形象显示，气泡提示“Live2D 模型缺失，已回退到默认形象”（`live2d-missing-fallback-final2.png`）；恢复文件并设回 Live2D 后 `model ready` |

## 三、未通过/未实测项

1. **真机多显示器吸附**：环境只有单屏，无法实际把窗口拖到副屏。已用副屏工作区参数单测覆盖算法（4 项 PASS），但不等同于真机。
2. **角色矩形内、模型包围盒外的透明边距**：这部分仍由覆盖窗口接收，右键/拖拽不会触发任何桌宠操作，但也不会把右键传给桌面。角色矩形之外的桌面区域点击、右键、框选完全正常。原因是尝试用 `SetWindowRgn` 进一步裁剪时 WPF 分层窗口直接崩溃（`0xc000041d`），已回退到稳定实现。

除以上两项外，第 1–4 条和本轮可执行的全局回归均通过。

## 四、发布产物

- `dist/WhalePet-win-x64/WhalePet.exe`（单文件自包含，2026-11-27 20:33 发布）
- `Live2DWeb/app.js`、`Live2DWeb/index.html`
- 关键代码：`desktop/WhalePet/Game/Live2DWindow.xaml.cs`、`desktop/WhalePet/MainWindow.xaml.cs`、`desktop/WhalePet/MonitorPlacement.cs`、`desktop/WhalePet/GlobalInputHook.cs`

> 发布前已从 `dist` 目录启动验证：`content root` 指向 `dist\Live2DWeb`，模型 `model ready`，`motionGroups=["Idle","Blink","Nod","Shake","Talk"]`。
