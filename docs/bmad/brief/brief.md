---
title: Equipment-Tracker Product Brief
status: final
created: 2026-10-21
updated: 2026-10-21
---

# Equipment-Tracker: Product Brief

**Client:** Horizon Survey Services, Lahore (about 25 staff, about 14 in the field).
**Context:** Hiring assessment (Solvefy). The task document (owner voice note and follow-up email) is the source of truth. Nothing here goes beyond it except items marked as assumptions.

## Problem

Horizon owns expensive equipment (total stations, GPS rovers, a drone, tripods, field tablets, laptops) and nobody knows where it is. Staff take what they need on trust, with no record. Consequences so far: a second GPS rover bought for 11 lakh because the first was in someone's car boot for three weeks; equipment kept for weeks when needed for a day; damaged equipment returned without a word, found on site, costing two days of client work. Saima in the office takes the "where is it?" calls.

## Users

- **Field staff:** borrow and return. Mostly WhatsApp users, so the flow must take seconds on a phone.
- **Saima (office):** answers "where is it?"; restores damaged equipment.
- **Hamza:** the only licensed drone operator; approves drone checkouts.
- **Mr Tariq (owner):** wants the overview and the history; manages equipment, approval settings and users.

## Goal

Make "who has what, since when, due when, in what condition" visible on one screen, with minimum effort from the people who borrow equipment.

## Scope (in)

1. **Equipment model.** One Equipment concept with a tracking mode. *Individual* items are identified one by one. *Quantity* items (4 tripods, 6 field tablets) are counted, with total vs. usable quantity and no invented identities.
2. **Checkout.** Pick your name, select one or more items in a single checkout (this is how "the whole survey kit" is handled; there is no kit entity), and choose a required due date. Quantity items record how many.
3. **Return.** Return items fully or partially. Each return records condition (OK or Damaged, optional note). If a kit comes back short, the missing individual item stays checked out to the same person and the transaction stays open until every individual item is back.
4. **Approval.** An approval setting lives on the individual item (the drone now; the new GPS rover can be switched on later without affecting the old one). Each item with approval names an approver (a user). A request stays pending until the approver approves or rejects it. The item is held while pending, is not checked out until approved, and a rejection releases the hold (reason optional). Request, decision, approver and time are recorded.
5. **Damage.** Known-damaged equipment cannot be checked out. Individual items are blocked until restored to OK. Quantity items track total vs. usable (for example 6 total, 5 usable, 1 damaged). Saima or Tariq can restore, with a PIN. The last reported condition and note are shown to borrowers.
6. **Visibility.** A current-state screen shows who has what, due dates, overdue status, and available quantities. Overdue is computed from the due date and shown on screen. There are no reminders or escalation.
7. **History.** A date-range list of checkout and return records (borrower, equipment, checkout date, due date, return date). Records are immutable; corrections are new records, not overwrites.
8. **Identity.** Borrowing uses name selection with no PIN. Approval and damage restoration require a short PIN. Tariq manages equipment, approval settings and users. Saima restores damaged equipment only. Other staff have no management actions.

## Out of scope

Notifications, reminders and WhatsApp integration; kit templates or kit entities; repair workflows; extra roles or permission layers; full logins; point-in-time reconstruction; anything not in the task or the decisions above.

## Success looks like

- Saima or Tariq can find who has any item in a few seconds, without calling anyone.
- A routine checkout takes a few taps on a phone.
- The drone cannot leave without Hamza's approval; damaged equipment cannot be issued to the next person.
- Overdue items are visible without anyone asking.
- Last month's checkouts and returns can be listed by date range.

## Technical constraints (from the task)

ASP.NET Core Web API on .NET 8 or 9 in C#; a relational database (SQLite is the sensible choice); a simple, mobile-friendly frontend (plain HTML and JavaScript is acceptable). A sound data model matters more than visual design.

## Key decisions

| # | Decision | Reason |
|---|----------|--------|
| D1 | One Equipment concept with TrackingMode (Individual or Quantity); history records quantity and person for counted items | Respects "not individually tracked"; keeps one checkout flow |
| D2 | No kit entity; a kit is a multi-item checkout; a missing item stays checked out | The task never defines a kit; keeps missing items visible |
| D3 | Approval is a per-item setting with a user as approver; pending requests hold the item | The "new rover maybe" needs flexibility; avoids double requests |
| D4 | Required due date chosen at checkout; overdue is visible only | Supports "back on time" without inventing defaults or reminders |
| D5 | Damaged equipment is blocked from checkout; counted items track total vs. usable | Addresses the two-day loss directly; consistent for both modes |
| D6 | Name selection for borrowing; PIN for approval and restoration | Keeps the field flow fast; protects consequential actions |
| D7 | Date-range history list; immutable records, corrections as new records | Meets "look back at last month" with the least machinery |

## Assumptions (not stated in the task; correct me if wrong)

- Approval applies to individual items only; quantity items never need approval.
- Approver, restorer and manager roles come from a simple role on each person, plus a per-item approver. No other role system.
- The current-state screen is visible to everyone and needs no PIN; the history screen is for Tariq and Saima, read-only for Saima.
- A due date cannot be in the past. A checkout has one due date for all its items.
- Returns can be recorded by whoever picks their name; the record notes who recorded it.
- A pending approval request has no timeout.
- A mistaken Damaged report is fixed by a restore, which is recorded in history.
- Borrowing identity is trust-based: someone could check out under another person's name. This is accepted, in line with "it's all on trust".
- Dates and times use Pakistan local time. Sample data (the drone, two rovers, 4 tripods, 6 tablets) is seeded for demonstration.
- A correction is a new history record that references the one it corrects.
