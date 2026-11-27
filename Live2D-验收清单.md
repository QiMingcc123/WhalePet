# Live2D 形象切换验收清单

> 说明：本文件是最初集成验收记录。修复单完成后的最新行为、已知限制和回归结果以 [Live2D-修复验收.md](Live2D-修复验收.md) 为准。

## 实际运行参数

- 引擎：`untitled-pixi-live2d-engine@1.4.0`，统一入口 `Live2DWeb/lib/index.min.js`
- PixiJS：`8.23.0`，`Live2DWeb/lib/pixi.min.js`
- Cubism Core：使用带 `getDrawableRenderOrders` API 的 Cubism 5 Core，`Live2DWeb/lib/live2dcubismcore.min.js`
- Legacy runtime：`Live2DWeb/lib/live2d.min.js`。`index.min.js` 虽为统一入口，但实际启动时仍会检查 Cubism 2 runtime；不加载该文件会报 `Could not find Cubism 2 runtime`。
- 模型：`Live2DWeb/models/seethrough_output/`，未移动、重命名或删除；`.4096` 纹理目录保持原结构。

Block1 实测结果：

```json
{
  "globalName": "PIXI.live2d",
  "hasPixi": true,
  "hasCubismCore": true,
  "hasLive2DModel": true,
  "hasLive2DPlugin": true,
  "hasConfigureCubismSDK": true,
  "version": "1.4.0"
}
```

`Live2DModel.focus(x, y)` 接收世界空间像素坐标；`FocusController.focus(x, y)` 接收 `[-1, 1]` 归一化坐标。当前实现使用前者。

## 已实现的 Block

### Block2：Web 渲染层

- `index.html` 按 `pixi.min.js → live2dcubismcore.min.js → live2d.min.js → index.min.js → app.js` 顺序加载。
- 使用本地 `https://live2d.local/` 虚拟主机，不依赖 CDN。
- 加载 `seethrough_output.model3.json`，居中缩放，背景透明。
- 使用 `useHighPrecisionMask: false` 与 `textureOptions: { lod: 'single-auto' }` 规避 3 张 4096 纹理在 WebView2 高精度遮罩路径上的兼容问题。
- 页面异常、模型异常均通过 `postMessage` 上报，并触发降级。

### Block3/4：透明 WebView2 窗口

- 新增 `Game/Live2DWindow.xaml` 与 `Live2DWindow.xaml.cs`。
- 窗口 `AllowsTransparency=True`、`WindowStyle=None`、`ShowInTaskbar=False`、`ShowActivated=False`、`Focusable=False`，`Owner` 为主窗口。
- 跟随主窗口 `Left/Top/Width/Height`；主窗口隐藏时隐藏；主窗口恢复时按当前目标状态恢复。
- WebView2 用户数据目录：`%LOCALAPPDATA%\DSHWhalePet\WebView2`。
- 日志：`%LOCALAPPDATA%\DSHWhalePet\logs\log-yyyy-MM-dd.txt`，启动时清理超过 3 天的文件。
- 关闭顺序：主窗口 `OnClosing` 中先停止/释放 WebView2，再关闭 Live2D 窗口。

### Block5：输入复用

- 在现有 `GlobalInputHook` 上增加按钮事件，未重写原有键盘/鼠标钩子。
- 主窗口把全局鼠标坐标用 `PointFromScreen` 转成 Live2D 窗口归一化坐标，发送 `mouseMove`，页面调用 `model.focus(pixelX, pixelY)`。
- 左键、右键、键盘分别发送 `mouseLeftDown`、`mouseRightDown`、`keyDown`；页面映射到模型真实 motion 组 `Nod`、`Shake`、`Talk`。
- 模型没有 HitAreas，因此不硬编码 `TapBody`；点击反馈使用真实 motion 组。

### Block6：切换体系

- `AvatarKind` 新增 `Live2D`，作为第四种 `CurrentAvatarType`。
- `CurrentLive2DModelId` 默认 `seethrough_output`，已加入 `AppState`、旧库补列、保存/加载与迁移包。
- 切换窗口新增独立“Live2D”Tab，当前显示“大肥鱼”；模型列表会扫描 `Live2DWeb/models` 下含 `.model3.json` 的子目录。
- 选中 Live2D 后设置 `AvatarType=Live2D` 并显示/创建 Live2D 窗口；切换回默认/GIF/自定义图时只 `Hide()`，不销毁 WebView2。
- 模型路径缺失、WebView2 初始化失败、模型加载/渲染失败均回退 `DefaultStatic` 并提示。
- 取消不写入状态。

## 验证结果

- `dotnet build desktop/WhalePet/WhalePet.csproj -c Release`：0 警告、0 错误。
- `dotnet run --project tools/GameSmoke/GameSmoke.csproj -c Release`：78 项全部通过，包含 Live2D 枚举/默认模型 ID 检查。
- `dotnet run --project tools/LayoutSmoke/LayoutSmoke.csproj -c Release`：通过。
- Live2D 启动日志出现 `model ready`；实际截图见 `verification/live2d-final.png`、`verification/live2d-publish.png`。
- 通过切换界面从 Live2D 切到默认主形象再切回 Live2D：数据库状态正确；`msedgewebview2.exe` 进程组数量前后均为 6，未销毁 WebView2。
- 关闭主程序后 `msedgewebview2.exe` 为 0。
- 将 `CurrentLive2DModelId` 指向不存在的模型后启动：自动回退 `DefaultStatic`，未启动 WebView2 进程，无崩溃。
- 发布目录 `dist/WhalePet-win-x64/Live2DWeb/models/seethrough_output/` 的 `.moc3` 与 `.4096/texture_00..02.png` 均存在；发布版实测 `model ready`。

## 已知边界

- 当前模型没有 HitAreas，点击只做整体动作反馈，不做部位级命中测试。
- 当前阶段未实现点击穿透；Live2D 窗口是独立顶层窗口，后续需要时可再处理穿透。
- WebView2 Runtime 需安装在目标机器；缺失时按降级路径回退。
- 模型纹理为 3 张 4096 PNG，首次加载会占用较多显存；已启用 `single-auto` LOD 降低开销。
