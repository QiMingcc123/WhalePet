# DSH 蓝色鲸鱼娘桌宠 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付一个可安装的 DSH Web 插件，在右下角显示会响应键盘/鼠标的蓝色鲸鱼娘，并展示上下文、token、skills 与 agent 状态。

**Architecture:** Host 半边为空；Client 半边注册 `conversation.composer.dock`，通过 portal 固定到 `document.body`。数据来自现有 session projections 与 `ctx.remote.skills.list`，不新增 Host 服务或模型工具。

**Tech Stack:** Plain ESM bundle、React（browser module table）、inline SVG、CSS、Cordis slots、DSH Client Remote。

**Spec:** `docs/superpowers/specs/2026-11-11-dsh-whale-pet-design.md`

## Global Constraints

- 只在当前 profile 安装，不修改 DSH 本体或 profile 的 `package.json` / `cordis.patch.yml`。
- 不新增运行时依赖。
- 使用 `whale-pet-` 前缀避免样式冲突。
- 数据缺失显示 `—`，不伪造读数。
- 动画尊重 `prefers-reduced-motion`。

## Review Focus

- **技能接口不可用或返回空数组**：应显示 `—` 或 `0`，不能崩溃或误显示。
- **上下文窗口缺失**：不能除以零或显示 `NaN%`。
- **窄屏 360px**：统计卡片不能盖住输入框主体，应收缩或只留鲸鱼。
- **快速连续按键/指针移动**：不能每个事件触发整棵 React 树重渲染或堆积定时器。
- **slot 卸载/切换会话**：事件监听、portal、remote 请求、定时器必须全部清理。

---

### Task 1: Bundle scaffold and display assets

**Files:**
- Create: `package.json`
- Create: `cordis.patch.yml`
- Create: `index.js`
- Create: `locale/en.json`
- Create: `locale/zh.json`
- Create: `icon.svg`

**Interfaces:**
- Produces: package `@local/dsh-blue-whale-pet`, patch row id `blue-whale-pet`, client export `./client.js`.

- [ ] Write the manifest with `dsh.bundle.patch`, `dsh.client.platform = web`, `immediately: true`, and locale/icon exports.
- [ ] Write an empty Host `apply` and the patch insert row.
- [ ] Draw an original whale-girl icon and write localized metadata.
- [ ] Validate JSON and `node --check index.js`.

### Task 2: Whale pet client

**Files:**
- Create: `client.js`

**Interfaces:**
- Consumes: `conversation.composer.dock` standard props `sessionId`, `useSession`, `useProjection`; Client services `slots`, `remote`, `locale`.
- Produces: slot occupant `whale-pet`, fixed overlay with class prefix `whale-pet-`.

- [ ] Register the slot occupant and portal the component to `document.body`.
- [ ] Render the SVG whale girl and stats card; read `contextPressure`, `tokenUsage`, `agentPreset`, `subagentCatalog`; load skills from `ctx.remote.skills.list({ sessionId }, signal)`.
- [ ] Add keyboard, pointer-move, pointer-down, click, and bubble behavior with cleanup.
- [ ] Add responsive and reduced-motion CSS.
- [ ] Validate `node --check client.js`.

### Task 3: Install and verify

**Files:**
- None.

- [ ] Run `plugin_manager install_bundle` with the absolute package directory.
- [ ] Confirm `application: applied` and no blocking warnings.
- [ ] Query `Slots.listSubTree` for `conversation.composer.dock` and confirm `whale-pet` is active.
- [ ] Report that visual layout remains for the user to confirm in the refreshed GUI if browser control is unavailable.
