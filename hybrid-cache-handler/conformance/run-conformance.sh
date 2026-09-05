#!/usr/bin/env bash
# Runs the http-tests/cache-tests RFC 9111 suite against HttpHybridCacheHandler
# via the ConformanceProxy, and gates the results against expected-results.json.
#
# Usage:
#   ./run-conformance.sh            # run suite, compare against baseline
#   ./run-conformance.sh --update   # run suite, rewrite the baseline
#   ./run-conformance.sh --file-system # streaming filesystem mode, same baseline
set -euo pipefail

CONFORMANCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SUITE_DIR="$CONFORMANCE_DIR/.cache-tests"
SUITE_REPO="https://github.com/http-tests/cache-tests.git"
SUITE_PIN="b55b8bda3dbb8c927c04e85bd8d496a8caa3e4ba"
PROXY_PROJECT="$CONFORMANCE_DIR/ConformanceProxy/ConformanceProxy.csproj"
RESULTS="$CONFORMANCE_DIR/results.json"
BASELINE="$CONFORMANCE_DIR/expected-results.json"
ORIGIN_PORT="${ORIGIN_PORT:-8000}"
PROXY_PORT="${PROXY_PORT:-8081}"
FILE_SYSTEM=false
UPDATE=false
for arg in "$@"; do
  case "$arg" in
    --file-system) FILE_SYSTEM=true; RESULTS="$CONFORMANCE_DIR/results-filesystem.json" ;;
    --update) UPDATE=true ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

wait_for_http() {
  local url="$1"
  for _ in $(seq 1 120); do
    if curl -s -o /dev/null "$url"; then return 0; fi
    sleep 0.25
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

# 1. Clone/update the suite at the pinned commit
if [ ! -d "$SUITE_DIR/.git" ]; then
  echo "Cloning cache-tests into $SUITE_DIR"
  git clone --quiet "$SUITE_REPO" "$SUITE_DIR"
fi
if [ "$(git -C "$SUITE_DIR" rev-parse HEAD)" != "$SUITE_PIN" ]; then
  git -C "$SUITE_DIR" fetch --quiet origin
  git -C "$SUITE_DIR" checkout --quiet "$SUITE_PIN"
fi
if [ ! -d "$SUITE_DIR/node_modules" ]; then
  echo "Installing suite dependencies"
  (cd "$SUITE_DIR" && npm install --no-audit --no-fund --silent)
fi

# 2. Build the proxy
dotnet build "$PROXY_PROJECT" -v q --nologo

ORIGIN_PID=""
PROXY_PID=""
CONTENT_ROOT="$(mktemp -d "$CONFORMANCE_DIR/.content-store-XXXXXXXX")"
cleanup() {
  [ -n "$PROXY_PID" ] && kill "$PROXY_PID" 2>/dev/null || true
  [ -n "$ORIGIN_PID" ] && kill "$ORIGIN_PID" 2>/dev/null || true
  [ -n "$PROXY_PID" ] && wait "$PROXY_PID" 2>/dev/null || true
  rm -rf -- "$CONTENT_ROOT"
}
trap cleanup EXIT

# 3. Start the suite origin server and the caching proxy
echo "Starting suite server on :$ORIGIN_PORT"
# Must launch via npm (server.mjs requires npm_package_config_* env vars)
(cd "$SUITE_DIR" && npm_config_port="$ORIGIN_PORT" npm run server) &
ORIGIN_PID=$!
echo "Starting ConformanceProxy on :$PROXY_PORT"
dotnet run --project "$PROXY_PROJECT" --no-build -- --port "$PROXY_PORT" --origin "http://127.0.0.1:$ORIGIN_PORT" --file-system "$FILE_SYSTEM" --content-root "$CONTENT_ROOT" &
PROXY_PID=$!

wait_for_http "http://127.0.0.1:$ORIGIN_PORT/"
wait_for_http "http://127.0.0.1:$PROXY_PORT/proxy-health"

# 4. Run the suite client through the proxy
echo "Running full suite (takes a few minutes)"
(cd "$SUITE_DIR" && npm run --silent cli "--base=http://127.0.0.1:$PROXY_PORT") > "$RESULTS"

# 5. Gate against (or update) the baseline
if [ "$UPDATE" = true ]; then
  node "$CONFORMANCE_DIR/compare-results.mjs" "$RESULTS" --update "$BASELINE"
else
  node "$CONFORMANCE_DIR/compare-results.mjs" "$RESULTS" "$BASELINE"
fi
