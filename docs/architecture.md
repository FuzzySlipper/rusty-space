# Rusty Space architecture

Rusty Space is a C# downstream product. Its product project consumes one
immutable `Rusty.Engine` SDK package; the package generates the internal
CoreCLR and NativeAOT composition under the project's ignored `obj/` tree.
The matching runtime pack supplies `rusty dev`, `rusty-product-host` with the
wgpu renderer and host audio, and the browser frame viewer.

```text
Product.Game (safe C# product state and domain behavior)
  -> packaged Rusty.Engine service contracts
  -> `rusty dev` host/runtime (CoreCLR for ordinary development)
  -> Engine canvas, renderer, input, spatial, resources, and lifecycle

src/ui/main.js (product-owned DOM UI)
  -> staged as product UI; no world renderer or gameplay authority
```

The product runs on one exact matched release pair, and this document does not
name it. `RustyEnginePackageVersion` in `Directory.Build.props` is the one pin;
the `rusty` CLI installs and runs that pair; the adopting Den task records it. Both halves come from one Engine revision and carry a
matching generated ABI identity. Keep the pair together and let the host
reject a mismatch; products do not add version negotiation, copied Engine
assets, or handwritten interop. Adopt through `rusty update`, inspect the release notes, then rebuild and
run. `rusty install` installs the exact pin into the shared cache.

## Ownership

The product decides; the Engine guarantees. Space owns flight commands,
inertial state, field meaning, tuning, camera policy, presentation facts, HUD
projection, and lifecycle policy. The Engine owns update admission and clock
facts, input delivery, Dynamics and Camera mechanisms, Appearance resources and
retained frames, canvas/backend integration, host lifecycle, and UI transport.

The per-owner map of which Space class holds which state, and where newly
adopted Engine surface is meant to be used, is
[Space gameplay design](gameplay-design.md).

`SpaceProduct` is the lifecycle entrypoint. `SpaceProductComposition` wires
the named product owners. `SpaceFlight` translates product commands into
Engine Dynamics actions; `SpacePresentation` translates product readouts into
Engine Appearance and UI facts; `TrackingCamera` owns product framing policy
around the Engine camera service. None of these classes is a second host loop
or renderer.

The canonical split between physical state, authored ship-system state,
disposable telemetry, and downstream presentation, plus the invariants that keep
them from drifting into one another, is stated in
[space sailing campaign](space-sailing-campaign.md).

## Runtime lanes

The standard launch path runs the pinned pair through the Engine `rusty` CLI:

```bash
rusty dev \
  --project ./src/Product.Game/Product.Game.csproj \
  --live-debug --port 8787
```

`rusty dev` builds and stages a loose Product directory, loads Product.Game
through CoreCLR, and renders frames on the host with wgpu. The Engine-owned browser canvas
shows streamed frames alongside the product DOM UI. Audio uses the runtime
host device, not the browser.
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
