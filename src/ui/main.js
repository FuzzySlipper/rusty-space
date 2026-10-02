/**
 * Mounts the small product-owned DOM layer beside the Engine-owned canvas.
 * It owns no world facts or input delivery; HUD numbers arrive only through
 * the Engine-admitted UI projection the product publishes from its flight
 * readout and telemetry (contract `rusty.space.hud`: heading radians, planar
 * speed, thrust share, felt acceleration, turn rate, coupling the hull is
 * answering the environment at, the flow the hull is sitting in, how far
 * apart the two sides of the effector pair have gotten, and how hard the hull
 * last arrived at something).
 */
export function mountProductUi(root, context) {
  const panel = document.createElement('aside');
  panel.setAttribute('aria-label', 'Rusty Space controls');

  const title = document.createElement('h1');
  title.textContent = 'Rusty Space';
  panel.append(title);

  const controls = document.createElement('p');
  controls.textContent = 'W thrust · A/D steer · Q/E coupling · X uncouple · T attitude hold · G hold patch · C chart/helm · wheel zoom · R reset · F abort. Xbox: RT thrust, left stick steer, stick click patch, Back reset.';
  panel.append(controls);

  const hud = document.createElement('p');
  hud.textContent = 'heading — speed — thrust — accel — turn — coupling — flow — asym — impact —';
  panel.append(hud);

  const systems = document.createElement('p');
  systems.setAttribute('role', 'status');
  systems.textContent = 'Short burns leave power for the approach. Coast to recharge and cool.';
  panel.append(systems);

  root.append(panel);

  const projection = context?.projection;
  let unsubscribe;
  if (projection?.subscribe !== undefined) {
    unsubscribe = projection.subscribe((envelope) => {
      if (envelope === null || typeof envelope.value !== 'object' || envelope.value === null) {
        return;
      }
      const { heading, speed, thrust, accel, turn, coupling, flow, asym, impact,
        reserve, heat, output, low, hot, jams, patch } = envelope.value;
      const headingDegrees = Number.isFinite(heading)
        ? Math.round(((heading % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI) * (180 / Math.PI))
        : null;
      const number = (value, places) => (Number.isFinite(value) ? Number(value).toFixed(places) : '—');
      hud.textContent = `heading ${headingDegrees ?? '—'}° speed ${number(speed, 1)
        } thrust ${number(thrust, 2)} accel ${number(accel, 2)} turn ${number(turn, 2)
        } coupling ${number(coupling, 2)} flow ${number(flow, 2)} asym ${number(asym, 2)
        } impact ${number(impact, 2)}`;
      const condition = jams > 0
        ? `Jammed steering — hold G while flying to free the vane${patch > 0 ? ` (${Math.round(patch * 100)}%)` : ''}. The dent stays.`
        : hot > 0 ? 'Drive warming — coast to cool; long burns lose thrust.'
        : low > 0 ? 'Reserve low — ease thrust to recharge. Steering still answers.'
        : 'Short burns leave power for the approach. Coast to recharge and cool.';
      systems.textContent = `${condition} Reserve ${number(reserve * 100, 0)}% · drive heat ${number(heat, 2)} · available thrust ${number(output * 100, 0)}%`;
    });
  }

  return Object.freeze({
    dispose: () => {
      unsubscribe?.();
      panel.remove();
    },
  });
}
