import { mountRustyApplication, type RustyApplicationUiContext, type RustyApplicationUiOwner } from '@rusty-engine/application-host';

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
  frame: Readonly<Record<string, unknown>>;
  readout: ServerReadout;
}

function mountUi(root: HTMLElement, context: RustyApplicationUiContext): RustyApplicationUiOwner {
  const surface = document.createElement('main');
  surface.className = 'space-surface';
  surface.setAttribute('aria-label', 'Rusty Space viewport');

  const label = document.createElement('p');
  label.className = 'space-label';
  label.dataset.testid = 'space-label';
  label.textContent = 'Rusty Space · connecting…';
  surface.append(label);
  root.append(surface);

  // Top-down navigation view: look straight down at the XZ flight plane.
  context.renderer.setCameraPose({ position: [0, 25, 0], pitchDegrees: -90, yawDegrees: 0 });

  // Classic Asteroids input: up = thrust, left/right = turn. Input is local
  // presentation only — it routes a typed intent to the Rust host.
  const thrustKeys = new Set(['ArrowUp', 'KeyW']);
  const leftKeys = new Set(['ArrowLeft', 'KeyA']);
  const rightKeys = new Set(['ArrowRight', 'KeyD']);
  const held = new Set<string>();

  const socket = new WebSocket(`ws://${location.host}${SESSION_PATH}`);
  let open = false;

  const currentIntent = (): { throttle: number; turn: number } => {
    const thrust = [...thrustKeys].some((key) => held.has(key));
    const left = [...leftKeys].some((key) => held.has(key));
    const right = [...rightKeys].some((key) => held.has(key));
    return { throttle: thrust ? 1 : 0, turn: right ? 1 : left ? -1 : 0 };
  };

  const sendIntent = (): void => {
    if (!open) return;
    socket.send(JSON.stringify(currentIntent()));
  };

  const updateHud = (readout: ServerReadout): void => {
    const speed = Math.hypot(readout.linearVelocity.x, readout.linearVelocity.z);
    const degrees = ((readout.heading * 180) / Math.PI).toFixed(0);
    label.textContent =
      `Rusty Space · pos ${readout.position.x.toFixed(1)}, ${readout.position.z.toFixed(1)} · ` +
      `speed ${speed.toFixed(1)} · heading ${degrees}°`;
  };

  socket.addEventListener('open', () => {
    open = true;
    sendIntent();
  });
  socket.addEventListener('message', (event) => {
    const update = JSON.parse(String(event.data)) as ServerUpdate;
    context.renderer.applyFrame(update.frame);
    context.renderer.renderOnce();
    updateHud(update.readout);
  });
  socket.addEventListener('close', () => {
    open = false;
    label.textContent = 'Rusty Space · disconnected';
  });
  socket.addEventListener('error', () => {
    label.textContent = 'Rusty Space · socket error';
  });

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
  context.renderer.renderOnce();

  return {
    dispose: () => {
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
      socket.close();
      surface.remove();
    },
  };
}
