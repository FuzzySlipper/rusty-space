#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
engine_root="$repo_root/../rusty-engine"
engine_host="$engine_root/render/artifacts/application-host/index.js"

if [[ ! -d "$engine_root/.git" ]]; then
  printf '%s\n' "Expected an adjacent rusty-engine checkout at: $engine_root" >&2
  printf '%s\n' "Clone it beside this repository; bootstrap will not fetch or manage it." >&2
  exit 1
fi

if [[ ! -f "$engine_host" ]]; then
  printf '%s\n' "Missing the public Engine application-host artifact: $engine_host" >&2
  printf '%s\n' "Build it explicitly in the Engine checkout, then rerun bootstrap:" >&2
  printf '%s\n' "  cd $engine_root/render && pnpm install && pnpm build:application-host-artifact" >&2
  exit 1
fi

cd "$repo_root"
pnpm install
pnpm authoring:materialize
pnpm export:frame
printf '%s\n' "Bootstrap complete. Start the bounded browser proof with: pnpm --dir apps/web dev"
