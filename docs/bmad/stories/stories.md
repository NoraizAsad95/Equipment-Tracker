Equipment-Tracker - User Stories

All stories with their acceptance criteria and current sprint status, in one place.
Source of truth: `planning-artifacts/epics.md` (text) and `implementation-artifacts/sprint-status.yaml` (status). Regenerate this file if either changes.

Epic 1: Foundation and visibility

Horizon can start the app and see every item with its status.

Story 1.1: Project scaffold, database and seed data

As a developer,
I want a running solution with the data model and seed data,
So that every later story builds on a working app.

Acceptance Criteria:

Given a clean machine with .NET and LocalDB
When I run `dotnet run`
Then the database is created by migration and seeded with the people and equipment listed in the PRD assumptions
And the app serves a page at `/` and returns ProblemDetails for errors
And a test project exists and `dotnet test` passes against a LocalDB test database.

Given the model
When I inspect it
Then Equipment has tracking mode, total and damaged quantity, condition, note, approval flag and approver; and people have role and hashed PIN (FR-1..3).

Story 1.2: Current status screen

As Saima,
I want one screen showing every item's status,
So that I can answer "where is it?" in seconds.

Acceptance Criteria:

Given seeded data
When I open the current screen on a phone
Then I see every item as available, held, checked out (borrower, checkout date, due date) or damaged
And Quantity items show total, usable and available quantities
And the screen requires no PIN and has no horizontal scroll.

Given many items
When I type a name in the search box
Then the list filters by item name or person name.

Story 1.3: Manage equipment and people

As Mr Tariq,
I want to add and edit equipment, approval settings and people,
So that the system reflects what Horizon owns.

Acceptance Criteria:

Given I am the Owner and enter my PIN
When I add or edit equipment, an approval flag with its approver, or a person
Then the change is saved.

Given I am not the Owner or my PIN is wrong
When I try a management action
Then the API returns 403 and nothing changes.
And approval can be turned on only for Individual items, and a PIN is never returned by the API.

Epic 2: Checkout and return

Staff can borrow and return equipment, and overdue items are visible.

Story 2.1: Checkout

As a field staff member,
I want to pick my name, select items and set a due date,
So that Horizon knows who has what and when it is due back.

Acceptance Criteria:

Given I pick my name and select one or more available items and a due date today or later
When I confirm
Then one checkout is created with one due date, each item has a line, and the items show as checked out to me
And a checkout event is recorded for each line.

Given a Quantity item
When I enter a quantity above what is available
Then the request is rejected with 400.

Given an item is checked out, held, or damaged
When I try to select it
Then it is not selectable and the API rejects it.

Given two people take the same item at the same time
When both confirm
Then one succeeds and the other gets 409.
And the due date cannot be in the past, and I see each item's last condition and note before confirming (FR-11).

Story 2.2: Return with condition

As a field staff member,
I want to return some or all items and state their condition,
So that damage is reported at the moment it is found.

Acceptance Criteria:

Given an open checkout
When I pick my name, select returned items (and quantities), and mark each OK or Damaged with an optional note
Then those lines or quantities are marked returned and a return event is recorded with who recorded it.

Given I return only some items
When I confirm
Then the rest stay checked out to the original borrower and the checkout stays open until all are returned.

Given an Individual item is returned Damaged
When I confirm
Then its condition becomes Damaged and it is unavailable for checkout.

Given 1 of 2 returned tablets is Damaged
When I confirm
Then the damaged quantity increases by 1, usable quantity falls by 1, and the other unit is available again.

Story 2.3: Overdue display

As Mr Tariq,
I want overdue items clearly marked,
So that I can see what is late without asking.

Acceptance Criteria:

Given a line whose due date is before today in Pakistan time and is not fully returned
When I open the current screen
Then it is marked overdue and shows how many days late
And outstanding Quantity items show the quantity and holder.

Given a line returned in full
Then it is never shown as overdue.

Epic 3: Approval

Restricted equipment such as the drone cannot leave without its approver.

Story 3.1: Request and hold

As a field staff member,
I want to request an item that needs approval,
So that the owner of the licence can approve it before I take it.

Acceptance Criteria:

Given an item with approval on
When I include it in a checkout
Then a pending request is created, the item shows as held, and it is not checked out
And other people cannot select it or request it.

Given a checkout mixing an approval item and ordinary items
Then the ordinary items are checked out immediately and the approval item waits.

Story 3.2: Approve or reject

As Hamza,
I want to approve or reject requests with my PIN,
So that only approved checkouts happen.

Acceptance Criteria:

Given a pending request for an item where I am the named approver
When I enter my PIN and approve
Then the line becomes checked out with the original due date, and the decision, approver and time are recorded.

Given I reject (reason optional)
Then the hold is released and the item is available again.

Given I am not the item's approver, or my PIN is wrong
Then the API returns 403 and the request stays pending.

Epic 4: Damage and restore

Known-damaged equipment is held back until it is fixed.

Story 4.1: Restore damaged equipment

As Saima,
I want to restore a repaired item or units,
So that usable equipment returns to circulation.

Acceptance Criteria:

Given a damaged Individual item
When I (Office or Owner) enter my PIN and restore it
Then its condition is OK, it is available, and a restore event is recorded.

Given a Quantity item with damaged units
When I restore a number up to the damaged quantity
Then damaged falls and usable rises by that number.

Given I am Staff or my PIN is wrong
Then the API returns 403.

Epic 5: History

Tariq can look back at what was out and who had it.

Story 5.1: Date-range history

As Mr Tariq,
I want to list checkouts and returns for a date range,
So that I can see what was out last month and who had it.

Acceptance Criteria:

Given a start and end date
When I (Owner or Office) open history
Then I see checkout and return records in that range with borrower, equipment, quantity, checkout date, due date and return date, including items still out.
And records cannot be edited or deleted through any endpoint.

Story 5.2: Corrections

As Mr Tariq,
I want to record a correction against a wrong history entry,
So that history stays honest without being rewritten.

Acceptance Criteria:

Given an existing history entry
When I (Owner, with PIN) add a correction with a note
Then a new entry referencing the original is stored and shown beside it
And the original entry is unchanged.
