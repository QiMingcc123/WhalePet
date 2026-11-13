window.__ModuleLoader__.load({
  id: '@local/dsh-blue-whale-pet',
  factory(require) {
    const React = require('react')
    const ReactDOM = require('react-dom')
    const h = React.createElement

    const STORAGE_KEY = 'dsh-blue-whale-pet.settings.v2'
    const MEMO_KEY = 'dsh-blue-whale-pet.memos.v1'
    const PACKAGE_ID = '@local/dsh-blue-whale-pet'
    const ASSET_BASE = 'plugins/@local/dsh-blue-whale-pet/assets/'

    const CSS = `
.whale-pet-root {
  position: fixed;
  right: 20px;
  bottom: 18px;
  z-index: 900;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 7px;
  max-height: calc(100vh - 16px);
  pointer-events: none;
  color: #eaf6ff;
  font-family: "Segoe UI Variable", "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
}
.whale-pet-root *, .whale-pet-root *::before, .whale-pet-root *::after { box-sizing: border-box; }
.whale-pet-root button { font: inherit; }

.whale-pet-weather {
  pointer-events: auto;
  align-self: flex-end;
  max-width: 360px;
  padding: 5px 10px;
  overflow: hidden;
  border: 1px solid rgba(143, 211, 255, .28);
  border-radius: 10px;
  background: rgba(6, 20, 42, .8);
  box-shadow: 0 6px 18px rgba(0, 7, 20, .24);
  color: #dcefff;
  font-size: 9.5px;
  font-weight: 650;
  line-height: 1.2;
  text-overflow: ellipsis;
  white-space: nowrap;
  backdrop-filter: blur(10px);
}
.whale-pet-stage {
  --whale-pet-stage-w: 360px;
  --whale-pet-stage-h: 340px;
  position: relative;
  width: calc(var(--whale-pet-stage-w) * var(--whale-pet-char-scale, 1));
  height: calc(var(--whale-pet-stage-h) * var(--whale-pet-char-scale, 1));
  flex: 0 0 auto;
  pointer-events: none;
}
.whale-pet-character {
  position: absolute;
  inset: 0;
  display: block;
  width: 100%;
  height: 100%;
  padding: 0;
  border: 0;
  border-radius: 32px;
  background: transparent;
  cursor: pointer;
  pointer-events: auto;
  outline: none;
}
.whale-pet-character:focus-visible {
  outline: 2px solid #8fd3ff;
  outline-offset: 4px;
}
.whale-pet-art {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: contain;
  object-position: bottom center;
  opacity: 0;
  transform: scale(.985) translateY(3px);
  transition: opacity var(--whale-pet-ms, 420ms) ease, transform var(--whale-pet-ms, 420ms) cubic-bezier(.2, .8, .2, 1);
  pointer-events: none;
  user-select: none;
  -webkit-user-drag: none;
  filter: drop-shadow(0 16px 18px rgba(0, 7, 20, .34));
}
.whale-pet-art-current { opacity: 1; transform: scale(1) translateY(0); animation: whale-pet-art-in var(--whale-pet-ms, 420ms) ease both; }
.whale-pet-art-prev { opacity: 0; transform: scale(1.012) translateY(-2px); animation: whale-pet-art-out var(--whale-pet-ms, 420ms) ease both; }
.whale-pet-character:hover .whale-pet-art-current { transform: translateY(-2px) scale(1.008); }
@keyframes whale-pet-art-in {
  0% { opacity: 0; transform: scale(.985) translateY(3px); }
  100% { opacity: 1; transform: scale(1) translateY(0); }
}
@keyframes whale-pet-art-out {
  0% { opacity: 1; transform: scale(1) translateY(0); }
  100% { opacity: 0; transform: scale(1.012) translateY(-2px); }
}

.whale-pet-memo-button {
  position: absolute;
  top: 8px;
  left: 8px;
  z-index: 6;
  width: 28px;
  height: 28px;
  display: grid;
  place-items: center;
  padding: 0;
  border: 1px solid rgba(143, 211, 255, .26);
  border-radius: 10px;
  background: rgba(6, 20, 42, .62);
  color: #cfeaff;
  font-size: 13px;
  line-height: 1;
  cursor: pointer;
  opacity: .68;
  pointer-events: auto;
  transition: opacity .18s ease, background .18s ease, transform .18s ease;
  backdrop-filter: blur(8px);
}
.whale-pet-memo-button:hover { opacity: 1; background: rgba(20, 50, 90, .88); transform: translateY(-1px); }
.whale-pet-memo-button:focus-visible { outline: 2px solid #8fd3ff; outline-offset: 2px; opacity: 1; }
.whale-pet-memo-count {
  position: absolute;
  top: -5px;
  right: -5px;
  min-width: 14px;
  height: 14px;
  display: grid;
  place-items: center;
  padding: 0 3px;
  border: 1px solid rgba(255, 255, 255, .22);
  border-radius: 99px;
  background: #ffb35c;
  color: #2a1600;
  font-size: 7px;
  font-weight: 800;
  line-height: 1;
}

.whale-pet-input-badge {
  position: absolute;
  top: 42px;
  left: 8px;
  z-index: 6;
  display: flex;
  align-items: center;
  gap: 5px;
  padding: 4px 8px;
  border: 1px solid rgba(143, 211, 255, .3);
  border-radius: 9px;
  background: rgba(6, 20, 42, .78);
  box-shadow: 0 6px 16px rgba(0, 7, 20, .2);
  color: #dff2ff;
  font-size: 9px;
  font-weight: 650;
  pointer-events: none;
  animation: whale-pet-input-in .18s ease both;
  backdrop-filter: blur(8px);
}
.whale-pet-input-badge[data-kind="typing"] { border-color: rgba(143, 211, 255, .5); }
.whale-pet-input-badge[data-kind="click"] { border-color: rgba(255, 179, 92, .6); color: #ffe3b6; }
.whale-pet-input-icon { font-size: 11px; line-height: 1; }
.whale-pet-keycaps { display: inline-flex; gap: 2px; margin-left: 2px; }
.whale-pet-keycaps i {
  display: grid;
  place-items: center;
  min-width: 13px;
  height: 13px;
  padding: 0 2px;
  border: 1px solid rgba(143, 211, 255, .35);
  border-radius: 3px;
  background: rgba(143, 211, 255, .12);
  color: #b9e7ff;
  font-size: 7px;
  font-style: normal;
  font-weight: 800;
  animation: whale-pet-keycap .55s ease-in-out infinite alternate;
}
.whale-pet-keycaps i:nth-child(2) { animation-delay: .12s; }
.whale-pet-keycaps i:nth-child(3) { animation-delay: .24s; }
.whale-pet-root[data-pressed="true"] .whale-pet-art-current { filter: drop-shadow(0 14px 16px rgba(0, 7, 20, .34)) brightness(1.06); }
.whale-pet-root[data-pressed="true"] .whale-pet-input-badge { animation: whale-pet-click-pulse .25s ease; }

.whale-pet-memo-card {
  width: min(560px, calc(100vw - 32px));
  border-color: #C9B78E;
  background:
    linear-gradient(90deg, transparent 0, transparent 39px, rgba(177, 96, 82, .36) 40px, transparent 41px),
    repeating-linear-gradient(180deg, rgba(120, 93, 55, .0) 0, rgba(120, 93, 55, .0) 23px, rgba(120, 93, 55, .18) 24px),
    linear-gradient(160deg, #fbf5e5, #f1e5c9);
  color: #3a2a18;
  box-shadow: 0 26px 70px rgba(0, 0, 0, .5), inset 0 0 38px rgba(112, 80, 34, .08);
}
.whale-pet-memo-card .whale-pet-modal-title { color: #2b2012; }
.whale-pet-memo-card .whale-pet-modal-note { color: #7a674a; }
.whale-pet-memo-card .whale-pet-empty { color: #8a7656; }
.whale-pet-memo-card .whale-pet-btn { border-color: #B8A47D; background: rgba(255, 255, 255, .36); color: #4b3821; }
.whale-pet-memo-card .whale-pet-btn:hover { border-color: #8D7550; background: rgba(255, 255, 255, .62); }
.whale-pet-memo-card .whale-pet-btn.primary { border-color: #4D6BFE; background: #4D6BFE; color: #fff; }
.whale-pet-memo-head { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
.whale-pet-memo-input {
  width: 100%;
  min-height: 92px;
  margin-top: 8px;
  padding: 7px 9px 7px 48px;
  resize: vertical;
  border: 1px solid #CBB990;
  border-radius: 6px;
  outline: none;
  background:
    repeating-linear-gradient(180deg, transparent 0, transparent 23px, rgba(120, 93, 55, .16) 24px),
    rgba(255, 252, 242, .42);
  color: #3a2a18;
  font: 12px/24px "KaiTi", "STKaiti", "Microsoft YaHei", serif;
  caret-color: #7a2f2f;
}
.whale-pet-memo-input::placeholder { color: #9a8766; }
.whale-pet-weather { font-size: var(--whale-weather-font, 9.5px); }
.whale-pet-memo-button { zoom: var(--whale-memo-button-scale, 1); }
.whale-pet-gear { zoom: var(--whale-settings-button-scale, 1); }
.whale-pet-bubble { zoom: var(--whale-bubble-scale, 1); }
.whale-pet-input-badge { zoom: var(--whale-input-scale, 1); }
.whale-pet-settings-modal { overflow: auto; }
.whale-pet-settings-modal, .whale-pet-settings-modal * { box-sizing: border-box; }
.whale-pet-settings-modal button { font: inherit; }
.whale-pet-settings-modal .whale-pet-settings-card { flex: 0 0 auto; margin: auto; }
.whale-pet-settings-card .whale-pet-modal-footer {
  position: sticky;
  bottom: -13px;
  z-index: 2;
  margin-top: 10px;
  padding: 9px 0 4px;
  background: rgba(6, 18, 38, .97);
}
.whale-pet-memo-input:focus { border-color: #8D7550; box-shadow: 0 0 0 2px rgba(141, 117, 80, .12); }
.whale-pet-memo-actions { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-top: 8px; }
.whale-pet-memo-list { display: grid; gap: 6px; max-height: 300px; margin-top: 12px; overflow: auto; padding-right: 2px; }
.whale-pet-memo-empty {
  padding: 20px 12px;
  border: 1px dashed #CBB990;
  border-radius: 7px;
  color: #8a7656;
  font-size: 10px;
  text-align: center;
}
.whale-pet-memo-item {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 7px 9px 7px 7px;
  border: 1px solid rgba(141, 117, 80, .32);
  border-radius: 6px;
  background:
    repeating-linear-gradient(180deg, transparent 0, transparent 23px, rgba(120, 93, 55, .14) 24px),
    rgba(255, 252, 242, .46);
}
.whale-pet-memo-item[data-done="true"] { background: rgba(213, 226, 195, .38); }
.whale-pet-memo-check {
  position: relative;
  flex: 0 0 auto;
  width: 18px;
  height: 18px;
  margin-top: 2px;
  display: grid;
  place-items: center;
  border: 1.5px solid #9d8660;
  border-radius: 4px;
  background: rgba(255, 255, 255, .35);
  color: #4f6b3a;
  cursor: pointer;
}
.whale-pet-memo-check input { position: absolute; inset: 0; width: 100%; height: 100%; opacity: 0; cursor: pointer; }
.whale-pet-memo-check span { font-size: 12px; font-weight: 900; opacity: 0; transform: scale(.5); transition: opacity .12s ease, transform .12s ease; }
.whale-pet-memo-check input:checked + span { opacity: 1; transform: scale(1); }
.whale-pet-memo-check input:focus-visible + span { outline: 2px solid #4d6bfe; outline-offset: 2px; }
.whale-pet-memo-body { min-width: 0; flex: 1; }
.whale-pet-memo-text { color: #3a2a18; font-size: 11px; line-height: 24px; white-space: pre-wrap; word-break: break-word; font-family: "KaiTi", "STKaiti", "Microsoft YaHei", serif; }
.whale-pet-memo-item[data-done="true"] .whale-pet-memo-text { color: #6c7a58; text-decoration: line-through; }
.whale-pet-memo-time { margin-top: 1px; color: #8a7656; font-size: 8px; }
.whale-pet-memo-delete {
  flex: 0 0 auto;
  width: 22px;
  height: 22px;
  padding: 0;
  border: 1px solid rgba(141, 117, 80, .28);
  border-radius: 6px;
  background: transparent;
  color: #7d6847;
  cursor: pointer;
  font-size: 14px;
  line-height: 1;
}
.whale-pet-memo-delete:hover { border-color: #a94b4b; color: #a94b4b; }
.whale-pet-memo-delete:focus-visible { outline: 2px solid #4d6bfe; outline-offset: 2px; }

.whale-pet-form-alert {
  margin: 0 0 10px;
  padding: 8px 11px;
  border: 1px solid rgba(255, 154, 152, .42);
  border-radius: 12px 12px 4px 12px;
  background: linear-gradient(160deg, #fff6ee, #ffe6df);
  box-shadow: 0 8px 20px rgba(0, 0, 0, .2);
  color: #8a2e2e;
  font-size: 10.5px;
  font-weight: 650;
  line-height: 1.5;
  animation: whale-pet-bubble-in .22s ease both;
}

.whale-pet-gear {
  position: absolute;
  top: 8px;
  right: 8px;
  z-index: 5;
  width: 28px;
  height: 28px;
  display: grid;
  place-items: center;
  padding: 0;
  border: 1px solid rgba(143, 211, 255, .26);
  border-radius: 10px;
  background: rgba(6, 20, 42, .62);
  color: #cfeaff;
  font-size: 14px;
  line-height: 1;
  cursor: pointer;
  opacity: .42;
  pointer-events: auto;
  transition: opacity .18s ease, background .18s ease, transform .18s ease;
  backdrop-filter: blur(8px);
}
.whale-pet-stage:hover .whale-pet-gear { opacity: 1; }
.whale-pet-gear:hover { background: rgba(20, 50, 90, .88); transform: rotate(16deg); }
.whale-pet-gear:focus-visible { outline: 2px solid #8fd3ff; outline-offset: 2px; opacity: 1; }

.whale-pet-bubble {
  position: absolute;
  right: 10px;
  bottom: calc(100% - 4px);
  z-index: 4;
  width: max-content;
  max-width: 260px;
  padding: 9px 12px;
  border: 1px solid rgba(11, 27, 58, .12);
  border-radius: 14px 14px 4px 14px;
  background: linear-gradient(160deg, #fffaf0, #f4ecdb);
  box-shadow: 0 10px 26px rgba(0, 7, 20, .28);
  color: #0b1b3a;
  font-size: 12px;
  font-weight: 650;
  line-height: 1.55;
  pointer-events: none;
  transform-origin: 90% 100%;
  animation: whale-pet-bubble-in .3s cubic-bezier(.2, .9, .2, 1.15);
}
.whale-pet-bubble::after {
  content: "";
  position: absolute;
  right: 13px;
  bottom: -7px;
  width: 13px;
  height: 13px;
  background: #f4ecdb;
  border-right: 1px solid rgba(11, 27, 58, .12);
  border-bottom: 1px solid rgba(11, 27, 58, .12);
  transform: rotate(45deg);
  border-radius: 0 0 3px 0;
}
.whale-pet-bubble[data-kind="greeting"] { background: linear-gradient(160deg, #eefaff, #dceeff); }
.whale-pet-bubble[data-kind="greeting"]::after { background: #dceeff; }
.whale-pet-bubble[data-kind="proactive"] { background: linear-gradient(160deg, #f0fff8, #d9f5e7); }
.whale-pet-bubble[data-kind="proactive"]::after { background: #d9f5e7; }
.whale-pet-bubble[data-kind="system"] { background: linear-gradient(160deg, #f5f0ff, #e4dcff); }
.whale-pet-bubble[data-kind="system"]::after { background: #e4dcff; }

.whale-pet-summary {
  pointer-events: auto;
  position: relative;
  width: 360px;
  height: 40px;
  display: flex;
  align-items: center;
  gap: 7px;
  padding: 0 12px;
  overflow: hidden;
  border: 1px solid rgba(143, 211, 255, .38);
  border-radius: 12px;
  background:
    radial-gradient(120% 100% at 100% 0%, rgba(77, 107, 254, .3), transparent 60%),
    linear-gradient(150deg, rgba(10, 30, 58, .98), rgba(5, 16, 35, .97));
  box-shadow: 0 10px 28px rgba(0, 7, 20, .32), inset 0 1px 0 rgba(255, 255, 255, .08);
  color: #eaf6ff;
  cursor: pointer;
  text-align: left;
  backdrop-filter: blur(14px);
}
.whale-pet-summary:hover { border-color: rgba(143, 211, 255, .5); }
.whale-pet-summary:focus-visible { outline: 2px solid #8fd3ff; outline-offset: 2px; }
.whale-pet-dot {
  width: 6px;
  height: 6px;
  flex: 0 0 auto;
  border-radius: 50%;
  background: #58e0b4;
  box-shadow: 0 0 0 3px rgba(88, 224, 180, .12);
}
.whale-pet-dot[data-running="false"] { background: #7c93aa; box-shadow: 0 0 0 3px rgba(124, 147, 170, .12); }
.whale-pet-brand {
  flex: 0 0 auto;
  color: #8fd3ff;
  font-size: 9.5px;
  font-weight: 850;
  letter-spacing: .16em;
}
.whale-pet-track {
  min-width: 0;
  flex: 1;
  overflow: hidden;
  color: #b8d3e7;
  font-size: 10px;
  line-height: 1;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.whale-pet-stat { white-space: nowrap; }
.whale-pet-stat .label { color: #8fb0c9; font-size: 9px; letter-spacing: .04em; }
.whale-pet-stat b { color: #f3faff; font-size: 10.5px; font-weight: 750; font-variant-numeric: tabular-nums; }
.whale-pet-sep { padding: 0 4px; color: #3d5a74; }
.whale-pet-chevron { flex: 0 0 auto; color: #7fa5c5; font-size: 10px; }

.whale-pet-details {
  pointer-events: auto;
  width: 360px;
  max-height: 236px;
  overflow: auto;
  padding: 11px 12px 10px;
  border: 1px solid rgba(143, 211, 255, .24);
  border-radius: 12px;
  background:
    radial-gradient(120% 90% at 100% 0%, rgba(77, 107, 254, .2), transparent 58%),
    linear-gradient(150deg, rgba(9, 27, 54, .96), rgba(5, 16, 35, .94));
  box-shadow: 0 14px 32px rgba(0, 7, 20, .35), inset 0 1px 0 rgba(255, 255, 255, .06);
  color: #a9c6de;
  font-size: 9px;
  backdrop-filter: blur(14px);
  scrollbar-width: thin;
  scrollbar-color: rgba(143, 211, 255, .3) transparent;
}
.whale-pet-detail-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 7px;
  color: #dff2ff;
  font-size: 10px;
  font-weight: 750;
  letter-spacing: .04em;
}
.whale-pet-detail-actions { display: flex; gap: 5px; }
.whale-pet-mini {
  padding: 3px 7px;
  border: 1px solid rgba(143, 211, 255, .2);
  border-radius: 6px;
  background: transparent;
  color: #9ac8e8;
  font-size: 8px;
  cursor: pointer;
}
.whale-pet-mini:hover { border-color: rgba(143, 211, 255, .42); color: #dff2ff; }
.whale-pet-mini:focus-visible { outline: 2px solid #8fd3ff; outline-offset: 2px; }
.whale-pet-detail-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 5px;
  margin-bottom: 7px;
}
.whale-pet-detail-cell {
  min-width: 0;
  padding: 6px 7px;
  border: 1px solid rgba(143, 211, 255, .11);
  border-radius: 8px;
  background: rgba(143, 211, 255, .05);
}
.whale-pet-detail-cell span { display: block; color: #6f8ca6; font-size: 8px; }
.whale-pet-detail-cell strong { display: block; margin-top: 2px; color: #dff2ff; font-size: 12px; font-weight: 750; font-variant-numeric: tabular-nums; }
.whale-pet-detail-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 2px 0;
}
.whale-pet-detail-row strong { max-width: 72%; color: #dff2ff; font-weight: 700; font-variant-numeric: tabular-nums; text-align: right; white-space: normal; }
.whale-pet-skills { display: flex; flex-wrap: wrap; gap: 3px; margin-top: 6px; }
.whale-pet-skill {
  max-width: 100%;
  overflow: hidden;
  padding: 2px 5px;
  border: 1px solid rgba(143, 211, 255, .14);
  border-radius: 5px;
  background: rgba(143, 211, 255, .06);
  color: #b9d8ee;
  font-size: 8px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.whale-pet-empty { color: #6f8ca6; font-size: 8.5px; }
.whale-pet-agent {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid rgba(143, 211, 255, .13);
}
.whale-pet-agent .whale-pet-dot { width: 6px; height: 6px; }
.whale-pet-agent-name { color: #dff2ff; font-size: 9px; font-weight: 750; letter-spacing: .08em; text-transform: uppercase; }
.whale-pet-agent-state { margin-left: auto; color: #7fa5c5; font-size: 8px; }
.whale-pet-agent-child { padding: 2px 5px; border: 1px solid rgba(143, 211, 255, .18); border-radius: 99px; color: #b9e7ff; font-size: 8px; }

.whale-pet-modal {
  position: fixed;
  inset: 0;
  z-index: 2147483000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 18px;
  background: rgba(2, 8, 20, .58);
  pointer-events: auto;
  backdrop-filter: blur(5px);
}
.whale-pet-modal-card {
  width: min(560px, calc(100vw - 32px));
  max-height: calc(100vh - 36px);
  overflow: auto;
  padding: 16px 17px 13px;
  border: 1px solid rgba(143, 211, 255, .3);
  border-radius: 16px;
  background: linear-gradient(150deg, #0a1b32, #061226);
  box-shadow: 0 26px 70px rgba(0, 0, 0, .5);
  color: #dff2ff;
  font-size: 11px;
}
.whale-pet-modal-card.whale-pet-memo-card {
  --whale-memo-line: calc(var(--whale-memo-font, 12px) + 12px);
  border-color: #C9B78E;
  background:
    linear-gradient(90deg, transparent 0, transparent 39px, rgba(177, 96, 82, .36) 40px, transparent 41px),
    repeating-linear-gradient(180deg, rgba(120, 93, 55, .0) 0, rgba(120, 93, 55, .0) calc(var(--whale-memo-line) - 1px), rgba(120, 93, 55, .18) var(--whale-memo-line)),
    linear-gradient(160deg, #fbf5e5, #f1e5c9);
  color: #3a2a18;
}
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-modal-title { color: #2b2012; }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-modal-note { color: #7a674a; }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-empty { color: #8a7656; }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-memo-text { color: #3a2a18; }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-memo-input { color: #3a2a18; }
.whale-pet-modal-title { margin: 0 0 4px; color: #f3faff; font-size: 15px; font-weight: 750; }
.whale-pet-modal-note { margin: 0 0 12px; color: #7fa5c5; font-size: 10px; line-height: 1.5; }
.whale-pet-form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px 10px; }
.whale-pet-field { display: grid; gap: 4px; min-width: 0; }
.whale-pet-field > span { overflow-wrap: anywhere; }
.whale-pet-field.full { grid-column: 1 / -1; }
.whale-pet-field > span { color: #9ab6ce; font-size: 9px; }
.whale-pet-field input[type="number"], .whale-pet-field input[type="text"], .whale-pet-field textarea {
  width: 100%;
  min-width: 0;
  max-width: 100%;
  border: 1px solid rgba(143, 211, 255, .2);
  border-radius: 8px;
  background: rgba(143, 211, 255, .05);
  color: #eaf6ff;
  font: 11px/1.45 "Consolas", "Microsoft YaHei", monospace;
  outline: none;
}
.whale-pet-field input[type="number"], .whale-pet-field input[type="text"] { height: 30px; padding: 0 8px; }
.whale-pet-field textarea { min-height: 58px; padding: 6px 8px; resize: vertical; }
.whale-pet-field input:focus, .whale-pet-field textarea:focus { border-color: rgba(143, 211, 255, .55); }
.whale-pet-check { display: flex; align-items: center; gap: 6px; color: #b9d8ee; font-size: 10px; }
.whale-pet-check input { accent-color: #4d6bfe; }
.whale-pet-modal-error { margin-top: 8px; color: #ff9a98; font-size: 10px; }
.whale-pet-modal-footer { display: flex; align-items: center; justify-content: flex-end; gap: 7px; margin-top: 12px; }
.whale-pet-btn {
  height: 30px;
  padding: 0 12px;
  border: 1px solid rgba(143, 211, 255, .22);
  border-radius: 8px;
  background: rgba(143, 211, 255, .06);
  color: #cfeaff;
  font-size: 10px;
  cursor: pointer;
}
.whale-pet-btn:hover { border-color: rgba(143, 211, 255, .48); background: rgba(143, 211, 255, .12); }
.whale-pet-btn.primary { border-color: transparent; background: linear-gradient(135deg, #4d6bfe, #2f50d8); color: #fff; }
.whale-pet-btn.primary:hover { filter: brightness(1.08); }
.whale-pet-btn:focus-visible { outline: 2px solid #8fd3ff; outline-offset: 2px; }
.whale-pet-reset { margin-right: auto; color: #8fb4d1; }

@keyframes whale-pet-bubble-in {
  0% { opacity: 0; transform: translateY(7px) scale(.9); }
  100% { opacity: 1; transform: translateY(0) scale(1); }
}
@keyframes whale-pet-input-in {
  0% { opacity: 0; transform: translateY(-4px) scale(.94); }
  100% { opacity: 1; transform: translateY(0) scale(1); }
}
@keyframes whale-pet-keycap {
  0% { transform: translateY(0); background: rgba(143, 211, 255, .1); }
  100% { transform: translateY(-2px); background: rgba(143, 211, 255, .28); }
}
@keyframes whale-pet-click-pulse {
  0% { box-shadow: 0 0 0 0 rgba(255, 179, 92, .45); }
  100% { box-shadow: 0 0 0 8px rgba(255, 179, 92, 0); }
}

/* User appearance settings: theme accents, per-area text colors and font sizes. */
.whale-pet-summary { border-color: color-mix(in srgb, var(--whale-theme, #4d6bfe) 58%, transparent); color: var(--whale-status-text, #eaf6ff); }
.whale-pet-brand { color: var(--whale-theme, #4d6bfe); }
.whale-pet-track { color: var(--whale-status-text, #eaf6ff); }
.whale-pet-stat .label { color: var(--whale-status-text, #eaf6ff); opacity: .68; }
.whale-pet-stat b { color: var(--whale-status-text, #eaf6ff); }
.whale-pet-chevron { color: var(--whale-status-text, #eaf6ff); opacity: .7; }
.whale-pet-summary { font-size: var(--whale-status-font, 10px); }
.whale-pet-brand { font-size: calc(var(--whale-status-font, 10px) - .5px); }
.whale-pet-track { font-size: var(--whale-status-font, 10px); }
.whale-pet-stat .label { font-size: calc(var(--whale-status-font, 10px) - 1px); }
.whale-pet-stat b { font-size: calc(var(--whale-status-font, 10px) + .5px); }
.whale-pet-details { color: var(--whale-status-text, #eaf6ff); font-size: calc(var(--whale-status-font, 10px) - .5px); }
.whale-pet-detail-row strong, .whale-pet-detail-cell strong, .whale-pet-agent-name { color: var(--whale-status-text, #eaf6ff); }
.whale-pet-detail-cell span, .whale-pet-agent-state, .whale-pet-empty { color: var(--whale-status-text, #eaf6ff); opacity: .7; }
.whale-pet-detail-head { color: var(--whale-status-text, #eaf6ff); }
.whale-pet-detail-head .whale-pet-mini { color: var(--whale-status-text, #eaf6ff); }
.whale-pet-summary:hover { border-color: var(--whale-theme, #4d6bfe); }
.whale-pet-mini:hover { border-color: var(--whale-theme, #4d6bfe); }
.whale-pet-toggle:hover, .whale-pet-refresh:hover { border-color: var(--whale-theme, #4d6bfe); }
.whale-pet-gear:hover, .whale-pet-memo-button:hover { border-color: var(--whale-theme, #4d6bfe); }
.whale-pet-detail-cell { border-color: color-mix(in srgb, var(--whale-theme, #4d6bfe) 24%, transparent); }
.whale-pet-btn.primary { background: var(--whale-theme, #4d6bfe); border-color: var(--whale-theme, #4d6bfe); }
.whale-pet-memo-check { border-color: var(--whale-theme, #4d6bfe); }
.whale-pet-modal-card { color: var(--whale-settings-text, #dff2ff); font-size: var(--whale-settings-font, 11px); }
.whale-pet-modal-card .whale-pet-modal-title { color: var(--whale-settings-text, #dff2ff); font-size: calc(var(--whale-settings-font, 11px) + 4px); }
.whale-pet-modal-card .whale-pet-modal-note, .whale-pet-modal-card .whale-pet-field > span, .whale-pet-modal-card .whale-pet-check { color: var(--whale-settings-text, #dff2ff); font-size: var(--whale-settings-font, 11px); }
.whale-pet-modal-card .whale-pet-field input[type="number"], .whale-pet-modal-card .whale-pet-field input[type="text"], .whale-pet-modal-card .whale-pet-field textarea, .whale-pet-modal-card .whale-pet-field input[type="color"] { color: var(--whale-settings-text, #dff2ff); font-size: var(--whale-settings-font, 11px); }
.whale-pet-modal-card .whale-pet-btn, .whale-pet-modal-card .whale-pet-mini { font-size: var(--whale-settings-font, 11px); }
.whale-pet-modal-card.whale-pet-memo-card { color: var(--whale-memo-text, #3a2a18); font-size: var(--whale-memo-font, 12px); }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-modal-title { color: var(--whale-memo-text, #3a2a18); font-size: calc(var(--whale-memo-font, 12px) + 4px); }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-modal-note, .whale-pet-modal-card.whale-pet-memo-card .whale-pet-memo-time, .whale-pet-modal-card.whale-pet-memo-card .whale-pet-empty { color: var(--whale-memo-text, #3a2a18); opacity: .72; font-size: calc(var(--whale-memo-font, 12px) - 2px); }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-memo-text, .whale-pet-modal-card.whale-pet-memo-card .whale-pet-memo-input { color: var(--whale-memo-text, #3a2a18); font-size: var(--whale-memo-font, 12px); }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-memo-text { line-height: var(--whale-memo-line); }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-memo-input { line-height: var(--whale-memo-line); background: repeating-linear-gradient(180deg, transparent 0, transparent calc(var(--whale-memo-line) - 1px), rgba(120, 93, 55, .16) var(--whale-memo-line)), rgba(255, 252, 242, .42); }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-memo-item { background: repeating-linear-gradient(180deg, transparent 0, transparent calc(var(--whale-memo-line) - 1px), rgba(120, 93, 55, .14) var(--whale-memo-line)), rgba(255, 252, 242, .46); }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-btn { color: var(--whale-memo-text, #3a2a18); font-size: var(--whale-memo-font, 12px); }
.whale-pet-modal-card.whale-pet-memo-card .whale-pet-btn.primary { color: #fff; }
.whale-pet-field input[type="color"] { width: 100%; min-width: 0; max-width: 100%; height: 30px; padding: 2px 4px; cursor: pointer; }

@media (max-width: 620px) {
  .whale-pet-root { right: 10px; bottom: 10px; }
  .whale-pet-stage { --whale-pet-stage-w: 280px; --whale-pet-stage-h: 280px; }
  .whale-pet-summary, .whale-pet-details { width: 280px; }
  .whale-pet-summary { height: 36px; padding: 0 9px; gap: 5px; }
  .whale-pet-track { font-size: 8.5px; }
  .whale-pet-brand { font-size: 8px; }
}
@media (max-width: 420px) {
  .whale-pet-stage { --whale-pet-stage-w: 230px; --whale-pet-stage-h: 236px; }
  .whale-pet-summary, .whale-pet-details { width: 230px; }
  .whale-pet-stat:nth-of-type(n+5) { display: none; }
  .whale-pet-detail-grid { grid-template-columns: 1fr 1fr; }
}
@media (max-height: 700px) {
  .whale-pet-stage { height: 270px; }
  .whale-pet-details { max-height: 180px; }
}
@media (prefers-reduced-motion: reduce) {
  .whale-pet-root *, .whale-pet-root *::before, .whale-pet-root *::after {
    animation: none !important;
    transition: none !important;
  }
}
`

    const COPY = {
      kicker: 'DSH · DEEP SEA',
      context: '上下文',
      tokens: 'TOKEN',
      skills: '技能',
      system: '系统',
      tools: '工具',
      messages: '消息',
      input: '输入',
      output: '输出',
      cacheRead: '缓存读',
      cacheWrite: '缓存写',
      breakdown: '上下文构成',
      refresh: '刷新技能',
      settings: '设置',
      close: '关闭',
      save: '保存',
      cancel: '取消',
      reset: '恢复默认',
      statusVisible: '显示状态栏',
      greetingEnabled: '启动时打招呼',
      idleMinutes: '空闲判定（分钟）',
      playSeconds: '互动形象持续（秒）',
      transitionMs: '切换动画（毫秒）',
      mainImages: '主形象图片（每行一个）',
      idleImages: '空闲形象图片（每行一个）',
      playImages: '互动形象图片（每行一个）',
      pathNote: 'Web 版支持插件 assets 路径或 HTTPS 图片 URL；本地绝对路径请使用独立桌面版。每种状态至少保留 1 张图片。',
      invalidLists: '主形象、空闲形象、互动形象都至少需要 1 张图片。',
      skillsEmpty: '当前会话没有可用技能',
      skillsLoading: '读取中',
      skillsUnavailable: '不可用',
      enabled: '已启用',
      cached: '缓存',
      idle: '待机',
      running: '运行中',
      childAgents: '个子代理',
      aria: '蓝色鲸鱼娘桌宠，点击互动',
      settingsAria: '打开桌宠设置',
      phrases: [
        '今天也要好好写代码呀。',
        '我在看着你敲键盘哦。',
        '上下文还宽裕，放心游。',
        '要不要先整理一下思路？',
        '摸摸头，继续加油。',
        '深海里也有星光。',
        '又写下一个漂亮的函数！',
        '累了就看看海吧。',
      ],
    }

    function assetUrl(name) {
      try {
        return new URL(ASSET_BASE + name, document.baseURI).href
      } catch {
        return ASSET_BASE + name
      }
    }

    function defaultSettings() {
      return {
        showStatus: true,
        greetingEnabled: true,
        weatherEnabled: true,
        weatherCity: '',
        scale: 1,
        characterScale: 1,
        statusFontSize: 10,
        settingsFontSize: 11,
        memoFontSize: 12,
        themeColor: '#4d6bfe',
        statusTextColor: '#eaf6ff',
        settingsTextColor: '#dff2ff',
        memoTextColor: '#3a2a18',
        settingsUiScale: 1,
        settingsButtonScale: 1,
        memoButtonScale: 1,
        speechBubbleScale: 1,
        inputBadgeScale: 1,
        weatherFontSize: 9.5,
        idleMinutes: 5,
        playSeconds: 30,
        chatIdleMinutes: 3,
        transitionMs: 420,
        phrases: COPY.phrases.slice(),
        chatPhrases: [
          '还在忙吗？要不要休息一下。',
          '鲸鱼娘在这里，随时可以叫我。',
          '记得喝口水，活动一下肩膀。',
          '需要我陪你理一下思路吗？',
        ],
        mainImages: [assetUrl('main.webp')],
        idleImages: [assetUrl('sleep.webp')],
        playImages: [
          assetUrl('play1.webp'),
          assetUrl('play2.webp'),
          assetUrl('play3.webp'),
          assetUrl('play4.webp'),
        ],
      }
    }

    function numberOr(value, fallback, min, max) {
      const number = typeof value === 'number' ? value : Number(value)
      if (!Number.isFinite(number)) return fallback
      return Math.max(min, Math.min(max, Math.round(number)))
    }

    function decimalOr(value, fallback, min, max) {
      const number = typeof value === 'number' ? value : Number(value)
      if (!Number.isFinite(number)) return fallback
      return Math.max(min, Math.min(max, Math.round(number * 100) / 100))
    }

    function colorOr(value, fallback) {
      if (typeof value !== 'string') return fallback
      const color = value.trim()
      return /^#[0-9a-f]{6}$/i.test(color) ? color : fallback
    }

    function cleanList(value, fallback) {
      if (!Array.isArray(value)) return fallback.slice()
      const seen = new Set()
      const cleaned = []
      for (const item of value) {
        if (typeof item !== 'string') continue
        const text = item.trim()
        if (!text || seen.has(text)) continue
        seen.add(text)
        cleaned.push(text)
        if (cleaned.length >= 24) break
      }
      return cleaned.length > 0 ? cleaned : fallback.slice()
    }

    function normalizeSettings(value, defaults) {
      const source = value && typeof value === 'object' ? value : {}
      return {
        showStatus: source.showStatus !== false,
        greetingEnabled: source.greetingEnabled !== false,
        weatherEnabled: source.weatherEnabled !== false,
        weatherCity: typeof source.weatherCity === 'string' ? source.weatherCity.trim().slice(0, 60) : '',
        scale: decimalOr(source.scale, defaults.scale, 0.1, 1.5),
        characterScale: decimalOr(source.characterScale, defaults.characterScale, 0.1, 1.5),
        statusFontSize: numberOr(source.statusFontSize, defaults.statusFontSize, 8, 32),
        settingsFontSize: numberOr(source.settingsFontSize, defaults.settingsFontSize, 8, 32),
        memoFontSize: numberOr(source.memoFontSize, defaults.memoFontSize, 8, 32),
        themeColor: colorOr(source.themeColor, defaults.themeColor),
        statusTextColor: colorOr(source.statusTextColor, defaults.statusTextColor),
        settingsTextColor: colorOr(source.settingsTextColor, defaults.settingsTextColor),
        memoTextColor: colorOr(source.memoTextColor, defaults.memoTextColor),
        settingsUiScale: numberOr(source.settingsUiScale, defaults.settingsUiScale, 0.5, 2),
        settingsButtonScale: numberOr(source.settingsButtonScale, defaults.settingsButtonScale, 0.5, 2),
        memoButtonScale: numberOr(source.memoButtonScale, defaults.memoButtonScale, 0.5, 2),
        speechBubbleScale: numberOr(source.speechBubbleScale, defaults.speechBubbleScale, 0.5, 2),
        inputBadgeScale: numberOr(source.inputBadgeScale, defaults.inputBadgeScale, 0.5, 2),
        weatherFontSize: numberOr(source.weatherFontSize, defaults.weatherFontSize, 8, 32),
        idleMinutes: numberOr(source.idleMinutes, defaults.idleMinutes, 1, 240),
        playSeconds: numberOr(source.playSeconds, defaults.playSeconds, 5, 600),
        chatIdleMinutes: numberOr(source.chatIdleMinutes, defaults.chatIdleMinutes, 0, 240),
        transitionMs: numberOr(source.transitionMs, defaults.transitionMs, 120, 2000),
        phrases: cleanList(source.phrases, defaults.phrases),
        chatPhrases: cleanList(source.chatPhrases, defaults.chatPhrases),
        mainImages: cleanList(source.mainImages, defaults.mainImages),
        idleImages: cleanList(source.idleImages, defaults.idleImages),
        playImages: cleanList(source.playImages, defaults.playImages),
      }
    }

    function loadSettings(defaults) {
      try {
        const raw = window.localStorage.getItem(STORAGE_KEY)
        return normalizeSettings(raw ? JSON.parse(raw) : null, defaults)
      } catch {
        return normalizeSettings(null, defaults)
      }
    }

    function persistSettings(settings) {
      try {
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(settings))
      } catch {
        // Storage can be unavailable in hardened browser profiles; the live session still works.
      }
    }

    function loadMemos() {
      try {
        const raw = window.localStorage.getItem(MEMO_KEY)
        const value = raw ? JSON.parse(raw) : []
        if (!Array.isArray(value)) return []
        return value
          .filter((memo) => memo && typeof memo.text === 'string' && memo.text.trim())
          .slice(0, 100)
          .map((memo) => ({
            id: typeof memo.id === 'string' ? memo.id : String(Date.now() + Math.random()),
            text: memo.text.trim().slice(0, 2000),
            time: typeof memo.time === 'number' ? memo.time : Date.now(),
            done: memo.done === true,
          }))
      } catch {
        return []
      }
    }

    function persistMemos(memos) {
      try {
        window.localStorage.setItem(MEMO_KEY, JSON.stringify(memos))
      } catch {
        // Memo state remains live when storage is unavailable.
      }
    }

    function greetingText(date) {
      const hour = date.getHours()
      if (hour < 5) return '夜深了，鲸鱼娘陪你守一会儿。'
      if (hour < 9) return '早上好，今天想先做哪件事？'
      if (hour < 12) return '上午好，慢慢来，海会等你。'
      if (hour < 14) return '中午好，记得休息一下眼睛。'
      if (hour < 18) return '下午好，继续写点漂亮的东西吧。'
      if (hour < 23) return '晚上好，今天也辛苦了。'
      return '夜深了，别熬太久哦。'
    }

    function pickImage(list, current) {
      if (!Array.isArray(list) || list.length === 0) return null
      if (list.length === 1) return list[0]
      const choices = list.filter((item) => item !== current)
      return choices[Math.floor(Math.random() * choices.length)] || list[0]
    }

    function shorten(text, maxLength) {
      const value = String(text || '').replace(/\s+/g, ' ').trim()
      return value.length > maxLength ? value.slice(0, maxLength - 1) + '…' : value
    }

    function formatCompact(value) {
      if (value === null || value === undefined) return '—'
      const absolute = Math.abs(value)
      if (absolute >= 1000000) {
        const scaled = value / 1000000
        return (Math.abs(scaled) >= 10 ? Math.round(scaled) : Math.round(scaled * 10) / 10) + 'M'
      }
      if (absolute >= 1000) {
        const scaled = value / 1000
        return (Math.abs(scaled) >= 100 ? Math.round(scaled) : Math.round(scaled * 10) / 10) + 'k'
      }
      return String(Math.round(value))
    }

    function firstNumber() {
      for (let index = 0; index < arguments.length; index += 1) {
        const value = arguments[index]
        if (typeof value === 'number' && Number.isFinite(value)) return value
      }
      return null
    }

    function normalizeContext(value) {
      if (!value || typeof value !== 'object') return { pressure: null, windowSize: null, pct: null }
      const pressure = firstNumber(value.pressureTokens, value.projectedTokens, value.totalTokens)
      const windowSize = firstNumber(value.contextWindow, value.capacity)
      const pct = pressure !== null && windowSize !== null && windowSize > 0
        ? Math.max(0, Math.min(100, (pressure / windowSize) * 100))
        : null
      return { pressure, windowSize, pct }
    }

    function normalizeTokens(value) {
      if (!value || typeof value !== 'object') {
        return { total: null, input: null, output: null, cacheRead: null, cacheWrite: null, cachePct: null }
      }
      const input = firstNumber(value.uncachedInputTokens, value.inputTokens)
      const output = firstNumber(value.outputTokens)
      const cacheRead = firstNumber(value.cacheReadTokens)
      const cacheWrite = firstNumber(value.cacheWriteTokens)
      const parts = [input, output, cacheRead, cacheWrite].filter((part) => part !== null)
      const summed = parts.length > 0 ? parts.reduce((total, part) => total + part, 0) : null
      const total = firstNumber(value.totalTokens)
      const hitBase = (input || 0) + (cacheRead || 0)
      const cachePct = cacheRead !== null && hitBase > 0 ? Math.round((cacheRead / hitBase) * 100) : null
      return { total: total !== null ? total : summed, input, output, cacheRead, cacheWrite, cachePct }
    }

    function normalizeBreakdown(value) {
      if (!value || typeof value !== 'object') return { system: null, tools: null, messages: null }
      return {
        system: firstNumber(value.systemTokens),
        tools: firstNumber(value.toolsTokens),
        messages: firstNumber(value.messageTokens),
      }
    }

    function normalizePreset(value) {
      if (typeof value === 'string' && value) return value
      if (value && typeof value === 'object') {
        for (const key of ['agentPreset', 'preset', 'id', 'name']) {
          if (typeof value[key] === 'string' && value[key]) return value[key]
        }
      }
      return null
    }

    function normalizeSubagents(value) {
      if (Array.isArray(value)) return value
      if (!value || typeof value !== 'object') return []
      if (Array.isArray(value.items)) return value.items
      if (Array.isArray(value.children)) return value.children
      if (Array.isArray(value.rows)) return value.rows
      if (Array.isArray(value.entries)) return value.entries
      return []
    }

    function prettyPreset(preset) {
      if (!preset) return 'DEFAULT'
      return preset.replace(/^@/, '').replace(/[-_]+/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 22).toUpperCase()
    }

    function unwrapRemote(result) {
      if (result && typeof result === 'object' && 'ok' in result) {
        if (result.ok === false) throw new Error('Remote skills request failed')
        if (result.value !== undefined) return result.value
      }
      return result
    }

    function useSkills(ctx, sessionId, nonce) {
      const [state, setState] = React.useState({ status: 'loading', items: [] })
      React.useEffect(() => {
        let alive = true
        const controller = new AbortController()
        const remote = ctx.get('remote')
        const skills = remote && remote.skills
        const method = skills && skills.list
        if (typeof method !== 'function') {
          setState({ status: 'unavailable', items: [] })
          return () => { alive = false; controller.abort() }
        }
        setState((previous) => previous.status === 'ready' ? previous : { status: 'loading', items: [] })
        Promise.resolve()
          .then(() => method.call(skills, { sessionId }, controller.signal))
          .then((result) => {
            if (!alive) return
            const value = unwrapRemote(result)
            const items = Array.isArray(value) ? value : value && Array.isArray(value.skills) ? value.skills : []
            setState({ status: 'ready', items })
          })
          .catch(() => {
            if (!alive || controller.signal.aborted) return
            setState({ status: 'unavailable', items: [] })
          })
        return () => { alive = false; controller.abort() }
      }, [ctx, sessionId, nonce])
      return state
    }

    function weatherDescription(code) {
      if (code === 0) return '晴'
      if (code === 1) return '大部晴朗'
      if (code === 2) return '多云'
      if (code === 3) return '阴'
      if (code === 45 || code === 48) return '有雾'
      if (code >= 51 && code <= 57) return '毛毛雨'
      if (code >= 61 && code <= 67) return '雨'
      if (code >= 71 && code <= 77) return '雪'
      if (code >= 80 && code <= 82) return '阵雨'
      if (code >= 85 && code <= 86) return '阵雪'
      if (code >= 95) return '雷雨'
      return '天气变化'
    }

    function weatherIcon(code, isDay) {
      if (code === 0) return isDay ? '☀' : '☾'
      if (code === 1 || code === 2) return isDay ? '🌤' : '☁'
      if (code === 3 || code === 45 || code === 48) return '☁'
      if (code >= 51 && code <= 67) return '🌦'
      if (code >= 71 && code <= 86) return '❄'
      if (code >= 95) return '⛈'
      return '🌤'
    }

    function beaufort(speed) {
      const limits = [1, 5, 11, 19, 28, 38, 49, 61, 74, 88, 102, 117]
      for (let index = 0; index < limits.length; index += 1) {
        if (speed < limits[index]) return index
      }
      return 12
    }

    function windDirection(degrees) {
      const names = ['北', '东北', '东', '东南', '南', '西南', '西', '西北']
      return names[Math.round((((degrees % 360) + 360) % 360) / 45) % 8]
    }

    function useWeather(enabled, city) {
      const [weather, setWeather] = React.useState(null)
      React.useEffect(() => {
        if (!enabled) {
          setWeather(null)
          return undefined
        }
        let alive = true
        let timer = 0
        let controller = null

        async function load() {
          if (typeof navigator !== 'undefined' && navigator.onLine === false) return
          controller?.abort()
          controller = new AbortController()
          const abortTimer = window.setTimeout(() => controller.abort(), 9000)
          try {
            let location = null
            if (city && city.trim()) {
              try {
                const response = await fetch(
                  'https://geocoding-api.open-meteo.com/v1/search?name=' + encodeURIComponent(city.trim()) + '&count=1&language=zh&format=json',
                  { signal: controller.signal }
                )
                const data = await response.json()
                const hit = data && Array.isArray(data.results) ? data.results[0] : null
                if (hit && typeof hit.latitude === 'number' && typeof hit.longitude === 'number') {
                  location = {
                    latitude: hit.latitude,
                    longitude: hit.longitude,
                    place: hit.name || hit.admin1 || city.trim(),
                  }
                }
              } catch {
                // Fall back to IP if the city cannot be resolved.
              }
            }
            if (!location) {
              try {
                const response = await fetch('https://ipwho.is/', { signal: controller.signal })
                const data = await response.json()
                if (data && data.success && typeof data.latitude === 'number' && typeof data.longitude === 'number') {
                  location = {
                    latitude: data.latitude,
                    longitude: data.longitude,
                    place: data.city || data.region || '本地',
                  }
                }
              } catch {
                // Try the secondary locator below.
              }
            }
            if (!location) {
              const response = await fetch('https://ipapi.co/json/', { signal: controller.signal })
              const data = await response.json()
              if (typeof data.latitude === 'number' && typeof data.longitude === 'number') {
                location = { latitude: data.latitude, longitude: data.longitude, place: data.city || '本地' }
              }
            }
            if (!location || !alive) return
            const url = 'https://api.open-meteo.com/v1/forecast'
              + '?latitude=' + location.latitude
              + '&longitude=' + location.longitude
              + '&current=temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,rain,showers,snowfall,weather_code,wind_speed_10m,wind_direction_10m,is_day'
              + '&timezone=auto'
            const response = await fetch(url, { signal: controller.signal })
            const data = await response.json()
            if (!alive || !data || !data.current) return
            const current = data.current
            const snow = Number(current.snowfall) || 0
            const rain = (Number(current.rain) || 0) + (Number(current.showers) || 0)
            const precipitation = Number(current.precipitation) || 0
            setWeather({
              place: location.place,
              temperature: Number(current.temperature_2m) || 0,
              feelsLike: Number(current.apparent_temperature) || 0,
              humidity: Number(current.relative_humidity_2m) || 0,
              precipitation,
              rain,
              snow,
              windSpeed: Number(current.wind_speed_10m) || 0,
              windLevel: beaufort(Number(current.wind_speed_10m) || 0),
              windDirection: windDirection(Number(current.wind_direction_10m) || 0),
              description: weatherDescription(Number(current.weather_code) || 0),
              icon: weatherIcon(Number(current.weather_code) || 0, Number(current.is_day) === 1),
            })
          } catch {
            // Offline or blocked external requests leave the weather pill hidden.
          } finally {
            window.clearTimeout(abortTimer)
          }
        }

        load()
        timer = window.setInterval(load, 20 * 60 * 1000)
        return () => {
          alive = false
          window.clearInterval(timer)
          controller?.abort()
        }
      }, [enabled, city])
      return weather
    }

    function MemoPanel(props) {
      const [draft, setDraft] = React.useState('')
      const add = () => {
        const text = draft.trim()
        if (!text) return
        props.onAdd(text)
        setDraft('')
      }
      return h('div', {
        className: 'whale-pet-modal',
        role: 'dialog',
        'aria-modal': 'true',
        'aria-label': '备忘录',
        onClick: (event) => { if (event.target === event.currentTarget) props.onClose() },
      },
        h('div', { className: 'whale-pet-modal-card whale-pet-memo-card', onClick: (event) => event.stopPropagation() },
          h('div', { className: 'whale-pet-memo-head' },
            h('h2', { className: 'whale-pet-modal-title' }, '鲸鱼娘备忘录'),
            h('button', { type: 'button', className: 'whale-pet-btn', onClick: props.onClose }, COPY.close)
          ),
          h('p', { className: 'whale-pet-modal-note' }, '记下要跟进的事，保存在当前设备。共 ' + props.memos.length + ' 条。'),
          h('p', { className: 'whale-pet-modal-note' }, '提示：勾选对应备忘录可在互动中显示。'),
          h('textarea', {
            className: 'whale-pet-memo-input',
            value: draft,
            placeholder: '写点什么… 例如：明天 10 点前提交方案',
            onChange: (event) => setDraft(event.target.value),
            onKeyDown: (event) => {
              if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) add()
            },
          }),
          h('div', { className: 'whale-pet-memo-actions' },
            h('span', { className: 'whale-pet-empty' }, 'Ctrl / ⌘ + Enter 快速添加'),
            h('button', { type: 'button', className: 'whale-pet-btn primary', onClick: add, disabled: !draft.trim() }, '添加备忘录')
          ),
          h('div', { className: 'whale-pet-memo-list' },
            props.memos.length === 0
              ? h('div', { className: 'whale-pet-memo-empty' }, '还没有备忘录。')
              : props.memos.map((memo) => h('div', { className: 'whale-pet-memo-item', key: memo.id, 'data-done': memo.done ? 'true' : 'false' },
                  h('label', { className: 'whale-pet-memo-check', title: '勾选后会出现在点击互动中' },
                    h('input', {
                      type: 'checkbox',
                      checked: memo.done === true,
                      onChange: (event) => props.onToggle(memo.id, event.target.checked),
                    }),
                    h('span', { 'aria-hidden': 'true' }, '✓')
                  ),
                  h('div', { className: 'whale-pet-memo-body' },
                    h('div', { className: 'whale-pet-memo-text' }, memo.text),
                    h('div', { className: 'whale-pet-memo-time' }, (memo.done ? '已勾选 · ' : '') + new Date(memo.time).toLocaleString())
                  ),
                  h('button', {
                    type: 'button',
                    className: 'whale-pet-memo-delete',
                    title: '删除',
                    'aria-label': '删除备忘录',
                    onClick: () => props.onDelete(memo.id),
                  }, '×')
                ))
          )
        )
      )
    }

    function SettingsPanel(props) {
      const [draft, setDraft] = React.useState(() => ({
        showStatus: props.initial.showStatus,
        greetingEnabled: props.initial.greetingEnabled,
        weatherEnabled: props.initial.weatherEnabled,
        weatherCity: props.initial.weatherCity,
        scale: String(props.initial.scale),
        characterScale: String(props.initial.characterScale),
        statusFontSize: String(props.initial.statusFontSize),
        settingsFontSize: String(props.initial.settingsFontSize),
        memoFontSize: String(props.initial.memoFontSize),
        themeColor: props.initial.themeColor,
        statusTextColor: props.initial.statusTextColor,
        settingsTextColor: props.initial.settingsTextColor,
        memoTextColor: props.initial.memoTextColor,
        settingsUiScale: String(props.initial.settingsUiScale),
        settingsButtonScale: String(props.initial.settingsButtonScale),
        memoButtonScale: String(props.initial.memoButtonScale),
        speechBubbleScale: String(props.initial.speechBubbleScale),
        inputBadgeScale: String(props.initial.inputBadgeScale),
        weatherFontSize: String(props.initial.weatherFontSize),
        idleMinutes: String(props.initial.idleMinutes),
        playSeconds: String(props.initial.playSeconds),
        chatIdleMinutes: String(props.initial.chatIdleMinutes),
        transitionMs: String(props.initial.transitionMs),
        phrases: props.initial.phrases.join('\n'),
        chatPhrases: props.initial.chatPhrases.join('\n'),
        main: props.initial.mainImages.join('\n'),
        idle: props.initial.idleImages.join('\n'),
        play: props.initial.playImages.join('\n'),
      }))
      const [error, setError] = React.useState('')
      const set = (key, value) => setDraft((previous) => ({ ...previous, [key]: value }))

      function splitList(text) {
        return text.split(/\r?\n/).map((line) => line.trim()).filter(Boolean)
      }

      function save() {
        const mainImages = splitList(draft.main)
        const idleImages = splitList(draft.idle)
        const playImages = splitList(draft.play)
        const phrases = splitList(draft.phrases)
        if (mainImages.length < 1 || idleImages.length < 1 || playImages.length < 1) {
          setError(COPY.invalidLists)
          return
        }
        if (phrases.length < 1) {
          setError('互动短句至少需要 1 条。')
          return
        }
        const chatIdleMinutes = numberOr(draft.chatIdleMinutes, props.initial.chatIdleMinutes, 0, 240)
        const chatPhrases = splitList(draft.chatPhrases)
        if (chatIdleMinutes > 0 && chatPhrases.length < 1) {
          setError('主动聊天间隔大于 0 时，至少需要 1 条主动聊天内容。')
          return
        }
        props.onSave({
          showStatus: draft.showStatus,
          greetingEnabled: draft.greetingEnabled,
          weatherEnabled: draft.weatherEnabled,
          weatherCity: draft.weatherCity,
          scale: decimalOr(draft.scale, props.initial.scale, 0.1, 1.5),
          characterScale: decimalOr(draft.characterScale, props.initial.characterScale, 0.1, 1.5),
          statusFontSize: numberOr(draft.statusFontSize, props.initial.statusFontSize, 8, 32),
          settingsFontSize: numberOr(draft.settingsFontSize, props.initial.settingsFontSize, 8, 32),
          memoFontSize: numberOr(draft.memoFontSize, props.initial.memoFontSize, 8, 32),
          themeColor: colorOr(draft.themeColor, props.defaults.themeColor),
          statusTextColor: colorOr(draft.statusTextColor, props.defaults.statusTextColor),
          settingsTextColor: colorOr(draft.settingsTextColor, props.defaults.settingsTextColor),
          memoTextColor: colorOr(draft.memoTextColor, props.defaults.memoTextColor),
          settingsUiScale: numberOr(draft.settingsUiScale, props.initial.settingsUiScale, 0.5, 2),
          settingsButtonScale: numberOr(draft.settingsButtonScale, props.initial.settingsButtonScale, 0.5, 2),
          memoButtonScale: numberOr(draft.memoButtonScale, props.initial.memoButtonScale, 0.5, 2),
          speechBubbleScale: numberOr(draft.speechBubbleScale, props.initial.speechBubbleScale, 0.5, 2),
          inputBadgeScale: numberOr(draft.inputBadgeScale, props.initial.inputBadgeScale, 0.5, 2),
          weatherFontSize: numberOr(draft.weatherFontSize, props.initial.weatherFontSize, 8, 32),
          idleMinutes: numberOr(draft.idleMinutes, props.initial.idleMinutes, 1, 240),
          playSeconds: numberOr(draft.playSeconds, props.initial.playSeconds, 5, 600),
          chatIdleMinutes,
          transitionMs: numberOr(draft.transitionMs, props.initial.transitionMs, 120, 2000),
          phrases,
          chatPhrases: chatPhrases.length > 0 ? chatPhrases : props.defaults.chatPhrases,
          mainImages,
          idleImages,
          playImages,
        })
      }

      const uiScale = numberOr(props.initial.settingsUiScale, 1, 0.5, 2)
      const viewportWidth = typeof window === 'undefined' ? 1200 : window.innerWidth
      const viewportHeight = typeof window === 'undefined' ? 900 : window.innerHeight
      const cardWidth = Math.max(280, Math.min(560, (viewportWidth - 32) / uiScale))
      const cardMaxHeight = Math.max(320, (viewportHeight - 36) / uiScale)

      return h('div', {
        className: 'whale-pet-modal whale-pet-settings-modal',
        role: 'dialog',
        'aria-modal': 'true',
        'aria-label': COPY.settings,
        style: {
          '--whale-settings-text': props.initial.settingsTextColor,
          '--whale-settings-font': props.initial.settingsFontSize + 'px',
          '--whale-theme': props.initial.themeColor,
          '--whale-settings-ui-scale': uiScale,
        },
        onClick: (event) => { if (event.target === event.currentTarget) props.onCancel() },
      },
        h('div', {
          className: 'whale-pet-modal-card whale-pet-settings-card',
          style: { width: cardWidth + 'px', maxHeight: cardMaxHeight + 'px', zoom: uiScale },
          onClick: (event) => event.stopPropagation(),
        },
          h('h2', { className: 'whale-pet-modal-title' }, '鲸鱼娘桌宠设置'),
          h('p', { className: 'whale-pet-modal-note' }, COPY.pathNote),
          error ? h('div', { className: 'whale-pet-form-alert', role: 'alert' }, error) : null,
          h('div', { className: 'whale-pet-form-grid' },
            h('label', { className: 'whale-pet-check' },
              h('input', { type: 'checkbox', checked: draft.showStatus, onChange: (event) => set('showStatus', event.target.checked) }),
              COPY.statusVisible
            ),
            h('label', { className: 'whale-pet-check' },
              h('input', { type: 'checkbox', checked: draft.greetingEnabled, onChange: (event) => set('greetingEnabled', event.target.checked) }),
              COPY.greetingEnabled
            ),
            h('label', { className: 'whale-pet-check' },
              h('input', { type: 'checkbox', checked: draft.weatherEnabled, onChange: (event) => set('weatherEnabled', event.target.checked) }),
              '在角色上方显示本地天气'
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '天气城市（留空按 IP 自动定位）'),
              h('input', { type: 'text', value: draft.weatherCity, placeholder: '例如：杭州', onChange: (event) => set('weatherCity', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '天气文字大小（px，独立于状态栏）'),
              h('input', { type: 'number', min: 8, max: 32, value: draft.weatherFontSize, onChange: (event) => set('weatherFontSize', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '界面大小（0.1–1.5）'),
              h('input', { type: 'number', min: 0.1, max: 1.5, step: 0.05, value: draft.scale, onChange: (event) => set('scale', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '角色大小（0.1–1.5）'),
              h('input', { type: 'number', min: 0.1, max: 1.5, step: 0.05, value: draft.characterScale, onChange: (event) => set('characterScale', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '状态栏文字大小（px）'),
              h('input', { type: 'number', min: 8, max: 32, value: draft.statusFontSize, onChange: (event) => set('statusFontSize', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '设置文字大小（px）'),
              h('input', { type: 'number', min: 8, max: 32, value: draft.settingsFontSize, onChange: (event) => set('settingsFontSize', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '备忘录文字大小（px）'),
              h('input', { type: 'number', min: 8, max: 32, value: draft.memoFontSize, onChange: (event) => set('memoFontSize', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '界面主题色'),
              h('input', { type: 'color', value: draft.themeColor, onChange: (event) => set('themeColor', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '状态栏文字颜色'),
              h('input', { type: 'color', value: draft.statusTextColor, onChange: (event) => set('statusTextColor', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '设置文字颜色'),
              h('input', { type: 'color', value: draft.settingsTextColor, onChange: (event) => set('settingsTextColor', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '备忘录文字颜色'),
              h('input', { type: 'color', value: draft.memoTextColor, onChange: (event) => set('memoTextColor', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '设置窗口大小（0.5–2）'),
              h('input', { type: 'number', min: 0.5, max: 2, step: 0.05, value: draft.settingsUiScale, onChange: (event) => set('settingsUiScale', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '设置按钮大小（0.5–2）'),
              h('input', { type: 'number', min: 0.5, max: 2, step: 0.05, value: draft.settingsButtonScale, onChange: (event) => set('settingsButtonScale', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '备忘录按钮大小（0.5–2）'),
              h('input', { type: 'number', min: 0.5, max: 2, step: 0.05, value: draft.memoButtonScale, onChange: (event) => set('memoButtonScale', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '互动气泡大小（0.5–2）'),
              h('input', { type: 'number', min: 0.5, max: 2, step: 0.05, value: draft.speechBubbleScale, onChange: (event) => set('speechBubbleScale', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '键鼠状态大小（0.5–2）'),
              h('input', { type: 'number', min: 0.5, max: 2, step: 0.05, value: draft.inputBadgeScale, onChange: (event) => set('inputBadgeScale', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, COPY.idleMinutes),
              h('input', { type: 'number', min: 1, max: 240, value: draft.idleMinutes, onChange: (event) => set('idleMinutes', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, COPY.playSeconds),
              h('input', { type: 'number', min: 5, max: 600, value: draft.playSeconds, onChange: (event) => set('playSeconds', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field full' },
              h('span', null, COPY.transitionMs),
              h('input', { type: 'number', min: 120, max: 2000, step: 10, value: draft.transitionMs, onChange: (event) => set('transitionMs', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field full' },
              h('span', null, COPY.mainImages),
              h('textarea', { value: draft.main, onChange: (event) => set('main', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field full' },
              h('span', null, COPY.idleImages),
              h('textarea', { value: draft.idle, onChange: (event) => set('idle', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field full' },
              h('span', null, COPY.playImages),
              h('textarea', { value: draft.play, onChange: (event) => set('play', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field full' },
              h('span', null, '点击互动的短句（每行一条，至少 1 条）'),
              h('textarea', { value: draft.phrases, onChange: (event) => set('phrases', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field' },
              h('span', null, '主动聊天间隔（分钟，0=关闭）'),
              h('input', { type: 'number', min: 0, max: 240, value: draft.chatIdleMinutes, onChange: (event) => set('chatIdleMinutes', event.target.value) })
            ),
            h('label', { className: 'whale-pet-field full' },
              h('span', null, '主动聊天内容（每行一条）'),
              h('textarea', { value: draft.chatPhrases, onChange: (event) => set('chatPhrases', event.target.value) })
            )
          ),
          h('div', { className: 'whale-pet-modal-footer' },
            h('button', {
              type: 'button',
              className: 'whale-pet-btn whale-pet-reset',
              onClick: () => {
                const next = props.defaults
                setDraft({
                  showStatus: next.showStatus,
                  greetingEnabled: next.greetingEnabled,
                  weatherEnabled: next.weatherEnabled,
                  weatherCity: next.weatherCity,
                  scale: String(next.scale),
                  characterScale: String(next.characterScale),
                  statusFontSize: String(next.statusFontSize),
                  settingsFontSize: String(next.settingsFontSize),
                  memoFontSize: String(next.memoFontSize),
                  themeColor: next.themeColor,
                  statusTextColor: next.statusTextColor,
                  settingsTextColor: next.settingsTextColor,
                  memoTextColor: next.memoTextColor,
                  settingsUiScale: String(next.settingsUiScale),
                  settingsButtonScale: String(next.settingsButtonScale),
                  memoButtonScale: String(next.memoButtonScale),
                  speechBubbleScale: String(next.speechBubbleScale),
                  inputBadgeScale: String(next.inputBadgeScale),
                  weatherFontSize: String(next.weatherFontSize),
                  idleMinutes: String(next.idleMinutes),
                  playSeconds: String(next.playSeconds),
                  chatIdleMinutes: String(next.chatIdleMinutes),
                  transitionMs: String(next.transitionMs),
                  phrases: next.phrases.join('\n'),
                  chatPhrases: next.chatPhrases.join('\n'),
                  main: next.mainImages.join('\n'),
                  idle: next.idleImages.join('\n'),
                  play: next.playImages.join('\n'),
                })
                setError('')
              },
            }, COPY.reset),
            h('button', { type: 'button', className: 'whale-pet-btn', onClick: props.onCancel }, COPY.cancel),
            h('button', { type: 'button', className: 'whale-pet-btn primary', onClick: save }, COPY.save)
          )
        )
      )
    }

    function Stat(props) {
      return h('span', { className: 'whale-pet-stat' },
        h('span', { className: 'label' }, props.label + ' '),
        h('b', null, props.value)
      )
    }

    function WhalePet(props) {
      const ctx = props.appCtx
      const defaults = React.useMemo(() => defaultSettings(), [])
      const [settings, setSettings] = React.useState(() => loadSettings(defaults))
      const settingsRef = React.useRef(settings)
      settingsRef.current = settings
      const [mode, setMode] = React.useState('main')
      const modeRef = React.useRef('main')
      const [current, setCurrent] = React.useState(() => pickImage(settings.mainImages, null) || defaults.mainImages[0])
      const currentRef = React.useRef(current)
      currentRef.current = current
      const [previous, setPrevious] = React.useState(null)
      const previousTimer = React.useRef(0)
      const [bubble, setBubble] = React.useState(() => settings.greetingEnabled ? greetingText(new Date()) : null)
      const [bubbleKind, setBubbleKind] = React.useState('greeting')
      const [bubbleTitle, setBubbleTitle] = React.useState('')
      const bubbleTimer = React.useRef(0)
      const lastInput = React.useRef(Date.now())
      const lastInteraction = React.useRef(Date.now())
      const lastProactive = React.useRef(0)
      const playUntil = React.useRef(0)
      const [statusOpen, setStatusOpen] = React.useState(false)
      const [settingsOpen, setSettingsOpen] = React.useState(false)
      const [memoOpen, setMemoOpen] = React.useState(false)
      const [memos, setMemos] = React.useState(() => loadMemos())
      const [typing, setTyping] = React.useState(false)
      const [pointerActive, setPointerActive] = React.useState(false)
      const [pressed, setPressed] = React.useState(false)
      const [nonce, setNonce] = React.useState(0)
      const [now, setNow] = React.useState(() => new Date())
      const badImages = React.useRef(new Set())
      const typingTimer = React.useRef(0)
      const pointerTimer = React.useRef(0)
      const pressTimer = React.useRef(0)

      const running = props.useSession((session) => Boolean(session && session.running))
      const context = normalizeContext(props.useProjection('contextPressure'))
      const tokens = normalizeTokens(props.useProjection('tokenUsage'))
      const breakdown = normalizeBreakdown(props.useProjection('contextBreakdown'))
      const preset = normalizePreset(props.useProjection('agentPreset'))
      const subagents = normalizeSubagents(props.useProjection('subagentCatalog'))
      const skills = useSkills(ctx, props.sessionId, nonce)
      const weather = useWeather(settings.weatherEnabled, settings.weatherCity)

      const showImage = React.useCallback((next, duration) => {
        if (!next || next === currentRef.current) return
        const from = currentRef.current
        setPrevious(from)
        setCurrent(next)
        currentRef.current = next
        window.clearTimeout(previousTimer.current)
        previousTimer.current = window.setTimeout(() => setPrevious(null), duration)
      }, [])

      const applyMode = React.useCallback((nextMode, force) => {
        if (!force && modeRef.current === nextMode) return
        const source = settingsRef.current
        const list = nextMode === 'idle' ? source.idleImages : nextMode === 'play' ? source.playImages : source.mainImages
        const chosen = pickImage(list, currentRef.current)
        modeRef.current = nextMode
        setMode(nextMode)
        if (chosen) showImage(chosen, source.transitionMs)
      }, [showImage])

      const showBubble = React.useCallback((text, kind, duration, fullText) => {
        setBubble(text)
        setBubbleKind(kind)
        setBubbleTitle(fullText && fullText.length > text.length ? fullText : '')
        window.clearTimeout(bubbleTimer.current)
        bubbleTimer.current = window.setTimeout(() => {
          setBubble(null)
          setBubbleTitle('')
        }, duration)
      }, [])

      React.useEffect(() => {
        if (bubble) {
          window.clearTimeout(bubbleTimer.current)
          bubbleTimer.current = window.setTimeout(() => setBubble(null), 6500)
        }
        return () => {
          window.clearTimeout(bubbleTimer.current)
          window.clearTimeout(previousTimer.current)
        }
      }, [])

      React.useEffect(() => {
        const touchInput = () => {
          lastInput.current = Date.now()
          if (modeRef.current === 'idle') applyMode('main')
        }
        const onKeyDown = (event) => {
          if (event.repeat) return
          const key = event.key
          if (key === 'Shift' || key === 'Control' || key === 'Alt' || key === 'Meta' ||
              key === 'CapsLock' || key === 'Tab' || key === 'Escape' || key === 'Dead' || key === 'Unidentified') return
          touchInput()
          setTyping(true)
          window.clearTimeout(typingTimer.current)
          typingTimer.current = window.setTimeout(() => setTyping(false), 1200)
        }
        const onPointerMove = (event) => {
          if (event.pointerType === 'touch') return
          touchInput()
          setPointerActive(true)
          window.clearTimeout(pointerTimer.current)
          pointerTimer.current = window.setTimeout(() => setPointerActive(false), 1600)
        }
        const onPointerDown = (event) => {
          if (event.button !== undefined && event.button !== 0) return
          touchInput()
          setPointerActive(true)
          setPressed(true)
          window.clearTimeout(pointerTimer.current)
          pointerTimer.current = window.setTimeout(() => setPointerActive(false), 1600)
          window.clearTimeout(pressTimer.current)
          pressTimer.current = window.setTimeout(() => setPressed(false), 260)
        }
        const onPointerUp = () => {
          window.clearTimeout(pressTimer.current)
          setPressed(false)
        }
        const onBlur = () => {
          setTyping(false)
          setPointerActive(false)
          setPressed(false)
        }
        window.addEventListener('keydown', onKeyDown, true)
        window.addEventListener('pointermove', onPointerMove, { passive: true })
        window.addEventListener('pointerdown', onPointerDown, true)
        window.addEventListener('pointerup', onPointerUp, true)
        window.addEventListener('pointercancel', onPointerUp, true)
        window.addEventListener('blur', onBlur)
        return () => {
          window.removeEventListener('keydown', onKeyDown, true)
          window.removeEventListener('pointermove', onPointerMove, { passive: true })
          window.removeEventListener('pointerdown', onPointerDown, true)
          window.removeEventListener('pointerup', onPointerUp, true)
          window.removeEventListener('pointercancel', onPointerUp, true)
          window.removeEventListener('blur', onBlur)
          window.clearTimeout(typingTimer.current)
          window.clearTimeout(pointerTimer.current)
          window.clearTimeout(pressTimer.current)
        }
      }, [applyMode])

      React.useEffect(() => {
        const timer = window.setInterval(() => {
          const timestamp = Date.now()
          setNow(new Date(timestamp))
          const source = settingsRef.current
          if (playUntil.current > timestamp) {
            applyMode('play')
            return
          }
          playUntil.current = 0
          if (timestamp - lastInput.current >= source.idleMinutes * 60000) applyMode('idle')
          else applyMode('main')

          if (
            !settingsOpen &&
            !memoOpen &&
            !bubble &&
            source.chatIdleMinutes > 0 &&
            modeRef.current === 'main' &&
            timestamp - lastInteraction.current >= source.chatIdleMinutes * 60000 &&
            timestamp - lastProactive.current >= source.chatIdleMinutes * 60000
          ) {
            const pool = source.chatPhrases
            const picked = pool[Math.floor(Math.random() * pool.length)]
            if (picked) {
              lastProactive.current = timestamp
              showBubble(shorten(picked, 90), 'proactive', 6200, picked)
            }
          }
        }, 1000)
        return () => window.clearInterval(timer)
      }, [applyMode, bubble, memoOpen, settingsOpen, showBubble])

      React.useEffect(() => {
        const timer = window.setInterval(() => setNonce((value) => value + 1), 30000)
        return () => window.clearInterval(timer)
      }, [])

      React.useEffect(() => () => {
        window.clearTimeout(bubbleTimer.current)
        window.clearTimeout(previousTimer.current)
      }, [])

      function failImage(source) {
        if (!source || badImages.current.has(source)) return
        badImages.current.add(source)
        const fallback = pickImage(settingsRef.current.mainImages, source) || defaults.mainImages[0]
        if (fallback) showImage(fallback, 180)
      }

      function poke() {
        const source = settingsRef.current
        const playImages = source.playImages
        const nextImage = playImages[Math.floor(Math.random() * playImages.length)]
        lastInput.current = Date.now()
        lastInteraction.current = Date.now()
        playUntil.current = Date.now() + source.playSeconds * 1000
        if (nextImage) showImage(nextImage, source.transitionMs)
        modeRef.current = 'play'
        setMode('play')
        const checked = memos.filter((memo) => memo.done && memo.text.trim())
        const pool = checked.length > 0 && Math.random() < 0.7
          ? checked.map((memo) => memo.text)
          : source.phrases
        const picked = pool[Math.floor(Math.random() * pool.length)] || source.phrases[0]
        showBubble(shorten(picked, 90), 'play', Math.min(6000, source.playSeconds * 1000), picked)
      }

      function addMemo(text) {
        const memo = {
          id: typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : String(Date.now() + Math.random()),
          text: text.trim().slice(0, 2000),
          time: Date.now(),
          done: false,
        }
        const next = [memo, ...memos].slice(0, 100)
        setMemos(next)
        persistMemos(next)
      }

      function deleteMemo(id) {
        const next = memos.filter((memo) => memo.id !== id)
        setMemos(next)
        persistMemos(next)
      }

      function toggleMemo(id, done) {
        const next = memos.map((memo) => memo.id === id ? { ...memo, done } : memo)
        setMemos(next)
        persistMemos(next)
      }

      function saveSettings(next) {
        const normalized = normalizeSettings(next, defaults)
        settingsRef.current = normalized
        setSettings(normalized)
        persistSettings(normalized)
        setSettingsOpen(false)
        setStatusOpen(false)
        lastInput.current = Date.now()
        playUntil.current = 0
        const chosen = pickImage(normalized.mainImages, currentRef.current)
        if (chosen) showImage(chosen, normalized.transitionMs)
        modeRef.current = 'main'
        setMode('main')
        showBubble('设置已保存。', 'system', 3200)
      }

      const pctLabel = context.pct === null ? '—' : Math.round(context.pct) + '%'
      const contextSub = context.pressure === null
        ? '—'
        : formatCompact(context.pressure) + (context.windowSize === null ? '' : ' / ' + formatCompact(context.windowSize))
      const tokenSub = tokens.cachePct === null ? '—' : tokens.cachePct + '%'
      const skillsValue = skills.status === 'ready' ? String(skills.items.length) : '—'
      const skillsSub = skills.status === 'ready' ? COPY.enabled : skills.status === 'loading' ? COPY.skillsLoading : COPY.skillsUnavailable
      const agentName = prettyPreset(preset)
      const childCount = subagents.length
      const idleLeft = Math.max(0, settings.idleMinutes * 60000 - (now.getTime() - lastInput.current))
      const idleText = idleLeft <= 0 ? '已休眠' : Math.ceil(idleLeft / 60000) + ' 分钟'
      const stateLabel = mode === 'play' ? '互动' : mode === 'idle' ? '休眠' : '待机'
      const inputState = pressed
        ? { kind: 'click', label: '🖱 点击' }
        : typing
          ? { kind: 'typing', label: '⌨ 键盘输入' }
          : pointerActive
            ? { kind: 'pointer', label: '🖱 鼠标移动' }
            : null

      const summary = settings.showStatus
        ? h('button', {
            type: 'button',
            className: 'whale-pet-summary',
            onClick: () => setStatusOpen((value) => !value),
            'aria-expanded': statusOpen,
            'aria-label': '展开上下文、token、技能和 agent 明细',
          },
            h('span', { className: 'whale-pet-dot', 'data-running': running ? 'true' : 'false' }),
            h('span', { className: 'whale-pet-brand' }, 'DSH'),
            h('span', { className: 'whale-pet-track' },
              h(Stat, { label: COPY.context, value: pctLabel }),
              h('span', { className: 'whale-pet-sep' }, '·'),
              h(Stat, { label: COPY.tokens, value: formatCompact(tokens.total) }),
              h('span', { className: 'whale-pet-sep' }, '·'),
              h(Stat, { label: COPY.skills, value: skillsValue }),
              h('span', { className: 'whale-pet-sep' }, '·'),
              h(Stat, { label: 'AGENT', value: agentName })
            ),
            h('span', { className: 'whale-pet-chevron' }, statusOpen ? '▴' : '▾')
          )
        : null

      const details = settings.showStatus && statusOpen
        ? h('div', { className: 'whale-pet-details' },
            h('div', { className: 'whale-pet-detail-head' },
              h('span', null, '会话明细 · ' + stateLabel + ' · 距离休眠 ' + idleText),
              h('div', { className: 'whale-pet-detail-actions' },
                h('button', { type: 'button', className: 'whale-pet-mini', onClick: () => setNonce((value) => value + 1), title: COPY.refresh }, '↻'),
                h('button', { type: 'button', className: 'whale-pet-mini', onClick: () => setMemoOpen(true), title: '备忘录' }, '📝'),
                h('button', { type: 'button', className: 'whale-pet-mini', onClick: () => setSettingsOpen(true) }, COPY.settings)
              )
            ),
            h('div', { className: 'whale-pet-detail-grid' },
              h('div', { className: 'whale-pet-detail-cell' }, h('span', null, COPY.system), h('strong', null, formatCompact(breakdown.system))),
              h('div', { className: 'whale-pet-detail-cell' }, h('span', null, COPY.tools), h('strong', null, formatCompact(breakdown.tools))),
              h('div', { className: 'whale-pet-detail-cell' }, h('span', null, COPY.messages), h('strong', null, formatCompact(breakdown.messages)))
            ),
            h('div', { className: 'whale-pet-detail-row' }, h('span', null, COPY.context), h('strong', null, pctLabel + ' · ' + contextSub)),
            h('div', { className: 'whale-pet-detail-row' }, h('span', null, COPY.tokens), h('strong', null, formatCompact(tokens.total) + ' · ' + COPY.cached + ' ' + tokenSub)),
            h('div', { className: 'whale-pet-detail-row' }, h('span', null, COPY.input), h('strong', null, formatCompact(tokens.input))),
            h('div', { className: 'whale-pet-detail-row' }, h('span', null, COPY.output), h('strong', null, formatCompact(tokens.output))),
            h('div', { className: 'whale-pet-detail-row' }, h('span', null, COPY.cacheRead), h('strong', null, formatCompact(tokens.cacheRead))),
            h('div', { className: 'whale-pet-detail-row' }, h('span', null, COPY.cacheWrite), h('strong', null, formatCompact(tokens.cacheWrite))),
            weather
              ? h('div', { className: 'whale-pet-detail-row' },
                  h('span', null, '天气'),
                  h('strong', null, weather.place + ' · ' + weather.icon + ' ' + Math.round(weather.temperature) + '°C · ' + weather.description + ' · ' + (weather.snow > 0.01 ? '降雪' : weather.rain > 0.01 || weather.precipitation > 0.01 ? '降雨' : '无降雨') + ' · ' + weather.windDirection + '风 ' + weather.windLevel + '级')
                )
              : null,
            skills.items.length > 0
              ? h('div', { className: 'whale-pet-skills' },
                  skills.items.slice(0, 8).map((skill, index) =>
                    h('span', {
                      className: 'whale-pet-skill',
                      key: (skill && skill.name ? skill.name : 'skill') + '-' + index,
                      title: skill && skill.description ? skill.description : undefined,
                    }, skill && skill.name ? skill.name : String(skill))
                  )
                )
              : h('span', { className: 'whale-pet-empty' }, skillsSub),
            h('div', { className: 'whale-pet-agent' },
              h('span', { className: 'whale-pet-dot', 'data-running': running ? 'true' : 'false' }),
              h('span', { className: 'whale-pet-agent-name' }, agentName),
              h('span', { className: 'whale-pet-agent-state' }, running ? COPY.running : COPY.idle),
              childCount > 0 ? h('span', { className: 'whale-pet-agent-child' }, '+' + childCount) : null
            )
          )
        : null

      const overlay = h(React.Fragment, null,
        h('style', { key: 'whale-pet-style' }, CSS),
        h('div', {
          className: 'whale-pet-root',
          'data-whale-pet': 'true',
          'data-mode': mode,
          'data-typing': typing ? 'true' : 'false',
          'data-pointer': pointerActive ? 'true' : 'false',
          'data-pressed': pressed ? 'true' : 'false',
          style: {
            '--whale-pet-ms': settings.transitionMs + 'ms',
            '--whale-pet-char-scale': settings.characterScale,
            '--whale-theme': settings.themeColor,
            '--whale-status-text': settings.statusTextColor,
            '--whale-settings-text': settings.settingsTextColor,
            '--whale-memo-text': settings.memoTextColor,
            '--whale-status-font': settings.statusFontSize + 'px',
            '--whale-settings-font': settings.settingsFontSize + 'px',
            '--whale-memo-font': settings.memoFontSize + 'px',
            '--whale-settings-ui-scale': settings.settingsUiScale,
            '--whale-settings-button-scale': settings.settingsButtonScale,
            '--whale-memo-button-scale': settings.memoButtonScale,
            '--whale-bubble-scale': settings.speechBubbleScale,
            '--whale-input-scale': settings.inputBadgeScale,
            '--whale-weather-font': settings.weatherFontSize + 'px',
            zoom: settings.scale,
          },
        },
          weather
            ? h('div', { className: 'whale-pet-weather' },
                weather.place + ' · ' + weather.icon + ' ' + Math.round(weather.temperature) + '°C · ' + weather.description
              )
            : null,
          h('div', { className: 'whale-pet-stage' },
            previous ? h('img', {
              className: 'whale-pet-art whale-pet-art-prev',
              key: 'prev-' + previous,
              src: previous,
              alt: '',
              draggable: false,
              onError: () => failImage(previous),
            }) : null,
            h('img', {
              className: 'whale-pet-art whale-pet-art-current',
              key: 'current-' + current,
              src: current,
              alt: '蓝色鲸鱼娘',
              draggable: false,
              onError: () => failImage(current),
            }),
            bubble ? h('div', { className: 'whale-pet-bubble', 'data-kind': bubbleKind, role: 'status', 'aria-live': 'polite', title: bubbleTitle || undefined }, bubble) : null,
            inputState ? h('div', { className: 'whale-pet-input-badge', 'data-kind': inputState.kind, 'aria-live': 'polite' },
              h('span', { className: 'whale-pet-input-icon' }, inputState.label.slice(0, 2)),
              h('span', null, inputState.label.slice(2).trim()),
              inputState.kind === 'typing' ? h('span', { className: 'whale-pet-keycaps', 'aria-hidden': 'true' },
                h('i', null, 'A'), h('i', null, 'B'), h('i', null, 'C')
              ) : null
            ) : null,
            h('button', {
              type: 'button',
              className: 'whale-pet-memo-button',
              onClick: () => setMemoOpen(true),
              title: '备忘录',
              'aria-label': '打开备忘录',
            },
              '📝',
              memos.length > 0 ? h('span', { className: 'whale-pet-memo-count' }, memos.length > 99 ? '99+' : String(memos.length)) : null
            ),
            h('button', {
              type: 'button',
              className: 'whale-pet-gear',
              onClick: () => setSettingsOpen(true),
              title: COPY.settings,
              'aria-label': COPY.settingsAria,
            }, '⚙'),
            h('button', {
              type: 'button',
              className: 'whale-pet-character',
              onClick: poke,
              'aria-label': COPY.aria,
              title: COPY.aria,
            })
          ),
          summary,
          details,
          settingsOpen ? ReactDOM.createPortal(h(SettingsPanel, {
            initial: settings,
            defaults,
            onCancel: () => setSettingsOpen(false),
            onSave: saveSettings,
          }), document.body) : null,
          memoOpen ? h(MemoPanel, {
            memos,
            onAdd: addMemo,
            onDelete: deleteMemo,
            onToggle: toggleMemo,
            onClose: () => setMemoOpen(false),
          }) : null
        )
      )

      if (typeof document === 'undefined' || !document.body) return null
      return ReactDOM.createPortal(overlay, document.body)
    }

    class PetBoundary extends React.Component {
      constructor(props) {
        super(props)
        this.state = { error: null }
      }

      static getDerivedStateFromError(error) {
        return { error }
      }

      componentDidCatch(error, info) {
        if (typeof window !== 'undefined') window.__whalePetError = String((error && error.stack) || error)
        console.error('[whale-pet] render failed', error, info)
      }

      render() {
        if (!this.state.error) return this.props.children
        return h('div', {
          style: {
            position: 'fixed',
            right: '20px',
            bottom: '20px',
            zIndex: 900,
            padding: '10px 13px',
            border: '1px solid rgba(143, 211, 255, .28)',
            borderRadius: '12px',
            background: 'rgba(6, 20, 42, .94)',
            color: '#b9d8ee',
            font: '11px/1.5 "Segoe UI", "Microsoft YaHei", sans-serif',
            pointerEvents: 'auto',
          },
        },
          h('strong', { style: { display: 'block', color: '#dff2ff' } }, '鲸鱼娘打了个盹'),
          h('span', null, '刷新页面后再叫她出来吧。 / Refresh to wake her up.')
        )
      }
    }

    return {
      inject: ['slots', 'remote', 'remote.skills'],
      apply(ctx) {
        function PetSlot(props) {
          return h(PetBoundary, null, h(WhalePet, Object.assign({}, props, { appCtx: ctx })))
        }
        return ctx.slots.inject('conversation.composer.dock', () =>
          ctx.slots.register({
            name: 'conversation.composer.dock',
            id: 'whale-pet',
            order: 20,
            label: () => 'Blue whale companion',
          }, PetSlot)
        )
      },
    }
  },
})
