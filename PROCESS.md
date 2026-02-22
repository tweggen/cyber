# Modification Process Guide

This document defines the standard process for making changes to the codebase, to maintain consistency across Claude Code instances and ensure proper documentation cleanup.

## Workflow Overview

### 1. Planning Phase

When implementing a non-trivial feature or change:

- Use `EnterPlanMode` to create a structured implementation plan
- Store the plan in `docs/roadmap/proposed/` with a descriptive filename
- Plan should include:
  - Clear objectives
  - Files that will be modified/created
  - Implementation approach
  - Testing strategy (if applicable)

**Example:** `docs/roadmap/proposed/07-USER-BULK-OPERATIONS.md`

### 2. Execution Phase

Before writing code:

- **Read first**: Always read existing code before modifying it (never propose changes to unread code)
- **Keep it simple**: Only make changes directly requested. Avoid over-engineering, unnecessary refactoring, or adding features beyond scope
- **No backwards-compatibility hacks**: If something is unused, delete it completely
- **Minimal comments**: Only add comments where logic isn't self-evident
- **Use proper tools**: Use Edit/Write/Read tools instead of bash for file operations

After implementation completes:

- Move the plan file from `docs/roadmap/proposed/` → `docs/roadmap/done/`
- Delete from `docs/roadmap/planned/` if it was moved there during planning

### 3. Documentation Updates

After executing changes, update documentation in this order:

**a) CLAUDE.md** — If changes affect:
- Architecture or codebase structure
- Build/development commands
- Project status or phase completion
- Key design decisions

**b) README.md** — If changes affect:
- User-facing features or API
- Installation/setup instructions
- Feature list or capabilities
- Project overview

**c) Other Docs** — Update any referenced documentation:
- `docs/` subdirectories
- Inline code comments (sparingly, only where logic isn't obvious)
- Component/function documentation

### 4. Commit & Cleanup

When all changes are complete:

- Run `git status` to verify all changes
- Create a single, clear commit with message format:
  ```
  <action> <feature>: <brief description>

  - Specific change 1
  - Specific change 2
  - Moved plan: proposed/ → done/

  Co-Authored-By: Claude <model> <noreply@anthropic.com>
  ```
- Examples: "Implement Phase 3: Advanced Audit Filtering", "Fix: User quota inheritance bug"
- **Never force push to main** unless explicitly authorized
- **Do not commit secrets** (.env files, credentials, API keys)

## Key Principles

### Code Quality
- Prefer existing patterns in the codebase
- Don't add error handling for impossible scenarios
- Don't create utilities for one-time operations
- Trust framework guarantees; only validate at system boundaries

### Documentation Discipline
- Documentation updates are mandatory, not optional
- Keep PROCESS.md synchronized if the workflow changes
- Out-of-date docs are worse than no docs

### Tool Usage
- Use `Glob`, `Grep`, `Read` for code exploration (not bash `find`, `grep`, `cat`)
- Use `Edit`/`Write` for file modifications (not bash `sed`, `awk`, `echo`)
- Reserve bash for actual terminal operations (git, npm, docker, etc.)
- For broad codebase exploration, use `Task` with `subagent_type=Explore`

### File Organization
- Never create files unless absolutely necessary
- Prefer editing existing files
- Follow existing directory structure and naming conventions
- Place roadmap files in appropriate phase subdirectories

## Checklist for Future Claude Instances

Before committing changes:

- [ ] All requested features implemented and tested
- [ ] Code follows existing patterns and style
- [ ] CLAUDE.md updated (if applicable)
- [ ] README.md updated (if applicable)
- [ ] Other documentation updated (if applicable)
- [ ] Plan file moved from proposed/ to done/ (if applicable)
- [ ] No debug code, console logs, or commented-out code left behind
- [ ] No unnecessary files created
- [ ] Git status reviewed for unexpected changes
- [ ] Commit message is clear and descriptive
