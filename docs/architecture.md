# Rusty Space architecture

Rusty Space keeps one bounded downstream ownership path:

```text
TypeScript pure builders
  -> content/gameplay/rusty-space-core.package.json (committed ship-handling artifact)
  -> Rust ship-handling admission
  -> SpaceProductService live sessions and fixed-step scheduling
  -> renderer-neutral retained-frame projection
  -> thin Rust browser host
  -> public Engine application-host
  -> one Engine canvas plus bounded downstream UI root
```

The `rusty-space/core` package is a closed product format. Rust rejects the
wrong package identity, unknown payload fields, unsupported schema versions,
and invalid flight constants before it creates gameplay meaning. TypeScript
only materializes that Rust-owned package at build time; it cannot run in play
or evaluate live flight state.

`SpaceProductService` admits the committed ship package and owns live flight
state, controller sessions, semantic commands, fixed-step accumulation,
readouts, and renderer-neutral retained-frame projection. `product-host` owns
only local browser transport, wall-clock observation, and built-shell
delivery; it passes elapsed time and typed intent to the service, then sends
each session its baseline and retained updates. The application frame keeps all
canvas and UI geometry within the browser/WebView viewport so the DOM shell
does not become an accidental web-app authority.

Related Engine documents:

- [Greenfield downstream product path](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/greenfield-downstream-product.md)
- [Downstream renderer and Studio boundary](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/downstream-renderer-and-studio.md)
- [Rust code style](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/rust-style.md)
