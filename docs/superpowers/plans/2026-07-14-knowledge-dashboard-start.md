# Knowledge Dashboard Start Script Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a root-level shell command that releases port 5173 and restarts the local Understand-Anything dashboard for this ERP repository.

**Architecture:** A single POSIX-compatible shell script resolves its own repository root, verifies the dashboard directory, kills listeners on the configured port, and execs Vite with `GRAPH_DIR` pointing at the repository. The script stays in the foreground so Vite exposes its tokenized URL and Ctrl+C stops the process.

**Tech Stack:** Bash/Zsh, `lsof`, `npx`, Vite.

## Global Constraints

- Default dashboard port is `5173`.
- Port cleanup is intentionally broad: terminate every process listening on the selected port.
- The knowledge graph path is `/Users/lobster/Test/test-cc-umc/.understand-anything/knowledge-graph.json` via `GRAPH_DIR`.
- Do not modify ERP business code or dashboard dependencies.

---

### Task 1: Add and verify the restart script

**Files:**
- Create: `/Users/lobster/Test/test-cc-umc/start-knowledge-dashboard.sh`
- Test: temporary shell harness under `/tmp` (removed after verification)

**Interfaces:**
- Environment: optional `DASHBOARD_PORT` overrides the default `5173`.
- Produces: a foreground Vite process and its tokenized dashboard URL.

- [x] **Step 1: Write the failing test harness**

Create a temporary test that expects `start-knowledge-dashboard.sh` to exist and be executable, then checks that it invokes `lsof`, kills the returned PID, and starts `npx` with `GRAPH_DIR` and the configured port.

- [x] **Step 2: Run the test to verify it fails**

Run the harness before creating the script. Expected result: FAIL because `start-knowledge-dashboard.sh` does not exist.

- [x] **Step 3: Create the minimal implementation**

The script will:

1. Set strict shell options.
2. Resolve `SCRIPT_DIR` and `PROJECT_ROOT` from its own location.
3. Set `DASHBOARD_PORT="${DASHBOARD_PORT:-5173}"`.
4. Verify `packages/dashboard` and the knowledge graph exist.
5. Run `lsof -tiTCP:"$DASHBOARD_PORT" -sTCP:LISTEN`, kill returned PIDs if any, and tolerate no matches.
6. `cd` into the dashboard package and `exec env GRAPH_DIR="$PROJECT_ROOT" npx vite --host 127.0.0.1 --port "$DASHBOARD_PORT"`.

- [x] **Step 4: Run the test to verify it passes**

Run the harness with stubbed `lsof`, `kill`, and `npx` commands. Expected result: PASS, including the kill call and the exact `GRAPH_DIR`/port arguments.

- [x] **Step 5: Run a real startup smoke test**

Start the script with `DASHBOARD_PORT=5173`, confirm Vite prints a `Dashboard URL` containing `?token=`, confirm an HTTP request returns status 200, then stop it with Ctrl+C.

- [x] **Step 6: Remove temporary harness and report the script path**

Delete the temporary test harness after the verification evidence is captured; keep only the requested startup script and design/plan documentation.
