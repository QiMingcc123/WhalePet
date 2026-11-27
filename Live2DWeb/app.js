/* Local PixiJS 8 + untitled-pixi-live2d-engine 1.4.0 renderer for the WPF host. */
(() => {
  'use strict';

  const canvas = document.getElementById('canvas');
  const errorBox = document.getElementById('error');
  const TARGET_FPS_VISIBLE = 30;
  const TARGET_FPS_HIDDEN = 15;
  const SAFE_PADDING_RATIO = 0.035;

  let app = null;
  let model = null;
  let origW = 1;
  let origH = 1;
  let keyLockUntil = 0;
  let expressionUntil = 0;
  let expressionTimer = null;
  let motionTimer = null;
  let motionGroups = [];
  let hitAreaNames = [];
  let visible = false;
  let leftPointer = null;
  let noHitAreaLogged = false;
  let layoutRect = { x: 0, y: 0, width: 1, height: 1 };
  let lastHostLayoutAt = 0;
  let fitTimer = null;
  let lastRightClickAt = 0;
  let renderHandle = 0;
  let lastFrameAt = 0;
  let renderedFrames = 0;
  let statsStartedAt = performance.now();
  let tickerErrorLogged = false;
  let lastGeometrySignature = '';

  function post(message) {
    try {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(message);
      }
    } catch (_) {
      // The WPF host may be closing.
    }
  }

  function fail(message) {
    const text = String(message || 'Live2D 加载失败');
    if (errorBox) {
      errorBox.textContent = text;
      errorBox.style.display = 'block';
    }
    post({ type: 'modelError', message: text });
  }

  function findEngine() {
    const candidates = [];
    if (window.PIXI && window.PIXI.live2d) candidates.push(['PIXI.live2d', window.PIXI.live2d]);
    for (const key of Object.keys(window)) {
      try {
        const value = window[key];
        if (value && value.Live2DModel && value.Live2DPlugin) candidates.push([key, value]);
      } catch (_) { /* cross-origin/global getter */ }
    }
    return candidates[0] || [null, null];
  }

  function expressionNames() {
    try {
      const definitions = model?.internalModel?.motionManager?.expressionManager?.definitions;
      if (Array.isArray(definitions)) {
        return definitions.map((item) => item?.Name || item?.name || '').filter(Boolean);
      }
    } catch (_) { }
    return [];
  }

  function probe() {
    const [globalName, engine] = findEngine();
    const result = {
      type: 'probe',
      globalName,
      hasPixi: !!window.PIXI,
      hasCubismCore: !!window.Live2DCubismCore,
      hasLive2DModel: !!(engine && engine.Live2DModel),
      hasLive2DPlugin: !!(engine && engine.Live2DPlugin),
      hasConfigureCubismSDK: !!(engine && (engine.configureCubismSDK || engine.reconfigureCubismSDK)),
      version: engine && engine.VERSION ? engine.VERSION : '',
      motionGroups,
      hitAreas: hitAreaNames,
      expressions: expressionNames(),
    };
    post(result);
    return result;
  }

  function resizeRenderer() {
    if (!app) return;
    const width = Math.max(1, Math.round(window.innerWidth));
    const height = Math.max(1, Math.round(window.innerHeight));
    if (app.screen.width !== width || app.screen.height !== height) {
      app.renderer.resize(width, height);
    }
  }

  /**
   * contain + safe padding: the model always fits inside the character area,
   * keeps a small margin on every side, and never clips when the window grows
   * or shrinks because the layout changes.
   */
  function fitModel() {
    if (!model || !app) return;
    resizeRenderer();
    const width = app.screen.width;
    const height = app.screen.height;
    if (!width || !height) return;
    const areaX = layoutRect.x * width;
    const areaY = layoutRect.y * height;
    const areaW = Math.max(1, layoutRect.width * width);
    const areaH = Math.max(1, layoutRect.height * height);
    const padding = Math.max(2, Math.min(areaW, areaH) * SAFE_PADDING_RATIO);
    const availableW = Math.max(1, areaW - padding * 2);
    const availableH = Math.max(1, areaH - padding * 2);
    const scale = Math.min(availableW / Math.max(1, origW), availableH / Math.max(1, origH));
    model.anchor.set(0.5);
    model.scale.set(scale);
    model.position.set(areaX + areaW / 2, areaY + areaH / 2);
    let bounds = { x: 0, y: 0, width: 1, height: 1 };
    try {
      const b = model.getBounds();
      bounds = {
        x: Math.max(0, Math.min(1, b.x / width)),
        y: Math.max(0, Math.min(1, b.y / height)),
        width: Math.max(0, Math.min(1, b.width / width)),
        height: Math.max(0, Math.min(1, b.height / height)),
      };
    } catch (_) { }
    const signature = `${width}x${height}|${areaW.toFixed(1)}x${areaH.toFixed(1)}|${scale.toFixed(4)}|` +
      `${bounds.x.toFixed(3)},${bounds.y.toFixed(3)},${bounds.width.toFixed(3)},${bounds.height.toFixed(3)}`;
    if (signature !== lastGeometrySignature) {
      lastGeometrySignature = signature;
      post({
        type: 'geometry',
        canvas: `${width}x${height}`,
        area: `${areaW.toFixed(1)}x${areaH.toFixed(1)}`,
        scale: Number(scale.toFixed(4)),
        padding: Number(padding.toFixed(1)),
        bounds,
      });
    }
  }

  function scheduleFit() {
    if (fitTimer) return;
    fitTimer = setTimeout(() => {
      fitTimer = null;
      fitModel();
    }, 100);
  }

  function applyExpression(name) {
    if (!model || typeof model.expression !== 'function') return false;
    try {
      model.expression(name);
      return true;
    } catch (error) {
      post({ type: 'expressionError', name: String(name), message: String(error) });
      return false;
    }
  }

  function defaultExpression() {
    const names = expressionNames();
    if (names.includes('微笑')) applyExpression('微笑');
  }

  // A clicked expression owns a five-second lock. Further clicks are ignored
  // rather than queued or allowed to interrupt the active expression.
  function playLockedExpression(name) {
    if (Date.now() < expressionUntil) {
      post({ type: 'expression', name: String(name), action: 'ignored' });
      return false;
    }
    if (!applyExpression(name)) return false;
    expressionUntil = Date.now() + 5000;
    post({ type: 'expression', name: String(name), action: 'start' });
    if (expressionTimer) clearTimeout(expressionTimer);
    expressionTimer = setTimeout(() => {
      expressionUntil = 0;
      expressionTimer = null;
      post({ type: 'expression', name: String(name), action: 'end' });
      defaultExpression();
    }, 5000);
    return true;
  }

  function partFromHitName(name) {
    const value = String(name || '').toLowerCase();
    if (/(head|头)/.test(value)) return 'head';
    if (/(face|脸|顔)/.test(value)) return 'face';
    if (/(hand|arm|手)/.test(value)) return 'hands';
    if (/(foot|feet|leg|脚|足)/.test(value)) return 'feet';
    if (/(tail|尾巴)/.test(value)) return 'tail';
    if (/(skirt|dress|裙)/.test(value)) return 'skirt';
    if (/(body|torso|身体|体)/.test(value)) return 'body';
    return null;
  }

  function fallbackPart(nx, ny) {
    if (ny < 0.46) {
      if (nx < 0.20 || nx > 0.80) return 'hands';
      if (nx > 0.32 && nx < 0.68 && ny > 0.18) return 'face';
      return 'head';
    }
    if (ny < 0.62) {
      if (nx > 0.80) return 'tail';
      return 'body';
    }
    if (ny < 0.82) {
      if (nx > 0.80) return 'tail';
      return 'skirt';
    }
    return 'feet';
  }

  function localModelPoint(clientX, clientY) {
    if (!app) return null;
    const rect = canvas.getBoundingClientRect();
    if (!rect.width || !rect.height) return null;
    return {
      x: (clientX - rect.left) / rect.width * app.screen.width,
      y: (clientY - rect.top) / rect.height * app.screen.height,
      fullX: (clientX - rect.left) / rect.width,
      fullY: (clientY - rect.top) / rect.height,
    };
  }

  function isInsideModel(clientX, clientY) {
    if (!model) return false;
    const point = localModelPoint(clientX, clientY);
    if (!point) return false;
    let bounds = null;
    try { bounds = model.getBounds(); } catch (_) { }
    if (!bounds) return false;
    return point.x >= bounds.x && point.y >= bounds.y &&
      point.x <= bounds.x + bounds.width && point.y <= bounds.y + bounds.height;
  }

  function detectPart(clientX, clientY) {
    if (!model || !app) return null;
    const point = localModelPoint(clientX, clientY);
    if (!point) return null;
    const x = point.x;
    const y = point.y;

    let bounds = null;
    try { bounds = model.getBounds(); } catch (_) { }
    if (bounds && (x < bounds.x || y < bounds.y ||
        x > bounds.x + bounds.width || y > bounds.y + bounds.height)) {
      return null;
    }

    if (hitAreaNames.length) {
      let names = [];
      try {
        names = (model.hitTest.length >= 3
          ? model.hitTest(null, x, y)
          : model.hitTest(x, y)) || [];
      } catch (_) { }
      for (const name of names) {
        const part = partFromHitName(name);
        if (part) return part;
      }
      return null;
    }

    if (!noHitAreaLogged) {
      noHitAreaLogged = true;
      post({ type: 'noHitAreas', message: 'model has no HitAreas; using normalized fallback regions' });
    }
    const localX = bounds && bounds.width > 0
      ? (x - bounds.x) / bounds.width
      : point.fullX;
    const localY = bounds && bounds.height > 0
      ? (y - bounds.y) / bounds.height
      : point.fullY;
    return fallbackPart(localX, localY);
  }

  function startMotionTimer() {
    if (motionTimer || !visible || !model || !motionGroups.length) return;
    motionTimer = setInterval(() => {
      if (!visible || !model) return;
      const candidates = motionGroups.filter((group) => group !== 'Idle');
      if (!candidates.length) return;
      const group = candidates[Math.floor(Math.random() * candidates.length)];
      try {
        model.motion(group, 0);
        post({ type: 'randomMotion', group });
      } catch (error) {
        post({ type: 'randomMotionError', group, message: String(error) });
      }
    }, 10 * 60 * 1000);
  }

  function stopMotionTimer() {
    if (motionTimer) {
      clearInterval(motionTimer);
      motionTimer = null;
    }
  }

  function setVisible(next) {
    visible = !!next;
    if (visible) {
      startMotionTimer();
      fitModel();
    } else {
      stopMotionTimer();
    }
  }

  function focusNormalized(nx, ny) {
    if (!model || typeof model.focus !== 'function') return;
    try {
      model.focus(nx * app.screen.width, ny * app.screen.height);
    } catch (_) { }
  }

  function focusAt(clientX, clientY) {
    if (!model || typeof model.focus !== 'function') return;
    const rect = canvas.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    const x = (clientX - rect.left) / rect.width;
    const y = (clientY - rect.top) / rect.height;
    try {
      model.focus(x * app.screen.width, y * app.screen.height);
    } catch (_) { }
  }

  function installPointerEvents() {
    document.addEventListener('contextmenu', (event) => {
      event.preventDefault();
      event.stopPropagation();
    });
    canvas.addEventListener('contextmenu', (event) => {
      event.preventDefault();
      event.stopPropagation();
    });

    canvas.addEventListener('pointerdown', (event) => {
      if (event.button === 2) {
        event.preventDefault();
        event.stopPropagation();
        if (!isInsideModel(event.clientX, event.clientY)) return;
        const now = Date.now();
        if (now - lastRightClickAt < 400) return;
        lastRightClickAt = now;
        post({ type: 'rightClick' });
        return;
      }
      if (event.button !== 0) return;
      // Drag starts only when the press is on the character itself; empty
      // canvas space is not forwarded to the WPF drag path.
      if (!isInsideModel(event.clientX, event.clientY)) return;
      event.preventDefault();
      try { canvas.setPointerCapture(event.pointerId); } catch (_) { }
      leftPointer = {
        pointerId: event.pointerId,
        startScreenX: event.screenX,
        startScreenY: event.screenY,
        lastScreenX: event.screenX,
        lastScreenY: event.screenY,
        dragging: false,
      };
    });

    canvas.addEventListener('pointermove', (event) => {
      focusAt(event.clientX, event.clientY);
      if (!leftPointer || leftPointer.pointerId !== event.pointerId) return;
      const dx = event.screenX - leftPointer.startScreenX;
      const dy = event.screenY - leftPointer.startScreenY;
      if (!leftPointer.dragging && (Math.abs(dx) > 4 || Math.abs(dy) > 4)) {
        leftPointer.dragging = true;
        post({
          type: 'dragStart',
          screenX: leftPointer.startScreenX,
          screenY: leftPointer.startScreenY,
        });
      }
      if (leftPointer.dragging) {
        leftPointer.lastScreenX = event.screenX;
        leftPointer.lastScreenY = event.screenY;
        post({ type: 'dragMove', screenX: event.screenX, screenY: event.screenY });
      }
    });

    const finishPointer = (event, cancelled) => {
      if (!leftPointer || leftPointer.pointerId !== event.pointerId) return;
      const wasDragging = leftPointer.dragging;
      leftPointer = null;
      try { canvas.releasePointerCapture(event.pointerId); } catch (_) { }
      if (wasDragging) {
        post({ type: 'dragEnd' });
      } else if (!cancelled) {
        const part = detectPart(event.clientX, event.clientY);
        if (part) post({ type: 'partClick', part });
      }
    };

    canvas.addEventListener('pointerup', (event) => finishPointer(event, false));
    canvas.addEventListener('pointercancel', (event) => finishPointer(event, true));
  }

  function listenToHost() {
    if (!window.chrome || !window.chrome.webview) return;
    window.chrome.webview.addEventListener('message', (event) => {
      let message = event.data;
      if (typeof message === 'string') {
        try { message = JSON.parse(message); } catch (_) { return; }
      }
      if (!message) return;
      if (message.type === 'input') {
        if (message.event === 'mouseMove' && model) {
          focusNormalized(Number(message.x || 0), Number(message.y || 0));
        } else if (message.event === 'mouseLeftDown' && model) {
          model.motion('Nod', 0);
        } else if (message.event === 'mouseRightDown') {
          post({ type: 'rightClick' });
        } else if (message.event === 'keyDown' && model) {
          const now = Date.now();
          if (now >= keyLockUntil) {
            keyLockUntil = now + 300;
            model.motion('Talk', 0);
          }
        }
      } else if (message.type === 'expression') {
        playLockedExpression(message.name);
      } else if (message.type === 'visibility') {
        setVisible(message.visible);
      } else if (message.type === 'layout') {
        lastHostLayoutAt = Date.now();
        layoutRect = {
          x: Number(message.x || 0),
          y: Number(message.y || 0),
          width: Number(message.width || 1),
          height: Number(message.height || 1),
        };
        fitModel();
      } else if (message.type === 'resize') {
        lastHostLayoutAt = Date.now();
        resizeRenderer();
        fitModel();
        window.dispatchEvent(new Event('resize'));
      } else if (message.type === 'syncExpression') {
        applyExpression(message.name);
      }
    });
  }

  /**
   * requestAnimationFrame with timestamp skipping. Visible = 30 FPS,
   * hidden/background = 15 FPS. Pixi's own ticker is stopped and driven from
   * this loop so the browser cannot render at 60 FPS behind our back.
   */
  function startRenderLoop() {
    if (renderHandle) return;
    const frame = (timestamp) => {
      renderHandle = requestAnimationFrame(frame);
      const target = visible ? TARGET_FPS_VISIBLE : TARGET_FPS_HIDDEN;
      const interval = 1000 / target;
      if (lastFrameAt && timestamp - lastFrameAt < interval - 0.6) return;
      lastFrameAt = timestamp;
      try {
        app.ticker.update(timestamp);
      } catch (error) {
        if (!tickerErrorLogged) {
          tickerErrorLogged = true;
          post({ type: 'pageError', message: '渲染循环异常：' + String(error), stack: String(error?.stack || '') });
        }
      }
      renderedFrames++;
      const elapsed = timestamp - statsStartedAt;
      if (elapsed >= 10000) {
        const measured = renderedFrames * 1000 / elapsed;
        post({ type: 'fps', fps: Number(measured.toFixed(1)), target });
        renderedFrames = 0;
        statsStartedAt = timestamp;
      }
    };
    renderHandle = requestAnimationFrame(frame);
  }

  function installResizeObserver() {
    if (typeof ResizeObserver !== 'function') return;
    const observer = new ResizeObserver(() => {
      // The host layout message is authoritative. The observer is only a
      // fallback for size changes that did not come through WPF.
      if (Date.now() - lastHostLayoutAt < 200) return;
      scheduleFit();
    });
    observer.observe(document.body);
    observer.observe(canvas);
    window.addEventListener('resize', () => {
      if (Date.now() - lastHostLayoutAt < 200) return;
      scheduleFit();
    });
  }

  async function start() {
    const [globalName, engine] = findEngine();
    probe();
    if (!window.PIXI || !window.Live2DCubismCore || !engine || !engine.Live2DModel || !engine.Live2DPlugin) {
      throw new Error('Live2D 运行时库加载不完整');
    }

    PIXI.extensions.add(engine.Live2DPlugin);
    const configure = engine.configureCubismSDK || engine.reconfigureCubismSDK;
    if (typeof configure === 'function') {
      try { configure({ memorySizeMB: 32 }); } catch (_) { /* optional tuning */ }
    }

    app = new PIXI.Application();
    await app.init({
      view: canvas,
      backgroundAlpha: 0,
      resizeTo: window,
      autoStart: false,
      resolution: window.devicePixelRatio || 1,
      autoDensity: true,
    });
    app.ticker.stop();

    model = await engine.Live2DModel.from(
      'https://live2d.local/models/seethrough_output/seethrough_output.model3.json',
      {
        autoFocus: false,
        autoHitTest: false,
        useHighPrecisionMask: false,
        textureOptions: { lod: 'single-auto' },
      }
    );
    origW = model.width;
    origH = model.height;
    motionGroups = Object.keys(
      model.internalModel?.motionManager?.definitions || model.settings?.motions || {}
    );
    hitAreaNames = Object.keys(model.internalModel?.hitAreas || {});
    app.stage.addChild(model);
    fitModel();
    applyExpression('微笑');
    window.live2dModel = model;
    installPointerEvents();
    installResizeObserver();
    startRenderLoop();
    probe();
    post({
      type: 'modelReady',
      globalName,
      motionGroups,
      hitAreas: hitAreaNames,
    });
  }

  window.addEventListener('error', (event) => {
    fail(event.message || 'Live2D 页面脚本异常');
  });

  listenToHost();
  start().catch((error) => fail(error && error.message ? error.message : error));
})();
