# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository purpose

This repo is training material for an internal "AI coding agent" workshop. It is **not** itself the
product — it's a container for:

- `training-repo/` — the actual codebase trainees work in: **OrderHub**, an ASP.NET Core MVC order
  management system (customers, products, orders). All real code changes happen here.
- `documents/` — workshop content: `PROCESS.md` (trainee reflection log template),
  `activities/activity-guideline.md` (the 4-exercise curriculum: read the codebase, fix 3 seeded
  bugs, add a low-stock feature, do a small refactor), and `references/` (agent configuration and
  prompting guides, in Chinese).

If asked to build, test, or run "the project", that almost always means `training-repo/`, not the
repo root (which has no build system of its own).

⚠️ `training-repo/OrderHub.Core/Services/OrderService.cs` currently contains **intentionally seeded
bugs** used as workshop exercises (e.g. discount logic, stock restoration on cancel, pagination).
Do not silently "fix" issues you notice there unless the user is explicitly working on a bug fix —
these are the exercise content, not accidental defects.

## Commands (run from `training-repo/`)

```powershell
dotnet build                                  # build the solution
dotnet test                                   # run all tests (xUnit + EF Core InMemory, no SQL Server needed)
dotnet test --filter FullyQualifiedName~OrderServiceCreateTests   # run one test class
dotnet run --project src/OrderHub.Web         # run the site (auto-migrates + seeds DB on first run)
dotnet ef database drop -f -p src/OrderHub.Infrastructure -s src/OrderHub.Web   # reset local DB
```

Requires a local SQL Server instance (any edition, including LocalDB) for `dotnet run`; tests never
touch it since they use EF Core InMemory. Default connection string lives in
`src/OrderHub.Web/appsettings.Development.json`.

## Architecture

Three-layer solution, referenced top-down (`Web` → `Core` → `Infrastructure` is *not* a dependency
chain — `Web` and `Infrastructure` both depend on `Core`; `Infrastructure` implements `Core`'s
interfaces):

```
src/
├── OrderHub.Web/            # Controllers, ViewModels, Razor Views — wiring + display only
├── OrderHub.Core/           # Domain models, service interfaces, business logic (discounts, stock, status transitions)
└── OrderHub.Infrastructure/ # EF Core DbContext, repository implementations, migrations, DB seeding
tests/
└── OrderHub.Tests/          # xUnit, EF Core InMemory (see TestSetup.cs for shared fixtures)
```

Conventions to follow when adding or modifying code:

- **Controllers stay thin**: no business logic, no direct `DbContext` use. They call a `Core`
  service and map the result to a ViewModel. See `ProductsController.cs` as the reference shape.
- **Business logic lives in `Core` services**, injected via interface (`IOrderService`,
  `IProductService`, `ICustomerService`). Only repositories touch `DbContext` — never call EF Core
  directly from a service or controller.
- **Services return `ServiceResult<T>`** (`OrderHub.Core/Common/ServiceResult.cs`) to express
  expected failures (validation, not-found, business rule violations) — don't throw exceptions for
  those cases. `Ok(value)` / `Fail(params string[])` / `Fail(IEnumerable<string>)`.
  `ErrorMessage` joins errors with `；`.
- **Views bind to ViewModels**, never domain models directly; mapping is written by hand in the
  controller.
- **User input validation** uses DataAnnotations + `ModelState` — invalid input must re-render the
  form with errors, never a 500.
- **Money is always `decimal`**. Discount logic is centralized in `OrderService`
  (`GetDiscountRate` / `CalculateSubtotal` / `CalculateTotal`) — don't recompute discounts elsewhere.
- New DI registrations go in `Program.cs` (repository + service, both `Scoped`).
- Test helpers (`TestSetup.CreateContext/CreateOrderService/CreateProductService/AddCustomer/AddProduct`)
  build an isolated InMemory DB per test — extend these rather than hand-rolling setup in new test
  files.

## Sensitive / do-not-touch

- `src/OrderHub.Infrastructure/Migrations/**` — EF Core migration history; never hand-edit. Add a
  new migration instead of modifying an existing one.
- `src/OrderHub.Web/appsettings.Development.json` — local connection string; confirm before changing.
- Never add a NuGet package without checking with the user first.
