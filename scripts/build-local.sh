#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

command -v dotnet >/dev/null || { echo '.NET 10 SDK is required.' >&2; exit 1; }
command -v node >/dev/null || { echo 'Node.js is required.' >&2; exit 1; }
command -v npm >/dev/null || { echo 'npm is required.' >&2; exit 1; }

[[ "$(dotnet --version)" == "10.0.400" ]] || { echo 'Expected .NET SDK 10.0.400.' >&2; exit 1; }

dotnet restore FoodCousins.sln
dotnet build FoodCousins.sln -c Release --no-restore
dotnet test FoodCousins.sln -c Release --no-build

pushd frontend/foodcousins-web >/dev/null
if [[ -f package-lock.json ]]; then npm ci --no-audit --no-fund; else npm install --no-audit --no-fund; fi
VITE_API_BASE_URL=https://example.invalid npm run build
popd >/dev/null

if command -v terraform >/dev/null; then
  terraform -chdir=infra/environments/dev init -backend=false
  terraform -chdir=infra/environments/dev validate
else
  echo 'NOTE: Terraform is not installed; skipping Terraform validation.'
fi

echo 'SUCCESS: FoodCousins local build checks passed.'
