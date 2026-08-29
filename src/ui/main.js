/**
 * Mounts the small product-owned DOM layer beside the Engine-owned canvas.
 * It owns no world facts or input delivery; HUD numbers arrive only through
 * the Engine-admitted UI projection the product publishes from its flight
 * readout (contract `rusty.space.hud`: heading radians, planar speed).
 */
export function mountProductUi(root, context) {
  const panel = document.createElement('aside');
  panel.setAttribute('aria-label', 'Rusty Space controls');

  const title = document.createElement('h1');
  title.textContent = 'Rusty Space';
  panel.append(title);

  const controls = document.createElement('p');
  controls.textContent = 'W thrusts. A and D steer. R resets flight. F aborts.';
  panel.append(controls);

  const hud = document.createElement('p');
  hud.textContent = 'heading — speed —';
  panel.append(hud);

  root.append(panel);

  const projection = context?.projection;
  let unsubscribe;
  if (projection?.subscribe !== undefined) {
    unsubscribe = projection.subscribe((envelope) => {
      if (envelope === null || typeof envelope.value !== 'object' || envelope.value === null) {
        return;
      }
      const { heading, speed } = envelope.value;
      const headingDegrees = Number.isFinite(heading)
        ? Math.round(((heading % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI) * (180 / Math.PI))
        : null;
      hud.textContent = `heading ${headingDegrees ?? '—'}° speed ${
        Number.isFinite(speed) ? Number(speed).toFixed(1) : '—'
      }`;
    });
  }

  return Object.freeze({
    dispose: () => {
      unsubscribe?.();
      panel.remove();
    },
  });
}
