# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

This fork is my submission for the four tasks in [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md).
The work is on `feature/ticket-escalation`, one commit per step, with a pull request into `main`.
How I approached each task is in [`APPROACH.md`](APPROACH.md).

| # | Task | Status |
| --- | --- | --- |
| 1 | Fix DEFECT-117, add server-side filtering | Done |
| 2 | Ticket escalation & assignment | Done, including the optional SLA summary report (2.5) |
| 3 | Login and authentication | Done |
| 4 | Dockerise the API | Done |

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

### How to log in

Every active seeded agent signs in with the development password **`LocalDev-Only-Password1!`**:
`sara.lindqvist@`, `alex.turner@`, `ben.osei@`, `priya.nair@` or `marcus.doyle@` followed by
`northwind-support.example`. Yuki Tanaka is inactive and is refused. The password is a local
fixture (`SupportDbSeeder.DevelopmentPassword`); only its hash is stored.

In Swagger: call `POST /api/auth/login`, click **Authorize** and paste the `token`.

---

## What I implemented

- **Task 1:** fixed DEFECT-117 at its cause. Every list filter is now applied in SQL before the
  count and the page.
- **Task 2:** a new ticket is triaged (priority, due date, owner, each with a reason). Tickets can
  be escalated with a reason, and each escalation is an immutable history row. One SLA indicator
  is used in the list and on the detail page, and there is an SLA filter. The optional SLA summary
  report is built too.
- **Task 3:** email + password login with a JWT. Every endpoint except login and `/health` needs a
  token. The web app has a login page, protected routes, the agent's name, logout, and expired-
  session handling.
- **Task 4:** a multi-stage Dockerfile, an `api` service in compose, and `/health`.

**The business rules as I understood them:**

- **Priority:** the requested one, or Medium. Categories flagged `ForcesCriticalPriority` are
  always Critical.
- **SLA window:** Critical 4 h, High 8 h, Medium 24 h, Low 72 h. Premium is halved, with a 1 h
  minimum.
- **Assignment:** eligible = active, specialised when the category `RequiresSpecialist`, and
  strictly under `MaxOpenTickets`. Fewest open tickets (not Resolved/Closed) wins; ties go to the
  lowest id. If nobody is eligible, the ticket is saved unassigned with a reason.
- **Escalation:** one level up, and the due date is counted from the escalation. The owner is kept
  if still eligible, otherwise reassigned. Critical / Resolved / Closed tickets are refused with
  409 and a message saying which.
- **SLA status:**
  - no due date → No SLA;
  - resolved on or before the due date → Met;
  - resolved late, or open past due → Breached;
  - open with ≤ 25% of the window left → At risk;
  - otherwise → Within SLA.

**Where the spec left room, I decided:**

- Exactly on the due date, an open ticket is still At risk; one tick later it is Breached.
- When checking if the current owner is still eligible, the ticket itself doesn't count against
  their limit. An agent at 6/6 keeps the ticket they already hold.
- After Task 3, `escalatedBy` is the signed-in agent's **email** from the token. It is unique and
  fits the 100-character column. The request body no longer has it, and a value sent anyway is
  ignored (an API test checks this).

---

## Design decisions

**The rules live in the domain, once.** Each rule is a small class in
`SupportDesk.Domain/Aggregates/Tickets/Triage/`:

- `PriorityPolicy` (BR-1)
- `SlaPolicy` (BR-2/3)
- `AgentSelector` (BR-4/5)
- `TicketTriage`, which combines them

Create and escalate both call `TicketTriage` (`ForNewTicket` / `ForEscalation`), so the two can't
compute an SLA or pick an agent differently. Handlers only load, call the domain, and save.

I didn't put an interface in front of each policy: each has one implementation, and the tests use
the real classes. Interfaces are where there is a real seam:

- password hashing and token issuing, which live in Infrastructure, and Application must not
  depend on that;
- the clock, for tests;
- agent workloads;
- repositories.

No MediatR or AutoMapper; for this many endpoints they'd be ceremony.

**One SLA status rule for C# and SQL.** `SlaEvaluator.Rule` is a LINQ expression. C# compiles it
to show the status, and the list query puts the same expression into the SQL for `?slaStatus=`, so
display and filter can't disagree.

To keep the rule to plain comparisons, I store `Tickets.AtRiskFromUtc` (the moment a ticket becomes
at risk) next to `DueAtUtc`. BR-8 says status is "never stored", and it isn't: it's still worked
out from "now" on every read. What's stored is a moment, like the due date. The alternative was
date arithmetic inside SQL, which is SQL Server-specific and untestable on SQLite. The cost:
changing `AtRiskThresholdPercent` only affects SLAs set after the change, just as changing a base
window only affects new due dates.

**SLA settings are in `appsettings.json` and checked at startup.** The spec pointed at the `Sla`
section in `appsettings.Development.json`. That file is only loaded in Development, so in Docker
or production the rules wouldn't exist. They are business rules, the same everywhere, so I moved
them. They can still be changed per environment without code (`Sla__BaseWindowHours__Critical=2`).
Invalid values stop the API at startup with a message listing each problem. Category behaviour
uses the existing `RequiresSpecialist` / `ForcesCriticalPriority` columns; no category name
appears in code.

**Patterns:**

- **Policy / strategy** for the rules: small, separately tested, changeable on their own.
- **Factory for triaged creation:** `Ticket.Raise` + `ApplyTriage`, so a ticket never exists
  without its triage decision.
- **Shared query object:** `TicketQuery` → `TicketQueries.ApplyFilters`, so a new filter is one
  property and one `if`.

---

## The defect (DEFECT-117)

**Root cause.** The fetch effect in `useTicketList.ts` only depended on `filters.search`,
`filters.page` and `filters.pageSize`. An `eslint-disable` comment hid the warning, wrongly
claiming the filters object changes every render. So changing any other filter sent no request,
and the next search change fetched with whatever filters were set by then.

**Fix.**

- The search text is kept separately and debounced (300 ms, `useDebouncedValue`).
- The request is a `useMemo` of the filters plus the debounced search, and the effect depends on
  it.

Any filter change sends a request at once, and typing sends one request per pause, not per key. A
filter added later (like the SLA filter) needs no extra wiring. A response that arrives after a
newer request is dropped.

**Verify.** With the Network tab open, change Status to In progress: `?status=InProgress` goes out
straight away. The regression tests in `useTicketList.test.ts` assert the request sent; against the
original hook they failed ("expected to be called 1 times, but got 0 times").

---

## Filtering

`TicketQueries.ApplyFilters` adds one `if` per filter to the `IQueryable`, before `COUNT` and
`OFFSET/FETCH`, so `totalCount` and the pager match the filtered set.

- Status, priority, category, customer and agent are exact matches.
- Search is trimmed, ignored when blank, and matches title, reference or customer name.

I checked the SQL in the API log (EF command logging is on in Development). For
`?search=contoso&priority=Critical`:

```sql
SELECT COUNT(*) FROM [Tickets] AS [t]
INNER JOIN [Customers] AS [c] ON [t].[CustomerId] = [c].[Id] ...
WHERE [t].[Priority] = @priority AND ([t].[Title] LIKE @search_contains ESCAPE N'\'
   OR [t].[Reference] LIKE @search_contains0 ESCAPE N'\' OR [c].[Name] LIKE @search_contains1 ESCAPE N'\')
```

The page query has the same `WHERE` plus `ORDER BY … OFFSET … FETCH`, and all values are
parameters. Results matched the seed: contoso + Critical gives 3 tickets, In progress gives 7.
`?slaStatus=` becomes a `CASE` generated from `SlaEvaluator.Rule`.

**Next filter:** a property on `TicketQuery` and one `if` in `ApplyFilters`. On the web, add it to
`TicketFilters` and the filter bar; the hook picks it up.

### SLA summary report (optional 2.5)

`GET /api/reports/sla-summary?from=2026-10-01&to=2026-11-01&groupBy=agent|priority`

- **Which tickets:** those raised from `from` (inclusive) to `to` (exclusive), in UTC.
- **Each group gets:** total, breached, at risk, within SLA and met (status as of now).
- **Breach rate:** breached ÷ tickets that have an SLA.
- **Average resolution hours:** resolved tickets only.

It is one `GROUP BY` in the database. Each ticket's status comes from the same `SlaEvaluator.Rule`,
inlined as a `CASE`, then counted with `COUNT(CASE …)`. A test records the SQL and checks it's a
single grouped query. On the seeded desk the report's totals match the list filter exactly (21
breached, 1 at risk, 6 within, 12 met).

The awkward part was "hours between two dates": SQL Server and SQLite spell it differently. So
`SqlDate.HoursBetween` is mapped per database: `DATEDIFF_BIG` on SQL Server, `julianday` on
SQLite.

---

## Authentication

- **Credentials:** a nullable `PasswordHash` column on `Agent`, not a Users table. Agents are the
  only users and roles are out of scope, so a second table would only add a join. I rejected full
  ASP.NET Core Identity for the same reason.
- **Hashing:** Identity's `PasswordHasher<Agent>` (PBKDF2-HMAC-SHA512, 100,000 iterations, random
  salt), behind `IPasswordHasher`. The seeder hashes the dev password at seed time.
- **One answer for every failure:** wrong email, wrong password and inactive agent all get
  `401 "Invalid email or password."`. An unknown email is still checked against a dummy hash, so
  timing doesn't reveal it.
- **Token:** JWT, HMAC-SHA256, claims `sub` / `email` / `name`.
  - **Lifetime 60 minutes:** a working session, without a leaked token staying useful for long.
  - The key comes from `Jwt:SigningKey` (env `Jwt__SigningKey`), must be ≥ 32 bytes or the API
    won't start, and only an obviously fake dev key is committed.
- **Applied centrally:** an authorization `FallbackPolicy` covers every endpoint. Only login and
  `/health` are `AllowAnonymous`, and an API test fails if any other endpoint becomes anonymous.
  Anonymous requests to unknown URLs get 401 rather than 404, which I'm fine with.
- **Browser:** the token is kept in `sessionStorage` (`apps/web/src/auth/session.ts`), and
  `client.ts` attaches it to every call.
  - A 401 on a call that carried a token ends the session and goes to `/login` ("session
    expired").
  - A 401 on login just shows the error.
  - Trade-off: sessionStorage survives a refresh and ends with the tab, but scripts can read it,
    so an XSS bug would expose the token.
- **In production:** an httpOnly SameSite cookie with CSRF protection, login rate limiting, and the
  key in a secret store with rotation.

---

## Docker

```bash
docker compose up --build                                  # SQL Server + API on :5080
curl http://localhost:5080/health                          # Healthy
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web    # web app against the container
```

On an empty volume the API migrates and seeds, so the logins above work.

Compose sets these environment variables; the defaults work on a clean checkout, and `.env`
overrides them:

- `ConnectionStrings__Default` points at the `sqlserver` service, not `localhost`, which inside a
  container is the container itself.
- `Jwt__SigningKey` is the token signing key.
- `ASPNETCORE_ENVIRONMENT` is `Development`, so Swagger is on at :5080.
- `Database__MigrateOnStartup` is `true`.

**The image:**

- **Two stages.** SDK to build, ASP.NET runtime to run.
- **Restore is cached.** The project files and `Directory.*.props` / `global.json` are restored
  before the source is copied.
- **Non-root.** It runs as the image's `app` user, on port 8080.
- **No configuration inside.** `appsettings.Development.json` is not published, so no key or
  connection string is in the image.
- **Allowlist `.dockerignore`.**
- **Linux target only.** It publishes for the build machine's architecture (`-a $TARGETARCH`). The
  first image carried 67 MB of Windows/macOS native libraries a Linux container never uses.
- **Size:** **276 MB**, against **975 MB** for the SDK image. The base is 262 MB; the app is about
  14 MB.

**Checked:**

- `docker compose down -v && docker compose up --build` applied all 4 migrations and seeded the
  40 tickets; `/health` returned 200.
- `/api/tickets` returned 401 without a token and data with one.
- After changing one `.cs` file and rebuilding, every step up to and including `dotnet restore`
  was `CACHED`.
- `docker compose exec api whoami` printed `app`.
- The web app worked through `VITE_API_PROXY_TARGET`.

**Migrations in production.** Migrating on startup is a setting (`Database:MigrateOnStartup`), not
a side effect of the environment name. It's on locally and in compose, off by default. In
production I'd run migrations as a release step before the new version starts (e.g. an EF Core
migrations bundle):

- instances don't race to migrate the same database;
- a failed migration stops the release instead of crash-looping the app;
- the app's database user doesn't need schema rights.

I'd also run as Production, pin base images by digest, consider the chiseled runtime image, and
scan the image in CI.

---

## Database

Three migrations; all apply cleanly to an empty database.

- **`AddTicketAtRiskFrom`:** `Tickets.AtRiskFromUtc`, backfilled for existing tickets.
- **`AddTicketEscalations`:** the table from the spec.
  - `Reason` (500) and `EscalatedBy` (100) are required, and priorities have a check constraint.
  - All three foreign keys are `Restrict`, so the audit trail can't vanish as a side effect.
  - Rows are created only by `Ticket.Escalate` and have no public setters.
- **`AddAgentPasswordHash`:** `Agents.PasswordHash nvarchar(256) NULL`.

**Index:** `IX_TicketEscalations_TicketId_EscalatedAtUtc` on `(TicketId, EscalatedAtUtc DESC)`.
The only query is "history of ticket X, newest first": this is a seek that returns rows already in
order, and it also covers the `TicketId` foreign key. I kept EF's indexes on `FromAgentId` /
`ToAgentId`, because with `Restrict`, deleting an agent checks this table.

Reads use `AsNoTracking()` and project to DTOs. Triage loads all agent workloads in one query.

---

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest); no database needed
```

**261 tests:**

- 173 unit tests: domain rules, handlers, validators, query tests on in-memory SQLite.
- 31 API tests (new project): the real app over HTTP with `WebApplicationFactory`, on SQLite
  seeded by the real seeder.
- 4 architecture tests.
- 53 web tests.

**What I tested:**

- every edge case the spec lists, plus the PDF's worked examples as tests;
- SLA boundaries: exactly on the due date, one tick after, exactly at the threshold, resolved on
  time and late;
- the filters, including search by customer name;
- auth end to end: same 401 for wrong email and password, inactive refused, 401/200 per endpoint,
  a tampered token, the escalation actor taken from the token;
- on the web: the DEFECT-117 regressions, escalation validation and 409, each SLA state, the
  login redirect and the token handling.

Time always comes from a fixed clock. To check that the assignment tests can actually fail, I
broke the rules on purpose (ignored `RequiresSpecialist`, used `<=` for the limit) and 5 and 4
tests failed.

**Not tested:** CSS and layout, and most SQL Server-specific behaviour. The query tests run on
SQLite, so I checked SQL Server's SQL by reading the log. One SQL Server translation, the report's
hours calculation, has a test that generates the SQL Server query without a server.

---

## AI usage

**Tools.** I used **Claude Code** (Claude Opus, terminal) for:

- reading the repo and spec;
- planning each task;
- writing code and tests;
- explaining .NET and Docker, which I know less well than React/TypeScript.

**Workflow that worked.** Test first: write the failing test, run it against the old code, then
fix. Before each commit, I tried the change myself in Swagger or the browser. For DEFECT-117 the
regression tests failed against the original hook before any fix existed, so I knew they caught
the real bug.

**Accepted largely as-is.** The triage rule classes (`SlaPolicy`, `PriorityPolicy`,
`AgentSelector`), after I posted each of the PDF's worked examples in Swagger and got the expected
priority, window and agent.

**Changed or rejected:**

- **Escalation panel.** The first version kept the fields and a disabled button on a ticket that
  couldn't be escalated, and it wasn't clear why. I had it replaced with an info box that says why
  and what to do instead.
- **Loading state.** ESLint rejected the first DEFECT-117 fix, which set state inside an effect.

**Bugs.**

- **Introduced:**
  - When the web tests failed on a fresh install, Claude blamed the lockfile and edited
    `package.json`. The diff showed the real cause was `legacy-peer-deps=true` in my global npm
    config, so I reverted it.
  - The first version of the SLA report passed every SQLite test but returned a 500 on SQL
    Server: `bigint / 3600.0` is a decimal there, not a double. Calling the endpoint against the
    real database caught it. I added a test that checks the SQL Server translation, watched it
    fail, then fixed it with a cast.
- **Found:**
  - the DEFECT-117 root cause;
  - 67 MB of unused native libraries in the image;
  - that the `System.Security.Cryptography.Pkcs` version I'd have pinned (10.0.12) was still
    vulnerable. I checked the advisories and pinned 10.0.13, so the build has no warnings now.

**How I verified:**

- every test seen failing first;
- deliberately breaking rules;
- reading the generated SQL;
- Swagger for the worked examples and the 401/409 cases;
- the browser;
- lint and typecheck;
- the Docker checks above.

---

## Known limitations

- **The SLA filter can't use an index.** It's a `CASE` in `WHERE`, and search is `LIKE '%x%'`.
  At 10M tickets I'd generate range predicates per status from the same rule, add an index on open
  tickets, and use full-text search.
- **Changing the at-risk threshold** only affects SLAs set afterwards.
- **No refresh tokens** (out of scope), so agents sign in again after 60 minutes. The token in
  `sessionStorage` is readable by scripts. There's no login rate limiting (out of scope).
- **Query tests use SQLite**, which matches text case-sensitively unlike SQL Server; I checked
  search casing by hand on SQL Server. Next step: run them against a SQL Server container.
- **Compose runs the API as Development** (Swagger on); production notes are in Docker above.
- **Clearing filters during a search** sends one extra request when the debounce catches up.
- **Not built:** the optional lead-only escalation role.
- **The SLA report's queries are tested on SQLite.** Its SQL Server version was checked against
  the real database and by a test on the generated SQL.

---

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `docker compose up --build` | SQL Server + the API in a container on :5080 |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

Every endpoint needs `Authorization: Bearer <token>` except login and `/health`.

| Endpoint | Description |
| --- | --- |
| `POST /api/auth/login` · `GET /api/auth/me` | Sign in / the signed-in agent |
| `GET /api/tickets` | Paged list; `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`, all in SQL |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket; returns the triage decision |
| `POST /api/tickets/{id}/escalate` | `{ "reason": "..." }`; 400 / 404 / 409 as in the spec |
| `GET /api/tickets/{id}/escalations` | History, newest first |
| `GET /api/reports/sla-summary` | `from`, `to`, `groupBy=agent\|priority`; SLA counts, breach rate and average resolution hours per group |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |
| `GET /health` | 200 `Healthy` when the database is reachable, otherwise 503 |

Errors are `ProblemDetails`: `400` validation, `401` not signed in, `404` not found, `409`
conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Every call returns 401 | Sign in; in Swagger use **Authorize** with the token from login |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
