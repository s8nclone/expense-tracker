# Expense Tracker API

A RESTful Expense Tracker API built with .NET 10 Minimal APIs, Entity Framework Core, and SQLite. Features includes clean REST conventions, Data Annotations validation, filtering/sorting/pagination, summary reporting, Swagger UI, and automated integration tests.

## Tech Stack

- **Runtime / Framework:** .NET 10, ASP.NET Core Minimal APIs
- **Persistence:** Entity Framework Core 10 + SQLite
- **Validation & Error Handling:** Data Annotations (`AddValidation()`), RFC 9457 `ProblemDetails` / `ValidationProblemDetails`, `IExceptionHandler`
- **API Docs:** Built-in OpenAPI (`/openapi/v1.json`) + Swagger UI (`/swagger`)
- **Testing:** xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory` with isolated in-memory SQLite)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`10.0.x`)

### 1. Restore packages & local tools

```powershell
dotnet restore
dotnet tool restore
```

### 2. Apply database migrations

```powershell
dotnet ef database update --project src/ExpenseTracker.Api
```

This creates the local `expenses.db` SQLite database in `src/ExpenseTracker.Api/`.

### 3. Run the API

```powershell
dotnet run --project src/ExpenseTracker.Api
```

- **Base URL:** `http://localhost:5296` (or `https://localhost:7207` with `--launch-profile https`)
- **Swagger UI (Development):** `http://localhost:5296/swagger`
- **OpenAPI JSON (Development):** `http://localhost:5296/openapi/v1.json`

## API Endpoints

| Method | Route | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/expenses` | List expenses with filtering, sorting, and pagination | `200`, `400` |
| `GET` | `/api/expenses/{id}` | Get a single expense by ID | `200`, `404` |
| `POST` | `/api/expenses` | Create a new expense (returns `Location` header) | `201`, `400` |
| `PUT` | `/api/expenses/{id}` | Update an existing expense | `204`, `400`, `404` |
| `DELETE` | `/api/expenses/{id}` | Delete an expense | `204`, `404` |
| `GET` | `/api/expenses/summary` | Totals grouped by category and month (`yyyy-MM`) | `200`, `400` |

### Query Parameters for `GET /api/expenses`

- `category` — Case-insensitive exact match (e.g. `Food`)
- `fromDate` / `toDate` — Inclusive date range (`YYYY-MM-DD`)
- `sortBy` — `date` *(default)*, `amount`, `category`, or `title`
- `sortOrder` — `desc` *(default)* or `asc`
- `page` — Page number, `>= 1` *(default `1`)*
- `pageSize` — Items per page, `1–100` *(default `20`)*

### Example Request (`POST /api/expenses`)

```json
{
  "title": "Coffee",
  "description": "Morning espresso at local cafe",
  "amount": 4.75,
  "category": "Food",
  "date": "2026-11-23"
}
```

Validation errors return RFC 9457 `application/problem+json` (`400 Bad Request`) with per-field error messages.

## Running Tests

```powershell
dotnet test
```

## Deployment (Docker & Render)

The repository includes a multi-stage [Dockerfile](Dockerfile) and a [render.yaml](render.yaml) Blueprint configured for Render's free tier:

### Deploy to Render
Push this repository to GitHub.

### Run locally with Docker
```powershell
docker build -t expense-tracker-api .
docker run -p 8080:8080 expense-tracker-api
```
Then open `http://localhost:8080/swagger`.
