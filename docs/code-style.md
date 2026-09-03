# C# code placement and tuning

`Product.Game` is ordinary safe C# with nullable reference types, implicit
usings, deterministic builds, and warnings treated as errors. Keep unsafe
code, P/Invoke, raw native handles, ABI layouts, and pointer lifetimes out of
the product. The packaged SDK owns all generated binding and composition
details below ignored `obj/` paths.

Place code by the product domain that owns its meaning instead of horizontal
bags such as `Managers`, `Helpers`, `Utils`, or `Data`. Give every mutable
state family one owner. Lifecycle entrypoints and coordinators should remain
thin and follow a short read -> decide -> apply -> publish flow. Use records
for immutable facts, definitions, settings, and views; use named classes for
mutable owners and meaningful behavior. Prefer explicit construction and
constructor-supplied dependencies.

Production literals must be discoverable and carry product meaning. Put an
algorithmic invariant in a named constant beside its owner; put adjustable
values in an immutable domain tuning record; give meaningful identities typed
IDs or named definitions. Compose the domain records in the explicit product
composition root and inject each owner only what it needs. A JSON development
overlay is an optional future product tool, not a prerequisite, mutable global
options system, save format, or Engine configuration.

Use named safe `Rusty.Engine` services for Engine-owned behavior. Do not add a
private update loop, timer, browser state, rendering implementation, or local
substitute when the SDK lacks a capability. File the narrow upstream request
and stop that slice instead. Tests and staging are evidence for the actual
change, not a reason to resurrect the retired Rust/TypeScript host or broad
parity gates.
