# Cyber User Manual

User manual for the Cyber Knowledge Exchange Platform. Comprehensive guide for all user personas with step-by-step workflows, reference material, and troubleshooting.

## Quick Start

**To read the manual:**

1. **Online (HTML):** Open `docs/manual/index.md` in a Markdown viewer or build HTML version
2. **PDF:** Build using the build script (see below)
3. **Raw Markdown:** Navigate to `chapters/` directory

## Directory Structure

```
manual/
├── README.md                    # This file
├── index.md                     # Manual home page
├── mkdocs.yml                   # MkDocs configuration for HTML
├── chapters/                    # Main content (markdown files)
│   ├── 01-platform-overview.md
│   ├── 02-security-model.md
│   ├── 03-getting-started.md
│   ├── 04-knowledge-contributor.md    (TODO)
│   ├── 05-org-administrator.md        (TODO)
│   ├── 06-notebook-owner.md           (TODO)
│   ├── 07-auditor.md                  (TODO)
│   ├── 08-system-administrator.md     (TODO)
│   ├── 09-thinker-operator.md         (TODO)
│   ├── 10-cross-org-coordinator.md    (TODO)
│   ├── 11-mcp-reference.md            (TODO)
│   ├── 12-ui-reference.md             (TODO)
│   ├── 13-security-reference.md       (TODO)
│   ├── 14-data-model.md               (TODO)
│   ├── 15-troubleshooting.md          (TODO)
│   └── 16-glossary.md                 (TODO)
├── workflows/                   # Detailed workflow modules
│   ├── wf-kc-001-mcp-setup.md
│   ├── wf-kc-002-creating-entries.md  (TODO)
│   ├── wf-kc-003-browsing-knowledge.md (TODO)
│   ├── wf-kc-004-managing-revisions.md (TODO)
│   ├── wf-kc-005-observing-changes.md  (TODO)
│   └── ... (more workflows)
├── images/                      # Screenshots and diagrams
│   ├── dashboard.png            (TODO)
│   ├── notebook-creation.png    (TODO)
│   └── ...
├── templates/                   # Content templates
│   ├── workflow-template.md
│   ├── persona-chapter-template.md (TODO)
│   └── reference-chapter-template.md (TODO)
└── build/                       # Build configuration
    ├── pandoc-build.sh          # PDF/HTML builder script
    ├── style.css                # HTML stylesheet (TODO)
    └── manual-template.html     # Pandoc HTML template (TODO)
```

## Admin Panel Implementation Status

**Phase 1: User Management Enhancements** ✅ LIVE

The following features are now implemented in the Admin Panel (`/admin` section):

✅ **User List Page**
- Search by username, email, or display name
- Filter by user type (User, Service Account, Bot)
- Filter by lock status (Active, Locked)
- Sort by username, created date, or last login
- View user metadata (created date, last login, type badge)
- Lock/Unlock users directly from list

✅ **Lock with Reason Modal**
- Predefined lock reasons (Security violation, Policy violation, Inactive, etc.)
- Optional notes for additional context
- Silent lock option (UI ready, email integration in Phase 2)

✅ **User Detail Page**
- View created date and last login timestamp
- Edit user type (User, Service Account, Bot)
- View lock reason in account status card
- Display quota usage with color-coded progress bars (green/yellow/red)
- Estimate storage usage based on entry count

✅ **Database Changes**
- New columns: `CreatedAt`, `LastLoginAt`, `LockReason`, `UserType`
- Automatic indexes for efficient filtering and sorting
- Migration file: `20260222085328_AddUserManagementFields`

**Features documented in:** [Chapter 8: System Administrator](chapters/08-system-administrator.md) — Workflow 1: User Management

---

## Content Status

### Part I: Introduction ✓ COMPLETE
- [x] Chapter 1: Platform Overview (1500 words)
- [x] Chapter 2: Security Model (2000 words)
- [x] Chapter 3: Getting Started (1800 words)

### Part II: Persona Guides ⚠️ IN PROGRESS
- [ ] Chapter 4: Knowledge Contributor (5 workflows, ~3000 words)
- [ ] Chapter 5: Organization Administrator (4 workflows, ~2500 words)
- [ ] Chapter 6: Notebook Owner (5 workflows, ~3000 words)
- [ ] Chapter 7: Auditor/Compliance Officer (3 workflows, ~1800 words)
- [x] Chapter 8: System Administrator (Phase 1 features implemented, workflows ~2500 words) — **User Management features documented and live**
- [ ] Chapter 9: ThinkerAgent Operator (3 workflows, ~1800 words)
- [ ] Chapter 10: Cross-Organization Coordinator (3 workflows, ~1800 words)

### Part III: Reference ⚠️ PLANNED
- [ ] Chapter 11: MCP Integration Reference (~2000 words)
- [ ] Chapter 12: UI Reference (~1500 words)
- [ ] Chapter 13: Security Reference (~2000 words)
- [ ] Chapter 14: Data Model Deep-Dive (~2000 words)
- [ ] Chapter 15: Troubleshooting (~1500 words)
- [ ] Chapter 16: Glossary & Index (~1000 words)

### Workflows ✓ TEMPLATE COMPLETE
- [x] Workflow Template (reusable structure)
- [x] WF-KC-001: Setting up MCP Access (complete example, ~2000 words)
- [ ] WF-KC-002: Creating and organizing entries
- [ ] WF-KC-003: Browsing and discovering knowledge
- [ ] WF-KC-004: Managing revisions
- [ ] WF-KC-005: Observing changes
- [ ] ... (21 more workflows planned)

## Building the Manual

### Prerequisites

- Python 3.9+
- Pandoc (for PDF generation)
- MkDocs (for HTML generation)

#### Installation (macOS)

```bash
# Install Pandoc
brew install pandoc

# Install MkDocs and plugins
pip3 install mkdocs mkdocs-material pymdown-extensions

# Make build script executable
chmod +x build/pandoc-build.sh
```

#### Installation (Linux)

```bash
# Install Pandoc
sudo apt-get install pandoc

# Install MkDocs
pip3 install mkdocs mkdocs-material pymdown-extensions

# Make build script executable
chmod +x build/pandoc-build.sh
```

#### Installation (Windows)

```powershell
# Install Pandoc (via Chocolatey)
choco install pandoc

# Install MkDocs
pip install mkdocs mkdocs-material pymdown-extensions

# Build script works with PowerShell (no need to make executable)
```

### Build Commands

**Build PDF only:**
```bash
./build/pandoc-build.sh pdf
```

**Build HTML only:**
```bash
mkdocs build
# or
./build/pandoc-build.sh html
```

**Build both PDF and HTML:**
```bash
./build/pandoc-build.sh both
```

### Output

Built files appear in `dist/`:
- `Cyber-User-Manual.pdf` — Complete manual in PDF format
- `index.html` — HTML version (open in browser)

## Editing the Manual

### Adding a New Chapter

1. Create a new markdown file in `chapters/` (e.g., `04-knowledge-contributor.md`)
2. Use the template structure from `templates/workflow-template.md`
3. Add to `mkdocs.yml` in the appropriate section
4. Reference from `index.md` in the table of contents

### Adding a New Workflow

1. Create a workflow markdown file in `workflows/` (e.g., `wf-kc-002-creating-entries.md`)
2. Use the template from `templates/workflow-template.md`
3. Link from the corresponding persona chapter
4. Add to `mkdocs.yml` under the Workflows section

### Adding Screenshots

1. Save images to `images/` with descriptive names
2. Reference in markdown as:
   ```markdown
   ![Description of image](../images/filename.png)
   ```
3. Consider using vector graphics (SVG) for diagrams where possible

### Workflow Frontmatter

Every workflow has YAML frontmatter describing its metadata:

```yaml
---
id: "WF-XX-XXX"              # Unique workflow ID
title: "[Workflow Title]"
personas: ["Persona 1", "Persona 2"]  # Who uses this
overlaps_with: ["WF-XX-XXX"]  # Related workflows
prerequisites:
  - "Requirement 1"
  - "Requirement 2"
estimated_time: "5-10 minutes"
difficulty: "Beginner|Intermediate|Advanced"
---
```

## Workflow Naming Convention

Workflow IDs follow this pattern: `WF-[PERSONA]-[NUMBER]`

| Persona Code | Full Name | Color |
|---|---|---|
| KC | Knowledge Contributor | 🟦 Blue |
| OA | Organization Administrator | 🟪 Purple |
| NO | Notebook Owner | 🟩 Green |
| AU | Auditor/Compliance Officer | 🟨 Yellow |
| SA | System Administrator | 🟥 Red |
| TO | ThinkerAgent Operator | 🟧 Orange |
| CO | Cross-Organization Coordinator | 🟦 Indigo |

**Examples:**
- `WF-KC-001` → Knowledge Contributor, Workflow 1
- `WF-OA-003` → Organization Administrator, Workflow 3
- `WF-NO-005` → Notebook Owner, Workflow 5

## Overlapping Workflows

When workflows are shared across personas (e.g., "MCP Setup" used by both Knowledge Contributors and Notebook Owners):

1. **Primary location:** Write the full workflow in the persona who uses it most frequently
2. **Secondary location:** Reference with a brief overview + link
3. **Shared content:** For 3+ personas, consider Part III (Reference) instead

**Example:** MCP setup is in WF-KC-001 (Knowledge Contributor) with cross-reference from WF-NO-001 (Notebook Owner).

## Content Guidelines

### Writing Style

- **Active voice:** "You can create entries" not "Entries can be created"
- **Conversational:** Use "you" and "we" naturally
- **Concise:** Long-winded explanations hurt comprehension
- **Concrete:** Use examples, not abstractions
- **Structured:** Use headings, lists, and callouts liberally

### Code Examples

- **Comprehensive:** Show full context, error handling, sample output
- **Runnable:** Examples should work as-is without modifications (except tokens/URLs)
- **Annotated:** Add comments for non-obvious parts
- **Multiple options:** Show both UI and CLI/API approaches when applicable

### Screenshots

- **Professional:** Use vector graphics or high-resolution PNG
- **Annotated:** Add arrows, boxes, or callouts to highlight key elements
- **Sequential:** Use consistent numbering (1️⃣ 2️⃣ 3️⃣) for multi-step workflows
- **Accessible:** Include alt text describing what's shown

### Cross-References

- **Link generously:** Help readers navigate between related content
- **Provide context:** Don't just say "[See Chapter 5](#)"; explain why
- **Check links:** Broken links harm credibility and usability

## Version Control

- **Version the manual with the platform:** Manual v1.0 corresponds to Platform v2.1
- **Changelog:** Include a changelog section in `index.md` tracking major updates
- **Deprecation notices:** Mark removed features clearly with deprecation dates

## Maintenance Plan

### Upon Platform Release

1. Update version number in `index.md` and `mkdocs.yml`
2. Review all chapters for feature changes
3. Update screenshots if UI changed
4. Add entry to changelog
5. Rebuild PDF and HTML

### Quarterly Review

1. Review support tickets for common questions
2. Add FAQ section or update troubleshooting chapter
3. Update workflow descriptions based on user feedback
4. Fix any typos or clarity issues

### Annual Review

1. Refresh screenshots (UI may have evolved)
2. Solicit feedback from each persona type
3. Reorganize based on usage patterns
4. Consider new workflows or chapters

## Contributing

To contribute to the manual:

1. **Create a branch** for your changes
2. **Edit markdown files** in `chapters/` or `workflows/`
3. **Test locally** by building with `pandoc-build.sh`
4. **Submit a pull request** with a clear description
5. **Request review** from at least one persona expert

## FAQ

**Q: Should I write in Markdown or reStructuredText?**
A: Markdown. It's more readable and portable.

**Q: How many words per chapter?**
A: Typically 1500-3000 words for persona chapters, 500-2000 for reference chapters.

**Q: Can I link to external resources?**
A: Yes, but prefer self-contained content. External links may break.

**Q: How do I handle version-specific features?**
A: Use version callouts:
```markdown
> **Added in Platform 2.2:** This feature is new.
> **Deprecated in Platform 2.3:** This workflow is being phased out.
```

**Q: Who reviews the manual?**
A: Technical writers, product managers, and power users from each persona type.

## Support

Questions about the manual itself?

- **Technical issues** — File an issue on GitHub
- **Content feedback** — Contact the documentation team
- **Typos/corrections** — Submit a PR with fixes

---

**Last updated:** February 22, 2026
**Manual version:** 1.0.1 (Beta) — Added Phase 1 user management implementation notes
**Platform version:** 2.1.0
**Admin Panel Phases:** Phase 0 (shell) ✅, Phase 1 (user management) ✅, Phase 2 (advanced features) 🔮
