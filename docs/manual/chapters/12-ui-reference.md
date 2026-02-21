# Chapter 12: UI Reference

## Page Navigation

### Sidebar Menu
```
Cyber (logo)
━━━━━━━━━━━━━━
📊 Dashboard      → Home page, system status
📂 Notebooks      → Your notebooks
📝 Entries        → Browse/search entries
📚 Explore        → Topic hierarchy
🔍 Search         → Full-text search
[Divider]
⚙️ Settings       → Preferences, API tokens
👤 Profile        → Account info, clearance
🔐 Security       → Keys, 2FA
📋 Audit Log      → Your access history
[Divider]
🚀 Admin Panel    → Admin-only features (if applicable)
```

### Key Pages

| Page | URL | Purpose | Access |
|------|-----|---------|--------|
| Dashboard | `/` | Overview, status | All users |
| Notebooks | `/notebooks` | Your notebooks | All users |
| Entries | `/entries` | Global entry list | All users |
| Explore | `/explore` | Topic browser | All users |
| Search | `/search` | Full-text search | All users |
| Profile | `/profile` | Account settings | All users |
| Settings | `/settings` | Preferences | All users |
| Audit Log | `/audit-log` | Your audit trail | All users |
| Admin Panel | `/admin` | User/org management | Admins only |

## Keyboard Shortcuts

| Shortcut | Action | Context |
|----------|--------|---------|
| `/` | Focus search box | Anywhere |
| `?` | Show help menu | Anywhere |
| `n` | New entry/notebook | In notebook |
| `e` | Edit/revise entry | On entry |
| `s` | Save | In edit mode |
| `Esc` | Close modal/exit edit | Modal/edit mode |
| `g d` | Go to Dashboard | Anywhere |
| `g n` | Go to Notebooks | Anywhere |
| `g e` | Go to Entries | Anywhere |
| `g s` | Go to Search | Anywhere |
| `j` | Next result | Search results |
| `k` | Previous result | Search results |

## Common UI Components

### Badges

| Badge | Meaning |
|-------|---------|
| ✅ | Success/healthy |
| ⚠️ | Warning/caution |
| ❌ | Error/failed |
| ⏳ | In progress/pending |
| 🔒 | Locked/restricted |
| ★ | Starred/favorite |

### Status Indicators

| Status | Color | Meaning |
|--------|-------|---------|
| Integrated | Green | Stable, well-aligned |
| Probation | Yellow | New, still analyzing |
| Contested | Red | High friction, controversial |
| Offline | Gray | Agent not responding |
| Syncing | Blue | Data transfer in progress |

### Classification Labels

```
PUBLIC             (open)
CONFIDENTIAL       (restricted)
SECRET             (very restricted)
TOP_SECRET         (maximum restriction)

With compartments:  SECRET / {Operations, Database}
```

### Access Tiers

```
Existence    (know it exists)
Read         (can view)
Read+Write   (can create/edit)
Admin        (full control)
```

## Filters

### Topic Filter
```
[Organization] > [Team] > [Subject] > [Subtopic]

Examples:
  organization/engineering/backend/database
  organization/operations/incidents/security
```

### Status Filter
```
○ All Statuses
☑ Integrated   (stable entries)
☑ Probation    (new entries)
☑ Contested    (controversial)
```

### Friction Filter
```
○ All Friction
○ Low (0-2)      (well aligned)
○ Medium (2-5)   (some disagreement)
○ High (5-10)    (major disagreement)
```

### Date Range
```
○ Last 7 days
○ Last 30 days
○ Last year
○ Custom: [From] to [To]
```

## Dialogs & Modals

### Confirmation Dialog
```
⚠️  Are you sure?

This action cannot be undone.

[Confirm] [Cancel]
```

### Error Dialog
```
❌ Error

Something went wrong:
"Clearance insufficient for this resource"

[OK] [View Details]
```

### Success Dialog
```
✅ Success

Entry created successfully!

Entry ID: entry_abc123
Position: 1,247

[View] [Create Another] [Close]
```

## Accessibility

- **Screen Reader:** Full ARIA labels on all elements
- **Keyboard Navigation:** Use Tab to navigate, Enter to activate
- **High Contrast:** Toggle in Settings → Appearance
- **Font Size:** Adjust in Settings → Appearance
- **Dark Mode:** Toggle in Settings → Appearance

---

**Last updated:** February 21, 2026
**UI Version:** 2.1.0
**Platform Version:** 2.1.0
