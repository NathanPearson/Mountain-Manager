# Mountain Manager

Mountain Manager is a small full-stack task management app I built for the Function Health take-home exercise.

I treated this as a small production-style feature rather than a demo. The scope is intentionally practical: one API, one frontend, persistent data, clear validation, authenticated ownership, and enough tests to protect the parts most likely to break.

## What It Does

Users can register, sign in, and manage their own tasks. The task workflow supports:

- Create, edit, complete/incomplete, delete, and list flows
- Required title and due date fields with inline validation
- Per-user unique task titles with clear duplicate-title validation
- Priority values: `Low`, `Medium`, `High`, `Urgent`
- Due-date buckets: `Overdue`, `Today`, `Upcoming`, `Completed`
- Filters for status, priority, and due-date bucket
- SQLite persistence across app restarts

I included authentication because I wanted to close the ownership loop rather than leave auth as a partial feature. A user cannot read, update, complete, or delete another user's tasks.

## Tech Stack

- Backend: .NET 8
- Data access and persistence: EF Core with SQLite
- Backend date/time types: NodaTime (`LocalDate` for due dates, `Instant` for audit timestamps)
- Frontend: React, TypeScript, Vite
- Frontend date formatting: Luxon
- API documentation: OpenAPI with Scalar UI
- Tests: xUnit integration tests with `WebApplicationFactory`

## Prerequisites And Assumptions

I built and verified this locally with:

- .NET 8 SDK
- Node.js 22
- npm

Install links:

- .NET SDK: https://dotnet.microsoft.com/download
- Node.js: https://nodejs.org

The app assumes these local ports are available:

- API: `http://localhost:5033`
- Frontend: `http://localhost:5173`

The frontend is configured to call the API at `http://localhost:5033` by default. If that port needs to change, start the API on a different port and set `VITE_API_BASE_URL` before running the frontend.

The local SQLite database is created at:

```text
src/api/mountain-manager.dev.db
```

No external database server is required.

## Running Locally On Windows

From the repository root, start the API in PowerShell:

```powershell
dotnet run --project .\src\api\MountainManager.Api.csproj --urls http://localhost:5033
```

Open a second PowerShell terminal and start the frontend:

```powershell
cd .\src\web
npm install
npm run dev
```

Open the app:

```text
http://localhost:5173
```

On first launch, choose **Register** to create a local account, then use that account to sign in and manage tasks.

## Running Locally On macOS Or Linux

From the repository root, start the API:

```bash
dotnet run --project src/api/MountainManager.Api.csproj --urls http://localhost:5033
```

Open a second terminal and start the frontend:

```bash
cd src/web
npm install
npm run dev
```

Open the app:

```text
http://localhost:5173
```

## API Documentation

Interactive API documentation is available in development through Scalar:

```text
http://localhost:5033/scalar
```

The raw OpenAPI document is available at:

```text
http://localhost:5033/openapi/v1.json
```

## Tests And Build

Run the backend tests:

```bash
dotnet test MountainManager.sln
```

Build the frontend:

```bash
cd src/web
npm run build
```

The backend tests focus on the areas I would be most careful changing later: validation, authentication behavior, task CRUD, priority defaults, priority lookup storage, due-bucket filtering, sorting, and cross-user ownership enforcement.

## API Response Shape

I used a consistent response envelope for API responses:

```json
{
  "success": true,
  "data": {},
  "error": null,
  "traceId": "0HN..."
}
```

Errors use the same shape:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more validation errors occurred.",
    "details": {
      "title": ["Title is required."]
    }
  },
  "traceId": "0HN..."
}
```

The API includes `traceId` for log correlation. The frontend intentionally does not show trace IDs in normal user-facing validation or login messages.

## Due Date Approach

I modeled due dates as calendar dates, not instants.

The backend uses `NodaTime.LocalDate` for task due dates and accepts/returns ISO date strings such as `2026-06-18`. The app only asks users for a date, not a time of day, because a task due on June 18 should stay June 18 instead of shifting through a browser or server timezone conversion.

The frontend sends the date string from the date input directly to the API. It uses Luxon only to format that date string for display. Operational timestamps such as `createdAt`, `updatedAt`, and `completedAt` are separate audit timestamp fields stored as `NodaTime.Instant`.

Due-date buckets use the user's browser timezone. The frontend sends the browser's IANA timezone in an `X-Time-Zone` header, and the API uses that timezone when deciding whether a task is `Overdue`, `Today`, or `Upcoming`.

## Task Ordering

Tasks are grouped by due-date bucket first:

1. `Overdue`
2. `Today`
3. `Upcoming`
4. `Completed`

Ordering inside each bucket is intentional:

- `Overdue`: oldest due date first, then highest priority.
- `Today`: highest priority first.
- `Upcoming`: highest priority first, then nearest due date.
- Final tie-breaker: most recently updated.

## Architecture Notes

I intentionally kept the backend to one API project. There is no repository layer, CQRS, MediatR, Docker setup, or CI pipeline because this app has one main entity and a small number of endpoints. EF Core is used directly at the endpoint boundary so the code stays easy to follow.

I stored priorities as lookup data rather than task-level strings. The `TaskPriorities` table contains stable numeric IDs and sort ranks for `Low`, `Medium`, `High`, and `Urgent`; tasks store `PriorityId` as a foreign key. The API still accepts and returns readable priority names so the frontend contract stays simple.

Authentication is intentionally lightweight: email/password registration with PBKDF2 password hashing and JWT bearer tokens. Passwords must be at least 8 characters. For a production system, I would move signing keys to a secret store and add refresh token handling.

Logging uses built-in `ILogger<T>` with structured messages around startup, authentication, task mutations, validation failures, ownership misses, and unexpected exceptions. I avoided logging passwords, password hashes, JWTs, and full request bodies.

## What I Would Add Next

- Refresh tokens and token revocation
- EF Core migrations instead of `EnsureCreated`
- Playwright end-to-end tests for the browser flows
- Accessibility checks and keyboard-focused UI refinements
- Server-side pagination once task volume justifies it
- A full audit history table for task changes, beyond the current created/updated/completed timestamps
