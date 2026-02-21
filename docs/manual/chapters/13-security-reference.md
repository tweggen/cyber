# Chapter 13: Security Reference

## Classification Decision Tree

```
Is the information public knowledge?
    ↓ Yes
    PUBLIC

    ↓ No

Is disclosure embarrassing but not damaging?
    ↓ Yes
    CONFIDENTIAL

    ↓ No

Would disclosure cause significant competitive/operational harm?
    ↓ Yes
    SECRET

    ↓ No

Would disclosure cause severe national/organizational impact?
    ↓ Yes
    TOP_SECRET
```

## Clearance Dominance Examples

### Valid Clearances
```
✓ TOP_SECRET / {Medical, Ops}        dominates SECRET / {Ops}
✓ SECRET / {A, B, C}                 dominates SECRET / {A}
✓ TOP_SECRET / {}                    dominates SECRET / {Anything}
```

### Invalid Clearances
```
✗ SECRET / {Ops}                     does NOT dominate SECRET / {Ops, Sec}
✗ CONFIDENTIAL / {A, B}              does NOT dominate SECRET / {A}
✗ TOP_SECRET / {Ops}                 does NOT dominate TOP_SECRET / {Ops, Sec}
```

## Information Flow Examples

### Valid Flows (Information Flows Up)
```
PUBLIC notebook → CONFIDENTIAL user  ✓ OK
CONFIDENTIAL notebook → SECRET user  ✓ OK
SECRET notebook → TOP_SECRET user    ✓ OK
PUBLIC notebook → PUBLIC user        ✓ OK (same level)
```

### Invalid Flows (Information Flows Down)
```
CONFIDENTIAL notebook → PUBLIC user  ✗ DENIED
SECRET notebook → CONFIDENTIAL user  ✗ DENIED
TOP_SECRET notebook → SECRET user    ✗ DENIED
```

## Compartment Best Practices

### Naming Convention
```
✓ Functional:
  - Medical Research
  - Infrastructure Operations
  - Customer Data
  - Executive

✓ Geographic:
  - North America
  - EMEA (Europe, Middle East, Africa)
  - Asia Pacific

✓ Project-Based:
  - Project Alpha
  - Project Bravo

✗ Vague:
  - Sensitive
  - Internal
  - Secret1, Secret2
  - TBD
```

### Compartment Scope
```
Small organizations:    3-5 compartments
Medium organizations:   5-10 compartments
Large organizations:    10-20 compartments

⚠️  More than 20 compartments = management overhead
```

## Access Control Matrix

### User Types vs. Permissions

```
                Contributor   Manager   Owner   Admin
─────────────────────────────────────────────────────
Read entries       ✓           ✓        ✓       ✓
Write entries      ✓           ✓        ✓       ✓
Revise entries     ✓           ✓        ✓       ✓
Grant access       ✗           ✓        ✓       ✓
Manage groups      ✗           ✓        ✓       ✓
Delete entries     ✗           ✗        ✓       ✓
Manage org         ✗           ✗        ✗       ✓
```

## Compliance Checklists

### Monthly Audit
- [ ] Review access logs for anomalies
- [ ] Verify clearances match roles
- [ ] Check for orphaned access (people who left)
- [ ] Verify classification labels are correct
- [ ] Audit cross-org subscriptions

### Quarterly Review
- [ ] Full access control audit
- [ ] Compartment usage review
- [ ] Policy compliance check
- [ ] Generate compliance report
- [ ] Update security documentation

### Annual Review
- [ ] Comprehensive security audit
- [ ] Policy effectiveness assessment
- [ ] Compartment consolidation
- [ ] Clearance recertification
- [ ] Threat assessment update

## Security Incident Response

### Potential Breach
1. Isolate affected systems immediately
2. Lock affected user account
3. Review audit logs for extent
4. Notify security team and auditors
5. Document incident with timestamps
6. Contact affected parties if appropriate
7. Revoke compromised credentials
8. Implement preventive measures

### Access Control Misconfiguration
1. Identify incorrect access tier
2. Determine root cause
3. Correct the misconfiguration
4. Review for similar issues
5. Log incident
6. Document preventive measure

### Classification Error
1. Identify entries with incorrect classification
2. Correct classification
3. Revoke access from unauthorized users
4. Review for similar errors
5. Update classification procedures
6. Train affected users

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform Version:** 2.1.0
