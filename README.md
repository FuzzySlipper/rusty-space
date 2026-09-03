# Rusty Space

Rusty Space is a raw, evolving C# product built on the packaged Rusty Engine
SDK. `src/Product.Game/` contains the product logic and the SDK supplies its
CoreCLR and NativeAOT composition below ignored `obj/` paths. The ordinary
development and Den lane is CoreCLR through the matching `rusty dev` runtime;
NativeAOT is an explicit fidelity/release check.

> The product decides. The Engine guarantees.

C# owns flight meaning, product state, tuning, content meaning, policy, and
the small DOM UI projection. Rusty Engine owns lifecycle and update admission,
input, Dynamics, camera, appearance, rendering, canvas/backend, resources,
and host integration. If a needed mechanism is absent from the safe SDK,
record the exact upstream request and stop that slice; do not add a local
renderer, loop, bridge, or browser simulation.

## Repository shape

```text
src/
  Product.Game/     safe C# flight, field, presentation, and lifecycle code
  ui/               product-owned DOM UI only
content/            canonical product content and authored assets
.runtime/
  runtime-pack-cabba0f/  matching `rusty dev` runtime pack (ignored)
  sdk-feed/             matching Rusty.Engine package feed (ignored)
docs/                current ownership and product design notes
```

The installed pair is pinned to Engine revision `cabba0f`:
`Rusty.Engine` `0.1.0-dev.cabba0f` and
`.runtime/runtime-pack-cabba0f`. Keep the package and runtime pack matched;
do not replace one with an older backup. Product content and the exploratory
design notes under `docs/ideas/` are intentional provenance and should not be
removed as host cleanup.

## Develop or use the Den service

Use the installed runtime pack directly:

```bash
./.runtime/runtime-pack-cabba0f/bin/rusty dev \
  --runtime ./.runtime/runtime-pack-cabba0f \
  --project ./src/Product.Game/Product.Game.csproj \
  --live-debug --bind-host 127.0.0.1 --port 8787
```

`.den-serve.json` and `.den-playwright.json` use the same packaged command.
The host stages the product-owned DOM UI and content; Engine browser and
renderer assets stay in the runtime pack. There is no downstream browser
bundle generator, Cargo product host, or checked NativeProduct project.

Engine contributors may opt into a source build only with an explicit
`--engine-source /absolute/path/to/rusty-engine` argument. That override
selects a matching source runtime and supplies the MSBuild properties needed
to use the source SDK. Ordinary product work must not discover adjacent
checkouts or invoke Cargo.

## Product slice

The current product is deliberately a small flight and presentation base:

- `Flight` owns the inertial planar command model and Dynamics actions.
- `Field` owns the authored stellar flow and wake response.
- `Viewing` owns product camera framing and zoom policy around Engine Camera.
- `Presentation` publishes the ship, planet, wake, stars, and HUD facts through
  Engine Appearance and UI services.
- `Lifecycle` and `Composition` keep the product callback and dependency
  ordering explicit.

This is an experimentation base, not a claim of complete gameplay or broad
interactive certification. See [architecture](docs/architecture.md) and
[code style](docs/code-style.md) before changing the product/Engine boundary.
