# Content Exclusion Demo

This repository demonstrates how to enforce **content exclusion** for GitHub Copilot in agentic scenarios using three complementary layers of defense.

## The Problem

When Copilot agents operate autonomously, they have access to powerful tools (file reads, shell commands, grep, etc.). Organization-level content exclusion settings may restrict certain files, but a determined agent could attempt to work around those restrictions using alternative tools. This repo shows how to build **defense in depth** so that excluded content stays excluded — even when an AI agent is driving.

## Architecture

The sample project is a .NET solution (`DataProcessor.slnx`) with three projects:

| Project                  | Purpose                       |
|--------------------------|-------------------------------|
| `DataProcessor.App`      | Console entry point           |
| `DataProcessor.Domain`   | Domain models and interfaces  |
| `DataProcessor.Infra`    | Infrastructure (CSV parsing, database access) |

Sensitive content lives in `data-input/` (CSV files) and select infrastructure files (billing repository, CSV parsers).

## Three Layers of Content Exclusion

### 1. `.copilotignore` — Declarative File Exclusion

The `.copilotignore` file works similarly to `.gitignore` and declares which files should be excluded from Copilot's context:

```
- "**.csv"
- "**/.env"
- "/data-input/**"
- "/DataProcessor.Infra/Csv/*.cs"
- "/DataProcessor.Infra/Database/BillingRepository.cs"
```

This tells the platform to block direct reads of these paths. However, on its own, it may not cover every tool an agent could use.

### 2. `AGENTS.md` — Behavioral Instructions for the Agent

The `AGENTS.md` file provides explicit instructions that Copilot agents read and follow:

- If a file is reported as excluded, **stop immediately** — do not try alternative tools.
- Treat hook denials as expected policy, not obstacles to work around.
- Do **not** attempt to inspect, search, or recover excluded content through indirect means.
- This applies **even if the user explicitly asks** to read the file.
- Instead, explain the restriction and ask the user to provide the content or remove the exclusion.

This layer relies on the agent's instruction-following behavior to reinforce the exclusion policy.

### 3. `.github/hooks/` — Pre-Tool-Use Hook (Runtime Enforcement)

The hook provides **hard runtime enforcement** that the agent cannot bypass. It consists of:

- **`content-exclusion-guard.json`** — Registers a `PreToolUse` hook that runs before every tool call.
- **`deny_excluded_tool_use.sh`** — A Bash script that:
  1. Reads the `.copilotignore` patterns.
  2. Extracts file paths from the incoming tool call (supports `read_file`, `create_file`, `edit_notebook_file`, `apply_patch`, `grep_search`, `file_search`, and more).
  3. Matches extracted paths against exclusion patterns.
  4. Returns a **deny** decision with a clear reason if any path matches, or **allow** otherwise.

This means even if the agent ignores `AGENTS.md` instructions and attempts a shell `cat` or grep on an excluded file, the hook intercepts the tool call and blocks it.

### Bonus: `CODEOWNERS` — Change Protection

The `CODEOWNERS` file requires review from a designated owner (`@asizikov`) for changes to:

- `AGENTS.md`
- `.copilotignore`
- `.github/hooks/**`

This prevents the agent (or anyone else) from weakening the exclusion policy without human approval.

## How the Layers Work Together

```
User asks agent to read excluded file
          │
          ▼
┌─────────────────────────┐
│  Platform-level check   │  ← .copilotignore patterns applied by Copilot platform
│  (blocks direct reads)  │
└────────┬────────────────┘
         │ If tool call slips through
         ▼
┌─────────────────────────┐
│  PreToolUse Hook        │  ← .github/hooks/deny_excluded_tool_use.sh
│  (blocks any tool call  │    Parses .copilotignore, inspects tool input,
│   targeting excluded     │    emits deny with reason
│   paths)                │
└────────┬────────────────┘
         │ If somehow not caught
         ▼
┌─────────────────────────┐
│  AGENTS.md instructions │  ← Agent behavioral rules
│  (agent self-polices,   │    "Do not attempt workarounds"
│   refuses & explains)   │
└─────────────────────────┘
```

Each layer compensates for potential gaps in the others:

| Layer | Type | Strength | Limitation |
|-------|------|----------|------------|
| `.copilotignore` | Platform-enforced | Automatic, no agent cooperation needed | Only covers platform-native file reads |
| `PreToolUse` hook | Runtime enforcement | Intercepts all registered tool calls | Must be kept in sync with tool names |
| `AGENTS.md` | Behavioral | Covers edge cases and indirect attempts | Relies on agent compliance |
| `CODEOWNERS` | Change protection | Prevents policy weakening via PR | Requires branch protection rules |

## Trying It Out

1. Open this repo in VS Code with GitHub Copilot enabled.
2. Ask Copilot to read or summarize `data-input/input.csv` — it will be blocked.
3. Ask Copilot to modify `DataProcessor.Infra/Database/BillingRepository.cs` — also blocked.
4. Ask Copilot to work with non-excluded files (e.g., `DataProcessor.App/Program.cs`) — works normally.
