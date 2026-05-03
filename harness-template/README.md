<div align="center">

# 🐙 Claude Code Harness Template

**A structured harness that turns Claude Code into a disciplined, project-aware engineering partner.**

[![Built with Claude Code](https://img.shields.io/badge/Built%20with-Claude%20Code-6B46C1?logo=anthropic&logoColor=white)](https://claude.ai/code)
[![AI Assisted](https://img.shields.io/badge/AI%20Assisted-100%25-blueviolet)](https://claude.ai/code)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![Vue](https://img.shields.io/badge/Vue-3.5-4FC08D?logo=vuedotjs)](https://vuejs.org)

</div>

---

## 🤖 Built with Claude Code

> **Octopus ERP** — this entire project (backend, frontend, tests, documentation) — was designed and implemented using **Claude Code** as the primary engineering tool.
>
> 184 integration tests. 3 subsystems. OIDC SSO. Custom approval engine. Attendance, meetings, HR onboarding — all built through AI-assisted development guided by this harness.

Claude Code is Anthropic's CLI agent for software engineering. It reads, writes, runs, and reasons about code — but without structure, AI assistance produces inconsistent, hard-to-maintain results. **This harness is the structure.**

---

## What Is a Harness?

A harness is a set of machine-readable instructions that live in `.claude/` at your project root. Claude Code loads them automatically at the start of every session.

The harness answers the questions an AI coding assistant needs to stay aligned:

| Question | Harness file |
|----------|-------------|
| What does this project do? How is it structured? | `CLAUDE.md` |
| What are the non-negotiable coding rules? | `.claude/rules/` |
| How do I run build, test, deploy? | `.claude/commands/` |
| When should I call a specialized sub-agent? | `.claude/agents/` |
| What tools am I allowed to use? | `.claude/settings.json` |

Without a harness, every new Claude Code session starts from zero — re-explaining architecture, reminding it of conventions, watching it drift back toward generic patterns. **With a harness, Claude Code stays on-rails across hundreds of sessions.**

---

## Why This Matters for AI Programming

### The AI Alignment Problem (in software)

LLM coding assistants are capable but "amnesiac" — they forget context, generate plausible-but-wrong patterns, and drift toward their training distribution rather than your project's conventions.

The harness solves this with **explicit, machine-readable constraints**:

```
Session 1: You explain the architecture.
Session 2: Claude re-reads CLAUDE.md — it already knows.

Session 1: You remind Claude not to mock the database in tests.
Session 2: rules/testing.md says "do not mock the database" — Claude remembers.

Session 1: You fix a CORS middleware ordering bug.
Session 2: CLAUDE.md "known pitfalls" section — Claude avoids it automatically.
```

### Rules = Encoded Decisions

Every rule in `.claude/rules/` represents a decision your team has already made:

- **Architecture**: Clean Architecture layers, dependency direction
- **TypeScript**: `strict: true`, DTO mirroring, no `any`
- **Testing**: No mocked DB, integration tests only, naming conventions
- **API**: Unified response envelope, status codes, endpoint naming

Claude Code treats these as hard constraints, not suggestions. The result is output that matches your codebase's style on the first attempt, not the third.

### Agents = Specialized Sub-Processes

Some tasks need more focus than a single prompt allows. The `agents/` directory defines specialized Claude sub-agents:

- **code-reviewer**: systematic PR review with tiered output (blockers / suggestions / passed)
- **security-auditor**: deep auth chain audit before production releases

Claude Code spawns these agents automatically when the trigger condition is met — you get structured, consistent results without writing a novel in your prompt.

### Commands = Repeatable Recipes

Multi-step operations encoded once, executed reliably:

- `/build` — compile backend + frontend with exact success criteria
- `/review` — checklist-driven code review against the project's rules
- `/migrate` — DB migration with pre/post-flight checks
- `/deploy` — Docker Compose lifecycle with health verification
- `/fix-issue` — GitHub issue → branch → implementation → PR

### Skills = Conditional Context

Skills load additional context when Claude Code detects a trigger condition ("you're touching auth code → load security checklist"). They keep the base harness lean while ensuring relevant guidance is always available.

---

## Harness Structure

```
.claude/
├── agents/
│   ├── code-reviewer.md        Sub-agent: systematic code review with tiered output
│   └── security-auditor.md     Sub-agent: auth/security deep audit
│
├── commands/
│   ├── build.md                /build   — compile backend + frontend
│   ├── deploy.md               /deploy  — Docker Compose lifecycle
│   ├── fix-issue.md            /fix-issue — GitHub issue → PR
│   ├── migrate.md              /migrate — DB migration management
│   └── review.md               /review  — checklist-driven code review
│
├── rules/                      ← Always loaded. Hard constraints.
│   ├── api-conventions.md      Response envelope, status codes, endpoint catalogue
│   ├── code-style.md           Architecture layers, TS strict mode, DTO mirroring
│   └── testing.md              Test structure, naming, forbidden mocks, coverage
│
├── skills/                     ← Loaded conditionally by trigger
│   ├── deploy/SKILL.md         Pre-deploy verification checklist
│   └── security-review/SKILL.md  Auth chain audit guide
│
└── settings.json               ← Tool permission matrix (allow/deny)

CLAUDE.md                       ← Project system prompt (loaded first)
```

---

## The CLAUDE.md System Prompt

`CLAUDE.md` is the single most important file. Claude Code reads it at session start and treats it as ground truth. A well-written `CLAUDE.md` includes:

```markdown
## Project overview
What this system does. Who uses it. Why it exists.

## Directory layout
Where things live. What each directory is for.

## Tech stack
Exact versions. Notes on non-obvious choices.

## Default ports
Where each service runs locally.

## Development commands
Exact commands to build, test, run, reset seed data.

## Architecture principles
Layer dependencies. Key invariants. Things that must never change.

## Feature checklist
What's done. What's in progress. What's planned.

## Known pitfalls
Real bugs you've hit. Real misconfigurations you've debugged.
Claude reads this and avoids them automatically.
```

The pitfalls section alone saves hours — encoding "CORS middleware must come before auth middleware" means Claude Code will never put it in the wrong order again.

---

## Quick Start

### Use this harness in a new project

```bash
# 1. Copy the harness into your project root
cp -r harness-template/.claude /path/to/your-project/
cp harness-template/CLAUDE.md /path/to/your-project/

# 2. Find all placeholders
grep -r "{{" /path/to/your-project/.claude/ CLAUDE.md

# 3. Replace with your project values
# {{PROJECT_NAME}}, {{BACKEND_TECH}}, {{FRONTEND_TECH}}, etc.

# 4. Write your CLAUDE.md
# Use the template as a starting point — fill in every section

# 5. Adjust settings.json to match your toolchain
# Add allowed commands, remove ones you don't use
```

### Placeholders reference

| Placeholder | Example |
|---|---|
| `{{PROJECT_NAME}}` | `MyApp` |
| `{{SERVICE_A}}` / `{{SERVICE_B}}` | `MyApp.Api` / `MyApp.Web` |
| `{{BACKEND_TECH}}` | `.NET 10 + EF Core` |
| `{{FRONTEND_TECH}}` | `Vue 3 + TypeScript + Vite` |
| `{{DB_TECH}}` | `SQLite (dev) / MySQL (prod)` |
| `{{AUTH_TECH}}` | `OpenIddict OIDC` |
| `{{TEST_FRAMEWORK}}` | `xUnit + WebApplicationFactory` |
| `{{PACKAGE_MANAGER}}` | `npm` |
| `{{ARCH_LAYERS}}` | `Api → Infrastructure → Core` |
| `{{BACKEND_BUILD_CMD}}` | `dotnet build MyApp.sln` |
| `{{FRONTEND_BUILD_CMD}}` | `npm run build` |
| `{{TEST_CMD}}` | `dotnet test` |
| `{{API_PORT}}` | `5001` |
| `{{WEB_PORT}}` | `5173` |

---

## Harness Design Principles

### Rules: encode decisions, not aspirations

Only write a rule if your team will actually enforce it. "We prefer functional components" is not a rule — it's a preference. "Controllers must be ≤ 10 lines" is a rule — it's checkable, enforceable, and catches real problems.

**Bad rule**: "Write clean, readable code."
**Good rule**: "Controller methods must be ≤ 10 lines. Business logic belongs in services/use-cases."

### Commands: exact steps, explicit success criteria

A command is only useful if Claude Code can execute it without ambiguity. Include:
- The exact shell command (no "e.g." — the real thing)
- What "success" looks like (exit code, output pattern, file created)
- What to do when it fails (specific error → specific fix)

### Agents: narrow scope, structured output

An agent that can do anything is an agent that does nothing well. Define scope narrowly. Define output format explicitly so results are machine-readable and consistent across invocations.

### Skills: conditional, additive

Skills should be narrow and additive — they activate on a trigger condition and add context to the current task, they don't replace the base harness. If a skill is relevant in every session, it should be a rule instead.

### settings.json: explicit allow + deny

Default-deny is safer than default-allow. List every command you actually need. Explicitly deny destructive operations (`rm -rf`, `git push --force`, `docker system prune`). Claude Code will ask for approval before running anything not in the allow list.

---

## Results: What This Harness Produced

The Octopus ERP project demonstrates what AI-assisted development with a harness can produce:

| Metric | Value |
|--------|-------|
| Systems built | 3 (UMC, OA, PLM) |
| Integration tests | 184 passing |
| Backend | .NET 10 + OpenIddict OIDC + EF Core + SQLite/MySQL |
| Frontend | Vue 3 + TypeScript + Tailwind CSS v4 + shadcn-vue |
| Orchestration | .NET Aspire (one-command startup) |
| Key features | SSO, RBAC, Approval engine, Attendance, Meeting rooms, HR onboarding |
| Human review sessions | Every session guided by harness — zero architectural drift |

The harness kept Claude Code consistent across every session:
- Same response envelope format in every new controller
- Same TypeScript patterns in every new Vue component
- Same test structure for every new feature
- Known pitfalls avoided automatically (CORS order, token audience, EF Core SQLite DateTime)

---

## AI-Assisted Development — Lessons Learned

After building a production-grade ERP system entirely with Claude Code, here's what works:

**✅ Do:**
- Write `CLAUDE.md` before writing any code — not after
- Encode every bug you fix as a "known pitfall" immediately
- Use the six-step gate process: spec → skeleton → API contract → integration → SSO → acceptance
- Run `dotnet test` and `npx tsc --noEmit` after every AI-generated change
- Keep CLAUDE.md up to date as the system evolves — stale context is worse than no context

**❌ Don't:**
- Ask Claude Code to "figure out the architecture" — give it the architecture upfront
- Let Claude Code name its own files or invent its own conventions — your rules define conventions
- Skip the review step because "the AI wrote it" — the harness enables the AI, humans verify the result
- Put aspirational rules in `.claude/rules/` — only rules you will actually enforce

---

## License

[MIT](../LICENSE)

---

<div align="center">
  <sub>
    Built with <a href="https://claude.ai/code">Claude Code</a> · 
    Harness template extracted from <a href="..">Octopus ERP</a> ·
    AI-assisted development done right
  </sub>
</div>
