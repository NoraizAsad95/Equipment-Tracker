Equipment Tracker
Solvefy test task - AI Engineer I (Associate)

This is my submission for the Horizon Survey Services equipment tracking task. It is a small web app that answers the owner's main question: who has what, and when is it due back?



1. WHAT THIS IS

Mr Tariq at Horizon Survey Services never knows where the company's expensive equipment is. The field team just picks things up and goes to site, and nobody can say who has the drone, the GPS rover or the total station. This app puts all of that on one screen.

Staff pick their name, choose what they are taking and set a due date. Everyone can see the current status at a glance, including what is overdue, what is waiting for approval and what is damaged. The owner can also look back at any period and see what was out and who had it.



2. HOW TO RUN IT

What you need:

* The .NET 8 SDK
* SQL Server LocalDB (it comes with Visual Studio, or with the SQL Server Express LocalDB installer)

Steps:

1. Open a terminal in the folder src\\EquipmentTracker.Api
2. Run:  dotnet run
3. Open http://localhost:5007 in a browser

The first run creates the database by itself (through EF Core migrations) and fills it with sample people and equipment, so there is nothing to set up by hand.

The app has three pages:

* Home page (/)        the current status of every item, search, check out, return, approve, restore
* Manage (/manage.html)    add and edit equipment and people (Owner only)
* History (/history.html)  look back over a date range and add corrections (Owner or Office)

To run the automated tests, open a terminal in the main folder (the one with EquipmentTracker.sln) and run:  dotnet test
There are 141 tests and they all pass. Each test class creates its own LocalDB database, so the run takes around 15 to 20 seconds.



3. SAMPLE DATA AND DEMO PINS

So the app can be demonstrated straight away, it starts with these people:

* Tariq    Owner    PIN 1111
* Saima    Office   PIN 2222
* Hamza    Staff    PIN 1234
* Test User staff   PIN 1122 (the approver for the drone)
* Bilal, Ali, Imran   Staff, no PIN needed

Equipment: Drone (needs Hamza's approval), GPS Rover 1, GPS Rover 2 (new), Total Station, Laptop, plus two counted items, Tripod (4) and Field Tablet (6).

Borrowing only needs a name. PINs are only asked for approving, restoring damaged equipment, managing equipment and people, and the history screen. PINs are stored hashed. These are demo values only.



4. WHAT IS DONE

Everything in the plan I made with BMAD (5 epics, 11 stories) is built and working:

* One screen showing every item as available, held, checked out, or damaged, with who has it and when it is due. Counted items (tripods, tablets) show total, usable and available numbers. There is a search box that matches item names or people.
* Check out one item or several at once (this is how a whole survey kit is handled). Each item can come back separately, so anything missing stays checked out.
* Return with a condition. A damaged single item is blocked from checkout until it is restored. For counted items, only the damaged units are taken out of the usable count.
* Overdue items are clearly marked.
* Items that need approval (the drone) are held until the named approver approves or rejects with their PIN.
* Restore damaged equipment (Office or Owner, with PIN).
* Date-range history, including items that are still out. History is never edited or deleted. A mistake is fixed by adding a correction that sits next to the original entry.
* Owner screen to add and edit equipment and people, with the approval setting and approver per item.
* Two people trying to take the same item at the same moment: one succeeds and the other gets a clear conflict message.

All times use Pakistan local time.



5. WHAT IS NOT DONE, AND WHY

I left these out on purpose. The brief says reminders would be "good", but they are not what the owner asked for first, and each one needs extra infrastructure.

* Reminders and WhatsApp messages. Overdue items are shown on screen, but nothing is sent. This needs a messaging service and the company's preferred channel, which the task does not say.
* Kit records or templates. A kit is simply a checkout with several items, which keeps it clear what is still missing.
* A repair workflow. Damage is recorded when equipment is returned and cleared when it is restored. There are no repair tickets.
* Full user accounts and logins. Name picking plus a PIN for important actions fits the "trust-based, not like the army" request.
* Extra roles or a more complicated approval process (several approvers, time limits).
* Rebuilding the exact state of the inventory at any past moment. The history shows what happened and when.

Known limitations of this version:

* PINs are 4 to 6 digits and there is no lockout after wrong attempts.
* The sample people and PINs are created on every fresh database, including outside development.
* The app does not set up HTTPS itself, so it should sit behind TLS if used outside a trusted network.
* Returning an item does not ask who is returning it. It is recorded against the borrower.
* The history search reads all records and filters them in memory, which is fine at this size but would need work for years of data.



6. DECISIONS

The requirements were deliberately incomplete, so I wrote down every point where I had to choose. DECISIONS.md has 12 decisions, each with the gap, what I decided, the alternative, why, and what would change my mind. It also lists my assumptions and a few implementation notes. Please read that file first.



7. HOW I USED BMAD AND AI

I used the BMAD Method to plan before building. In order: a product brief, a PRD, an architecture document, then epics and stories, then sprint planning. I then built the stories one at a time, with automated tests for each.

AI tools used:

* Claude Code (Sonnet 5.5), for almost everything: the BMAD planning, writing or debugging the code and tests, reviewing the code, redesigning and fixing parts of the UI, and organising this submission.
* The BMAD Method skills, installed in the project (folders \_bmad and .claude/skills).

I directed the work, made the product decisions and reviewed what came back. The conversation logs include the places where things went wrong and had to be fixed.

Stack: .NET 8, ASP.NET Core Web API, EF Core, SQL Server LocalDB, and a plain HTML, CSS and JavaScript front end (no framework). Tests use xUnit.



8. TIME SPENT

Roughly 18 hours over six days:

* Monday      3 hours
* Tuesday     2 hours
* Wednesday   4 hours
* Thursday    1 hour
* Friday      5 hours
* Saturday    3 hours
* Total       18 hours



9. WHERE EVERYTHING IS
* src/             the application (src/EquipmentTracker.Api)
* tests/           the automated tests (tests/EquipmentTracker.Tests)
* docs/bmad/       brief, prd, epics, stories (with sprint status) and architecture
* conversations/   every AI conversation export
* DECISIONS.md     my assumptions and decisions
* README.md        this file
* EquipmentTracker.sln   the solution file

