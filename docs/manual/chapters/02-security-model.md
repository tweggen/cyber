# Chapter 2: Security Model

### Understanding Cyber's Security Architecture

Cyber implements the **Bell-LaPadula mandatory access control (MAC) model**, a formal security framework used in government, military, and enterprise systems. This chapter explains how classification levels, compartments, and clearances work together to prevent unauthorized information disclosure.

**Key principle:** *Information flows only upward.* Classified information never flows to less-classified recipients.

---

### Classification Levels

Cyber uses a **five-level classification hierarchy**. Each level represents increasing sensitivity and restricted distribution:

| Level | Name | Typical Use | Distribution |
|-------|------|-------------|--------------|
| 1 | `PUBLIC` | General company info, marketing, public research | No restrictions |
| 2 | `CONFIDENTIAL` | Internal memos, non-sensitive business data | Internal use only |
| 3 | `SECRET` | Strategic planning, customer data, technical designs | Need-to-know basis |
| 4 | `TOP_SECRET` | Military/national security, critical infrastructure | Severe impact if disclosed |
| 5+ | Custom | Organization-defined levels | Organization-defined |

**Dominance hierarchy:**
```
PUBLIC < CONFIDENTIAL < SECRET < TOP_SECRET < [Custom levels]
```

A principal (user or group) cleared for a higher level can read all lower levels. A `TOP_SECRET` user can read `PUBLIC`, `CONFIDENTIAL`, and `SECRET`. A `CONFIDENTIAL` user cannot read `SECRET` or above.

---

### Compartments (Domains)

**Compartments** are optional security categories that further restrict access *within* a classification level. They're used for:

- **Functional separation** — "Medical Research" vs. "Legal" vs. "Operations"
- **Project isolation** — "Project Alpha" vs. "Project Bravo"
- **Sensitive subjects** — "Executive Compensation", "Merger Negotiations", "Criminal Investigation"

**Key rule:** To access compartmented information, a user must be cleared for *both* the classification level AND the specific compartment.

**Example compartments:**
- Medical Research
- Strategic Planning
- Infrastructure Operations
- Customer PII
- Executive / Confidential
- International Operations

Organizations define their own compartment naming conventions. See [Chapter 13: Security Reference](#) for naming best practices.

---

### Security Labels

Every principal, notebook, and entry has a **security label** combining level + compartments:

**Format:** `LEVEL / {compartment1, compartment2, ...}`

**Examples:**

| Label | Meaning |
|-------|---------|
| `PUBLIC / {}` | No restrictions, anyone can access |
| `CONFIDENTIAL / {}` | Company-wide access only |
| `SECRET / {Medical Research}` | Medical researchers with SECRET clearance only |
| `TOP_SECRET / {Infrastructure, Operations}` | Users cleared for BOTH Infrastructure AND Operations compartments |
| `TOP_SECRET / {Executive}` | Senior leadership only |

**Empty compartment set (`{}`)** means no compartment restrictions — anyone at that level can access.

---

### Clearance Dominance

The security model uses **dominance rules** to determine whether a user can access information:

**Definition:** User clearance `C_user` **dominates** information label `L_info` if:
1. `C_user.level ≥ L_info.level` AND
2. `L_info.compartments ⊆ C_user.compartments`

In other words:
- User's level must be at least as high as the information's level, AND
- User's compartments must be a *superset* of the information's compartments

**Example calculations:**

**User:** `TOP_SECRET / {Medical, Infrastructure, Strategic}`

Can access? | Information Label | Why?
-----------|-------------------|-----
✓ | `PUBLIC / {}` | Level matches, no compartments needed
✓ | `CONFIDENTIAL / {}` | Level matches, no compartments
✓ | `SECRET / {Medical}` | Level matches, user has Medical compartment
✓ | `TOP_SECRET / {Infrastructure}` | Level matches, user has Infrastructure compartment
✓ | `TOP_SECRET / {Medical, Infrastructure}` | Level matches, user has both compartments
✗ | `TOP_SECRET / {Medical, Infrastructure, Executive}` | User lacks Executive compartment
✗ | `TOP_SECRET / {Finance}` | User lacks Finance compartment
✗ | `SECRET / {Finance}` | User lacks Finance compartment (even though level is OK)

---

### Information Flow Control

**Bell-LaPadula's central rule:** Information can only flow from a *lower* classification to *higher* classification (or same level).

**What this means in practice:**

1. **Read Rule ("Simple Security Property"):** A user can read information only if their clearance dominates the information's label.

2. **Write Rule ("*-Property" or "Confinement Property"):** A user can write to a notebook/entry only if the information's label dominates the user's clearance.

**The Write Rule is tricky.** Think of it this way:

- If you're cleared for `TOP_SECRET / {Medical}`, you should only write to notebooks labeled `TOP_SECRET / {Medical}` or higher
- You must NOT write to a `SECRET / {Medical}` notebook, because that would move information from your clearance level down to a lower level
- You CAN write to a `TOP_SECRET / {Medical, Infrastructure}` notebook, because you're adding information to a more restricted space

---

### Information Flow Across Organizations

When subscribing to notebooks in *other* organizations, additional rules apply:

**Cross-org subscription principle:** Information flows only from lower to higher classification.

- Organization A's `CONFIDENTIAL` notebook can subscribe to Organization B's `PUBLIC` notebook ✓
- Organization A's `PUBLIC` notebook cannot subscribe to Organization B's `SECRET` notebook ✗
- Same clearance dominance rules apply (users must be cleared for subscribed content)

See [Chapter 10: Cross-Organization Coordinator](#) for subscription workflows.

---

### Access Tiers Within a Classification

Once a user's clearance dominates an entry's security label, access is further restricted by **access tiers**, which control specific operations:

| Tier | Meaning | Operations Allowed |
|------|---------|-------------------|
| **Existence** | Principal knows the resource exists | Can see resource in lists, but not read content |
| **Read** | Can read the resource | Read entries, browse catalog, search |
| **Read+Write** | Can modify the resource | Create entries, revise entries, update metadata |
| **Admin** | Full control | Manage access tiers, set policies, delete entries |

**Example:** In a notebook labeled `SECRET / {Operations}`:
- A user cleared for `SECRET / {Operations}` might have "Read" tier (can view, but not edit)
- The notebook owner has "Admin" tier (full control)
- A contractor might have "Existence" tier (knows it exists, but can't read)

Access tiers are **separate from** classification levels. You can dominate the classification but still lack write access.

---

### Clearance Calculation in Practice

When you attempt an operation (read, write, admin), Cyber performs these checks:

```
1. Is the user authenticated?
   No → Deny (unauthenticated access)

2. Does user's clearance dominate the notebook's classification?
   No → Deny (user not cleared for this content)

3. Does user's clearance dominate the specific entry's classification?
   (Entries can have more restrictive labels than their notebook)
   No → Deny (user not cleared for this specific entry)

4. Does user's access tier allow this operation?
   - Reading? Need "Read" or higher
   - Writing? Need "Read+Write" or higher
   - Admin? Need "Admin" tier
   No → Deny (insufficient permissions)

5. Pass all checks → Allow
```

If any check fails, the operation is denied and logged.

---

### Practical Examples

#### Scenario 1: Medical Research Organization

**Organization:** Health.Corp

**Users:**
- Dr. Alice: `TOP_SECRET / {Medical Research, Operations}`
- Nurse Bob: `CONFIDENTIAL / {Medical Research}`
- Accountant Carol: `CONFIDENTIAL / {Finance}`

**Notebooks:**
- "Research Phase 3 Trials" — `TOP_SECRET / {Medical Research}`
- "Patient Demographics" — `SECRET / {Medical Research}`
- "Operations Budget" — `CONFIDENTIAL / {Finance}`

**Who can access what?**

| User | Research Phase 3 | Patient Demographics | Operations Budget |
|------|------------------|----------------------|-------------------|
| Dr. Alice | ✓ (dominates) | ✓ (dominates) | ✗ (lacks Finance) |
| Nurse Bob | ✗ (level too low) | ✓ (dominates) | ✗ (lacks Finance) |
| Accountant Carol | ✗ (level too low) | ✗ (level too low) | ✓ (dominates) |

#### Scenario 2: Multi-Project Company

**Company:** TechCorp

**User:** Engineer Eve (clearance: `SECRET / {ProjectAlpha, ProjectBeta, Infrastructure}`)

**Notebooks:**
- "ProjectAlpha Source Code" — `SECRET / {ProjectAlpha}`
- "ProjectAlpha + Beta Integration" — `SECRET / {ProjectAlpha, ProjectBeta}`
- "ProjectGamma Skunkworks" — `TOP_SECRET / {ProjectGamma}`
- "Infrastructure Hardening" — `SECRET / {Infrastructure}`

**Who can access what?**

| Notebook | Can Eve Access? | Why? |
|----------|-----------------|------|
| ProjectAlpha | ✓ | Eve has ProjectAlpha compartment |
| ProjectAlpha + Beta | ✓ | Eve has both compartments |
| ProjectGamma | ✗ | Eve lacks ProjectGamma clearance |
| Infrastructure | ✓ | Eve has Infrastructure compartment |

**Eve tries to write to each:**

| Notebook | Can Eve Write? | Why? |
|----------|---|---|
| ProjectAlpha | ✓ | Eve's clearance dominates (same level, same compartments) |
| ProjectAlpha + Beta | ✗ | Notebook is more restricted (requires Beta, which Eve has, but write rule: you can only write if your clearance is ≥ notebook's, not < ) |
| Infrastructure | ✓ | Eve's clearance dominates |

**Note:** The write rule prevents "downgrading" information. If Eve writes content cleared for `SECRET / {ProjectAlpha, ProjectBeta}` to a notebook labeled `SECRET / {ProjectAlpha}`, she's moving restricted info to a less restricted space.

---

### Clearance Dominance Rules (Reference)

For quick lookup, here's the formal definition:

```
Clearance C dominates Label L if:
  C.level ≥ L.level AND
  L.compartments ⊆ C.compartments

Examples:
- TOP_SECRET / {A, B, C} dominates TOP_SECRET / {A, B} ✓
- TOP_SECRET / {A, B} dominates TOP_SECRET / {A, B, C} ✗
- SECRET / {A, B} dominates SECRET / {A} ✓
- TOP_SECRET / {} dominates SECRET / {A} ✓ (no compartment restrictions)
- PUBLIC / {A} dominates CONFIDENTIAL / {} ✗ (level too low)
```

---

### Common Security Decisions

#### Decision: Should this notebook be classified?

Use this matrix to determine the appropriate classification level:

| Risk of Disclosure | Impact | Level |
|---|---|---|
| None | Public knowledge | `PUBLIC` |
| Low | Minor embarrassment | `CONFIDENTIAL` |
| Medium | Competitive disadvantage | `SECRET` |
| High | Severe impact (financial, legal, safety) | `TOP_SECRET` |

#### Decision: Do we need compartments?

Create compartments if:
- Different audiences need different subsets of information
- Projects or teams are isolated
- Sensitive subjects need extra restriction
- Regulatory requirements mandate it (HIPAA, ITAR, etc.)

Don't create compartments for:
- Purely organizational purposes (use notebook hierarchies instead)
- Temporary groupings (delete them, not archive)
- Redundant categories (avoid nested compartments like `{Medical-Research-Phase-1}`)

#### Decision: What clearance should a user have?

**Principle:** Users should have the **minimum clearance necessary** to do their job.

- Don't grant `TOP_SECRET` if `SECRET` is sufficient
- Don't grant broad compartments; grant only those needed
- Review clearances quarterly; remove unnecessary ones
- Document the business justification for each clearance

---

### Troubleshooting Access Denials

**You see: "Access Denied" or "Not Authorized"**

Use this decision tree:

```
1. Am I logged in?
   No → Log in first

2. Am I trying to access my own notebook?
   No → Skip to step 3
   Yes → Check notebook classification.
          Are you (the owner) still cleared for your own notebook?

3. What is the notebook's classification label?
   Ask the notebook owner or check /notebooks page

4. What is my clearance label?
   Check your user profile (/profile)

5. Does my clearance dominate the notebook's label?
   Use the dominance rule above
   No → Request clearance upgrade from your org admin

6. What operation am I trying (read/write/admin)?
   Check my access tier for this notebook
   No → Request access tier upgrade from notebook owner

7. Still blocked?
   Contact your security officer or notebook owner
```

---

### Best Practices

1. **Classify conservatively** — Classify information at the lowest level that protects it. Over-classification reduces information sharing and creates compliance burdens.

2. **Compartments are for separation, not granularity** — Use a small number of compartments. If you have more than 10 per organization, reconsider your strategy.

3. **Review clearances regularly** — Users' roles change. Audit clearances quarterly and remove unnecessary ones.

4. **Log access violations** — Cyber automatically logs all access denials. Review them monthly to catch policy issues or attacks.

5. **Use access tiers for least privilege** — Don't grant "Admin" to everyone. Use "Read+Write" by default, "Admin" only for owners.

6. **Communicate classification clearly** — Every notebook and entry displays its classification. Users should understand why information is restricted.

7. **Test before deploying** — Create test notebooks with different classifications. Verify that access rules work as expected before moving to production.

---

### Next Steps

Now that you understand the security model, you're ready to:

1. **[Chapter 3: Getting Started](#)** — First login and basic orientation
2. **[Chapter 13: Security Reference](#)** — Deep dive into compartment naming, classification examples, and decision trees
3. **[Jump to your role](#)** — Part II has persona-specific guides for different jobs

---

**Last updated:** February 21, 2026
**Manual version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
**Security model:** Bell-LaPadula (NIST SP 800-95)
