---
id: "WF-XX-XXX"
title: "[Workflow Title]"
personas: ["[Primary Persona]", "[Secondary Persona]"]
overlaps_with: ["WF-XX-XXX", "WF-XX-XXX"]
prerequisites:
  - "Account created and authenticated"
  - "[Permission/clearance requirement]"
estimated_time: "5-10 minutes"
difficulty: "Beginner|Intermediate|Advanced"
---

# [Workflow Title]

## Overview

Brief 2-3 sentence description of what this workflow accomplishes and when you'd use it.

**Use case:** Real-world scenario when this workflow is needed.

**Related workflows:** Links to [other workflows](#) that often come before or after this one.

## Prerequisites

Before starting, ensure you have:
- [ ] Account created and authenticated
- [ ] Appropriate permissions or clearance levels
- [ ] [Any required setup or configuration]

## Step-by-Step Instructions

### Step 1: [First Action]

**Navigate to:** [UI path] or **Command:** [MCP/CLI command]

1. Click on [button/menu]
2. Enter [information]
3. Verify [confirmation message]

**What you'll see:**
- [Expected result description]
- [Status indicator or confirmation]

**Common issues:**
- If you see [error message], then [solution]

### Step 2: [Second Action]

[Repeat structure from Step 1]

### Step 3: [Continue as needed]

## Verification

How to confirm the workflow completed successfully:

- [ ] [Verification criterion 1]
- [ ] [Verification criterion 2]
- [ ] [Verification criterion 3]

## Tips & Tricks

- **Shortcut:** [Keyboard shortcut or quick method]
- **Best practice:** [Recommended approach]
- **Performance:** [Optimization advice for large datasets]

## Next Steps

After completing this workflow, you might want to:
- [Related workflow 1](#)
- [Related workflow 2](#)

## Troubleshooting

### Error: [Common Error Message]

**Cause:** [Why this happens]

**Solution:** [Step-by-step fix]

### Error: [Another Common Error]

**Cause:** [Why this happens]

**Solution:** [Step-by-step fix]

---

## API Reference (if applicable)

### MCP Operation: WRITE

```bash
curl -X POST http://localhost:8000/write \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "notebook_id": "abc123",
    "content": "Entry content",
    "content_type": "text/plain",
    "topic": "organization/team/subject"
  }'
```

**Response:**
```json
{
  "entry_id": "entry_123",
  "position": 42,
  "created_at": "2026-02-21T10:30:00Z"
}
```

---

**Last updated:** [Date]
**Version:** [Manual Version]
**Related personas:** [Links to other personas using this workflow]
