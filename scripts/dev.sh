#!/usr/bin/env bash
set -euo pipefail

TASK="${1:-}"
if [[ -z "${TASK}" ]]; then
  echo "Usage: ./scripts/dev.sh <bootstrap|restore|build|test|format|ci>"
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT_DIR}"

case "${TASK}" in
  bootstrap)
    dotnet --info
    dotnet restore dotforge.sln --use-lock-file
    ;;
  restore)
    dotnet restore dotforge.sln --use-lock-file
    ;;
  build)
    dotnet build dotforge.sln --configuration Release --no-restore
    ;;
  test)
    dotnet test dotforge.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory ./TestResults
    ;;
  format)
    dotnet format dotforge.sln --verify-no-changes
    ;;
  ci)
    dotnet restore dotforge.sln --use-lock-file
    dotnet build dotforge.sln --configuration Release --no-restore
    dotnet test dotforge.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory ./TestResults
    ;;
  *)
    echo "Unknown task: ${TASK}"
    echo "Usage: ./scripts/dev.sh <bootstrap|restore|build|test|format|ci>"
    exit 1
    ;;
esac
