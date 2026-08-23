#!/usr/bin/env bash
set -euo pipefail

template_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
engine_host="$template_root/../rusty-engine/render/artifacts/application-host/index.js"

if [[ ! -f "$engine_host" ]]; then
  printf '%s\n' "Missing public Engine application-host artifact: $engine_host" >&2
  printf '%s\n' "Build it from the adjacent rusty-engine checkout, then retry." >&2
  exit 1
fi

cd "$template_root"
pnpm authoring:check
cargo fmt --check
cargo test --workspace --locked
cargo clippy --workspace --all-targets --locked -- -D warnings
pnpm typecheck
pnpm export:frame
pnpm --dir apps/web build
pnpm test:browser

