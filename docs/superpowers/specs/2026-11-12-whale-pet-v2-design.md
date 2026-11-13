# 蓝色鲸鱼娘桌宠 v2 设计

## 目标

把原来的手绘 SVG 桌宠升级为项目提供的角色图形象，增加单行状态栏、休眠/互动状态、可配置图片与时长、过渡动画；保留 DSH Web 插件，并新增可独立运行的 Windows 桌面版。

## 两套运行形态

### A. DSH Web 插件

- 继续注册在 `conversation.composer.dock`，portal 到 `document.body` 固定右下角。
- 角色图来自插件包 `assets/`，通过 `/plugins/<package>/assets/` 路由加载。
- 数据来源不变：`contextPressure`、`tokenUsage`、`contextBreakdown`、`agentPreset`、`subagentCatalog`、`ctx.remote.skills.list`。
- 状态栏在角色图下方，单行显示上下文、token、技能、agent；点击展开明细。
- 图片路径、状态栏可见性、空闲分钟、互动秒数、动画毫秒保存在 localStorage，齿轮面板可改。
- 空闲判定使用浏览器内 keydown/pointermove；超过阈值换休眠图，输入后回主图。
- 点击角色随机短句 + 随机互动图；默认 30 秒后回主图/休眠图。
- 图片切换使用 CSS opacity/scale keyframes 交叉淡入淡出。

### B. Windows 桌面版（WPF）

- `desktop/WhalePet/`，.NET 10 WPF，自包含单文件发布。
- 透明无边框、Topmost、可拖动，位置保存到本机配置。
- 角色图、状态栏、天气、问候气泡垂直排列；状态栏在角色下方。
- 系统级空闲检测使用 `GetLastInputInfo`，超过阈值换休眠图。
- 点击角色随机互动图 + 短句；互动计时结束回主图/休眠图。
- 天气：IP 定位（ipwho.is / ipapi.co）→ Open-Meteo 当前天气；显示温度、天气现象、降雨/降雪、风力等级。可在设置中关闭或手填经纬度。
- 启动时按本地小时数显示问候。
- 设置窗口：状态栏可见性、问候、天气、空闲分钟、互动秒数、动画毫秒、天气位置、三类图片路径（每行一张，至少 1 张）。
- 配置写入 `%LOCALAPPDATA%\DSHWhalePet\settings.json`。
- 已发布：单文件 `dist\WhalePet.exe`，并打包为 `dist\WhalePet.zip`。默认图片内嵌在 exe 中，首次启动解压到 `%LOCALAPPDATA%\DSHWhalePet\assets`。

## 资源处理

原始图片的棋盘格是烘焙进 PNG 的，不能直接用于透明桌宠。`tools/make_assets.py` 使用：
1. 从边框开始的连通域背景识别；
2. 移除近白/浅灰背景；
3. 对主图头顶一个封闭的棋盘格口袋做局部清理；
4. 生成 WebP（Web）与 PNG（桌面）两套透明资源。

## 取舍

- 桌面版不读取 DSH 会话数据：DSH 的上下文/token/skills/agent 需要会话桥，独立进程无法可靠获得。桌面状态栏显示时间、天气、空闲状态。
- Web 版天气是 best-effort：受 DSH 浏览器 CSP 限制时自动隐藏，不影响桌宠。桌面版天气始终由本机 HTTP 获取。
- 桌面版默认图片从 exe 内嵌资源解压到本机配置目录；配置文件可保存任意绝对路径。
