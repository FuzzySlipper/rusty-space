# Code style and change placement

Use a direct named Rust service, explicit state, closed product types, and
visible validation boundaries. Components and Engine mechanisms remain data or
focused named services; the product owns game meaning and orchestration.

Land a new product semantic, serialized field, or runtime behavior in Rust
first with admission/behavior tests. Add a TypeScript authoring constructor
only after the Rust wire vocabulary exists. Pure TypeScript may compose content
and render local UI, but may not acquire live gameplay evaluation, persistence,
scheduling, or canonical mutable facts.

Keep `apps/web/index.html` and `src/main.ts` as a thin composition root. Put
durable product behavior in named Rust modules, and keep DOM UI inside the
application host's supplied root. Browser-wide listeners must check
`context.ui.allowsGameplayInput(event)` before assigning gameplay meaning.

For the full provider architecture and promotion posture, see the
[Rusty Engine design](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/design.md)
and [upstream promotion and authoring DSL](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/upstream-promotion-and-authoring-dsl.md).

