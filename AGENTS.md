# Rusty Space agent guidance

## Purpose

This is a small, runnable reference downstream product for Rusty Engine. It
shows a Rust-owned gameplay/content admission path, an optional TypeScript
authoring DSL, and a single bounded browser viewport through the Engine public
application host. It is not a framework, a generic game template, or an
Engine implementation checkout.

Read the local sibling bootstrap when available:
`../rusty-engine/docs/topics/development/downstream-repository-bootstrap.md`.
For GitHub or other remote agents, use the canonical bootstrap at
<https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/downstream-repository-bootstrap.md>,
then continue with the
[greenfield product guide](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/greenfield-downstream-product.md).
Local relative paths are useful in an adjacent checkout but are not portable
Markdown links on GitHub.

## Authority

Rust owns the product's admitted gameplay vocabulary, semantic interpretation,
render-frame projection, and any future live service state. The TypeScript in
`gameplay/authoring` is a pure build-time materializer for a Rust-defined wire
format. The TypeScript in `apps/web` loads an already Rust-projected frame and
owns only bounded DOM presentation/input adaptation.

Do not add a TypeScript evaluator, live game state, save model, scheduler,
generic command bus, browser storage authority, a second canvas, or a private
Engine renderer import. A live browser or Tauri product keeps gameplay meaning
in one named Rust product service; it does not move that meaning into
TypeScript.

## Engine boundary

The sibling Engine path is required for development:

- Rust depends only on `../rusty-engine/rust/crates/rusty-engine`.
- Browser code depends only on the public
  `@rusty-engine/application-host` artifact at
  `../rusty-engine/render/artifacts/application-host`.

Do not clone, fetch, pin, or manage Engine from this product's source or CI.
An operator creates adjacent sibling checkouts. Never deep-import Engine
`src/` trees or renderer packages.

## Layout

- `crates/product-gameplay`: strict product content schema and admission.
- `crates/product-runtime`: named Rust service and renderer-neutral frame projection.
- `gameplay/authoring`: pure TypeScript builders that materialize committed content.
- `content/gameplay`: admitted product artifact, not a TypeScript runtime input.
- `apps/web`: thin Vite composition root, one Engine canvas, bounded UI root.

## Verification

Run `./scripts/verify.sh` from the repository root after `pnpm install`. It
checks authoring drift, Rust formatting/tests/lints, TypeScript, the web build,
and a real Chromium viewport proof. It assumes the sibling Engine
application-host artifact already exists; build that artifact in the Engine
checkout only when it is absent or intentionally changed.
