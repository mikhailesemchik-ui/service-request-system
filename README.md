# Service Request System

## Purpose

An internal system for employees to submit service requests, and for support agents
and administrators to track, assign, and resolve them.

## Technology Stack

- .NET 8 / ASP.NET Core 8 Web API (controllers)
- Entity Framework Core 8 (Code First)
- Angular 18 (standalone components, routing, SCSS)
- xUnit and Moq for backend tests
- Jasmine and Karma for frontend tests
- Swagger / Swashbuckle for API documentation

## Current Structure

```text
service-request-system/
├── ServiceRequestSystem.sln
├── src/
│   ├── ServiceRequest.Api/             ASP.NET Core Web API (controllers, Program.cs)
│   ├── ServiceRequest.Application/     Application layer (feature logic, DI extensions)
│   ├── ServiceRequest.Domain/          Domain entities, enums, rules
│   └── ServiceRequest.Infrastructure/  EF Core, persistence, DI extensions
├── tests/
│   ├── ServiceRequest.UnitTests/       xUnit + Moq
│   └── ServiceRequest.IntegrationTests/xUnit + WebApplicationFactory
├── service-request-client/             Angular 18 application
├── CLAUDE.md
└── README.md
```

The backend currently implements request-category management and a JWT-based
authentication/authorization foundation (login, current-user endpoint, role
policies). The Angular client implements the corresponding authentication
foundation (login, session restoration, route guards, authenticated shell).
Request CRUD, comments, history, and category management UI are not yet
implemented.

## Prerequisites

- .NET 8 SDK
- Node.js 18+
- npm
- Angular CLI 18 (`npx @angular/cli@18`)

Verify versions:

```powershell
dotnet --version
node --version
npm --version
npx ng version
```

## Backend

Restore and build:

```powershell
dotnet restore
dotnet build
```

Run the API:

```powershell
dotnet run --project src/ServiceRequest.Api
```

Swagger UI is available at the API root in the Development environment.

The Angular client is configured (see `src/environments/environment.development.ts`) to call
the API at `http://localhost:5080`. If your `dotnet run` session uses a different port (check
the console output or `Properties/launchSettings.json`), either update that file or run the API
with an explicit override so the two agree:

```powershell
dotnet run --project src/ServiceRequest.Api --urls http://localhost:5080
```

### Cross-origin requests (CORS)

The API and the Angular dev server run on different origins (different ports), and the API has
no CORS policy configured beyond a small, explicit allowlist. `Cors:AllowedOrigins` in
`appsettings.Development.json` allows only `http://localhost:4200` (the default `ng serve`
origin) and does not use credentialed CORS. If you serve the Angular app from a different port,
add that origin to the list — do not switch to `AllowAnyOrigin`.

## Authentication (Development Only)

The API uses JWT Bearer authentication. The signing key is **not** committed to
source control — it must be configured locally via user secrets (or an
environment variable) before the API will start.

### Configure the local signing key

```powershell
dotnet user-secrets init --project src/ServiceRequest.Api

dotnet user-secrets set `
  "Jwt:SigningKey" `
  "<replace-with-a-random-string-at-least-32-characters-long>" `
  --project src/ServiceRequest.Api
```

Generate a random value yourself rather than reusing the placeholder above —
for example, in PowerShell:

```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

Alternatively, set the `Jwt__SigningKey` environment variable. If the signing
key is missing or too short, the API fails to start with a clear validation
error rather than starting insecurely.

### Development user credentials

These accounts are seeded automatically the first time the API runs in the
Development environment. **They are development-only credentials and must
never be used, reused, or considered safe for any non-development
environment.**

| Role         | Username   | Password        |
|--------------|------------|-----------------|
| Admin        | `admin`    | `Admin123!`     |
| Support Agent| `agent`    | `Agent123!`     |
| Employee     | `employee` | `Employee123!`  |

### Logging in

```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "employee",
  "password": "Employee123!"
}
```

The response contains an `accessToken` (JWT), its `expiresAt` timestamp, and
the current user's profile. Use `GET /api/auth/me` with the token to fetch the
authenticated user's current database record.

### Authorizing in Swagger

1. Open Swagger UI (`/swagger`) while the API is running in Development.
2. Call `POST /api/auth/login` with one of the credentials above and copy the
   `accessToken` value from the response.
3. Click the **Authorize** button at the top of the page.
4. Enter `Bearer <accessToken>` (including the `Bearer ` prefix) and confirm.
5. Subsequent requests from Swagger will include the token automatically.

## Frontend

Install dependencies:

```powershell
cd service-request-client
npm install
```

Run the development server:

```powershell
npm start
```

Production build:

```powershell
npm run build
```

### Signing in

With the API running (see above) and the Angular dev server started (`npm start`), open
`http://localhost:4200/login` and sign in with one of the development accounts documented above
(for example `employee` / `Employee123!`). A successful login redirects to `/dashboard`, which
shows the signed-in user's display name and role; `Categories` is reachable from the top
navigation (a placeholder page for now). The **Log out** button in the header clears the session
and returns to `/login`. Visiting a protected route while signed out redirects to `/login` and
returns you to the page you asked for once you sign in.

### Session storage decision

The access token returned by `POST /api/auth/login` is stored in the browser's
`sessionStorage`, behind a single service (`AuthStorageService` — no other code touches
`sessionStorage` directly). `localStorage` is not used, and the token is never persisted
outside the current tab/session.

**This is not a secure-by-default design.** `sessionStorage` is readable by any JavaScript
running on the page, so it offers no protection against XSS — a script-injection
vulnerability elsewhere in the app could read the token directly. It was chosen here only
because the current backend returns the token in the JSON response body rather than an
`HttpOnly` cookie, and no such cookie-based flow exists yet. The preferred production design
is an `HttpOnly`, `Secure`, `SameSite` cookie issued by the server, combined with CSRF
protection — the backend would need to change to support that. Do not treat the current
`sessionStorage` approach as adequate for a production deployment without revisiting this.

## Tests

Backend (all projects):

```powershell
dotnet test
```

Backend unit tests only:

```powershell
dotnet test tests/ServiceRequest.UnitTests
```

Backend integration tests only:

```powershell
dotnet test tests/ServiceRequest.IntegrationTests
```

Frontend (single run, no watch):

```powershell
cd service-request-client
npx ng test --watch=false
```

## Known Limitations

- Registration, refresh tokens, token renewal, password reset, email
  verification, and user-management CRUD are not implemented — only login
  and the current-user endpoint exist.
- Request CRUD, comments, and history behavior are not implemented yet.
- The Angular client implements login, session restoration, route guards,
  and a minimal authenticated shell (Dashboard, Categories). Categories is a
  placeholder page only — category management UI is not implemented yet.
- The access token is stored in `sessionStorage`, which is readable by any
  JavaScript on the page (see "Session storage decision" above). There is no
  refresh token, so a session simply ends when the token expires or the tab
  closes.
- CORS is restricted to `http://localhost:4200` for local development only;
  a real deployment topology (and its allowed origins) has not been decided.
- Development seed users and their passwords (documented above) are for local
  development only and must never be used outside a developer's own machine.
