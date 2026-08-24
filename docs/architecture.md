# Rusty Space architecture

Rusty Space demonstrates a small downstream ownership slice rather than a
universal game grammar:

```text
TypeScript pure builders
  -> content/gameplay/rusty-space-core.package.json (committed ship-handling artifact)
  -> Rust ship-handling admission
  -> live Rust flight runtime
  -> renderer-neutral frame projection
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
state, semantic commands, fixed-step accumulation policy, readouts, and
renderer-neutral projection. `product-host` owns only the bounded local
browser transport and wall-clock observation; it passes elapsed time and typed
intent to the service, then delivers the retained update to the thin browser
shell. The application frame keeps all canvas and UI geometry within the
browser/WebView viewport so the DOM shell does not become an accidental
web-app authority.

Related Engine documents:

- [Greenfield downstream product path](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/greenfield-downstream-product.md)
- [Downstream renderer and Studio boundary](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/downstream-renderer-and-studio.md)
- [Rust code style](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/rust-style.md)
