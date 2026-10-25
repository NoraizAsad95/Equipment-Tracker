---
title: Equipment-Tracker PRD
status: final
created: 2026-10-21
updated: 2026-10-21
---

# Equipment-Tracker: PRD

**Source:** `_bmad-output/planning-artifacts/briefs/brief-Equipment-Tracker-2026-10-21/brief.md` (decisions D1 to D7 are settled and not reopened here).
**Client:** Horizon Survey Services, Lahore. Internal tool, about 25 people, about 14 in the field.
**Form factor:** mobile-friendly web app backed by a Web API.

## 1. Problem and goal

Nobody knows where Horizon's expensive survey equipment is, who has it, whether it is late, or whether it came back damaged. The goal is one screen that answers "who has what, since when, due when, in what condition" with minimal effort from borrowers.

## 2. Users

| Role | Who | Can do |
|------|-----|--------|
| Staff | Field staff, Hamza | Borrow and return (pick name, no PIN). Hamza also approves drone checkouts (PIN). |
| Office | Saima | Everything Staff can, plus restore damaged equipment (PIN). Read-only history. |
| Owner | Mr Tariq | Manage equipment, approval settings and people (PIN). View history. Restore damaged equipment (PIN). |

An approver is any person named on an item as its approver. It is not a separate role.

## 3. Scope

In scope: eight capabilities in the brief (equipment model, checkout, return, approval, damage, visibility, history, identity). Out of scope: notifications and reminders, WhatsApp integration, kit entities or templates, repair workflows, extra roles, full logins, point-in-time reconstruction.

## 4. Journeys

- **UJ-1 (routine borrow).** Bilal opens the app on his phone, picks his name, selects the total station and a tripod, sets a due date of tomorrow, taps Confirm. Done in a few taps.
- **UJ-2 (drone).** Ali requests the drone. The request is pending and the drone shows as held. Hamza enters his PIN and approves. The drone is now checked out to Ali.
- **UJ-3 (damaged return).** Bilal returns the rover and marks it Damaged with a note. The rover disappears from the available list. Saima later restores it with her PIN and it is available again.
- **UJ-4 (where is it?).** A client calls Saima asking where the GPS rover is. She opens the current screen, sees it is with Imran and overdue by two days.
- **UJ-5 (look-back).** Tariq selects last month's dates and reads every checkout and return with borrower, equipment and dates.

## 5. Functional requirements

### 5.1 Equipment and people

- **FR-1.** Each equipment item has a name, a tracking mode (Individual or Quantity), and a current condition.
- **FR-2.** Individual items are identified one by one (for example a serial or tag). Quantity items have a total quantity and a usable quantity (total minus damaged); they have no per-unit identity.
- **FR-3.** Each person has a name, a role (Staff, Office, Owner), and a PIN where their role or approver duty requires one.
- **FR-4.** An individual item may have an approval setting (on or off) and, when on, one approver (a person). The setting is per item, not per type.
- **FR-5.** Only the Owner can add or edit equipment, approval settings and people (PIN required).

### 5.2 Checkout

- **FR-6.** A borrower picks their name from a list, selects one or more items, and chooses a due date. No PIN is needed.
- **FR-7.** One checkout creates one transaction with one due date that covers all selected items.
- **FR-8.** For Quantity items the borrower enters how many; this cannot exceed the usable quantity.
- **FR-9.** The due date is required and cannot be in the past.
- **FR-10.** An item that is checked out, held by a pending request, or damaged cannot be selected.
- **FR-11.** Before confirming, the borrower sees each item's last reported condition and note.

### 5.3 Approval

- **FR-12.** If any selected item has approval on, checkout of that item creates a pending request instead of an immediate checkout.
- **FR-13.** A pending request holds the item. It is unavailable to others but not yet checked out.
- **FR-14.** The named approver approves or rejects with their PIN. Approval completes the checkout. Rejection releases the hold; a reason is optional.
- **FR-15.** The system records the request, the decision, the approver and the decision time.
- **FR-16.** In a checkout mixing approval and non-approval items, non-approval items are checked out immediately and approval items follow their own request.

### 5.4 Return and condition

- **FR-17.** A person picks their name and returns some or all items from an open transaction. Quantity items are returned by quantity.
- **FR-18.** Each returned item or quantity records a condition, OK or Damaged, with an optional note. For Quantity items the damaged quantity is stated.
- **FR-19.** Items not returned stay checked out to the same borrower; the transaction stays open until every individual item is returned and every quantity is accounted for. The record notes who recorded the return.
- **FR-20.** A returned item marked Damaged becomes unavailable for checkout. Damaged Quantity units leave the usable quantity.
- **FR-21.** The Office or Owner can restore a damaged item or unit to usable with a PIN. The restore is recorded.

### 5.5 Visibility

- **FR-22.** A current screen lists every item with its status: available, held (pending), checked out (borrower, checkout date, due date), or damaged.
- **FR-23.** Overdue is computed from the due date and shown on the current screen. Quantity items show total, usable, and outstanding quantities and who holds the outstanding units.
- **FR-24.** The current screen supports searching by item name or person. It is visible to everyone without a PIN.

### 5.6 History

- **FR-25.** Every checkout, return, approval decision, damage report and restore is stored as a dated, immutable record.
- **FR-26.** The Owner and Office can pick a date range and see checkout and return records with borrower, equipment, quantity, checkout date, due date and return date.
- **FR-27.** A correction is a new record that references the one it corrects. No record is edited or deleted.

## 6. Non-functional requirements

- **NFR-1 (speed of use).** A routine checkout is completable on a phone in a few taps (select name, select items, set due date, confirm).
- **NFR-2 (mobile).** All screens work at phone width without horizontal scroll.
- **NFR-3 (integrity).** Availability rules (FR-10, FR-13, FR-20) are enforced on the server, not only in the UI, and are safe against two people acting on the same item at once.
- **NFR-4 (access).** The only authentication is name selection plus a PIN for approval, restore and management actions (FR-5, FR-14, FR-21). PINs are stored hashed.
- **NFR-5 (stack).** ASP.NET Core Web API on .NET 8, C#, SQL Server LocalDB, simple HTML/JavaScript frontend.
- **NFR-6 (testability).** Core rules are covered by automated tests, and seed data allows a demo from a clean database.

## 7. Success metrics

- Anyone in the office can find the holder of any item from the current screen in a few seconds.
- A routine checkout takes a few taps.
- The drone cannot be checked out without Hamza's approval; damaged items cannot be issued.
- Overdue items are visible without anyone asking.
- Last month's records can be listed by date range.

Counter-metric: added friction. If checkout needs more than the steps in NFR-1, the field staff will stop using it.

## 8. Assumptions

- Approval applies to individual items only.
- A pending request has no timeout.
- History is read-only for the Office.
- Pakistan local time; dates without times are acceptable for due dates.
- Returns can be recorded by whoever picks their name.
- Borrowing identity is trust-based by design.
- Seed data: one drone (approval on, Hamza), two GPS rovers (approval off), a total station, 4 tripods, 6 field tablets, and a laptop; people Tariq, Saima, Hamza and a few staff.
- A mistaken Damaged report is fixed by a restore.
