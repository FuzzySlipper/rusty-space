#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
engine_root="$repo_root/../rusty-engine"
runtime_manifest="$engine_root/Cargo.toml"
product_project="$repo_root/src/Product.NativeProduct/Product.NativeProduct.csproj"
product_library="$repo_root/src/Product.NativeProduct/bin/Release/net10.0/linux-x64/publish/Product.NativeProduct.so"
host_bundle="$repo_root/src/ui/host"
content_root="$repo_root/content"
port=0

if [[ "${1:-}" == "--port" ]]; then
  port="${2:?--port requires a value}"
  shift 2
fi
if [[ "$#" -ne 0 ]]; then
  printf '%s\n' "usage: $0 [--port <u16>]" >&2
  exit 2
fi
if [[ ! -f "$runtime_manifest" ]]; then
  printf '%s\n' "Expected an adjacent rusty-engine checkout at: $engine_root" >&2
  exit 1
fi

dotnet publish "$product_project" --configuration Release --runtime linux-x64 --self-contained true
cargo run --manifest-path "$runtime_manifest" -p csharp-product-runtime -- \
  --library "$product_library" \
  --bundle-dir "$host_bundle" \
  --content-dir "$content_root" \
  --port "$port"
