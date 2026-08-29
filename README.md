# Rusty Space

Rusty Space is a raw, evolving Rusty Engine C# product. Its runnable path is
safe `src/Product.Game`, thin generated `src/Product.NativeProduct`, and the
standard Engine C# product runtime/host. Rust and TypeScript gameplay and host
implementations have been retired; Git history remains the donor record.

Run the current product from an adjacent `rusty-engine` checkout:

```bash
./scripts/run-csharp.sh --port 8787
```

The script publishes the NativeAOT shared library, assembles the ignored browser
bundle in `src/ui/generated/product-bundle`, and starts the Engine host. Product
DOM UI comes from `src/ui/main.js`; the Engine supplies the host and canvas. It
is a development base for flight, field, input/control, and appearance
experiments—not an interactive demo or parity claim.

The product decides; the Engine guarantees lifecycle, input admission,
control epochs, output fencing, Dynamics, appearance resources, renderer, and
host/backend. If a needed capability is not in the generated safe API, request
it upstream instead of adding a local substitute. See
[architecture](docs/architecture.md) and [code placement](docs/code-style.md).
