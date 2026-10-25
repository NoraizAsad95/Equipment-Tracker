**Equipment Tracker Decision Log**

This log explains the main choices I made while planning and building the Equipment Tracker. It also lists the assumptions and implementation details that shape this version.

**D1 - Use one equipment model with two tracking modes**

**The gap:** The task describes items tracked one by one and items tracked by quantity. It does not say whether they should use separate models.

**What I decided:** I use one Equipment model with an Individual or Quantity tracking mode. Individual equipment represents one specific item. Quantity equipment stores total and usable counts without inventing an identity for each unit.

**Alternative:** Use separate models for individual assets and counted stock.

**Why:** One model avoids duplicating checkout and status rules while still representing both kinds of equipment.

**What could change this:** If the two types need very different rules or lifecycles, separate models may be clearer.

**D2 - Treat a kit as a checkout with several items**

**The gap:** The task says people sometimes take a whole survey kit, but does not define kits as reusable inventory records.

**What I decided:** A kit is one checkout containing several equipment lines. There is no separate Kit record or template. Items can be returned separately, so anything still missing remains checked out.

**Alternative:** Create named kits with their own contents and status.

**Why:** The checkout already records what the borrower took. A separate kit model could hide which item is still missing.

**What could change this:** If Horizon needs reusable kit definitions or kit-level management, a Kit model may be useful.

**D3 - Require approval only for selected items**

**The gap:** The task requires approval for the drone and may require it for the new rover later. It does not define a general approval workflow.

**What I decided**: Each individually tracked item can name an approver. A request holds the item until that approver approves or rejects it.

**Alternative:** Apply one approval rule to every item of a type, or let all checkouts happen immediately.

**Why:** An item-level setting handles the drone now and allows approval for the new rover later without changing the older rover. The hold prevents another person from requesting the same item at the same time.

**What could change this:** If approvals need multiple stages, delegates, or time limits, the approval model would need to grow.

**D4 - Ask for a due date and show overdue items**

**The gap:** The task asks people to return items on time and says reminders would be useful, but it does not set loan periods or specify a notification service.

**What I decided:** The borrower chooses a due date at checkout. The app marks overdue items on screen but does not send reminders or WhatsApp messages.

**Alternative**: Set a default loan period and send reminders by email or WhatsApp.

**Why:** A required due date supports the main need and is straightforward to use. Notifications would add infrastructure that was not necessary for the first version.

**What could change this:** If reminders or automatic loan periods become a firm requirement, I would add them with the client’s preferred channel and rules.

**D5 - Keep damaged equipment out of circulation**

**The gap:** The task says damaged equipment has caused lost work. Counted items also need to remain partly available when only some units are damaged.

**What I decided:** A damaged individual item cannot be checked out until restored. For quantity items, damaged units are subtracted from usable stock. Damage is recorded on return; there is no repair workflow.

**Alternative:** Leave damaged items available with a warning, or build a repair workflow.

**Why:** Blocking damaged equipment helps prevent it being issued to the next person. Separate total and damaged counts show how many counted units are usable.

**What could change this:** If Horizon needs repair tickets, repair states, or per-unit repair tracking, that workflow should be added.

**D6 - Keep borrowing quick and protect important actions with a PIN**

**The gap:** The task says borrowing should be fast and trust-based, but approval, restoration, and management actions need accountability. It does not require full user accounts.

**What I decided:** For borrowing, a person selects their name. A PIN is required for approvals, restoration, and management changes. The Owner manages equipment and people; the Office and Owner can restore damaged equipment. An assigned approver can approve their item.

**Alternative**: Require a full login for every user, or leave management and approval actions unprotected.

**Why:** Name selection keeps checkout simple. PIN checks protect actions that change equipment status or settings without adding a full login system.

**What could change this:** If the app is used remotely or needs stronger proof of identity, it should use proper user accounts and authentication.

**D7 - Keep history and add corrections as new records**

**The gap:** The task asks Tariq to look back at last month but does not say whether old records can be edited or whether the app must recreate past states.

**What I decided:** The app keeps dated checkout, return, approval, damage, and restoration events. A correction is added as a new entry linked to the original; the original is not silently overwritten.

**Alternative**: Allow edits to old records, or build a system that reconstructs the full inventory state at any past time.

**Why**: Keeping the original entry makes changes visible and supports the requested look-back without a more complex history system.

**What could change this:** If the business needs formal audit requirements or exact past-state reconstruction, the history model should be expanded.

**D8 - Keep the first version focused**

**The gap**: The task leaves many details open, so it would be easy to add roles, workflows, and infrastructure the client has not requested.

**What I decided:** I focus on tracking, checkout, return, approval, restoration, and history. I avoid extra roles, complex workflows, and services that are not needed for this version.

**Alternative**: Build a larger, more flexible platform with extra workflows and abstractions from the start.

**Why:** A smaller application is easier to run, test, and explain in the interview, and fits the assessment scope.

**What could change this:** If the client adds important workflows or operational requirements, I would extend the design to meet them.

D9 - Use the requested .NET stack and a simple web interface

**The gap:** The task allows .NET 8 or 9, any relational database, and a frontend of the developer’s choice.

**What I decided:** I use .NET 8, ASP.NET Core Web API, SQL Server LocalDB, and a plain HTML, CSS, and JavaScript frontend.

**Alternative:** Use .NET 9, SQLite, or a frontend framework.

**Why:** This stack meets the task requirements and keeps the app and its setup manageable for a small assessment project.

**What could change this:** If the target machine or hosting environment cannot use LocalDB, I would choose a compatible relational database and document the change.

**D10 - Use EF Core for database access**

**The gap:** The task requires a relational database but does not prescribe how the application should access it.

**What I decided:** I use EF Core to map application data to SQL Server and manage schema changes with migrations.

**Alternative:** Write SQL directly or add a separate repository layer around EF Core.

**Why:** EF Core keeps database access concise and makes the data model and migrations easier to follow in this project.

What could change this: If a specific query needs tuning, I could use targeted SQL without replacing EF Core throughout the app.

**D11 - Enforce availability rules in the API**

**The gap:** The task requires the system to prevent unavailable equipment from being checked out, even if two people act at nearly the same time.

**What I decided:** The API checks availability and permissions when a request is submitted. SQL Server row-version concurrency checks detect conflicting changes to the same equipment.

**Alternative:** Rely on the browser to decide what is available, or use a more complex locking strategy.

**Why**: Browser data can become outdated. Server checks keep the rule authoritative, and concurrency checks help prevent double-booking.

**What could change this:** If observed usage or database constraints make this approach insufficient, I would revisit the concurrency strategy.

**D12 - Save a multi-item checkout as one operation**

**The gap:** The task allows a borrower to take several items together but does not say what happens if one becomes unavailable during checkout.

**What I decided:** A checkout succeeds only if every selected item is available. If one item fails validation or a concurrency check, none of that checkout is saved.

**Alternative:** Accept the available items and reject only the ones that could not be checked out.

**Why:** All-or-nothing behavior gives the borrower a clear result and avoids a confusing partial checkout.

What could change this: If Horizon wants partial checkouts, the screen should clearly show which items succeeded and which did not.

**Assumptions**

* Approval applies to individually tracked items. Counted items do not need approval in this version.
* One checkout has one due date for all its items, and the due date cannot be in the past.
* A pending approval request has no timeout.
* Borrowing uses name selection. A person could select another person’s name, which fits the trust-based approach in the task.
* Dates and times use Pakistan local time for display and overdue checks.
* The app uses sample people and equipment so it can be demonstrated with a clean database.

**Implementation notes**

These notes describe implementation choices rather than separate product decisions.

* EF Core migrations are stored under Data/Migrations.
* A design-time DbContext factory supports EF tooling without starting the application.
* The API includes a health-check endpoint at /api/health.
* The app uses SQL Server LocalDB. Demo PINs are hashed before storage.
* Returns are currently recorded against the borrower’s checkout; the app does not ask separately who recorded the return.

