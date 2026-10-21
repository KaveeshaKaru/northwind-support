# Approach

How I worked through the assignment and why I made the calls I did. What was built, and how to run
it, is in the [README](README.md).

## How I worked

I come from React and TypeScript, and this was my first real .NET project. So before writing
anything:

- I read the spec end to end;
- I ran the app and clicked through it as a support agent;
- I read the code in the order the spec suggests (the `Ticket` aggregate, the existing commands
  and queries, `TicketQueries`, the EF configurations, then the web feature folder).

That told me the codebase already had clear layers (Domain, Application, Infrastructure,
Presentation) and architecture tests that enforce them, so my job was to fit into that, not to
invent a structure.

For each task I wrote a short plan before coding:

- which files would change;
- where each rule would live;
- which tests would prove it.

I kept those plans outside the repo, and then worked in small steps:

- write the failing test and see it fail for the right reason;
- make it pass;
- try it myself in Swagger or the browser;
- commit.

I worked on one branch, with one commit per step, so the history reads in the same order as the
tasks. The defect fix and the filtering are separate commits, as asked.

I used Claude Code throughout (details in the README). The rule I held myself to: I don't commit
anything I haven't run, read, and can explain.

## Task 1: the defect and the filtering

**Reproducing it.** I followed the bug report with the Network tab open:

- changing Status sent nothing;
- typing in the search then sent a request that included the status I had picked earlier;
- clearing the search made it stale again.

The symptom pointed at *when* requests are sent, not at the API, which the spec also hinted at.

**Finding the cause.** The fetch effect in `useTicketList` only listed search, page and page size
as dependencies. Next to it was an `eslint-disable` comment saying the filters object changes on
every render. That comment was the real clue: someone had hit the lint warning, assumed it was
noise, and switched it off. In fact the filters only change when the user changes them. So the
other filters were never watched, and their values only went out when one of the three watched
values happened to change.

**Fixing the cause, not the symptom.** I didn't want a fix that just adds the missing fields to the
list, because the next filter (the SLA filter in Task 2) would break the same way. So:

- the request is built from *all* the filters;
- the effect depends on that request;
- only the search text is debounced. A dropdown change is one deliberate action and should go out
  at once, but typing produces a change per key.

I also drop responses that arrive after a newer request, so a slow old response can't overwrite
newer results. The regression tests check the request that is actually sent. I ran them against
the original hook first, and they failed there.

**Filtering.** The rule I set myself was "the database does the work": every filter is one `if`
on the `IQueryable`, applied before the count and the page. I checked by reading the SQL EF
printed in the API log, and by comparing counts with the seed data.

## Task 2: escalation and assignment

**Design first.** The spec says the challenge is *where* the rules go, so I settled that before
writing code:

- each rule is a small class in the domain: priority, SLA window, agent selection;
- one `TicketTriage` combines them, and both create and escalate call it;
- handlers stay thin, and controllers hold no logic.

I didn't add an interface for each rule, because each has one implementation and the tests can use
the real thing. Interfaces only went where something really varies or has to be kept out of the
inner layers: the clock, hashing, token issuing, the database reads.

**Making sure it's right.**
- I turned the PDF's worked examples into tests, plus every edge case in section 2.6.
- For the assignment rules, I broke them on purpose to check the tests notice: ignoring the
  specialist rule failed 5 tests, and using `<=` for the limit failed 4.
- Then I posted all five worked examples in Swagger against the seeded database, in order, and
  escalated a real ticket.
- I read the SQL behind the SLA filter.

**Things I changed my mind about:**

- **SLA settings.** The spec pointed at `appsettings.Development.json`. Once I understood that
  file is only loaded in Development, I realised the rules wouldn't exist in Docker or production.
  I moved them to `appsettings.json` and added a startup check, so bad values stop the app with a
  clear message instead of failing on the first request.
- **Storing the at-risk moment.** I wanted the SLA status rule written once, for both the C#
  display and the SQL filter. That only works if the rule is plain comparisons, so I store
  `AtRiskFromUtc` next to `DueAtUtc`. The status itself is still worked out every time. I wrote
  the trade-off down (a threshold change only affects new SLAs) rather than hide it.
- **The escalation panel.** The first version kept the form and a disabled button on tickets that
  can't be escalated. When I tried it in the browser it wasn't clear why the button was disabled,
  so it now hides the form and shows a short note saying why and what to do instead.

**What I found hardest:** making the SLA filter run in SQL while keeping one rule. Reading the
generated `CASE` also showed me its limit: it can't use an index, which is fine at 40 tickets and
not at 10 million (noted in the README).

## Task 3: authentication

I kept it as small as the spec allows:

- **A `PasswordHash` column on `Agent`**, because agents are the only users. A Users table or
  full ASP.NET Identity would be machinery with nothing to do.
- **`PasswordHasher<T>` for hashing**, instead of anything home-made.
- **One generic 401 for every failure.** An unknown email still goes through a password check
  against a dummy hash, so the response time doesn't give it away.
- **Authorization through a fallback policy**, so every endpoint is protected unless someone
  deliberately opts it out. A test lists the anonymous endpoints, so opening one by accident fails
  the build.
- **`escalatedBy` from the token**, so nobody can record an escalation under someone else's name.

On the web, I kept the session in one small module that both the React components and the API
client read. Then the client can attach the token and end the session on a 401 without being
wrapped in a React provider. I chose `sessionStorage` knowing the trade-off: it's simple and
survives a refresh, but scripts can read it.

**The weakest part, in my view:**
- A JWT can't be taken back before it expires. If an agent is deactivated, a token they already
  hold keeps working on every endpoint except `/api/auth/me` (which re-checks the agent) for up to
  60 minutes.
- Together with the token being readable by scripts and the lack of login rate limiting, that's
  what I'd fix first in production: shorter tokens with refresh, or a server-side check, and an
  httpOnly cookie.

## Task 4: Docker

I knew Docker as a user, not much more. Most of this task was learning why each requirement
exists:

- **Two stages**, so the compiler isn't shipped.
- **Copying the project files before the source**, so a code change doesn't throw away the
  package restore. I proved this by changing a file and watching the restore stay cached.
- **A non-root user.**
- **`sqlserver` instead of `localhost`**, because inside a container `localhost` is the container
  itself.

When the first image came out at 347 MB, I looked inside it instead of accepting it. 67 MB were
native libraries for Windows and macOS that a Linux container can't use. Publishing for the
target architecture brought it to 276 MB.

For migrations, I made "migrate on startup" its own setting rather than tying it to the
Development environment:
- local and compose turn it on, so one command gives a working database;
- a real deployment would leave it off and run migrations as a separate release step.

I didn't want "which environment is this" to quietly decide "may this change the database".

## The optional SLA report

I built it at the end, once everything else was done and tested. It reuses the same SLA rule
inside a `GROUP BY`, so the report and the list can't disagree, and on the seeded data the totals
match exactly.

Every test passed on SQLite, but the first call against the real SQL Server returned a 500:
dividing a `bigint` by `3600.0` gives a decimal there. It was a good reminder that a green test
suite on a different database isn't the same as working. I added a test that checks the SQL
Server translation, saw it fail, then fixed it.

## With more time

The web app works, and it's typed and tested, but the way it looks is uneven. The screens I added
(login, the triage result, the escalation panel and history) and the original pages don't yet
share one consistent style. The spec put a visual redesign out of scope, so I left it, but it's
the first thing I'd spend a day on:

- one small set of shared components (cards, buttons, form fields, badges);
- the same spacing and layout on every page;
- a pass over the empty, loading and error states, so they all look and read the same.
