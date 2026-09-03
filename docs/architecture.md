# Rusty Space architecture

Rusty Space is a C# downstream product. Its product project consumes one
immutable `Rusty.Engine` SDK package; the package generates the internal
CoreCLR and NativeAOT composition under the project's ignored `obj/` tree.
The matching runtime pack supplies `rusty dev`, the Rust host, and Engine's
browser/renderer assets.

```text
Product.Game (safe C# product state and domain behavior)
  -> packaged Rusty.Engine service contracts
  -> `rusty dev` host/runtime (CoreCLR for ordinary development)
  -> Engine canvas, renderer, input, spatial, resources, and lifecycle

src/ui/main.js (product-owned DOM UI)
  -> staged as product UI; no world renderer or gameplay authority
```

The product package is pinned to `Rusty.Engine` `0.1.0-dev.cabba0f` and
`.runtime/runtime-pack-cabba0f`. These artifacts carry a matching generated
ABI identity. Keep the pair together and let the host reject a mismatch;
products do not add version negotiation, copied Engine assets, or handwritten
interop.

## Ownership

The product decides; the Engine guarantees. Space owns flight commands,
inertial state, field meaning, tuning, camera policy, presentation facts, HUD
projection, and lifecycle policy. The Engine owns update admission and clock
facts, input delivery, Dynamics and Camera mechanisms, Appearance resources and
retained frames, canvas/backend integration, host lifecycle, and UI transport.

`SpaceProduct` is the lifecycle entrypoint. `SpaceProductComposition` wires
the named product owners. `SpaceFlight` translates product commands into
Engine Dynamics actions; `SpacePresentation` translates product readouts into
Engine Appearance and UI facts; `TrackingCamera` owns product framing policy
around the Engine camera service. None of these classes is a second host loop
or renderer.

## Runtime lanes

The standard launch path is:

```bash
./.runtime/runtime-pack-cabba0f/bin/rusty dev \
  --runtime ./.runtime/runtime-pack-cabba0f \
  --project ./src/Product.Game/Product.Game.csproj \
  --live-debug --port 8787
```

`rusty dev` builds and stages a loose Product directory, loads Product.Game
through CoreCLR, and serves the product UI alongside the Engine-owned canvas.
NativeAOT is a separate explicit fidelity/release operation through the SDK's
`VerifyRustyEngineAot` target. It is not a reason to keep a checked bridge
project or a custom product host in this repository.

Engine contributors can select a source checkout only with the explicit
`--engine-source` option. That option supplies matching source-build MSBuild
properties. No normal command may infer an adjacent `rusty-engine` checkout,
run Cargo, copy an Engine browser bundle, or regenerate bindings downstream.

## Missing capabilities

When the safe generated SDK cannot express a product need, record the exact
Engine-owned capability and stop that slice. A clear upstream request is a
valid result. Do not replace it with a C# native shim, a browser workaround, a
private loop, or a second renderer. Product design notes under `docs/ideas/`
remain useful donor/provenance material, but they do not override the current
packaged boundary.
