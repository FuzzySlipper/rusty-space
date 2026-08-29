# Rusty Space architecture

Rusty Space is a C# product base for continued experimentation, not a finished
interactive product.

```text
Product.Game (safe C# product state and domain behavior)
  -> named generated Rusty Engine service contracts
  -> Engine host, input, rendering, canvas/backend, spatial/physics, resources

Product.NativeProduct (thin generated NativeAOT composition)
  -> Engine binding and product generators

ui/main.js (product DOM UI copied into the generated browser bundle)
  -> no gameplay state, world renderer, canvas, or input authority
```

Product code owns authoritative product facts, gameplay decisions, content
meaning, and explicitly ordered work inside Engine-admitted updates. Engine
owns reusable mechanisms and lifecycle. The UI does not own product state or
world rendering.

The intended local shape is domain-oriented. An owner such as `FlightState` or
`FlightController` keeps its related behavior and tuning nearby; one clear
owner mutates each state family. Composition is explicit, dependencies arrive
through constructors, and coordination stays thin. “Module” may describe a
coherent folder and ownership boundary, but requires no registry, framework,
separate assembly, or dynamic loading.

The standard Engine runtime/host owns control epochs, input clear/rebind,
baseline-fenced output, and lifecycle. Space owns only closed command meaning,
flight reset policy, field/flight values, and published appearance facts. The
Engine C# surface is expected to grow. If a product need lacks a named safe API,
record the narrow upstream capability and stop at that boundary; do not add a
downstream renderer, simulation host, ABI layer, or fallback implementation.
The exploratory material in `docs/ideas/` remains product-design material.
