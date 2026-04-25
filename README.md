# Enterprise Ad-Hoc Reporting Module

Standalone enterprise-grade ad-hoc reporting module with independent auth, RBAC, datasource governance, report builder, report execution, and export support.

## Tech Stack

- Backend: ASP.NET Core Web API (net9.0 in this environment, net10-ready structure)
- Frontend: Next.js (App Router, TypeScript)
- Database: SQL Server
- Auth: JWT + Refresh Tokens
- Authorization: Role + permission claims and policy checks
- Architecture: Clean/Layered (Domain, Application, Infrastructure, API)

## Solution Structure

- AdHocReporting.Domain: entities, enums, and core domain model.
- AdHocReporting.Application: DTOs, service interfaces, validation.
- AdHocReporting.Infrastructure: EF Core persistence, auth/security, business services, exception middleware.
- AdHocReporting.API: controllers, API composition root, Swagger.
- AdHocReporting.Tests: unit tests for critical security behavior.
- ad-hoc-frontend: Next.js module UI.
- database: SQL schema, constraints, indexes, seed data, dummy tables/data.

## Backend Setup

1. Configure SQL Server in AdHocReporting.API/appsettings.json.
2. Update Jwt:Key to a secure key (32+ chars).
3. Run API:
   - dotnet restore
   - dotnet build AdHocReporting.sln
   - dotnet run --project AdHocReporting.API
4. API Swagger:
   - https://localhost:5001/swagger (or configured port)

Default seeded user in EF seed and SQL scripts:

- Username: admin
- Email: admin@adhoc.local
- Password: Admin@12345

## Frontend Setup

1. Configure API URL:
   - NEXT_PUBLIC_API_BASE_URL=https://localhost:5001/api
2. Run frontend:
   - cd ad-hoc-frontend
   - npm install
   - npm run dev
3. Open:
   - http://localhost:3000/login

## Database Setup

Execute:

- database/01_schema.sql

This script creates:

- Full module schema with keys, indexes, constraints
- Seed data for roles, permissions, admin user
- Dummy reporting tables and sample data
- A view and stored procedure for safe report testing

## Key Security Controls Implemented

- JWT access token + refresh token flow.
- Password hashing with BCrypt.
- Role and permission claims in token.
- Policy-based API authorization.
- Datasource visibility enforced server-side.
- Datasource execution restricted to allowed users/roles.
- SQL definition safety validator blocks destructive SQL and multi-statement payloads.
- Centralized exception handling with normalized response shape.
- Audit logging service and audit endpoint.

## Core API Areas

- Auth: /api/auth/login, /api/auth/refresh, /api/auth/change-password, /api/auth/reset-password
- Users: /api/users
- Datasources: /api/datasources/allowed, /api/datasources, /api/datasources/validate, /api/datasources/run
- Reports: /api/reports/my, /api/reports/shared, /api/reports, /api/reports/run
- Exports: /api/exports/pdf/{reportId}, /api/exports/excel/{reportId}
- Admin: /api/admin/roles, /api/admin/permissions, /api/admin/audit-logs

## Sample API Usage

### Login

POST /api/auth/login

{
  "usernameOrEmail": "admin",
  "password": "Admin@12345"
}

### Create Datasource (Admin/IT)

POST /api/datasources
Authorization: Bearer <token>

{
  "name": "Employee View",
  "code": "DS_EMP_VIEW",
  "description": "Safe employee listing",
  "datasourceType": 2,
  "sqlDefinitionOrObjectName": "vw_EmployeeList",
  "connectionName": "DefaultConnection",
  "parameters": [],
  "allowedColumns": [
    { "columnName": "EmployeeCode", "dataType": "string", "isAllowed": true },
    { "columnName": "FullName", "dataType": "string", "isAllowed": true }
  ]
}

### Run Report

POST /api/reports/run
Authorization: Bearer <token>

{
  "reportId": 1,
  "runtimeParameters": {},
  "pageNumber": 1,
  "pageSize": 100
}

### Export PDF

POST /api/exports/pdf/1
Authorization: Bearer <token>

{}

## Testing

Run:

- dotnet test AdHocReporting.Tests

Included tests:

- SQL safety validator rejects dangerous SQL
- Password hashing and verification behavior

## Notes for Production Hardening

- Move JWT key and connection strings to secure secret stores.
- Replace EnsureCreated with EF migrations strategy.
- Persist refresh tokens in hashed form.
- Add full request/response correlation IDs and structured audit payload redaction.
- Add comprehensive integration tests and API contract tests.
- Implement richer report builder model editor and server-side query projection constraints.

## .NET 10 Target

This machine has .NET 9 SDK installed. Code structure is net10-ready.
To move to .NET 10 later:

1. Install .NET 10 SDK.
2. Update TargetFramework to net10.0 across all projects.
3. Re-restore packages and rebuild.
