# Rusty Space

A deliberately small, runnable Rusty Engine downstream product. It has a
Rust-owned admitted gameplay artifact, a pure TypeScript authoring DSL, one
live `SpaceProductService`, and a viewport-constrained browser surface with an
Engine-owned canvas and a downstream UI label.

It is a gameplay-driven product, not an Engine runtime or browser-first game
framework. See [the architecture](docs/architecture.md) and
[change-placement guidance](docs/code-style.md).

## Adjacent Engine checkout

This product depends on one adjacent sibling checkout at `../rusty-engine`
relative to the repository root: the Rust facade
(`../rusty-engine/rust/crates/rusty-engine`) and the public
`@rusty-engine/application-host` artifact. The operator prepares that sibling;
this repository never fetches, clones, pins, or manages it.

Bootstrap dependencies and materialize the Rust-admitted ship package, build
the browser shell, then start the live Rust host:

```bash
./scripts/bootstrap.sh
pnpm --dir apps/web build
cargo run -p rusty-space-host --bin browser-host --locked
```

Open <http://127.0.0.1:8787>. The host admits the committed package into
`SpaceProductService`, which owns the live session and fixed-step schedule;
the host serves the built browser shell.

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
| `crates/product-runtime` | `SpaceProductService`: admitted live flight state, sessions, fixed-step scheduling, and retained-frame projection | Browser, DOM, WebGL, or host lifecycle |
| `crates/product-host` | Local browser transport, wall-clock observation, and built-shell delivery | Gameplay semantics, scheduling policy, or rendering |
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
