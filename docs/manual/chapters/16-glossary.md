# Chapter 16: Glossary & Index

## Glossary of Terms

### Core Concepts

**Causal Position**
A monotonically increasing sequence number that establishes the order of events in a notebook without relying on timestamps or synchronized clocks.

**Cyber**
A multi-organization classified knowledge exchange platform with enterprise-grade security, entropy-based knowledge integration, and federated identity.

**Entry**
An immutable unit of knowledge—a single piece of information in a notebook, characterized by content, authorship, classification, and references.

**Integration Cost**
A numerical measure (0-10) of how well an entry aligns with existing knowledge in a notebook, based on TF-IDF similarity and coherence analysis.

**Notebook**
A domain-specific, security-labeled knowledge space that contains entries and is managed by an owning group.

**Revision**
A new version of an existing entry, created when information needs to be updated. The original entry remains immutable; the revision supersedes it.

### Security

**Access Tier**
A permission level controlling what operations a principal (user/group) can perform: Existence, Read, Read+Write, Admin.

**Bell-LaPadula Model**
A formal security framework that enforces: (1) Information can only flow upward in classification, and (2) Users can only read information they're cleared for.

**Classification Level**
A five-level hierarchy (PUBLIC, CONFIDENTIAL, SECRET, TOP_SECRET, Custom) indicating information sensitivity and distribution restrictions.

**Clearance**
A security credential specifying what classified information a principal is authorized to access, consisting of a level + compartments.

**Compartment**
An optional security category (e.g., "Medical Research", "Strategic Planning") that further restricts access within a classification level.

**Dominance**
In Bell-LaPadula terms, one clearance dominates another if it has a higher or equal level AND includes all required compartments.

**Information Flow**
The movement of data through the system. Bell-LaPadula enforces that information flows only from lower to higher classification.

**Security Label**
A combination of classification level and compartments (e.g., `SECRET / {Operations, Database}`).

### Organizational

**Cross-Organization Coordinator**
A persona who manages knowledge sharing between organizations and ensures compliance with security boundaries.

**DAG (Directed Acyclic Graph)**
A structure describing organizational groups where a group can have multiple parents but no cycles (e.g., Engineering / Backend).

**Federated Identity**
A decentralized identity system using cryptographic keys (Ed25519) where users are identified by their public key hash, not usernames.

**Group**
An organizational unit that contains users and owns notebooks. Groups form a DAG hierarchy with inherited classification.

**Knowledge Contributor**
A persona focused on creating, discovering, and refining entries in notebooks.

**Notebook Owner**
A persona who creates and manages notebooks, controls access, reviews submissions, and monitors processing.

**Organization**
A top-level container representing a company or entity with its own security boundaries and group hierarchies.

**System Administrator**
A persona managing platform-wide settings: user accounts, quotas, agents, and system health.

### Technical

**Audit Log**
An immutable record of all operations, including actor, action, resource, timestamp, and cryptographic signature.

**Batch Entry Creation**
UI feature for importing multiple entries at once via CSV or text format.

**Claim**
An extracted piece of knowledge from an entry, identified by NLP processing (e.g., "Database indexing improves performance 50x").

**Comparison**
Analysis of semantic similarity between two claims or entries, used to identify disagreements or redundancy.

**Embedding**
A vector representation of text, created by AI models, used for semantic search and similarity analysis.

**Job**
A background processing task (DISTILL_CLAIMS, COMPARE_CLAIMS, EMBED_ENTRIES) run by ThinkerAgents.

**MCP (Model Context Protocol)**
A protocol enabling AI systems like Claude to interact with Cyber programmatically.

**Ollama**
An embedding service that runs AI models locally for creating text embeddings.

**ThinkerAgent**
An AI processing worker that analyzes notebook entries and extracts claims, embeddings, and comparisons.

**Watermark**
A tracking mechanism showing the last successfully synced position in a subscription.

### Operational

**Auditor/Compliance Officer**
A persona responsible for ensuring Cyber usage complies with security policies and investigating incidents.

**Chaos Engineering**
Intentional disruption testing to ensure system resilience.

**Coherence**
A measure of how consistently related entries align in meaning and approach.

**Friction**
Another term for integration cost; high friction indicates controversial or novel entries.

**Least Privilege**
A security principle: grant only the minimum permissions necessary for a user to do their job.

**Probation**
An entry status indicating it's new and still undergoing integration cost analysis.

**Contested**
An entry status indicating it has high integration cost and contradicts existing knowledge.

**Integrated**
An entry status indicating it's stable and well-aligned with existing knowledge.

**ThinkerAgent Operator**
A persona who deploys, configures, and monitors AI processing workers.

---

## Index of Workflows

| Workflow ID | Title | Persona | Chapter |
|-------------|-------|---------|---------|
| WF-KC-001 | Setting up MCP Access for Claude Desktop | Knowledge Contributor | 4 |
| WF-KC-002 | Creating and Organizing Entries | Knowledge Contributor | 4 |
| WF-KC-003 | Browsing and Discovering Knowledge | Knowledge Contributor | 4 |
| WF-KC-004 | Searching Across Notebooks | Knowledge Contributor | 4 |
| WF-KC-005 | Managing Revisions | Knowledge Contributor | 4 |
| WF-KC-006 | Observing Changes | Knowledge Contributor | 4 |
| WF-OA-001 | Creating Organizational Structure | Org Administrator | 5 |
| WF-OA-002 | Managing Group Memberships | Org Administrator | 5 |
| WF-OA-003 | Managing Security Clearances | Org Administrator | 5 |
| WF-OA-004 | Configuring ThinkerAgents | Org Administrator | 5 |
| WF-NO-001 | Creating and Configuring Notebooks | Notebook Owner | 6 |
| WF-NO-002 | Managing Access Control | Notebook Owner | 6 |
| WF-NO-003 | Reviewing Submissions | Notebook Owner | 6 |
| WF-NO-004 | Monitoring Job Pipeline | Notebook Owner | 6 |
| WF-NO-005 | Managing Subscriptions | Notebook Owner | 6 |
| WF-AU-001 | Querying Global Audit Logs | Auditor | 7 |
| WF-AU-002 | Investigating Security Events | Auditor | 7 |
| WF-AU-003 | Notebook-Scoped Auditing | Auditor | 7 |
| WF-SA-001 | User Management | System Admin | 8 |
| WF-SA-002 | Quota Management | System Admin | 8 |
| WF-SA-003 | System Monitoring | System Admin | 8 |
| WF-SA-004 | Agent Management | System Admin | 8 |
| WF-TO-001 | Deploying ThinkerAgents | ThinkerAgent Operator | 9 |
| WF-TO-002 | Configuring Ollama | ThinkerAgent Operator | 9 |
| WF-TO-003 | Monitoring Worker Health | ThinkerAgent Operator | 9 |
| WF-CO-001 | Setting Up Subscriptions | Cross-Org Coordinator | 10 |
| WF-CO-002 | Monitoring Cross-Organization Flows | Cross-Org Coordinator | 10 |
| WF-CO-003 | Ensuring Classification Compliance | Cross-Org Coordinator | 10 |

---

## Chapter Overview

| Chapter | Title | Type | Focus |
|---------|-------|------|-------|
| 1 | Platform Overview | Introduction | What Cyber is, why it exists, core concepts |
| 2 | Security Model | Introduction | Bell-LaPadula, classification, clearances |
| 3 | Getting Started | Introduction | First login, account setup, interface |
| 4 | Knowledge Contributor | Persona | Creating, discovering, managing entries |
| 5 | Organization Administrator | Persona | Structure, clearances, groups, agents |
| 6 | Notebook Owner | Persona | Creating, managing, reviewing notebooks |
| 7 | Auditor/Compliance Officer | Persona | Audit logs, investigations, compliance |
| 8 | System Administrator | Persona | Users, quotas, health, agents |
| 9 | ThinkerAgent Operator | Persona | Deployment, Ollama, monitoring |
| 10 | Cross-Organization Coordinator | Persona | Subscriptions, flows, compliance |
| 11 | MCP Integration Reference | Reference | API operations, authentication, errors |
| 12 | UI Reference | Reference | Navigation, shortcuts, components |
| 13 | Security Reference | Reference | Decision trees, examples, compliance |
| 14 | Data Model | Reference | Notebooks, entries, jobs, subscriptions |
| 15 | Troubleshooting | Reference | Common errors, solutions, support |
| 16 | Glossary & Index | Reference | Terms, acronyms, workflow index |

---

## Acronyms

| Acronym | Meaning |
|---------|---------|
| ACL | Access Control List |
| API | Application Programming Interface |
| CSV | Comma-Separated Values |
| DAG | Directed Acyclic Graph |
| JWT | JSON Web Token |
| MCP | Model Context Protocol |
| NLP | Natural Language Processing |
| OOM | Out of Memory |
| RBAC | Role-Based Access Control |
| SSH | Secure Shell |
| SSL/TLS | Secure Sockets Layer / Transport Layer Security |
| TF-IDF | Term Frequency - Inverse Document Frequency |
| UI | User Interface |
| VM | Virtual Machine |
| VPN | Virtual Private Network |

---

## Related Reading

For more information on security models and knowledge systems:

- **Bell and LaPadula (1973):** Original Bell-LaPadula model paper
- **NIST SP 800-95:** Guide to Secure Web Services
- **OWASP Top 10:** Common security vulnerabilities
- **Okapi BM25:** Probabilistic relevance ranking
- **Word2Vec / Embeddings:** Text representation in ML

---

## Quick Reference: Who Does What

| Task | Persona | Chapter |
|------|---------|---------|
| Create entry | Knowledge Contributor | 4 |
| Search for entry | Knowledge Contributor | 4 |
| Update entry | Knowledge Contributor | 4 |
| Create notebook | Notebook Owner | 6 |
| Grant notebook access | Notebook Owner | 6 |
| Review submissions | Notebook Owner | 6 |
| Create org structure | Org Administrator | 5 |
| Manage clearances | Org Administrator | 5 |
| Add users to groups | Org Administrator | 5 |
| Register agents | Org Administrator / System Admin | 5, 8 |
| Audit access | Auditor | 7 |
| Create users | System Administrator | 8 |
| Set quotas | System Administrator | 8 |
| Monitor system | System Administrator | 8 |
| Deploy agents | ThinkerAgent Operator | 9 |
| Monitor jobs | Notebook Owner / Operator | 6, 9 |
| Set up subscriptions | Notebook Owner / Cross-Org Coordinator | 6, 10 |
| Monitor subscriptions | Cross-Org Coordinator | 10 |

---

**Last updated:** February 21, 2026
**Manual version:** 1.0.0 (Beta)
**Platform version:** 2.1.0

**Total words:** ~30,000
**Total chapters:** 16
**Total workflows:** 28
**Total pages (estimated PDF):** 250+
