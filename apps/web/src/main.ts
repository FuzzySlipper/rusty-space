import {
  mountRustyApplication,
  type RustyApplicationFrame,
  type RustyApplicationUiContext,
  type RustyApplicationUiOwner,
} from '@rusty-engine/application-host';

import './styles.css';

const root = document.querySelector<HTMLElement>('#application');
if (root === null) throw new Error('Rusty Space application root is missing');

const SESSION_PATH = '/api/session';

await mountRustyApplication({
  root,
  initialInteractionMode: 'gameplay',
  loadingLabel: 'Loading Rusty Space…',
  failureLabel: 'Rusty Space failed to start',
  presentationAspectBounds: { minimum: 4 / 3, maximum: 16 / 9 },
  renderer: { clearColor: 0x071217, pixelRatio: 1 },
  mountUi,
});

interface ServerReadout {
  position: { x: number; z: number };
  heading: number;
  linearVelocity: { x: number; z: number };
  angularVelocity: number;
  throttleLevel: number;
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
  receipt: { sequence: number };
}

type ServerMessage = BaselineMessage | UpdateMessage | CommandRejectedMessage | CommandReceiptMessage;

function mountUi(root: HTMLElement, context: RustyApplicationUiContext): RustyApplicationUiOwner {
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
  root.append(surface);

  // Top-down navigation view: look straight down at the XZ flight plane.
  context.renderer.setCameraPose({ position: [0, 25, 0], pitchDegrees: -90, yawDegrees: 0 });

  // Classic Asteroids input: up = thrust, left/right = turn. Input is local
  // presentation only — it routes a typed intent to the Rust host.
  const thrustKeys = new Set(['ArrowUp', 'KeyW']);
  const leftKeys = new Set(['ArrowLeft', 'KeyA']);
  const rightKeys = new Set(['ArrowRight', 'KeyD']);
  const held = new Set<string>();

  let socket: WebSocket | null = null;
  let activeGeneration: number | null = null;
  let baselineApplied = false;
  let lastUpdateSequence: number | null = null;
  let terminalFailure: string | null = null;
  let frameDelivery: Promise<void> = Promise.resolve();
  let reconnectTimer: number | undefined;
  let disposed = false;

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

  const clearHeldInput = (): void => {
    held.clear();
    sendIntent();
  };

  const updateHud = (readout: ServerReadout): void => {
    const speed = Math.hypot(readout.linearVelocity.x, readout.linearVelocity.z);
    const degrees = ((readout.heading * 180) / Math.PI).toFixed(0);
    label.textContent =
      `Rusty Space · pos ${readout.position.x.toFixed(1)}, ${readout.position.z.toFixed(1)} · ` +
      `speed ${speed.toFixed(1)} · heading ${degrees}°`;
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
      context.renderer.renderOnce();
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
    activeGeneration = null;
    baselineApplied = false;
    lastUpdateSequence = null;
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
    if (!context.ui.allowsGameplayInput(event)) return;
    if (thrustKeys.has(event.code) || leftKeys.has(event.code) || rightKeys.has(event.code)) {
      held.add(event.code);
      event.preventDefault();
      sendIntent();
    }
  };
  const onKeyUp = (event: KeyboardEvent): void => {
    if (held.delete(event.code)) {
      sendIntent();
    }
  };
  window.addEventListener('keydown', onKeyDown);
  window.addEventListener('keyup', onKeyUp);
  window.addEventListener('blur', clearHeldInput);
  window.addEventListener('pagehide', clearHeldInput);
  connect();
  context.renderer.renderOnce();

  return {
    dispose: () => {
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
      window.removeEventListener('blur', clearHeldInput);
      window.removeEventListener('pagehide', clearHeldInput);
      disposed = true;
      clearHeldInput();
      if (reconnectTimer !== undefined) window.clearTimeout(reconnectTimer);
      socket?.close();
      surface.remove();
    },
  };
}
