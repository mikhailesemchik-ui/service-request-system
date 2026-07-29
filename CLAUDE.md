# Core Development Philosophy

## KISS — Keep It Simple

Simplicity is a primary goal.

Prefer straightforward and understandable solutions over unnecessarily complex abstractions. Simple solutions are easier to review, test, maintain, and debug.

Do not introduce design patterns, libraries, abstraction layers, or infrastructure unless they solve a concrete problem in the current project.

## YAGNI — You Aren't Gonna Need It

Do not implement functionality based on assumptions about future requirements.

Build only what is required for the current task or explicitly documented project scope.

Do not add speculative features such as:

* microservices;
* message queues;
* CQRS;
* event sourcing;
* generic frameworks;
* excessive base classes;
* unnecessary repository abstractions;
* complex caching;
* refresh-token systems unless required;
* additional roles or statuses not defined in the requirements.

## Design Principles

### Single Responsibility

Each class, method, component, service, and module must have one clear responsibility.

Controllers must handle HTTP concerns only.

Business logic belongs in application or domain services.

Database access belongs in the persistence layer.

Angular components must focus on presentation and user interaction rather than business logic.

### Dependency Inversion

High-level business logic must not depend directly on implementation details when an abstraction provides clear value.

Use dependency injection through ASP.NET Core and Angular.

Do not create interfaces automatically for every class. Add an interface when:

* multiple implementations exist;
* the dependency must be replaced in tests;
* the abstraction represents an external system;
* the abstraction creates a meaningful architectural boundary.

### Open/Closed Principle

Prefer designs that allow new behavior to be added without modifying unrelated existing code.

Do not over-engineer extension points for features that do not exist.

### Fail Fast

Validate invalid states and requests as early as possible.

Reject invalid input before modifying database state.

Return clear errors rather than allowing invalid data to propagate through the application.

---

# Code Structure and Modularity

## General Limits

These limits are guidelines, not reasons to split cohesive code into meaningless fragments.

* Avoid files longer than 500 lines.
* Prefer methods under 50 lines.
* Prefer Angular components under 300 lines.
* Prefer backend classes under 300 lines.
* Keep one primary public class, component, interface, enum, or record per file.
* Split files when they contain unrelated responsibilities.
* Do not create tiny wrapper methods or files that provide no real value.

## Backend Project Architecture

Use a feature-oriented layered architecture.

```text
ServiceRequestSystem/
├── ServiceRequestSystem.sln
├── src/
│   ├── ServiceRequest.Api/
│   │   ├── Controllers/
│   │   ├── Authentication/
│   │   ├── Authorization/
│   │   ├── ExceptionHandling/
│   │   ├── Extensions/
│   │   ├── Middleware/
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── ServiceRequest.Application/
│   │   ├── Common/
│   │   ├── Categories/
│   │   ├── Comments/
│   │   ├── Requests/
│   │   ├── Users/
│   │   └── DependencyInjection.cs
│   │
│   ├── ServiceRequest.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   ├── Exceptions/
│   │   └── Rules/
│   │
│   └── ServiceRequest.Infrastructure/
│       ├── Authentication/
│       ├── Data/
│       ├── Migrations/
│       ├── Persistence/
│       ├── Seed/
│       └── DependencyInjection.cs
│
└── tests/
    ├── ServiceRequest.UnitTests/
    └── ServiceRequest.IntegrationTests/
```

Keep code grouped by feature inside the Application project.

Example:

```text
Requests/
├── CreateRequest/
│   ├── CreateRequestCommand.cs
│   ├── CreateRequestRequest.cs
│   ├── CreateRequestResponse.cs
│   ├── CreateRequestService.cs
│   └── CreateRequestValidator.cs
│
├── GetRequest/
├── GetRequests/
├── UpdateRequest/
└── ChangeRequestStatus/
```

Do not place all DTOs, validators, services, and mappings for the entire application into large global folders when they can live near their feature.

## Angular Project Architecture

Use standalone Angular components and organize the application by feature.

```text
service-request-client/
└── src/
    └── app/
        ├── core/
        │   ├── auth/
        │   ├── guards/
        │   ├── interceptors/
        │   ├── layout/
        │   └── services/
        │
        ├── shared/
        │   ├── components/
        │   ├── directives/
        │   ├── models/
        │   ├── pipes/
        │   └── utils/
        │
        ├── features/
        │   ├── authentication/
        │   ├── dashboard/
        │   ├── requests/
        │   ├── categories/
        │   └── users/
        │
        ├── app.component.ts
        ├── app.config.ts
        └── app.routes.ts
```

Feature example:

```text
features/requests/
├── components/
├── pages/
├── models/
├── services/
├── request.routes.ts
└── request.validators.ts
```

Rules:

* Pages represent routed screens.
* Components represent reusable visual sections.
* API communication belongs in services.
* Components must not construct API URLs directly.
* Keep feature-specific types inside their feature.
* Put code in `shared` only when it is genuinely reusable across features.
* Put application-wide singleton behavior in `core`.
* Avoid circular dependencies.
* Lazy-load feature routes where appropriate.

---

# Development Environment

## Required Tooling

The project uses:

* .NET 8 SDK;
* ASP.NET Core 8 Web API;
* Entity Framework Core 8;
* Angular 18;
* TypeScript;
* Node.js 18 or newer;
* npm;
* Swagger / OpenAPI;
* xUnit for backend tests;
* Angular testing tools for frontend tests.

Verify installed versions before running project commands:

```powershell
dotnet --version
node --version
npm --version
npx ng version
```

## Backend Commands

Restore dependencies:

```powershell
dotnet restore
```

Build the complete solution:

```powershell
dotnet build
```

Run the API:

```powershell
dotnet run --project src/ServiceRequest.Api
```

Run all backend tests:

```powershell
dotnet test
```

Run unit tests:

```powershell
dotnet test tests/ServiceRequest.UnitTests
```

Run integration tests:

```powershell
dotnet test tests/ServiceRequest.IntegrationTests
```

Run tests without rebuilding:

```powershell
dotnet test --no-build
```

Format backend code:

```powershell
dotnet format
```

Check formatting without changing files:

```powershell
dotnet format --verify-no-changes
```

Create an EF Core migration:

```powershell
dotnet ef migrations add MigrationName `
  --project src/ServiceRequest.Infrastructure `
  --startup-project src/ServiceRequest.Api
```

Apply migrations:

```powershell
dotnet ef database update `
  --project src/ServiceRequest.Infrastructure `
  --startup-project src/ServiceRequest.Api
```

Remove the latest uncommitted migration:

```powershell
dotnet ef migrations remove `
  --project src/ServiceRequest.Infrastructure `
  --startup-project src/ServiceRequest.Api
```

Never edit generated migration files unless a concrete migration issue requires a deliberate manual correction.

Never recreate or delete existing migrations merely to make the migration history look cleaner.

## Frontend Commands

Install dependencies:

```powershell
cd service-request-client
npm install
```

Run the Angular development server:

```powershell
npm start
```

Alternative:

```powershell
npx ng serve
```

Create a production build:

```powershell
npm run build
```

Run frontend tests:

```powershell
npm test
```

Run tests once without watch mode:

```powershell
npx ng test --watch=false
```

Run linting when configured:

```powershell
npm run lint
```

Do not manually edit dependency versions in `package-lock.json`.

Install packages using npm commands:

```powershell
npm install package-name
npm install --save-dev package-name
npm uninstall package-name
```

Do not add a dependency when the task can be completed clearly using the existing stack.

---

# C# Style and Conventions

## General Style

Follow standard modern C# and .NET conventions.

* Enable nullable reference types.
* Enable implicit usings where appropriate.
* Prefer file-scoped namespaces.
* Prefer explicit and readable code over clever expressions.
* Use `async` database and I/O operations.
* Pass `CancellationToken` through asynchronous request paths.
* Do not use `.Result`, `.Wait()`, or blocking calls on asynchronous operations.
* Use `var` when the type is obvious from the right-hand side.
* Use the explicit type when it improves understanding.
* Prefer collection expressions when supported and readable.
* Avoid unnecessary regions.
* Remove unused usings and dead code.
* Do not suppress compiler warnings without documenting a real reason.

## Naming Conventions

* Classes, records, interfaces, enums and public members: `PascalCase`
* Local variables and parameters: `camelCase`
* Private fields: `_camelCase`
* Constants: `PascalCase`
* Interfaces: `I` prefix
* Async methods: `Async` suffix
* Boolean properties: `Is`, `Has`, `Can`, or `Should` prefix
* Cancellation token parameter: `cancellationToken`

Examples:

```csharp
public sealed class ServiceRequestService : IServiceRequestService
{
    private readonly ApplicationDbContext _dbContext;

    public Task<ServiceRequestDto?> GetByIdAsync(
        int requestId,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

Use descriptive domain names.

Prefer:

```csharp
serviceRequest
assignedAgent
requestCategory
createdByUserId
```

Avoid:

```csharp
item
data
obj
temp
value
x
sr
req
```

Short names such as `i` are acceptable only in very small and obvious loops.

## XML Documentation

Do not add XML comments mechanically to every class and method.

Add XML documentation to:

* public APIs used outside their project;
* non-obvious public abstractions;
* complex domain rules;
* methods whose behavior cannot be understood from their signature.

Comments must explain why something is done, not repeat what the code already says.

Bad:

```csharp
// Get request by id
var request = await GetRequestByIdAsync(requestId);
```

Useful:

```csharp
// A resolved request remains editable until it is explicitly closed,
// allowing the employee to confirm that the solution worked.
```

---

# TypeScript and Angular Conventions

## TypeScript Style

* Keep TypeScript strict mode enabled.
* Do not use `any` unless there is a documented and unavoidable reason.
* Prefer `unknown` over `any` for untrusted values.
* Define API request and response models explicitly.
* Use `readonly` for values that must not be reassigned.
* Prefer immutable transformations where practical.
* Avoid non-null assertions unless the value is guaranteed by lifecycle or validation.
* Avoid large inline template expressions.
* Keep business logic out of HTML templates.
* Use explicit return types for public service methods.
* Use RxJS operators deliberately.
* Do not create nested subscriptions.
* Use `async` pipe or controlled subscription cleanup.
* Use `takeUntilDestroyed` for subscriptions created in components.
* Use Angular signals where they simplify local state.
* Do not mix signals and RxJS without a clear reason.

## Angular Naming

* Components: `PascalCase` class with `.component.ts`
* Services: `PascalCase` class with `.service.ts`
* Guards: `.guard.ts`
* Interceptors: `.interceptor.ts`
* Models and interfaces: descriptive `PascalCase`
* Variables, functions and properties: `camelCase`
* Observables: optional `$` suffix when it improves clarity

Examples:

```typescript
export interface ServiceRequestSummary {
  id: number;
  title: string;
  status: RequestStatus;
  priority: RequestPriority;
  createdAt: string;
}

@Injectable({ providedIn: "root" })
export class ServiceRequestApiService {
  getRequests(): Observable<ServiceRequestSummary[]> {
    // Implementation
  }
}
```

## Angular Components

Use standalone components.

Prefer:

```typescript
@Component({
  selector: "app-request-list",
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: "./request-list.component.html",
  styleUrl: "./request-list.component.scss",
})
export class RequestListComponent {}
```

Rules:

* Do not place API requests directly in constructors.
* Use `ngOnInit`, signals, resolvers, or dedicated state services.
* Use reactive forms for application forms.
* Keep form creation and validation readable.
* Display validation errors only after a control is touched or submission is attempted.
* Always support loading, empty, error, and success states where applicable.
* Disable submit buttons during submission.
* Prevent duplicate submissions.
* Use semantic HTML.
* All buttons must have an explicit `type`.
* All form fields must have accessible labels.
* Do not rely only on color to communicate status or errors.

---

# API Design Standards

## REST Routes

Use plural resource names.

```http
GET    /api/requests
GET    /api/requests/{requestId}
POST   /api/requests
PUT    /api/requests/{requestId}
DELETE /api/requests/{requestId}

GET    /api/requests/{requestId}/comments
POST   /api/requests/{requestId}/comments

PATCH  /api/requests/{requestId}/status
PATCH  /api/requests/{requestId}/assignment
```

Do not place verbs in routes when a resource-oriented route is clear.

Prefer:

```http
PATCH /api/requests/{requestId}/status
```

Avoid:

```http
POST /api/requests/change-status
```

Use action-style routes only when the operation represents a clear domain command that does not fit ordinary CRUD.

Example:

```http
POST /api/requests/{requestId}/close
POST /api/requests/{requestId}/cancel
```

## HTTP Status Codes

Use appropriate status codes:

* `200 OK` for successful reads and updates returning data;
* `201 Created` for successful creation;
* `204 No Content` for successful operations without a response body;
* `400 Bad Request` for malformed or invalid requests;
* `401 Unauthorized` when authentication is missing or invalid;
* `403 Forbidden` when the authenticated user lacks permission;
* `404 Not Found` when a resource does not exist or must not be disclosed;
* `409 Conflict` for business-state conflicts;
* `422 Unprocessable Entity` only when deliberately adopted for semantic validation;
* `500 Internal Server Error` only for unexpected failures.

Use `CreatedAtAction` or an equivalent response for created resources.

## DTO Rules

Never expose EF Core entities directly from controllers.

Use separate models for:

* create requests;
* update requests;
* list responses;
* detail responses;
* authentication requests;
* authentication responses.

Example:

```csharp
public sealed record CreateServiceRequestRequest(
    string Title,
    string Description,
    int CategoryId,
    RequestPriority Priority);

public sealed record ServiceRequestDetailsDto(
    int Id,
    string Title,
    string Description,
    RequestStatus Status,
    RequestPriority Priority,
    CategoryDto Category,
    UserSummaryDto CreatedBy,
    UserSummaryDto? AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
```

Do not reuse response DTOs as command input models.

Do not add navigation properties or sensitive fields to DTOs unless required by the client.

## Pagination and Filtering

List endpoints must support pagination when result size can grow significantly.

Example:

```http
GET /api/requests?page=1&pageSize=20&status=New&priority=High
```

Rules:

* Page numbers shown to users start at `1`.
* Internal zero-based indexes must never leak into user-facing output.
* Set a reasonable maximum page size.
* Validate unsupported filter values.
* Apply filtering and sorting in the database query.
* Do not load all records before filtering.

Use a consistent paginated response model:

```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
```

---

# Entity Framework Core

## Entity Rules

Entities represent persisted domain state.

Do not use EF entities as API models.

Keep relationships explicit.

Example:

```csharp
public sealed class ServiceRequest
{
    public int Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public RequestStatus Status { get; private set; }

    public RequestPriority Priority { get; private set; }

    public int CategoryId { get; private set; }

    public RequestCategory Category { get; private set; } = null!;

    public int CreatedByUserId { get; private set; }

    public ApplicationUser CreatedByUser { get; private set; } = null!;

    public int? AssignedToUserId { get; private set; }

    public ApplicationUser? AssignedToUser { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }
}
```

Do not make all setters public merely to simplify mapping.

Protect important state transitions through methods when doing so prevents invalid states.

## Database Naming

Use consistent database naming.

For C# entities:

```text
ServiceRequest
RequestComment
RequestCategory
ApplicationUser
```

For database tables, use a single consistent convention selected by the project.

Default convention:

```text
ServiceRequests
RequestComments
RequestCategories
Users
```

Primary keys:

```text
Id
```

Foreign keys:

```text
ServiceRequestId
CategoryId
CreatedByUserId
AssignedToUserId
```

Timestamps:

```text
CreatedAt
UpdatedAt
ResolvedAt
ClosedAt
CancelledAt
```

Booleans:

```text
IsActive
IsDeleted
IsInternal
```

Counts:

```text
CommentCount
RequestCount
```

Do not mix conventions such as `request_id`, `RequestID`, and `RequestId`.

## Entity Configuration

Use `IEntityTypeConfiguration<TEntity>` for non-trivial configuration.

```csharp
public sealed class ServiceRequestConfiguration
    : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("ServiceRequests");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(request => request.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.HasIndex(request => request.CreatedAt);
        builder.HasIndex(request => request.Status);
    }
}
```

Do not place all entity configuration into one large `OnModelCreating` method.

## Query Rules

* Use asynchronous EF Core methods.
* Use `AsNoTracking()` for read-only queries.
* Project directly to DTOs when practical.
* Avoid unnecessary `Include` chains.
* Avoid N+1 queries.
* Apply filtering, sorting, and pagination before materialization.
* Do not call `ToListAsync` before applying database filters.
* Use `AnyAsync` for existence checks.
* Use transactions for multi-step operations that must succeed or fail together.
* Do not add a generic repository over EF Core by default.
* Use `DbContext` directly in application services unless a repository creates a meaningful domain boundary.
* Never expose `IQueryable` outside the persistence or application boundary.

## Migrations

* Every schema change requires an EF Core migration.
* Migration names must describe the change.
* Review generated migrations before applying them.
* Never delete production migration history.
* Do not modify the database schema manually without a corresponding migration.
* Keep development seed data separate from production data.
* Do not store real passwords or secrets in seed files.

Examples:

```text
InitialCreate
AddRequestAssignments
AddRequestCommentVisibility
AddRequestStatusIndexes
```

---

# Validation

## Backend Validation

All incoming API models must be validated.

Validation must cover:

* required fields;
* length limits;
* allowed enum values;
* valid identifier values;
* business-state rules;
* authorization rules;
* related-resource existence where required.

Simple request-shape validation may use data annotations.

Complex validation should use a dedicated validator or application service.

Do not rely only on frontend validation.

Frontend input is untrusted.

Example:

```csharp
public sealed record CreateServiceRequestRequest(
    [property: Required]
    [property: StringLength(200, MinimumLength = 3)]
    string Title,

    [property: Required]
    [property: StringLength(4000, MinimumLength = 10)]
    string Description,

    [property: Range(1, int.MaxValue)]
    int CategoryId,

    RequestPriority Priority);
```

## Angular Validation

Angular forms must provide matching client-side validation for user experience.

Backend validation remains the source of truth.

Display useful validation messages.

Avoid generic messages such as:

```text
Invalid value
Something went wrong
```

Prefer:

```text
Title must contain between 3 and 200 characters.
This category is no longer available.
The request cannot be closed while it is waiting for an agent response.
```

Do not duplicate complex business rules in Angular when they must be decided by the server.

---

# Business Rules

Business rules must be centralized and testable.

For the Service Request System, examples include:

* an employee can view only their own requests;
* support agents can view requests available to their role;
* an employee cannot assign an agent;
* only authorized roles can change priority;
* a closed request cannot return to `New`;
* a cancelled request cannot be resolved;
* a request cannot be assigned to an inactive user;
* an agent must have the appropriate role before assignment;
* a request cannot be deleted when history must be preserved;
* internal comments must not be visible to employees;
* status transitions must follow an explicit transition policy.

Do not scatter the same business rule across:

* controllers;
* Angular components;
* services;
* database queries.

Create one authoritative backend implementation.

Example:

```csharp
public static class RequestStatusTransitions
{
    private static readonly IReadOnlyDictionary<RequestStatus, RequestStatus[]>
        AllowedTransitions =
            new Dictionary<RequestStatus, RequestStatus[]>
            {
                [RequestStatus.New] =
                [
                    RequestStatus.InProgress,
                    RequestStatus.Cancelled,
                ],
                [RequestStatus.InProgress] =
                [
                    RequestStatus.WaitingForUser,
                    RequestStatus.Resolved,
                    RequestStatus.Cancelled,
                ],
                [RequestStatus.WaitingForUser] =
                [
                    RequestStatus.InProgress,
                    RequestStatus.Resolved,
                    RequestStatus.Cancelled,
                ],
                [RequestStatus.Resolved] =
                [
                    RequestStatus.InProgress,
                    RequestStatus.Closed,
                ],
                [RequestStatus.Closed] = [],
                [RequestStatus.Cancelled] = [],
            };
}
```

Do not invent business behavior when requirements are unclear.

Use the simplest behavior consistent with the existing requirements and document any necessary assumption.

---

# Error Handling

## Problem Details

Use RFC-compatible `ProblemDetails` responses for API errors.

Expected response example:

```json
{
  "type": "https://example.com/problems/invalid-status-transition",
  "title": "Invalid request status transition",
  "status": 409,
  "detail": "A closed request cannot be moved back to InProgress.",
  "instance": "/api/requests/42/status",
  "traceId": "00-example"
}
```

Use centralized exception handling through `IExceptionHandler` or middleware.

Do not place repetitive `try/catch` blocks in every controller.

Expected domain exceptions may include:

```csharp
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }
}

public sealed class RequestNotFoundException : DomainException
{
    public RequestNotFoundException(int requestId)
        : base($"Service request {requestId} was not found.")
    {
    }
}

public sealed class InvalidRequestStatusTransitionException : DomainException
{
    public InvalidRequestStatusTransitionException(
        RequestStatus currentStatus,
        RequestStatus requestedStatus)
        : base(
            $"Request status cannot change from " +
            $"{currentStatus} to {requestedStatus}.")
    {
    }
}
```

Rules:

* Catch exceptions only when they can be handled meaningfully.
* Do not catch `Exception` and silently continue.
* Do not expose stack traces or internal implementation details to clients.
* Log unexpected exceptions.
* Map expected domain exceptions to appropriate status codes.
* Keep user-facing error messages clear and non-technical.
* Include a trace or correlation identifier for unexpected errors.

---

# Logging

Use `ILogger<T>`.

Prefer structured logging with named properties.

Good:

```csharp
_logger.LogInformation(
    "Service request {RequestId} assigned to agent {AgentId} by user {ActorId}",
    requestId,
    agentId,
    actorId);
```

Avoid:

```csharp
_logger.LogInformation(
    $"Service request {requestId} assigned to {agentId}");
```

Do not log:

* passwords;
* password hashes;
* JWTs;
* refresh tokens;
* authorization headers;
* connection strings;
* secrets;
* full sensitive request bodies;
* personal data unless necessary and approved.

Use appropriate levels:

* `Trace`: extremely detailed development information;
* `Debug`: diagnostic information;
* `Information`: meaningful application events;
* `Warning`: expected but problematic situations;
* `Error`: failed operations;
* `Critical`: application-level failure.

Do not log every method entry and exit by default.

Log meaningful events and failures.

---

# Configuration Management

Use configuration providers and strongly typed options.

Example:

```csharp
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SigningKey { get; init; }

    public int ExpirationMinutes { get; init; }
}
```

Register and validate configuration:

```csharp
services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Rules:

* Never commit real secrets.
* Use environment variables or user secrets for local secret values.
* Keep safe defaults in `appsettings.json`.
* Use `appsettings.Development.json` only for non-sensitive development settings.
* Do not hardcode API URLs in Angular services.
* Use Angular environment configuration or an injected API configuration.
* Validate required configuration during startup.
* Fail startup when critical configuration is missing.

Local secrets example:

```powershell
dotnet user-secrets init --project src/ServiceRequest.Api

dotnet user-secrets set `
  "Jwt:SigningKey" `
  "development-only-secret" `
  --project src/ServiceRequest.Api
```

Do not place production secrets in documentation or example files.

---

# Authentication and Authorization

## Authentication

Use ASP.NET Core authentication.

Passwords must be hashed using a proven implementation such as ASP.NET Core Identity.

Never:

* store plain-text passwords;
* implement a custom password hashing algorithm;
* return password hashes through the API;
* log credentials;
* place credentials in source control.

## JWT Handling

JWTs must have:

* a valid issuer;
* a valid audience;
* a short expiration;
* a strong signing key;
* validated signature;
* validated expiration;
* required user and role claims.

Do not store long-lived JWTs in browser `localStorage`.

Preferred browser authentication approach:

* use a secure `HttpOnly` cookie;
* enable `Secure` outside local HTTP development;
* use an appropriate `SameSite` policy;
* add CSRF protection when cookie-based authentication requires it.

Do not claim that authentication is production-ready when development-only compromises remain.

## Authorization

Frontend role checks are for user experience only.

The API must enforce every permission independently.

Use policies when authorization depends on more than one role.

Example:

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy(
        "CanManageRequests",
        policy => policy.RequireRole("SupportAgent", "Admin"));
});
```

Prefer resource-based authorization when access depends on the specific request owner or assignee.

Example rule:

```text
Employee:
    Can read own requests.
    Cannot read another employee's requests.

SupportAgent:
    Can read requests available to support staff.
    Can update supported fields.

Admin:
    Can manage users, categories, and all requests.
```

Do not return sensitive resource details before authorization has succeeded.

---

# CORS and Browser Security

* Allow only required frontend origins.
* Do not use `AllowAnyOrigin` together with credentials.
* Do not expose unnecessary headers.
* Use HTTPS outside local development.
* Configure cookies securely.
* Treat all browser input as untrusted.
* Encode displayed user content.
* Do not render untrusted HTML with `innerHTML`.
* Avoid bypassing Angular sanitization.
* Add security headers in deployment configuration where appropriate.

---

# Testing Strategy

## Development Cycle

Use test-first development for important business rules and bug fixes.

Recommended cycle:

1. Define expected behavior.
2. Write or update the test.
3. Confirm the test fails for the correct reason.
4. Implement the smallest correct change.
5. Confirm the test passes.
6. Refactor without changing behavior.
7. Run the relevant broader test suite.

Do not mechanically write tests before trivial configuration or generated boilerplate when the test provides no value.

## Backend Unit Tests

Use xUnit.

Unit-test:

* status transitions;
* permission decisions;
* validation rules;
* assignment rules;
* domain methods;
* application services;
* mapping with non-trivial behavior;
* error conditions.

Use descriptive names:

```csharp
[Fact]
public async Task AssignAsync_WhenAgentIsInactive_ThrowsConflictException()
{
    // Arrange
    // Act
    // Assert
}
```

Preferred naming format:

```text
Method_WhenCondition_ExpectedResult
```

Do not test private methods directly.

Test public behavior.

Use mocks only for real boundaries.

Do not mock EF Core extensively when an integration test would provide more confidence.

## Backend Integration Tests

Use `WebApplicationFactory` for API integration tests.

Integration tests must cover:

* endpoint routing;
* authentication;
* authorization;
* model validation;
* database persistence;
* response status codes;
* `ProblemDetails` responses;
* important complete request workflows.

Use an isolated test database.

Tests must not depend on execution order.

Tests must clean up or recreate state predictably.

## Angular Tests

Test:

* services and API interactions;
* route guards;
* interceptors;
* form validation;
* role-dependent UI;
* loading states;
* error states;
* critical component interactions.

Avoid brittle tests based on implementation details.

Test user-visible behavior.

Use semantic selectors where possible.

## End-to-End Tests

Add end-to-end tests only for critical workflows when the project reaches a stable stage.

Candidate workflows:

* employee logs in and creates a request;
* support agent assigns the request;
* support agent changes request status;
* employee adds a comment;
* employee closes a resolved request;
* unauthorized user is prevented from accessing restricted pages.

Do not add a large E2E suite before core application behavior is stable.

## Coverage

Aim for strong coverage of critical behavior rather than maximizing a percentage.

Target at least 80% coverage for:

* domain rules;
* application services;
* authorization logic;
* validators;
* important frontend services.

A feature is not complete when its critical success and failure paths are untested.

---

# Test Data and Seed Data

Development seed data must be deterministic.

Include only fictional test data.

Provide documented development accounts for each role.

Example:

```text
Admin
Username: admin
Password: development-only password

Support Agent
Username: agent
Password: development-only password

Employee
Username: employee
Password: development-only password
```

Do not reuse development credentials in production.

Seed logic must not overwrite existing user data on every application start.

Password values must be hashed using the same secure mechanism used by the application.

---

# Git Workflow

## Branch Strategy

Use GitHub Flow.

```text
main
└── feature/*
└── fix/*
└── refactor/*
└── test/*
└── docs/*
└── chore/*
```

`main` must remain buildable and ready to run.

Daily workflow:

```powershell
git checkout main
git pull origin main
git checkout -b feature/request-creation
```

After implementation:

```powershell
dotnet format --verify-no-changes
dotnet build
dotnet test

cd service-request-client
npm run build
npx ng test --watch=false
```

Then:

```powershell
git status
git diff
git add <specific-files>
git commit
git push origin feature/request-creation
```

Create a pull request and merge only after required checks pass.

## Commit Format

Never include references such as:

* generated by Claude;
* written by Claude;
* Claude Code;
* AI-generated;
* co-authored by Claude.

Format:

```text
<type>(<scope>): <subject>
```

Types:

```text
feat
fix
docs
style
refactor
test
chore
build
ci
```

Examples:

```text
feat(requests): add request creation endpoint
fix(auth): prevent employees from reading other users requests
test(requests): cover invalid status transitions
refactor(categories): extract category validation service
docs(readme): add local setup instructions
```

Rules:

* Use the imperative mood.
* Keep the subject concise.
* Do not end the subject with a period.
* One commit should represent one coherent change.
* Do not mix unrelated refactoring with a feature.
* Do not commit broken code.
* Do not commit generated build output.
* Review staged files before committing.

Do not execute commits, pushes, merges, rebases, resets, or destructive Git commands unless explicitly requested.

---

# Documentation Standards

## README

Keep `README.md` updated.

It must include:

* project purpose;
* technology stack;
* architecture summary;
* prerequisites;
* backend setup;
* frontend setup;
* database setup;
* migration commands;
* environment configuration;
* development accounts;
* application URLs;
* test commands;
* role permissions;
* known limitations;
* security notes.

A new developer must be able to run the project using only the repository and README.

## API Documentation

Use Swagger / OpenAPI.

Document:

* endpoint purpose;
* authentication requirement;
* expected status codes;
* important validation rules;
* request and response schemas.

Do not overload controllers with large Swagger annotation blocks when conventions and response metadata can provide the same information clearly.

## Architectural Decisions

Document important decisions when there is a real tradeoff.

Examples:

* authentication storage approach;
* status-transition policy;
* deletion versus archival;
* database provider choice;
* use or rejection of repository pattern.

Do not create architecture documents for trivial decisions.

## Comments

Comments must explain:

* why a workaround exists;
* why a business rule is required;
* why a non-obvious implementation was selected;
* what external constraint affects the code.

Do not leave commented-out code.

Use Git history instead.

---

# Performance Guidelines

Do not optimize without evidence.

First:

1. identify a slow operation;
2. measure it;
3. determine the cause;
4. make the smallest useful optimization;
5. measure again.

Backend:

* use database-side filtering;
* use projection;
* use pagination;
* avoid N+1 queries;
* use `AsNoTracking` for read-only queries;
* create indexes for measured query patterns;
* avoid loading full entities when only a summary is needed;
* do not cache mutable data without an invalidation strategy.

Frontend:

* lazy-load routes;
* avoid repeated API requests;
* use `trackBy` or Angular tracking syntax for lists;
* avoid expensive functions in templates;
* do not create unnecessary global state;
* unsubscribe from long-lived observables;
* use pagination for large tables.

Do not add Redis or another cache unless required by measured project needs.

---

# User Experience Requirements

The application is built for users, not only developers.

Every feature must consider:

* clear labels;
* understandable status names;
* actionable error messages;
* loading indicators;
* empty states;
* confirmation for destructive actions;
* visible success feedback;
* disabled states during processing;
* keyboard navigation;
* basic accessibility;
* responsive layout.

Do not expose implementation details such as:

```text
NullReferenceException
Foreign key constraint failed
Status enum value 3
UserId must not be null
```

Show meaningful messages:

```text
The selected category no longer exists.
This request has already been assigned.
You do not have permission to edit this request.
```

Human-visible numbering must begin at `1` unless zero-based numbering is meaningful to the user.

Do not show zero-based row, page, position, or index values simply because they are used internally.

---

# Project Launch Experience

The repository must be easy to start.

Provide a root-level development launcher when useful.

Possible Windows PowerShell script:

```text
start-dev.ps1
```

It may:

* verify required tools;
* restore backend packages;
* install frontend dependencies when missing;
* start the API;
* start the Angular client;
* print application URLs.

Do not create fragile launcher scripts that hide errors.

Scripts must stop and display a clear message when a required command fails.

The README must always include manual startup commands even when helper scripts exist.

---

# Dependency Rules

Before adding a dependency:

1. check whether the platform already provides the needed capability;
2. check whether an existing dependency already solves it;
3. confirm that the dependency is actively maintained;
4. add only the required package;
5. document why it is needed when the reason is not obvious.

Backend packages must be added using:

```powershell
dotnet add <project-path> package <package-name>
```

Frontend packages must be added using:

```powershell
npm install <package-name>
```

Do not:

* manually insert package references without need;
* update unrelated dependencies;
* replace established project tools during a feature task;
* add overlapping libraries for the same responsibility;
* add a full framework for one small helper function.

Commit lock-file changes when dependencies change.

---

# Search Command Requirements

Use `rg` — ripgrep — instead of traditional `grep` and `find` commands.

Do not use:

```bash
grep -r "pattern" .
find . -name "*.cs"
```

Use:

```bash
rg "pattern"
rg --files -g "*.cs"
rg --files -g "*.ts"
rg --files | rg "Request"
```

Before modifying a symbol, search for:

* its declaration;
* all references;
* related tests;
* related DTOs;
* related routes;
* related frontend usage.

Do not perform broad repository exploration when the task concerns a known and limited set of files.

---

# Claude CLI Working Rules

## Scope Control

Work only on the current requested task.

Do not:

* refactor unrelated code;
* rename unrelated files;
* update unrelated packages;
* reorganize the project without request;
* add speculative functionality;
* rewrite working code only because another style is preferred;
* create documentation unrelated to the change;
* modify formatting across the entire repository for a local feature.

Before editing:

1. inspect the relevant files;
2. identify existing patterns;
3. identify relevant tests;
4. make the smallest coherent change.

## File Safety

Never overwrite a file without first reading it.

Never assume:

* file paths;
* project names;
* namespaces;
* class names;
* ports;
* environment variables;
* database provider;
* existing architecture.

Verify them in the repository.

Do not create duplicate implementations because an existing one was not searched for.

## Command Safety

Do not execute destructive commands without explicit user approval.

This includes:

```text
git reset --hard
git clean -fd
git rebase
git push --force
drop database
delete migrations
remove directories recursively
overwrite environment files
```

Do not kill unrelated processes.

Do not modify global machine configuration.

## Efficient Tool Usage

Keep investigation focused.

Prefer:

```text
read relevant file
search direct references
inspect related test
make change
run targeted test
run broader validation
```

Avoid:

```text
scan the complete repository
open unrelated files
explain every trivial command
run the same test repeatedly
rebuild unchanged projects repeatedly
```

Run targeted tests first.

Run the complete relevant suite after the targeted tests pass.

## Verification

After backend changes, run the narrowest relevant commands first:

```powershell
dotnet test tests/ServiceRequest.UnitTests `
  --filter "FullyQualifiedName~RelevantTest"
```

Then, when appropriate:

```powershell
dotnet build
dotnet test
```

After frontend changes:

```powershell
npx ng test --watch=false --include="<relevant-test-pattern>"
```

Then, when appropriate:

```powershell
npm run build
npx ng test --watch=false
```

Do not claim that a command passed unless it was actually executed successfully.

Report commands that could not be executed and explain why.

## Output Style

When completing a coding task, report:

1. what changed;
2. which files changed;
3. which tests or checks were run;
4. whether they passed;
5. remaining limitations or follow-up work directly related to the task.

Keep explanations concise.

Do not provide long tutorials unless requested.

---

# Security Checklist

Before considering authentication or authorization work complete, verify:

* passwords are securely hashed;
* secrets are not committed;
* JWT validation checks issuer, audience, signature, and expiration;
* protected endpoints require authentication;
* role checks exist in the backend;
* resource ownership is enforced in the backend;
* frontend role checks are not treated as security;
* sensitive fields are absent from API responses;
* logs contain no credentials or tokens;
* CORS is restricted;
* production cookies use secure settings;
* error responses do not expose stack traces;
* database queries are parameterized through EF Core;
* user-provided HTML is not rendered unsafely;
* seed credentials are clearly development-only.

---

# Definition of Done

A feature is complete only when:

* behavior matches the documented requirement;
* naming is clear and consistent;
* business rules are implemented on the backend;
* authorization is enforced by the API;
* backend input is validated;
* frontend validation supports the user experience;
* success, loading, empty, and error states are handled;
* relevant unit tests pass;
* relevant integration tests pass where required;
* frontend tests pass where required;
* backend builds without errors;
* frontend production build succeeds;
* database migrations are included when the schema changed;
* Swagger reflects the endpoint;
* README is updated when setup or behavior changed;
* no secrets or generated build files were committed;
* no unrelated files were modified.

---

# Important Notes

* Never assume a path, class, API route, or configuration value when it can be verified.
* Do not ask for clarification for minor details that can be resolved from the existing repository.
* Ask only when a missing requirement materially changes the implementation.
* Prefer the smallest correct implementation.
* Preserve established project patterns unless they are clearly broken.
* Do not hide uncertainty.
* Do not state that work is complete without verification.
* Test critical success and failure paths.
* Keep `CLAUDE.md` updated when the project adopts a new lasting convention.
* Keep the application understandable for both developers and end users.
* Do not expose zero-based internal indexes as human-facing numbering.
* Do not use vague variable, property, method, or class names.
* Do not store production JWT credentials in browser `localStorage`.
* Do not place business logic inside controllers or Angular components.
* No feature is complete without appropriate tests.
