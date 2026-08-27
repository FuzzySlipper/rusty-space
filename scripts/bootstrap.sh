#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
engine_root="$repo_root/../rusty-engine"
if [[ ! -d "$engine_root/.git" ]]; then
  printf '%s\n' "Expected an adjacent rusty-engine checkout at: $engine_root" >&2
  printf '%s\n' "Clone it beside this repository; bootstrap will not fetch or manage it." >&2
  exit 1
fi

cd "$repo_root"
dotnet restore src/Product.NativeProduct/Product.NativeProduct.csproj
printf '%s\n' "Bootstrap complete. Run the C# product with: ./scripts/run-csharp.sh --port 8787"
