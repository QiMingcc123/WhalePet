# DSH 蓝色鲸鱼娘桌宠设计

## 意图

在 DSH Web UI 的右下角常驻一只原创的蓝色鲸鱼娘桌宠，用轻量、可读的方式展示当前会话的上下文占用、token 用量、可用 skills 和 agent 状态，并对键盘输入、鼠标移动与点击给出即时反馈。它不是独立桌面应用，而是 DSH Web UI 内的固定浮层。

## 目标

- 原创 SVG 鲸鱼娘，深海蓝贴纸风，和 DSH 的蓝色品牌气质一致。
- 固定显示在页面右下角，不遮挡正文和输入框。
- 展示上下文百分比与 token 数、token 总量与缓存命中、可用 skill 数、当前 agent preset 与子代理数量。
- 键盘输入时切换到打字状态；鼠标移动时眼睛追随指针；鼠标按下时做出被戳到的反应；点击鲸鱼娘时出现对话气泡和弹跳。
- 可展开/收起统计卡片；窄屏下自动收成只带鲸鱼娘的紧凑形态。
- 尊重 `prefers-reduced-motion`，键盘可聚焦，带可访问名称。

## 非目标

- 不控制操作系统桌面，不创建 Electron 透明窗口。
- 不替换 DSH 自带的 `activity` / `usage` 统计控件。
- 不新增 agent 工具，不修改会话日志。
- 不实现拖拽移动，避免和点击互动冲突。

## 约束

- 安装为当前 profile 的持久 bundle，不修改 DSH 本体。
- 只在有会话时出现：注册到 `conversation.composer.dock`，再通过 `createPortal` 固定到 `document.body`，因为该 slot 位于 composer 的层叠上下文中。
- 数据只读：上下文用 `contextPressure` 投影，token 用 `tokenUsage` 投影，agent 用 `agentPreset` 与 `subagentCatalog` 投影，skills 用 Client `ctx.remote.skills.list({ sessionId })`。
- 数据不可用时显示 `—`，不伪造 0。
- 样式使用 `whale-pet-` 前缀；插画使用自有深海蓝配色，UI 控件遵守主题圆角与可读性。
- 不引入新的运行时依赖。

## 视觉

- 主色：DeepSeek 蓝 `#4D6BFE`、深海海军蓝 `#0B1B3A`、浅鲸蓝 `#8FD3FF`、奶白 `#F6F0E4`、腮红 `#FF9EB5`。
- 鲸鱼娘为圆润 chibi：鲸鱼连帽衫、白色鲸腹、水手领、尾鳍、小喷水；深海军蓝描边制造贴纸感。
- 统计卡片为半透明深海玻璃：深蓝底、浅蓝描边、细颗粒高光；三列读数，底部 agent 行。
- 签名元素：鲸鱼娘头顶的喷水会随状态变成小水泡/按键符号；点击时气泡里出现手写感短句。

## 交互状态

- `idle`：呼吸、眨眼、尾鳍轻摆、偶尔喷水。
- `typing`：举起小键盘，按键帽弹跳，眼睛专注；键盘停止约 900ms 后回到 idle。
- `cursor`：瞳孔跟随指针，身体轻微倾斜；指针静止约 1.6s 后回到 idle。
- `pressed`：鼠标按下时轻微压扁、眼睛睁大；松开恢复。
- `cheer`：点击后弹跳、飘心、显示随机短句约 2.8s。

## 数据口径

- 上下文：优先 `pressureTokens`，否则 `projectedTokens`；百分比为占用 / `contextWindow`，超过 85% 用警告色。
- Token：`uncachedInputTokens + outputTokens + cacheReadTokens + cacheWriteTokens`（按实际字段兜底 `inputTokens`）；缓存命中为 `cacheReadTokens / (uncachedInputTokens + cacheReadTokens)`。
- Skills：当前会话可用 skill 条目数量；展开后列出前几个名称。
- Agent：`agentPreset` 名称；`subagentCatalog` 行数作为子代理数；会话 `running` 决定状态点。

## 实现结构

- `package.json`：bundle patch + web client + locale/icon exports。
- `cordis.patch.yml`：插入 `blue-whale-pet`。
- `index.js`：Host 空实现，仅让 bundle 合法挂载。
- `client.js`：React/SVG 桌宠、slot 注册、portal、数据读取、DOM 事件、样式。
- `locale/en.json`、`locale/zh.json`：插件管理器的标题与描述。
- `icon.svg`：鲸鱼娘头像图标。

## 验证

- `node --check` 检查 Host 与 Client 入口；校验所有 JSON。
- `plugin_manager install_bundle` 安装后从返回的 `application` / `warnings` 确认已生效。
- 通过 `cordis_inspect_query Slots.listSubTree` 确认 `conversation.composer.dock` 出现新 occupant。
- 若无法控制页面，明确报告视觉渲染未截图验证；请用户刷新 GUI 后在右下角确认。
