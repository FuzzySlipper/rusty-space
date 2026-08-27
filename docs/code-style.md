# C# code placement and tuning

`Product.Game` uses safe, explicit C#: nullable and implicit usings enabled;
warnings treated as errors; build style enforcement and latest analysis;
deterministic output; unsafe disabled. Use file-scoped namespaces, explicit
access modifiers, `internal` by default, sealed classes unless inheritance is
intentional, one primary production concept per file, immutable records for
facts/settings/views, and classes for mutable owners.

Place code by the domain that owns its meaning instead of horizontal bags such
as `Managers`, `Helpers`, `Utils`, or `Data`. Give every mutable state family
one owner. Let lifecycle entrypoints coordinate a short read -> decide -> apply
-> publish flow; do not make them service containers or a second loop.

Do not leave production numeric or string literals unexplained. Put an
algorithmic invariant in a named constant beside its owner; put an adjustable
product value in an immutable domain tuning record; give cross-expression or
meaningful identities typed IDs or named definitions. Compose all domain tuning
into one discoverable immutable root aggregate at the composition root, then
pass each behavior only the record it needs. A JSON development overlay is a
future optional product tool: it overlays typed defaults once, validates, and
does not become mutable global options, a user setting, or Engine configuration.

`Product.NativeProduct` selects the real Space product and contains no
handwritten ABI or gameplay. Generated output is ignored and owns the native
boundary. Keep raw layouts, pointers, native handles, P/Invoke, and `unsafe`
out of `Product.Game`.
