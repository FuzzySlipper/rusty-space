# Template architecture

The template demonstrates a small downstream ownership slice rather than a
universal game grammar:

```text
TypeScript pure builders
  -> content/gameplay/sample-scene.json (committed canonical product artifact)
  -> Rust strict decode and product admission
  -> named Rust product service
  -> RenderFrameDiff JSON export
  -> public Engine application host
  -> one Engine canvas plus bounded downstream UI root
```

`AuthoredScene` is a closed product format. Rust rejects unknown fields,
unsupported schema versions, non-finite or out-of-range colors/scales, and bad
labels before it creates product meaning. The TypeScript source merely lowers
one readable authored form to that Rust-owned format; it cannot run in play or
evaluate a cube.

The exporter is deliberately a static development edge. It exists so a fresh
template can render a browser frame without claiming that HTTP or a game server
is fundamental. A real product replaces it with one named Rust service plus a
chosen typed host adapter. Its application frame keeps all canvas and UI
geometry within the browser/WebView viewport so that the DOM shell does not
become an accidental web-app authority.

Related Engine documents:

- [Greenfield downstream product path](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/greenfield-downstream-product.md)
- [Downstream renderer and Studio boundary](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/downstream-renderer-and-studio.md)
- [Rust code style](https://github.com/FuzzySlipper/rusty-engine/blob/main/docs/topics/development/rust-style.md)

