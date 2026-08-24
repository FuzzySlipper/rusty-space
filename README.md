# Rusty Space

A deliberately small, runnable Rusty Engine downstream product reference. It
has a Rust-owned admitted gameplay artifact, a pure TypeScript authoring DSL,
a named Rust projection service, and one viewport-constrained browser surface
with an Engine-owned canvas and a downstream UI label.

It is a starting shape for a gameplay-driven product, not an Engine runtime or
a browser-first game framework. See [the architecture](docs/architecture.md)
and [change-placement guidance](docs/code-style.md).

The active prototype is a 2D inertial-thrust space-sailing ship; design notes
live in [docs/ideas/](docs/ideas/) and the staged plan in
[docs/plans/ship-controls-prototype.md](docs/plans/ship-controls-prototype.md).

## Adjacent Engine checkout

This product depends on one adjacent sibling checkout at `../rusty-engine`
relative to the repository root: the Rust facade
(`../rusty-engine/rust/crates/rusty-engine`) and the public
`@rusty-engine/application-host` artifact. The operator prepares that sibling;
this repository never fetches, clones, pins, or manages it.

Bootstrap dependencies and materialize the Rust-admitted ship package:

```bash
./scripts/bootstrap.sh
pnpm --dir apps/web dev
```

If the public application-host artifact is missing, build it in the sibling
checkout only, then return here:

```bash
cd ../rusty-engine/render
pnpm install
pnpm build:application-host-artifact
```

## Verify

```bash
./scripts/verify.sh
```

The script checks TypeScript authoring drift, Rust formatting/tests/lints,
TypeScript, the Vite build, and real Chromium evidence at square and wide
viewport shapes. It assumes the sibling public Engine artifact exists and does
not run broad Engine verification.

## Ownership at a glance

| Location | Owner | Does not own |
| --- | --- | --- |
| `crates/product-gameplay` | Product vocabulary and strict admission | Generic Engine grammar or a TS evaluator |
| `crates/product-runtime` | `SpaceProductService`: admitted live flight state, fixed-step policy, and retained-frame projection | Browser, DOM, WebGL, or host lifecycle |
| `crates/product-host` | Live browser transport and wall-clock observation | Gameplay semantics, scheduling policy, or rendering |
| `gameplay/authoring` | Pure build-time composition/materialization | New serialized meaning or gameplay state |
| `apps/web` | Public host composition and local UI | Canvas, renderer, live gameplay state, or persistence |

For provider-level guidance, use the remote, portable documents:

- [Downstream repository bootstrap](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/downstream-repository-bootstrap.md)
- [Greenfield downstream product path](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/greenfield-downstream-product.md)
- [Downstream renderer and Studio boundary](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/downstream-renderer-and-studio.md)
- [Rusty Engine design](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/design.md)

When working in an adjacent local checkout, agents may read
`../rusty-engine/docs/topics/development/greenfield-downstream-product.md`.
That relative filesystem path is intentionally prose rather than the sole
Markdown link because it is not portable to GitHub.
