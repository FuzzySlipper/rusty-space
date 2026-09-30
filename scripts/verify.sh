#!/usr/bin/env bash
set -euo pipefail

case "${1:-}" in
  '') aot=false ;;
  --aot) aot=true ;;
  -h|--help) echo 'usage: scripts/verify.sh [--aot]'; exit 0 ;;
  *) echo 'usage: scripts/verify.sh [--aot]' >&2; exit 2 ;;
esac
if (( $# > 1 )); then
  echo 'usage: scripts/verify.sh [--aot]' >&2
  exit 2
fi

space_repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$space_repo_root"
rusty install
dotnet test tests/Product.Game.Tests/Product.Game.Tests.csproj --nologo
rusty build --project src/Product.Game/Product.Game.csproj
if [[ "$aot" == true ]]; then
  rusty build --project src/Product.Game/Product.Game.csproj --aot
fi
