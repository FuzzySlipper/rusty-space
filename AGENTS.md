# Rusty Space C# downstream guidance

## Current direction

Rusty Space is a raw, evolving C# downstream product. `Product.Game` and its
thin NativeAOT composition are the only local product implementation. Earlier
Rust and TypeScript gameplay, WebSocket hosting, and browser-world rendering
were retired during the cutover; consult Git history only when a future task
needs to recover a donor idea.

The C# path is fresh and raw. The current Den task and the Engine guidance
handles `rusty-engine/downstream-csharp-agent-brief` and
`rusty-engine/downstream-csharp-code-and-tuning` override this file when they
are more specific.

## Ownership and boundary

> The product decides. The Engine guarantees.

C# owns product/application logic, authoritative product state, domain
records, content meaning, policy, orchestration, and UI facts. Rusty Engine
owns host lifecycle and update admission, input delivery, rendering resources
and frames, canvas/backend, spatial and physics mechanisms, content/resource
mechanisms, persistence primitives, diagnostics, and other published Engine
capabilities.

Do not recreate Engine mechanisms in C#, Rust, or TypeScript. C# publishes
renderer-neutral facts through named Engine APIs; it does not build a renderer,
retained-frame substitute, resource loader, canvas, private loop, timer, or
browser simulation. TypeScript may provide DOM UI and accessibility only; it
does not render game elements or acquire gameplay state.

If the safe generated Engine API cannot express needed behavior, identify the
missing named upstream capability and stop with a narrow Engine request. That
is a valid result; do not substitute downstream infrastructure or fake proof.

## Source lanes

- `src/Product.Game/` is ordinary safe C# product code. It references the
  generated safe `Rusty.Engine` SDK and may not use `unsafe`, handwritten ABI,
  P/Invoke, raw native handles, pointer lifetimes, ambient service lookup, or
  a parallel update loop.
- `src/Product.NativeProduct/` is the thin NativeAOT composition
  boundary. It references `Product.Game`, the Engine SDK, and the Engine
  generator. Unsafe code is permitted only in generator-owned boundary output.
  Do not add handwritten gameplay, ABI layouts, function tables, pointer
  decoding, `GCHandle` ownership, or lifecycle behavior here.
- `src/ui/` contains only static/DOM-only host material. It must not simulate
  input, retain product state, render the world, or own a canvas.

Generated, intermediate, and NativeAOT output belongs under ignored build
directories. Never edit or commit it. The assembly selection in
`Product.NativeProduct` is the real Space product, not a fixture.

## Product organization

Place code by product domain so a change normally stays near the state,
behavior, tuning, and projection it owns. A module is optional vocabulary for
such a boundary, not an Engine interface, registry, plugin, assembly,
reflection convention, ECS/ESS, event bus, or framework requirement.

Use explicit construction and constructor-supplied dependencies. Each mutable
state family has one clear owner; coordinators remain thin and follow a bounded
read -> decide -> apply -> publish flow. Use records for immutable facts,
definitions, settings, and views; use named classes for mutable owners and
meaningful behavior. Avoid generic `Manager`, `Helper`, `Utils`, `Runtime`, or
`Data` containers.

Production literals must have product meaning: keep structural constants beside
their owning algorithm, tuning defaults beside their domain, and identities in
typed IDs or named definitions. Adjustable authored values belong in immutable
domain tuning records. Compose those records into one discoverable root tuning
aggregate at the explicit composition root, then inject each owner only its
own record. A partial JSON development overlay may be added later when useful;
it is not a prerequisite, user-settings system, save format, or Engine concern.

## Evidence

Use the smallest evidence that answers the seam: focused C# build, NativeAOT
publish, and a direct standard-host exercise. Do not create a legacy Rust/TS
gate, browser certification, packaging framework, or interactive-parity claim.
Known missing product behavior is future experimentation, not a reason to add
fallback infrastructure.
