# Chapter 4: Knowledge Contributor

## Role Overview

As a **Knowledge Contributor**, you are the backbone of Cyber. Your role is to create, organize, and refine the collective knowledge that your organization relies on. You spend most of your time creating entries, discovering related knowledge, and helping others find information they need.

**Key Responsibilities:**
- Create well-structured entries in assigned notebooks
- Organize content with clear topics and references
- Discover and learn from existing knowledge
- Revise and improve entries based on feedback
- Monitor changes to stay informed on evolving topics
- Collaborate with other contributors through references and causal linking

**Required Permissions:**
- At least "Read" access to one or more notebooks
- "Read+Write" access to contribute new entries
- Your organizational clearance (inherited from your role)

**Typical Workflows:** 5 core workflows in this chapter

---

## Workflow 1: Creating and Organizing Entries

### Overview

Learn how to create a new entry in a notebook, structure it with topics, and link it to related entries. This is the foundational workflow for all contributors.

**Use case:** You have new knowledge (research findings, meeting notes, architectural decisions) that needs to be recorded in your team's notebook.

**Related workflows:**
- [Managing Revisions](#workflow-4-managing-revisions) — Update entries after creation
- [Browsing Knowledge](#workflow-2-browsing-and-discovering-knowledge) — Find related entries to reference
- [Observing Changes](#workflow-5-observing-changes) — Track what others add

### Prerequisites

- [ ] Cyber account created and authenticated
- [ ] At least "Read+Write" access to a notebook
- [ ] Understanding of your notebook's topic structure
- [ ] Content ready to enter (notes, document, research)

### Step-by-Step Instructions

#### Step 1: Navigate to Your Notebook

**UI Path:** Left sidebar → Notebooks → Select notebook name

1. Click **Notebooks** in the left sidebar
2. You'll see a list of notebooks you have access to
3. Click the notebook where you want to add an entry
4. You'll see the **Entry Feed** with existing entries

**Example:**
```
Your Notebooks
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📂 Q1 Planning (Read+Write) ← Click here
📂 R&D Notes (Read+Write)
📂 Strategic Roadmap (Read only)
```

#### Step 2: Click "New Entry"

Once in the notebook, look for the **"+ New Entry"** button:

```
Q1 Planning
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[+ New Entry]  [Filters]  [Search box]

Entry Feed:
(list of existing entries)
```

Click **"+ New Entry"** to open the entry creation form.

#### Step 3: Fill in Entry Details

You'll see a form with these fields:

```
Create New Entry
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Title *
[Engineering Roadmap Q1 2026]

Topic *
[organization/engineering/roadmap]
  ↓ (click to browse topic hierarchy)

Content *
[Large text editor - use Markdown for formatting]

References (Optional)
[+] Add entry references...
  Search: [_____________]
  [Quarterly Goals] [Q1 Budget] [Team Charter]

[Create Entry]  [Preview]  [Save as Draft]  [Cancel]
```

**Field Explanations:**

| Field | Required | Guidelines |
|-------|----------|-----------|
| **Title** | Yes | Clear, specific (e.g., "Engineering Roadmap Q1 2026", not "Stuff") |
| **Topic** | Yes | Hierarchical path (e.g., org/engineering/roadmap). Start with org, team, or project name. |
| **Content** | Yes | Supports Markdown formatting (headers, lists, code blocks, links) |
| **References** | No | Link to related entries for context and cross-referencing |

#### Step 4: Write Your Content

The content editor supports **Markdown formatting**:

```markdown
# Engineering Roadmap Q1 2026

## Overview
This quarter we're focusing on infrastructure modernization.

## Key Initiatives

1. **Kubernetes Migration**
   - Timeline: Jan - Mar 2026
   - Team: DevOps + SRE
   - Status: In Progress

2. **API v2 Release**
   - Timeline: Feb - Mar 2026
   - Team: Backend Engineering
   - Status: Design phase

## Success Criteria
- [ ] All services containerized
- [ ] Zero-downtime deployments
- [ ] < 100ms API latency (p99)

---

**See also:** [Quarterly Goals], [Team Charter], [Infrastructure Budget]
```

**Pro tips:**
- Use headers (`# Big Title`, `## Smaller Heading`) to structure content
- Use bullet points and numbered lists for clarity
- Include status indicators (✓ Done, ⏳ In Progress, ❌ Blocked)
- Add checkboxes for tracking tasks

#### Step 5: Add References (Optional but Recommended)

References link your entry to related entries, creating a knowledge graph:

1. Click **"[+] Add References"** at the bottom
2. Search for entries by title or topic:
   ```
   Search references...
   ┌─────────────────────────────────┐
   │ [quarterly goals_______________] │
   └─────────────────────────────────┘

   Results:
   ☐ Quarterly Goals (engineering/planning)
   ☐ Q1 OKRs Overview (organization/goals)
   ☐ Quarterly Budget Review (finance/budgets)
   ```
3. Check the entries you want to reference
4. Click **"Add Selected"**

**What references do:**
- Create bidirectional links (both entries reference each other)
- Help Cyber measure entry coherence (integration cost)
- Allow readers to discover related content
- Build the knowledge graph structure

#### Step 6: Preview (Optional)

Click **"Preview"** to see how your entry will look:

```
Engineering Roadmap Q1 2026
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Author: You (Jane Smith)
Topic: organization/engineering/roadmap
References: 3 entries linked

## Overview
This quarter we're focusing on infrastructure modernization.
...
```

#### Step 7: Create Entry

Click **"Create Entry"**. You'll see:

```
✓ Entry created successfully!

Entry ID: entry_abc123
Position: 127
Integration Status: Probation (calculating friction)

[View Entry] [View in Notebook] [Create Another]
```

**What happens next:**
1. Your entry is signed with your cryptographic key (proof of authorship)
2. Background jobs analyze it for integration cost
3. Entry goes into "Probation" status while being analyzed
4. Within 1-5 minutes, it stabilizes to "Integrated" or "Contested" status

### Verification

Confirm your entry was created successfully:

- [ ] Entry appears at the bottom of your notebook's entry feed
- [ ] Title and topic are correct
- [ ] Content displays properly (Markdown formatted)
- [ ] References are linked correctly
- [ ] Your name appears as the author
- [ ] Timestamp shows current date/time
- [ ] Integration status shows "Probation" (will change to "Integrated")

### Tips & Tricks

#### Shortcut: Use MCP Integration

If you have MCP set up (see [Workflow 1 from Chapter 4](#workflow-1-setting-up-mcp-access)), you can create entries via Claude:

```
Claude: Create a new entry in the Q1 Planning notebook:
  Title: Engineering Roadmap Q1 2026
  Topic: organization/engineering/roadmap
  Content: [your content]
```

Claude will create the entry and sign it automatically.

#### Batch Import (Advanced)

For importing many entries at once, use the CLI:

```bash
cyber write --notebook-id nb_xyz \
  --title "My Entry" \
  --topic "org/team/subject" \
  --content "$(cat file.md)" \
  --references entry_1,entry_2
```

#### Draft Saving

Click **"Save as Draft"** to save without creating yet. Drafts are stored locally in your browser until you're ready to publish.

#### Structured Data

For technical entries, use code blocks:

````markdown
## Configuration

```yaml
database:
  host: db.prod.internal
  port: 5432
  replica_count: 3
```

```json
{
  "api_version": "v2",
  "deprecation_date": "2026-06-01"
}
```
````

### Next Steps

After creating your entry:
- [Browse and discover](#workflow-2-browsing-and-discovering-knowledge) other entries to build context
- [Manage revisions](#workflow-4-managing-revisions) if you need to update your entry
- [Observe changes](#workflow-5-observing-changes) to see how others respond

---

## Workflow 2: Browsing and Discovering Knowledge

### Overview

Learn how to search, filter, and navigate existing entries to find the information you need and understand how it connects to your work.

**Use case:** You're starting a new project and need to understand existing decisions, architecture, or past experiences on similar topics.

**Related workflows:**
- [Creating Entries](#workflow-1-creating-and-organizing-entries) — Add new entries informed by what you discover
- [Observing Changes](#workflow-5-observing-changes) — Monitor knowledge you're interested in

### Prerequisites

- [ ] Cyber account and at least "Read" access to notebooks
- [ ] Understanding of your organization's topic structure

### Step-by-Step Instructions

#### Method 1: Full-Text Search

**Search box location:** Top of any page (keyboard shortcut: `/`)

1. Click the search box at the top
2. Type your query:
   ```
   Search box
   [Kubernetes migration_____]
   ```
3. Press Enter or click Search
4. Results appear ranked by relevance:
   ```
   Search Results for "Kubernetes migration"
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   [Relevance: ████████░░] Kubernetes Migration Plan
     Topic: organization/infrastructure/migration
     Author: DevOps Team
     Created: Jan 15, 2026
     "We're planning a phased migration to Kubernetes..."

   [Relevance: ██████░░░░] K8s Security Considerations
     Topic: organization/infrastructure/security
     Author: Security Team
     Created: Jan 10, 2026
   ```

**Search syntax:**
```
Exact phrase:     "Kubernetes migration"
Author filter:    author:Alice
Topic filter:     topic:infrastructure
Classification:   level:SECRET
Friction range:   friction:>5 (high friction/controversial)
```

#### Method 2: Topic Hierarchy Browse

**Navigate to:** Sidebar → Explore

1. Click **"Explore"** in the sidebar
2. You'll see a hierarchical topic tree:
   ```
   📚 Explore
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   You have access to 42 notebooks

   📁 organization/
      📁 engineering/
         📁 backend/
            📄 Database Migrations
            📄 API Architecture
            📄 Performance Optimization
         📁 infrastructure/
            📁 cloud/
               📄 Kubernetes Migration
               📄 Multi-cloud Strategy
      📁 operations/
         📁 incidents/
            📄 2026-02 Outage Report
   ```
3. Click any topic to see all entries under it
4. Entries are listed with metadata:
   ```
   Topic: organization/engineering/infrastructure/cloud
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   [Entry] Kubernetes Migration Plan
      Author: DevOps Team  |  Created: Jan 15, 2026
      Integration: ✓ Integrated (stable)
      Friction: 0.34 (low - well aligned)
      References: 3 entries
      [Read] [History] [Compare]

   [Entry] Multi-cloud Strategy
      Author: Infrastructure Team  |  Created: Jan 10, 2026
      Integration: ⚠️ Probation (still calculating)
      Friction: 2.1 (medium - some disagreement)
      References: 2 entries
   ```

#### Method 3: Browse Your Notebook's Entries

**Navigate to:** Notebooks → Select notebook → Entry Feed

1. Go to **Notebooks** in the sidebar
2. Click a specific notebook
3. You'll see the **Entry Feed** with filters:
   ```
   Q1 Planning
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   [Filter: Topic ▼] [Filter: Status ▼] [Filter: Friction ▼]

   [Topic dropdown]
   ○ All Topics
   ○ organization/planning
   ○ organization/planning/goals
   ○ organization/planning/budget

   [Status dropdown]
   ○ All Statuses
   ☑ Integrated
   ☑ Probation
   ☑ Contested

   [Friction dropdown]
   ○ All Friction
   ○ Low (0-2)
   ○ Medium (2-5)
   ○ High (5-10)
   ```

4. Select filters to narrow results
5. Entries appear sorted by **newest first** (or selected filter)

#### Step 4: Read an Entry

Click any entry to open the full view:

```
Kubernetes Migration Plan
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

## Overview

We're planning a phased migration to Kubernetes over
the next three months...

[Full content displayed in readable format]

---

Metadata:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Author:         DevOps Team (Alice Chen)
Created:        Jan 15, 2026, 10:30 AM
Position:       127 (causal order)
Integration:    ✓ Integrated (stable)
Friction:       0.34 (low - well aligned with existing entries)
Topic:          organization/engineering/infrastructure/cloud
References:     → Multi-cloud Strategy
                → Q1 Budget Plan
                → Team Charter

This entry is referenced by:
                ← Infrastructure Roadmap
                ← Jan All-Hands Notes

[Related Entries] [View History] [Compare Versions] [Discussion]
```

#### Step 5: Understand Integration Status

Each entry shows its **integration status**:

| Status | Meaning | What to Do |
|--------|---------|-----------|
| **✓ Integrated** | Stable, well-aligned with other knowledge | Safe to rely on, reference in your work |
| **⏳ Probation** | New, still being analyzed for coherence | Wait a few minutes for final status, check back |
| **⚠️ Contested** | High friction, contradicts other entries | Investigate disagreement, discuss with authors |

**High friction doesn't mean wrong** — it might mean:
- This is a novel/innovative idea (not yet mainstream)
- Legitimate disagreement between approaches
- Outdated information vs. newer insights
- Different contexts (what works for one team may not work for another)

### Verification

Confirm you're effectively discovering knowledge:

- [ ] Found at least one entry related to your current project
- [ ] Used at least two discovery methods (search, topic browse, notebook feed)
- [ ] Understood the relationship between entries (references, friction)
- [ ] Noted entries with high friction for follow-up discussion
- [ ] Bookmarked or noted entry IDs for later reference

### Tips & Tricks

#### Use Friction Filtering for Learning

- **Low friction (0-2):** Established best practices, safe to follow
- **Medium friction (2-5):** Evolving approaches, worth understanding context
- **High friction (5-10):** Controversial or novel ideas, engage with authors

#### Follow Related Entries

When reading an entry, click **"Related Entries"** to see:
- Entries it references (what it builds on)
- Entries that reference it (what builds on this)
- Entries on the same topic

This creates a **knowledge exploration path**.

#### Watch Authors

If you find entries by great authors, click their name to see other entries they've created. Good contributors are gold mines of knowledge.

#### Use Causal Positions

Each entry has a **position number** (e.g., Position 127). Lower numbers = older, higher = newer within that notebook. This helps understand timeline of decisions.

### Next Steps

After discovering knowledge:
- Create an entry building on what you've learned
- Discuss high-friction entries with authors
- Reference the entries you found in your own work

---

## Workflow 3: Searching Across Notebooks

### Overview

Search simultaneously across multiple notebooks and organizations to find knowledge regardless of where it lives.

**Use case:** You're investigating a cross-cutting concern (e.g., security, compliance, architecture patterns) that spans multiple teams.

### Prerequisites

- [ ] At least "Read" access to 2+ notebooks
- [ ] Clear understanding of what you're searching for

### Step-by-Step Instructions

The **Global Search** is accessible from anywhere:

1. Press `/` (forward slash) on your keyboard
2. Or click the Search icon in the sidebar
3. Enter your query:
   ```
   Global Search
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   [Encryption standards____________]

   🔍 Searching across all accessible notebooks...
   ```
4. Results appear with filters:
   ```
   Results for "encryption standards" (42 matches)
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   [Notebook: All ▼] [Author: All ▼] [Date: All ▼]

   [Relevance ████████░░] Encryption Standards v2
     From: Operations/Security
     Author: Security Team
     Created: Jan 2026

   [Relevance ██████░░░░] TLS Configuration Guide
     From: Engineering/Infrastructure
     Author: DevOps Team
     Created: Dec 2025
   ```

**Advanced Filters:**
- Filter by notebook, author, date range
- Sort by relevance or date
- View entry counts per notebook

### Verification

- [ ] Found entries across multiple notebooks
- [ ] Used filters to narrow results effectively
- [ ] Compared approaches between teams
- [ ] Created an entry synthesizing findings

---

## Workflow 4: Managing Revisions

### Overview

Learn how to update entries over time. Cyber uses immutable revisions—you don't edit entries, you create new versions that supersede old ones.

**Use case:** You created an entry about a project roadmap, and it needs updating after a planning meeting. You revise it, creating a new version.

**Related workflows:**
- [Creating Entries](#workflow-1-creating-and-organizing-entries) — Your initial entry
- [Observing Changes](#workflow-5-observing-changes) — Track revisions others make

### Prerequisites

- [ ] "Read+Write" access to the notebook containing the entry
- [ ] The entry you want to revise
- [ ] Clear understanding of what needs to change

### Step-by-Step Instructions

#### Step 1: Find the Entry to Revise

Navigate to the entry (via notebook, search, or browse).

Click **"[Revise]"** button:

```
Engineering Roadmap Q1 2026
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Read] [History] [Revise] [Compare]
                  ↑ Click here
```

#### Step 2: Create a Revision

A new form appears with the **previous version's content pre-filled**:

```
Revise Entry
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Original Entry ID: entry_abc123
Revision Reason: [Updated after Jan planning meeting_____]

Content *
[Previous content pre-populated...]

[Update Entry] [Preview] [Cancel]
```

#### Step 3: Make Your Changes

Edit the content as needed:

```
Original:
## Key Initiatives
1. Kubernetes Migration
2. API v2 Release

Updated:
## Key Initiatives
1. Kubernetes Migration (timeline: Jan-Mar → Feb-Apr)
2. API v2 Release
3. Database Optimization (new initiative)
```

#### Step 4: Add a Reason

In the **"Revision Reason"** field, explain why you're revising:

```
Revision Reason Examples:
- "Updated after Jan 15 planning meeting"
- "Fixed typo in timeline"
- "Added new Q1 initiatives approved by leadership"
- "Corrected infrastructure budget numbers"
```

Good reasons help readers understand the change context.

#### Step 5: Submit Revision

Click **"Update Entry"**:

```
✓ Revision created successfully!

Original Entry:    entry_abc123, Position 127
New Revision:      entry_def456, Position 128
Reason:            Updated after Jan planning meeting

You can:
  [View New Revision] [View History] [Compare Versions]
```

### What Happens to the Old Version?

- ✅ Old version is preserved forever (immutable)
- ✅ New revision shows as current in the entry feed
- ✅ Readers see the new version by default
- ✅ History shows all revisions (with reasons)
- ✅ You can compare old vs. new side-by-side

### Verification

Confirm your revision:

- [ ] New version appears in the entry feed
- [ ] Revision reason is recorded
- [ ] History shows both old and new versions
- [ ] Changes are visible in the new version
- [ ] Revision count increments

### Tips & Tricks

#### View Entry History

Click **"[History]"** to see all versions:

```
Entry History: Engineering Roadmap Q1 2026
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Version 3 (Current) - Position 129
  Author: Jane Smith
  Date: Jan 22, 2026, 2:30 PM
  Reason: Added database optimization initiative
  [View] [Compare with v2]

Version 2 - Position 128
  Author: Jane Smith
  Date: Jan 15, 2026, 10:30 AM
  Reason: Updated timeline after planning meeting
  [View] [Compare with v1]

Version 1 (Original) - Position 127
  Author: Jane Smith
  Date: Jan 10, 2026, 9:00 AM
  Reason: (original creation)
  [View]
```

#### Compare Versions

Click **"[Compare]"** to see differences:

```
Comparison: v1 vs. v3
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

- Kubernetes Migration (timeline: Jan-Mar)
+ Kubernetes Migration (timeline: Feb-Apr)

  API v2 Release

+ Database Optimization (new initiative)
```

#### Revision Frequency

- **Small fixes** (typos, formatting): Revise immediately
- **Major changes** (scope, timeline, approach): Coordinate with stakeholders first
- **Multiple small changes**: Batch them into one revision with clear reason

### Next Steps

After revising:
- Notify stakeholders if it's an important change
- Check if dependent entries need updating
- Monitor discussion/comments on the revision

---

## Workflow 5: Observing Changes

### Overview

Learn how to track changes to notebooks you care about, staying informed without manually checking repeatedly.

**Use case:** You're implementing a feature based on an architectural entry, and want to know if requirements change.

**Related workflows:**
- [Browsing Knowledge](#workflow-2-browsing-and-discovering-knowledge) — Find entries to observe
- [Creating Entries](#workflow-1-creating-and-organizing-entries) — Contribute your own changes

### Prerequisites

- [ ] At least "Read" access to a notebook
- [ ] Specific notebook or entry you want to monitor

### Step-by-Step Instructions

#### Method 1: Watch a Notebook for Changes

**Via UI:**

1. Go to a notebook (Notebooks → Select notebook)
2. Click **"Watch"** or **"Subscribe"** button (location varies)
3. Select notification frequency:
   ```
   Watch Notebook
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   Q1 Planning

   Notify me of:
   ☑ New entries
   ☑ Revisions to existing entries
   ☐ Comments/discussions
   ☐ Integration status changes

   Frequency:
   ○ Immediately
   ◉ Daily digest
   ○ Weekly summary
   ○ Never (just view history)

   [Save Preferences]
   ```
4. You'll receive notifications matching your preferences

#### Method 2: Use the OBSERVE Operation (Advanced)

If using MCP or REST API, use the **OBSERVE** operation:

```bash
curl -X GET http://localhost:8000/observe \
  -H "Authorization: Bearer TOKEN" \
  -d '{
    "notebook_id": "nb_xyz789",
    "since_position": 120
  }'
```

This returns all entries added since position 120, allowing you to process changes programmatically.

#### Method 3: View Activity Timeline

In any notebook, click **"Activity"** or **"Timeline"**:

```
Q1 Planning - Recent Activity
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Position 130 - Jan 22, 2:30 PM
  Jane Smith revised "Engineering Roadmap Q1"
  Reason: Added database optimization initiative

Position 129 - Jan 22, 1:15 PM
  Bob Johnson created "Q1 Budget Summary"
  References: 2 entries

Position 128 - Jan 22, 10:00 AM
  Alice Chen revised "Team Onboarding Guide"
  Reason: Updated with new team members

Position 127 - Jan 21, 4:45 PM
  Jane Smith revised "Engineering Roadmap Q1"
  Reason: Updated timeline after planning meeting
```

**Key insights:**
- **Position** = causal order (not timestamps)
- **Chronological view** of what changed
- **Types of changes** visible at a glance
- **Who changed what** for audit purposes

### Verification

Confirm you're observing correctly:

- [ ] You're receiving notifications or can view activity timeline
- [ ] You can see new entries as they're added
- [ ] You can see revisions with reasons
- [ ] Activity is in causal order (positions increase)
- [ ] You understand the impact of changes

### Tips & Tricks

#### Set Smart Notification Frequency

- **Daily digest** — Good for active notebooks you check regularly
- **Weekly summary** — Good for passive monitoring
- **Immediately** — Only for critical entries (security, compliance)

#### Track Specific Topics

Some notebooks let you "watch" specific topics:

```
Watch Topics in Q1 Planning
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

☑ organization/planning/goals
☑ organization/planning/budget
☐ organization/planning/hiring

Notify when entries in these topics are created or revised.
```

#### Use Positions for Bookmarking

Note the **position number** of where you last caught up:

```
Last checked: Position 120
Today's new entries: Position 121-130
```

Next time you check, start from position 120 to see only new changes.

### Next Steps

After observing changes:
- Revise your entries if new information affects them
- Discuss contradictions with other contributors
- Update dependent work if requirements changed

---

## Summary: Quick Reference

### The 5 Workflows at a Glance

| Workflow | Purpose | Time | Frequency |
|----------|---------|------|-----------|
| **1. Create Entries** | Add new knowledge | 10-30 min | Weekly |
| **2. Browse & Search** | Discover existing knowledge | 5-15 min | Daily |
| **3. Search Notebooks** | Cross-team knowledge discovery | 5-10 min | As needed |
| **4. Manage Revisions** | Update entries over time | 10-20 min | As needed |
| **5. Observe Changes** | Stay informed of updates | 2-5 min | Continuous |

### Your Workflow Loop

```
1. Create Entry
   ↓ (Research needed)
2. Browse & Search
   ↓ (Found related entries)
3. Create Revision
   ↓ (Or create new entry building on discovery)
4. Observe Changes
   ↓ (Track impact and discussions)
5. Back to Step 1
   ↓ (Continuous knowledge refinement)
```

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `/` | Search |
| `n` | New entry |
| `e` | Edit/revise entry |
| `s` | Save |
| `Esc` | Close modal |
| `?` | Show all shortcuts |

---

## Related Personas

Your workflows often overlap with:

- **[Notebook Owner](06-notebook-owner.md)** — Who reviews your submissions and manages access
- **[Auditor/Compliance Officer](07-auditor.md)** — Who reviews your entries for security/compliance
- **[Cross-Org Coordinator](10-cross-org-coordinator.md)** — Who may mirror your entries to other organizations

---

## Troubleshooting

### "Access Denied" When Creating Entry

**Cause:** You don't have "Read+Write" access to this notebook.

**Solution:**
1. Ask the notebook owner to grant you write access
2. Check your clearance level (Settings → Profile)
3. Ensure you're trying to write to the right notebook

### Entry Stuck in "Probation" Status

**Cause:** Background analysis is taking longer than usual.

**Solution:**
1. Wait 5-15 minutes, then refresh
2. Check system status dashboard
3. Contact admin if stuck for > 1 hour

### Revision Didn't Save

**Cause:** Network error or session timeout.

**Solution:**
1. Try again; draft may be auto-saved locally
2. Copy your content to clipboard before retrying
3. Check your internet connection

### Can't Find an Entry I Know Exists

**Cause:** Search index lag or access restriction.

**Solution:**
1. Try browsing by topic instead of searching
2. Check your clearance level (you may not have access)
3. Ask notebook owner to confirm entry exists

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
