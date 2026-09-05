# Rusty Space C# downstream guidance

## Current direction

Rusty Space is a raw, evolving C# downstream product. `src/Product.Game/` is
the product implementation and consumes the immutable `Rusty.Engine` SDK.
CoreCLR through the matching runtime pack and `rusty dev` is the ordinary
development/Den path; NativeAOT is an explicit fidelity or release check.

Earlier Rust and TypeScript gameplay, browser-world rendering, and
source-coupled host projects were retired. Git history is a donor record only;
do not restore those lanes as part of ordinary product work.

## Ownership and boundary

> The product decides. The Engine guarantees.

C# owns product/application logic, authoritative state, domain records, content
meaning, policy, orchestration, and UI facts. Rusty Engine owns host lifecycle,
update admission, input delivery, rendering resources and frames, canvas and
backend integration, spatial and physics mechanisms, content/resource
mechanisms, persistence primitives, diagnostics, and other published Engine
capabilities.

Do not recreate Engine mechanisms in C# or TypeScript. C# publishes
renderer-neutral facts through named Engine APIs; it does not build a renderer,
retained-frame substitute, resource loader, canvas, private loop, timer, or
browser simulation. TypeScript may provide DOM UI and accessibility only; it
does not render game elements or acquire gameplay state.

If the safe generated Engine API cannot express needed behavior, identify the
missing named upstream capability and stop with a narrow Engine request. A
missing capability is a valid result; do not substitute downstream
infrastructure or fake proof.

## Project shape

- `src/Product.Game/` is ordinary safe C# product code. It references the
  packaged `Rusty.Engine` SDK and must not use `unsafe`, handwritten ABI,
  P/Invoke, raw native handles, pointer lifetimes, ambient service lookup, or a
  parallel update loop.
- The SDK generates CoreCLR and NativeAOT composition below ignored `obj/`.
  There is no checked `Product.NativeProduct` project, handwritten bind file,
  generated binding, or downstream host.
- `src/ui/` contains only the product-owned DOM companion. It must not
  simulate input, retain product state, render world elements, create a canvas,
  or own a transport.
- `content/` is canonical product content and should be preserved when host
  or packaging files are cleaned up.

The root `NuGet.Config` points at the installed local SDK feed. The product is
pinned to `Rusty.Engine` `0.1.0-dev.cbf35130d06c`; `.runtime/runtime-pack-cbf35130d06c`
is its matching `rusty dev` host/runtime. Keep the pair together. Generated
output, staging directories, and other build residue belong under ignored
paths and are disposable when they are not owned by a live service.

Ordinary commands use the installed runtime pack directly:

```bash
./.runtime/runtime-pack-cbf35130d06c/bin/rusty dev \
  --runtime ./.runtime/runtime-pack-cbf35130d06c \
  --project ./src/Product.Game/Product.Game.csproj \
  --live-debug --bind-host 127.0.0.1 --port 8787
```

Engine contributors may explicitly select a source checkout with
`--engine-source`; the product project must not discover an adjacent checkout
or invoke Cargo. The `rusty dev` source override supplies the matching MSBuild
properties automatically.

## Product organization

Place code by product domain so state, behavior, tuning, and projection stay
near the owner that gives them meaning. A module is optional vocabulary for a
coherent folder and ownership boundary, not a registry, plugin, assembly,
reflection convention, ECS/ESS framework, event bus, or Engine interface.

Use explicit construction and constructor-supplied dependencies. Each mutable
state family has one clear owner; coordinators stay thin and follow a bounded
read -> decide -> apply -> publish flow. Use records for immutable facts,
definitions, settings, and views; use named classes for mutable owners and
meaningful behavior. Avoid generic `Manager`, `Helper`, `Utils`, `Runtime`, or
`Data` containers.

Production literals must have product meaning: keep structural constants beside
their owning algorithm, tuning defaults beside their domain, and identities in
typed IDs or named definitions. Compose adjustable values into one discoverable
root tuning aggregate at the explicit composition root, then inject each owner
only the record it needs. A JSON development overlay may be added later when a
real product need warrants it; it is not a prerequisite, user-settings system,
save format, or Engine concern.

## Evidence

Use the smallest evidence that answers the seam: focused C# build, CoreCLR
staging or `rusty dev` startup, and NativeAOT only when the task requires
fidelity/release evidence. Do not recreate the retired Rust/TypeScript gates,
browser bundle, packaging framework, or interactive-parity claim. Preserve
known product limitations rather than adding fallback infrastructure merely to
make a check pass.
