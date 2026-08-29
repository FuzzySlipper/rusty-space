# Space UI lane

`main.js` is the small DOM-only UI mounted alongside the Engine-owned canvas.
It provides static accessibility and physical-control guidance only.

`../scripts/generate-browser-bundle.mjs` copies that UI and the current Engine
Product Browser Host artifact into ignored `generated/product-bundle`. The
Engine runtime injects the only renderer-preload descriptor from admitted
product resources; Space currently has none.

This lane must not retain gameplay state, simulate input, render world
elements, create a canvas, or add an alternate transport.
