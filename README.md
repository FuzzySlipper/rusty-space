# Rusty Template

A deliberately small, runnable Rusty Engine downstream product reference. It
has a Rust-owned admitted gameplay artifact, a pure TypeScript authoring DSL,
a named Rust projection service, and one viewport-constrained browser surface
with an Engine-owned canvas and a downstream UI label.

It is a starting shape for a gameplay-driven product, not an Engine runtime or
a browser-first game framework. See [the architecture](docs/architecture.md)
and [change-placement guidance](docs/code-style.md).

## Adjacent checkout bootstrap

Clone both public repositories under the same parent directory (the names
matter because the dependency paths are deliberate):

```bash
git clone https://github.com/FuzzySlipper/rusty-engine.git rusty-engine
git clone https://github.com/FuzzySlipper/rusty-template.git rusty-template
cd rusty-template
./scripts/bootstrap.sh
pnpm --dir apps/web dev
```

Then open the printed local address. The browser app fetches only the
already-exported Rust frame; it does not evaluate gameplay content.

`./scripts/bootstrap.sh` checks for the adjacent Engine checkout and its public
application-host artifact, installs this template's dependencies, materializes
TypeScript authoring, and exports the Rust frame. It never writes to the sibling
Engine checkout. If the public artifact is missing, build it explicitly there:

```bash
cd ../rusty-engine/render
pnpm install
pnpm build:application-host-artifact
```

Return to this repository afterward. This template intentionally does not
fetch, clone, pin, or manage its sibling Engine checkout in source or CI.

After creating a product from this template, rename the `rusty-template-*`
Rust packages, npm packages, metadata tags, and visible sample text. Keep or
update the sibling dependency paths deliberately; the Engine checkout itself
is still expected at `../rusty-engine` relative to the repository root.

## Verify

```bash
./scripts/verify.sh
```

The script checks TypeScript authoring drift, Rust formatting/tests/lints,
TypeScript, deterministic Rust frame export, Vite build, and real Chromium
evidence at square and wide viewport shapes. It assumes the sibling public
Engine artifact exists and does not run broad Engine verification.

## Ownership at a glance

| Location | Owner | Does not own |
| --- | --- | --- |
| `crates/product-gameplay` | Product vocabulary and strict admission | Generic Engine grammar or a TS evaluator |
| `crates/product-runtime` | Product service and retained-frame projection | Browser, DOM, WebGL, or host lifecycle |
| `crates/product-export` | Static initial-frame export | Live game runtime or server |
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
