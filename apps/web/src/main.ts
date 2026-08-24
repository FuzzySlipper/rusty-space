import {
  mountRustyApplication,
  type RustyApplicationHost,
  type RustyApplicationFrame,
  type RustyApplicationInteractionMode,
  type RustyApplicationUiContext,
  type RustyApplicationUiOwner,
} from '@rusty-engine/application-host';

import './styles.css';

const root = document.querySelector<HTMLElement>('#application');
if (root === null) throw new Error('Rusty Space application root is missing');

const SESSION_PATH = '/api/session';

interface RustySpaceBrowserTestProbe {
  application?: RustyApplicationHost;
  disposeAndRemount?: () => Promise<void>;
  setInteractionMode?: (mode: RustyApplicationInteractionMode) => void;
}

declare global {
  interface Window {
    __rustySpaceBrowserTestProbe?: RustySpaceBrowserTestProbe;
  }
}

const mountApplication = (): Promise<RustyApplicationHost> => mountRustyApplication({
  root,
  initialInteractionMode: 'gameplay',
  loadingLabel: 'Loading Rusty Space…',
  failureLabel: 'Rusty Space failed to start',
  presentationAspectBounds: { minimum: 4 / 3, maximum: 16 / 9 },
  renderer: { clearColor: 0x071217, pixelRatio: 1 },
  mountUi,
});

let application = await mountApplication();

const testProbe = window.__rustySpaceBrowserTestProbe;
const publishTestProbe = (): void => {
  if (testProbe === undefined) return;
  testProbe.application = application;
  testProbe.setInteractionMode = (mode) => application.ui.setInteractionMode(mode);
  testProbe.disposeAndRemount = async () => {
    await application.dispose();
    application = await mountApplication();
    publishTestProbe();
  };
};
publishTestProbe();

// Keep the Engine host reachable for the browser lifecycle. The host owns the
// renderer surface and the mounted UI owner; disposal on a real page exit
// releases both before the document is discarded. A BFCache page remains
// mounted and can be restored, while the mounted UI below still neutralizes
// gameplay input on every pagehide event.
const disposeApplicationOnExit = (event: PageTransitionEvent): void => {
  // Synthetic pagehide events used to verify neutralization do not carry the
  // PageTransitionEvent persisted bit; only a real navigation or an explicit
  // PageTransitionEvent requests teardown here.
  if (typeof event.persisted === 'boolean' && !event.persisted) void application.dispose();
};
window.addEventListener('pagehide', disposeApplicationOnExit);
window.addEventListener('beforeunload', () => void application.dispose(), { once: true });

interface ServerReadout {
  position: { x: number; z: number };
  heading: number;
  linearVelocity: { x: number; z: number };
  angularVelocity: number;
  throttleLevel: number;
  field: {
    flowVelocity: { x: number; z: number };
    intensity: number;
    gradient: [[number, number], [number, number]];
    turbulence: { x: number; z: number };
  };
}

interface ServerUpdate {
  sequence: number;
  tick: number;
  frame: RustyApplicationFrame;
  readout: ServerReadout;
}

interface BaselineMessage {
  type: 'baseline';
  generation: number;
  update: ServerUpdate;
}

interface UpdateMessage {
  type: 'update';
  generation: number;
  update: ServerUpdate;
}

interface CommandRejectedMessage {
  type: 'commandRejected';
  generation: number;
  code: 'malformedCommand' | 'unsupportedCommand' | 'staleGeneration' | 'invalidCommand';
  message: string;
}

interface CommandReceiptMessage {
  type: 'commandReceipt';
  generation: number;
  receipt: {
    sequence: number;
    command: { type: 'setFlightIntent' } | { type: 'resetFlight' };
  };
}

type ServerMessage = BaselineMessage | UpdateMessage | CommandRejectedMessage | CommandReceiptMessage;

function mountUi(root: HTMLElement, context: RustyApplicationUiContext): Promise<RustyApplicationUiOwner> {
  const surface = document.createElement('main');
  surface.className = 'space-surface';
  surface.setAttribute('aria-label', 'Rusty Space viewport');

  const label = document.createElement('p');
  label.className = 'space-label';
  label.dataset.testid = 'space-label';
  label.textContent = 'Rusty Space · connecting…';
  surface.append(label);

  const status = document.createElement('p');
  status.className = 'space-status';
  status.dataset.testid = 'session-status';
  status.textContent = 'Session connecting…';
  surface.append(status);

  const controls = document.createElement('p');
  controls.className = 'space-controls';
  controls.dataset.testid = 'space-controls';
  controls.textContent = 'W / ↑ thrust · A/D / ←/→ turn · R reset · wheel zoom';
  surface.append(controls);
  root.append(surface);

  // Top-down navigation view: the browser presents the Rust-authoritative
  // target with lag; it never predicts or changes flight position.
  const defaultCameraHeight = 25;
  const minimumCameraHeight = 8;
  const maximumCameraHeight = 60;
  const camera = {
    x: 0,
    z: 0,
    velocityX: 0,
    velocityZ: 0,
    targetX: 0,
    targetZ: 0,
    height: defaultCameraHeight,
  };
  let cameraFrame: number | undefined;
  let cameraLastTime: number | undefined;
  const renderCamera = (): void => {
    context.renderer.setCameraPose({
      position: [camera.x, camera.height, camera.z],
      pitchDegrees: -90,
      yawDegrees: 0,
    });
    // The deliberately cheap CSS star layer is parallaxed from the same
    // presentation state, giving camera motion a visible reference without a
    // second canvas or any gameplay authority in the DOM.
    surface.style.setProperty('--star-near-x', `${-camera.x * 18}px`);
    surface.style.setProperty('--star-near-z', `${-camera.z * 18}px`);
    surface.style.setProperty('--star-far-x', `${-camera.x * 5}px`);
    surface.style.setProperty('--star-far-z', `${-camera.z * 5}px`);
    context.renderer.renderOnce();
  };
  const snapCamera = (readout: ServerReadout): void => {
    camera.x = readout.position.x;
    camera.z = readout.position.z;
    camera.targetX = readout.position.x;
    camera.targetZ = readout.position.z;
    camera.velocityX = 0;
    camera.velocityZ = 0;
    renderCamera();
  };
  const targetCamera = (readout: ServerReadout): void => {
    camera.targetX = readout.position.x;
    camera.targetZ = readout.position.z;
  };
  const animateCamera = (time: number): void => {
    if (disposed) return;
    const elapsed = cameraLastTime === undefined ? 0 : Math.min((time - cameraLastTime) / 1000, 0.05);
    cameraLastTime = time;
    // Semi-implicit Euler integration of a critically damped spring. The
    // cap keeps a backgrounded tab from hurling the camera across the scene.
    const omega = 8;
    const advanceAxis = (position: number, velocity: number, target: number): [number, number] => {
      const acceleration = omega * omega * (target - position) - 2 * omega * velocity;
      const nextVelocity = velocity + acceleration * elapsed;
      return [position + nextVelocity * elapsed, nextVelocity];
    };
    [camera.x, camera.velocityX] = advanceAxis(camera.x, camera.velocityX, camera.targetX);
    [camera.z, camera.velocityZ] = advanceAxis(camera.z, camera.velocityZ, camera.targetZ);
    renderCamera();
    cameraFrame = window.requestAnimationFrame(animateCamera);
  };
  renderCamera();

  // Classic Asteroids input: up = thrust, left/right = turn. Input is local
  // presentation only — it routes a typed intent to the Rust host.
  const thrustKeys = new Set(['ArrowUp', 'KeyW']);
  const leftKeys = new Set(['ArrowLeft', 'KeyA']);
  const rightKeys = new Set(['ArrowRight', 'KeyD']);
  const movementKeys = new Set([...thrustKeys, ...leftKeys, ...rightKeys]);
  const held = new Set<string>();
  // A reset (or another gameplay neutralization) clears intent immediately,
  // but browsers may subsequently deliver `repeat: true` for a physically
  // held key. Keep that old press inert until its keyup; a genuinely fresh
  // non-repeat keydown deliberately re-arms it, including after a focus
  // transition where a keyup may have been lost.
  const suppressedUntilKeyUp = new Set<string>();

  let socket: WebSocket | null = null;
  let activeGeneration: number | null = null;
  let baselineApplied = false;
  let lastUpdateSequence: number | null = null;
  let resetUpdatePending = false;
  let terminalFailure: string | null = null;
  let frameDelivery: Promise<void> = Promise.resolve();
  let reconnectTimer: number | undefined;
  let interactionModeTimer: number | undefined;
  let disposed = false;
  let startupSettled = false;
  let resolveStartup!: (owner: RustyApplicationUiOwner) => void;
  let rejectStartup!: (reason: Error) => void;
  const startup = new Promise<RustyApplicationUiOwner>((resolve, reject) => {
    resolveStartup = resolve;
    rejectStartup = reject;
  });
  let disposeUi = (): void => undefined;

  const currentIntent = (): { throttle: number; turn: number } => {
    const thrust = [...thrustKeys].some((key) => held.has(key));
    const left = [...leftKeys].some((key) => held.has(key));
    const right = [...rightKeys].some((key) => held.has(key));
    return { throttle: thrust ? 1 : 0, turn: right ? 1 : left ? -1 : 0 };
  };

  const sendIntent = (): void => {
    if (socket?.readyState !== WebSocket.OPEN || activeGeneration === null || !baselineApplied) return;
    try {
      socket.send(JSON.stringify({ type: 'setFlightIntent', generation: activeGeneration, ...currentIntent() }));
    } catch {
      // A browser may transition from OPEN to CLOSING between the readiness
      // check and send. The host's release guard still neutralizes that lease.
    }
  };

  const sendReset = (): void => {
    if (socket?.readyState !== WebSocket.OPEN || activeGeneration === null || !baselineApplied) return;
    try {
      socket.send(JSON.stringify({ type: 'resetFlight', generation: activeGeneration }));
    } catch {
      // The session release guard neutralizes a lease if a close races this
      // presentation-only send.
    }
  };

  const clearHeldInput = (): void => {
    for (const key of held) {
      if (movementKeys.has(key)) suppressedUntilKeyUp.add(key);
    }
    held.clear();
    sendIntent();
  };

  // The public UI context intentionally exposes a snapshot rather than a
  // product-specific mode-change event. Polling this coarse state keeps an
  // intent lease from surviving a host-owned transition into interface/modal
  // mode when no keyboard event happens to follow it.
  const neutralizeInputOutsideGameplay = (): void => {
    if (!disposed && held.size > 0 && context.ui.interactionMode() !== 'gameplay') {
      clearHeldInput();
    }
  };

  const updateHud = (readout: ServerReadout): void => {
    const speed = Math.hypot(readout.linearVelocity.x, readout.linearVelocity.z);
    const degrees = ((readout.heading * 180) / Math.PI).toFixed(0);
    label.textContent =
      `Rusty Space · pos ${readout.position.x.toFixed(1)}, ${readout.position.z.toFixed(1)} · ` +
      `speed ${speed.toFixed(1)} · heading ${degrees}° · ` +
      `field flow ${readout.field.flowVelocity.x.toFixed(1)}, ${readout.field.flowVelocity.z.toFixed(1)} ` +
      `· intensity ${readout.field.intensity.toFixed(2)}`;
  };

  const failFrame = (
    kind: 'baseline failed' | 'frame failed',
    reason: unknown,
    origin?: WebSocket,
  ): void => {
    if (origin !== undefined && (disposed || socket !== origin)) return;
    const detail = reason instanceof Error ? reason.message : String(reason);
    terminalFailure = `${kind}: ${detail}`;
    baselineApplied = false;
    label.textContent = `Rusty Space · ${terminalFailure}`;
    status.textContent = `Session ${terminalFailure}`;
    origin?.close();
    if (!startupSettled) {
      startupSettled = true;
      disposeUi();
      rejectStartup(new Error(terminalFailure));
    }
  };

  const applyFrame = async (update: ServerUpdate, baseline: boolean, origin: WebSocket): Promise<boolean> => {
    try {
      const receipt = baseline
        ? await context.renderer.replaceFrame(update.frame)
        : context.renderer.applyFrame(update.frame);
      // `replaceFrame` is asynchronous. A later reconnect may have replaced
      // this socket while it was pending, in which case this old baseline must
      // not render or update any UI/session state.
      if (disposed || socket !== origin) return false;
      if (!receipt.applied) {
        failFrame(
          baseline ? 'baseline failed' : 'frame failed',
          receipt.diagnostics[0]?.message ?? 'renderer rejected the projected frame',
          origin,
        );
        return false;
      }
      // Baselines are complete renderer replacements and should never inherit
      // the old transport's camera position. An accepted reset likewise snaps
      // immediately; ordinary Rust updates advance a lagging presentation
      // target only.
      if (baseline || resetUpdatePending) {
        snapCamera(update.readout);
        resetUpdatePending = false;
      } else {
        targetCamera(update.readout);
      }
      updateHud(update.readout);
      return true;
    } catch (error) {
      failFrame(baseline ? 'baseline failed' : 'frame failed', error, origin);
      return false;
    }
  };

  const scheduleReconnect = (): void => {
    if (disposed || reconnectTimer !== undefined) return;
    reconnectTimer = window.setTimeout(() => {
      reconnectTimer = undefined;
      connect();
    }, 150);
  };

  const connect = (): void => {
    if (disposed) return;
    clearHeldInput();
    activeGeneration = null;
    baselineApplied = false;
    lastUpdateSequence = null;
    resetUpdatePending = false;
    terminalFailure = null;
    label.textContent = 'Rusty Space · connecting…';
    status.textContent = 'Session connecting…';
    const nextSocket = new WebSocket(`${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}${SESSION_PATH}`);
    socket = nextSocket;
    nextSocket.addEventListener('message', (event) => {
      if (disposed || socket !== nextSocket) return;
      let message: ServerMessage;
      try {
        message = JSON.parse(String(event.data)) as ServerMessage;
      } catch {
        failFrame('baseline failed', 'malformed server message', nextSocket);
        return;
      }
      if (message.type === 'baseline') {
        frameDelivery = frameDelivery.then(async () => {
          if (disposed || socket !== nextSocket) return;
          activeGeneration = message.generation;
          baselineApplied = false;
          lastUpdateSequence = null;
          if (await applyFrame(message.update, true, nextSocket)) {
            baselineApplied = true;
            lastUpdateSequence = message.update.sequence;
            status.textContent = `Session ${message.generation} ready`;
            if (!startupSettled) {
              startupSettled = true;
              resolveStartup(owner);
            }
          }
        }).catch((error: unknown) => failFrame('baseline failed', error, nextSocket));
        return;
      }
      if (message.type === 'update') {
        frameDelivery = frameDelivery.then(async () => {
          if (disposed || socket !== nextSocket) return;
          if (!baselineApplied || message.generation !== activeGeneration) {
            failFrame('frame failed', 'update arrived before its matching baseline', nextSocket);
            return;
          }
          if (lastUpdateSequence !== null && message.update.sequence <= lastUpdateSequence) return;
          if (await applyFrame(message.update, false, nextSocket)) lastUpdateSequence = message.update.sequence;
        }).catch((error: unknown) => failFrame('frame failed', error, nextSocket));
        return;
      }
      if (message.type === 'commandRejected') {
        if (message.generation === activeGeneration) {
          status.textContent = `Controls rejected: ${message.code}`;
        }
      } else if (message.type === 'commandReceipt' && message.generation === activeGeneration) {
        if (message.receipt.command.type === 'resetFlight') resetUpdatePending = true;
        status.textContent = `Session ${message.generation} command ${message.receipt.sequence} accepted`;
      }
    });
    nextSocket.addEventListener('close', () => {
      if (disposed || socket !== nextSocket) return;
      clearHeldInput();
      socket = null;
      activeGeneration = null;
      baselineApplied = false;
      lastUpdateSequence = null;
      resetUpdatePending = false;
      if (terminalFailure === null) {
        label.textContent = 'Rusty Space · disconnected';
        status.textContent = 'Session disconnected; reconnecting…';
        scheduleReconnect();
      }
    });
    nextSocket.addEventListener('error', () => {
      if (!disposed && socket === nextSocket && terminalFailure === null) {
        clearHeldInput();
        label.textContent = 'Rusty Space · socket error';
        status.textContent = 'Session transport error';
      }
    });
  };

  const onKeyDown = (event: KeyboardEvent): void => {
    if (!context.ui.allowsGameplayInput(event)) {
      clearHeldInput();
      return;
    }
    if (event.code === 'KeyR') {
      if (!event.repeat) {
        // A held movement key must be deliberately pressed again after reset;
        // clearing this local set prevents keyboard autorepeat from reviving
        // old thrust intent.
        clearHeldInput();
        sendReset();
      }
      event.preventDefault();
      return;
    }
    if (thrustKeys.has(event.code) || leftKeys.has(event.code) || rightKeys.has(event.code)) {
      if (event.repeat && suppressedUntilKeyUp.has(event.code)) {
        event.preventDefault();
        return;
      }
      if (!event.repeat) suppressedUntilKeyUp.delete(event.code);
      held.add(event.code);
      event.preventDefault();
      sendIntent();
    }
  };
  const onKeyUp = (event: KeyboardEvent): void => {
    if (!context.ui.allowsGameplayInput(event)) {
      clearHeldInput();
      return;
    }
    const releasedSuppressedKey = suppressedUntilKeyUp.delete(event.code);
    if (held.delete(event.code) || releasedSuppressedKey) {
      sendIntent();
    }
  };
  const onVisibilityChange = (): void => {
    if (document.visibilityState === 'hidden') clearHeldInput();
  };
  const onWheel = (event: WheelEvent): void => {
    if (!context.ui.allowsGameplayInput(event)) return;
    const nextHeight = Math.min(
      maximumCameraHeight,
      Math.max(minimumCameraHeight, camera.height + event.deltaY * 0.025),
    );
    if (Number.isFinite(nextHeight)) camera.height = nextHeight;
    event.preventDefault();
    renderCamera();
  };
  window.addEventListener('keydown', onKeyDown);
  window.addEventListener('keyup', onKeyUp);
  window.addEventListener('blur', clearHeldInput);
  window.addEventListener('pagehide', clearHeldInput);
  document.addEventListener('visibilitychange', onVisibilityChange);
  window.addEventListener('wheel', onWheel, { passive: false });
  interactionModeTimer = window.setInterval(neutralizeInputOutsideGameplay, 50);
  connect();
  cameraFrame = window.requestAnimationFrame(animateCamera);

  const owner: RustyApplicationUiOwner = {
    dispose: () => disposeUi(),
  };
  disposeUi = () => {
    if (disposed) return;
    window.removeEventListener('keydown', onKeyDown);
    window.removeEventListener('keyup', onKeyUp);
    window.removeEventListener('blur', clearHeldInput);
    window.removeEventListener('pagehide', clearHeldInput);
    window.removeEventListener('wheel', onWheel);
    clearHeldInput();
    disposed = true;
    document.removeEventListener('visibilitychange', onVisibilityChange);
    if (interactionModeTimer !== undefined) window.clearInterval(interactionModeTimer);
    if (reconnectTimer !== undefined) window.clearTimeout(reconnectTimer);
    if (cameraFrame !== undefined) window.cancelAnimationFrame(cameraFrame);
    socket?.close();
    surface.remove();
  };

  return startup;
}
