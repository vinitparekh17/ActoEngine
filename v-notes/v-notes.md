# ActoEngine

## What is ActoEngine?

ActoEngine is a database context and documentation platform. It answers the question: **"What if your database could explain itself?"**

Databases store data, but they don't store meaning. You can see that a table called `CustomerOrders` exists with 15 columns, but you can't see why it exists, who owns it, what happens if you change it, or which applications depend on it.

ActoEngine bridges this gap by creating a semantic layer on top of your database - capturing the business context, relationships, and knowledge that typically lives only in people's heads.

---

## The Problem

### Documentation Gets Stale
Traditional documentation lives in separate documents - wikis, Word files, Confluence pages. Over time, these drift from reality. The database changes, but the documentation doesn't. Nobody trusts it anymore.

### Knowledge Lives in People's Heads
"Ask John, he built that table five years ago." But what happens when John leaves? Or when you need to understand something at 2 AM during an outage and John is asleep?

### Impact Analysis is Hard
"What breaks if I change this column?" This simple question can take hours or days to answer. You have to trace through code, ask around, and hope you found everything.

### Repetitive Code is Error-Prone
Writing CRUD stored procedures is tedious. Every developer does it slightly differently. Mistakes happen. Standards drift.

### Onboarding Takes Forever
New team members stare at database schemas trying to understand what things mean and why they exist. They interrupt senior developers constantly.

---

## The Vision

ActoEngine exists to make databases self-documenting and self-explaining.

**Capture context alongside structure.** When you sync a database table, you also document its purpose, its owner, its criticality to the business, and its relationships to other entities.

**Track dependencies.** Know which clients, applications, and stored procedures depend on each table and column. Answer "what breaks if I change this?" in seconds, not hours.

**Automate the boring stuff.** Generate standardized stored procedures and forms from your schema. Stop writing the same CRUD code over and over.

**Keep documentation alive.** Documentation lives with the database objects it describes. When things change, you know what needs updating.

---

## Who It's For

### Database Administrators
Manage schemas across multiple databases. Generate standardized stored procedures. Understand dependencies before making changes.

### Backend Developers
Generate CRUD operations quickly. Build forms connected to database tables. Understand data structures before coding.

### Business Analysts
Document the business meaning of data. Track data ownership and subject matter experts. Understand what data exists and why.

### Technical Leads
Ensure documentation coverage. Track criticality and data sensitivity. Manage impact of changes.

### New Team Members
Understand unfamiliar databases quickly. Find who to ask about specific entities. Learn the business context, not just the technical structure.

---

## Key Capabilities

### Schema Synchronization
Connect to your SQL Server or PostgreSQL database and sync the schema into ActoEngine. Tables, columns, stored procedures, foreign keys - everything comes in automatically.

### Context Documentation
For each table, column, or stored procedure, capture:
- **Purpose**: Why does this exist? What business need does it serve?
- **Owner**: Who is responsible for this data?
- **Criticality**: How important is this? (1-5 scale)
- **Business Domain**: Sales? Finance? Operations?
- **Data Sensitivity**: Is this PII? Financial data?
- **Experts**: Who knows the most about this?

### Stored Procedure Generation
Select a table, choose your options, and generate:
- CUD stored procedures (Create, Update, Delete)
- SELECT stored procedures with filters and pagination
- Consistent naming conventions and error handling

### Form Builder
Build data entry forms visually:
- Generate HTML and JavaScript
- Include validation
- Connect to your stored procedures
- Bootstrap 5 styling out of the box

### Client and Project Management
Track which clients use which database objects. Understand multi-tenant dependencies. Manage multiple projects and databases.

### Completeness Tracking
See at a glance what's documented and what's not. Track stale documentation that needs review. Dashboard shows coverage gaps.

---

## Why This Matters

### Faster Onboarding
New developers understand databases in days, not months. They know who to ask and what things mean.

### Safer Changes
Impact analysis happens in seconds. You know what depends on what before you make changes.

### Better Documentation
Documentation stays current because it lives with the objects it describes. Staleness is tracked and flagged.

### Consistent Code
Generated stored procedures follow standards. No more "every developer does it differently."

### Preserved Knowledge
When people leave, their knowledge stays. Context and expertise are captured in the system.

### Reduced Interruptions
Developers can find answers themselves instead of interrupting colleagues.

---

## The Core Insight

Every organization has databases. Most organizations have poor database documentation. The documentation they do have is outdated, scattered, and incomplete.

This isn't because people are lazy. It's because traditional documentation tools are disconnected from the things they document. They require manual effort to keep in sync. They don't track what's documented and what isn't. They don't know when things get stale.

ActoEngine solves this by making documentation a first-class part of database management - not an afterthought, but an integral part of the workflow.

**Your database already knows what exists. ActoEngine helps you capture why it exists.**

---

## Summary

ActoEngine transforms databases from technical structures into documented, understandable, and maintainable systems. It captures the business context that makes databases meaningful, tracks the dependencies that make changes safe, and automates the repetitive work that slows teams down.

The goal is simple: anyone should be able to look at any database object and understand not just what it is, but why it exists, who owns it, and what depends on it.

That's the ActoEngine vision.
