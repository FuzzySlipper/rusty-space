import { mountRustyApplication, type RustyApplicationContent, type RustyApplicationUiContext, type RustyApplicationUiOwner } from '@rusty-engine/application-host';

import './styles.css';

const root = document.querySelector<HTMLElement>('#application');
if (root === null) throw new Error('template application root is missing');

const initialContent = await loadRustProjectedContent();

await mountRustyApplication({
  root,
  initialInteractionMode: 'gameplay',
  loadingLabel: 'Loading Rust-owned template scene…',
  failureLabel: 'Rusty Template failed to start',
  presentationAspectBounds: { minimum: 4 / 3, maximum: 16 / 9 },
  renderer: { clearColor: 0x071217, initialContent, pixelRatio: 1 },
  mountUi,
});

async function loadRustProjectedContent(): Promise<RustyApplicationContent> {
  const response = await fetch('/content/initial-frame.json', { cache: 'no-store' });
  if (!response.ok) throw new Error(`Rust frame export is unavailable (${String(response.status)})`);
  return { frame: await response.json() as Readonly<Record<string, unknown>> };
}

function mountUi(root: HTMLElement, context: RustyApplicationUiContext): RustyApplicationUiOwner {
  const surface = document.createElement('main');
  surface.className = 'template-surface';
  surface.setAttribute('aria-label', 'Rusty Template viewport');

  const label = document.createElement('p');
  label.className = 'template-label';
  label.dataset.testid = 'template-label';
  label.textContent = 'Rust owns gameplay and the projected cube · TypeScript presents this label';
  surface.append(label);
  root.append(surface);

  const onKeyDown = (event: KeyboardEvent): void => {
    if (!context.ui.allowsGameplayInput(event)) return;
    // This is intentionally only a guarded input seam. A real product routes
    // a typed request to its Rust service; this static template has no live loop.
    if (event.code === 'Space') event.preventDefault();
  };
  window.addEventListener('keydown', onKeyDown);
  context.renderer.renderOnce();

  return {
    dispose: () => {
      window.removeEventListener('keydown', onKeyDown);
      surface.remove();
    },
  };
}

