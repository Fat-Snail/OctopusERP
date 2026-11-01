#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT_ROOT="$SCRIPT_DIR"
DASHBOARD_DIR="${CODEX_UNDERSTAND_PLUGIN_ROOT:-$HOME/.understand-anything/repo/understand-anything-plugin}/packages/dashboard"
DASHBOARD_PORT="${DASHBOARD_PORT:-5173}"

if [[ ! -d "$DASHBOARD_DIR" ]]; then
  echo "Dashboard directory not found: $DASHBOARD_DIR" >&2
  exit 1
fi

if [[ ! -f "$PROJECT_ROOT/.understand-anything/knowledge-graph.json" ]]; then
  echo "Knowledge graph not found: $PROJECT_ROOT/.understand-anything/knowledge-graph.json" >&2
  exit 1
fi

if ! command -v lsof >/dev/null 2>&1; then
  echo "Required command not found: lsof" >&2
  exit 1
fi

listeners="$(lsof -tiTCP:"$DASHBOARD_PORT" -sTCP:LISTEN 2>/dev/null || true)"
if [[ -n "$listeners" ]]; then
  echo "Stopping process(es) on port $DASHBOARD_PORT: $listeners"
  kill $listeners 2>/dev/null || true
fi

echo "Starting knowledge dashboard on http://127.0.0.1:$DASHBOARD_PORT"
cd "$DASHBOARD_DIR"
exec env GRAPH_DIR="$PROJECT_ROOT" npx vite --host 127.0.0.1 --port "$DASHBOARD_PORT"
