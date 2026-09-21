/**
 * Mounts the small product-owned DOM layer beside the Engine-owned canvas.
 * It owns no world facts or input delivery; HUD numbers arrive only through
 * the Engine-admitted UI projection the product publishes from its flight
 * readout and telemetry (contract `rusty.space.hud`: heading radians, planar
 * speed, thrust share, felt acceleration, turn rate, coupling the hull is
 * answering the environment at, the flow the hull is sitting in, and how far
 * apart the two sides of the effector pair have gotten).
 */
export function mountProductUi(root, context) {
  const panel = document.createElement('aside');
  panel.setAttribute('aria-label', 'Rusty Space controls');

  const title = document.createElement('h1');
  title.textContent = 'Rusty Space';
  panel.append(title);

  const controls = document.createElement('p');
  controls.textContent = 'W thrusts. A and D steer. Mouse wheel zooms. R resets flight. F aborts. Xbox: RT thrusts proportionally, left stick steers, LB/RB steer, Back resets.';
  panel.append(controls);

  const hud = document.createElement('p');
  hud.textContent = 'heading — speed — thrust — accel — turn — coupling — flow — asym —';
  panel.append(hud);

  root.append(panel);

  const projection = context?.projection;
  let unsubscribe;
  if (projection?.subscribe !== undefined) {
    unsubscribe = projection.subscribe((envelope) => {
      if (envelope === null || typeof envelope.value !== 'object' || envelope.value === null) {
        return;
      }
      const { heading, speed, thrust, accel, turn, coupling, flow, asym } = envelope.value;
      const headingDegrees = Number.isFinite(heading)
        ? Math.round(((heading % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI) * (180 / Math.PI))
        : null;
      const number = (value, places) => (Number.isFinite(value) ? Number(value).toFixed(places) : '—');
      hud.textContent = `heading ${headingDegrees ?? '—'}° speed ${number(speed, 1)
        } thrust ${number(thrust, 2)} accel ${number(accel, 2)} turn ${number(turn, 2)
        } coupling ${number(coupling, 2)} flow ${number(flow, 2)} asym ${number(asym, 2)}`;
    });
  }

  return Object.freeze({
    dispose: () => {
      unsubscribe?.();
      panel.remove();
    },
  });
}
