# DnnMigration

A complete architectural transformation of the legacy DotNetNuke 4.x CMS from VB.NET/.NET Framework 2.0 to a modern technology stack featuring C# 12/.NET 8 backend API and Angular 19 frontend SPA.

## Table of Contents

- [Project Overview](#project-overview)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Build Commands](#build-commands)
- [Test Commands](#test-commands)
- [Docker Deployment](#docker-deployment)
- [API Documentation](#api-documentation)
- [Health Check Endpoints](#health-check-endpoints)
- [Contributing](#contributing)
- [License](#license)

## Project Overview

DnnMigration is a ground-up rewrite of the DotNetNuke 4.x Content Management System. The original system was built using:

- **Language:** VB.NET
- **Framework:** .NET Framework 2.0
- **UI Architecture:** ASP.NET WebForms with ASPX/ASCX pages
- **Data Access:** ADO.NET with SqlDataProvider and stored procedures
- **Authentication:** Forms Authentication
- **Deployment:** Windows IIS

The migration transforms this to a modern, cloud-native architecture:

- **Backend Language:** C# 12 with nullable reference types
- **Backend Framework:** .NET 8 LTS with ASP.NET Core 8 Web API
- **Frontend Framework:** Angular 19 Single Page Application with standalone components
- **Data Access:** Entity Framework Core 8 with LINQ queries
- **Authentication:** JWT Bearer tokens with BFF (Backend-for-Frontend) pattern
- **Deployment:** Docker containers targeting Linux (Alpine-based images)

### Key Features Migrated

| Domain | Description |
|--------|-------------|
| Portal Management | Multi-tenant site configuration and administration |
| Module System | Pluggable content components and module lifecycle |
| User Management | User CRUD, profiles, and membership |
| Role Management | Permission grouping and role-based access control |
| Tab/Page Navigation | Hierarchical page structure and navigation |
| Permissions | Fine-grained module, tab, and folder permissions |

### Migration Goals

- **Functional Parity:** All Portal, Module, User, Role, and Tab CRUD operations preserved
- **Multi-tenant Support:** Portal isolation semantics maintained
- **Modern APIs:** RESTful JSON API replacing WebForms postback
- **Type Safety:** Strongly-typed C# with nullable reference types
- **Testability:** Comprehensive unit and integration test coverage
- **Container-Ready:** Linux deployment via Docker

## Architecture

The project follows a Clean Architecture pattern with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────────┐
│                      Client Browser                              │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│               Angular 19 SPA (nginx container)                   │
│     Standalone Components | Signals | Reactive Forms | Router    │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ HTTP /api/*
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│              ASP.NET Core 8 BFF API (Kestrel container)         │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  Controllers (API Layer)                                 │    │
│  │  PortalsController | ModulesController | UsersController │    │
│  └───────────────────────────┬─────────────────────────────┘    │
│                              │                                   │
│  ┌───────────────────────────▼─────────────────────────────┐    │
│  │  Services (Application Layer)                            │    │
│  │  PortalService | ModuleService | UserService             │    │
│  └───────────────────────────┬─────────────────────────────┘    │
│                              │                                   │
│  ┌───────────────────────────▼─────────────────────────────┐    │
│  │  Repositories (Infrastructure Layer)                     │    │
│  │  PortalRepository | ModuleRepository | UserRepository    │    │
│  └───────────────────────────┬─────────────────────────────┘    │
│                              │                                   │
│  ┌───────────────────────────▼─────────────────────────────┐    │
│  │  EF Core DbContext                                       │    │
│  │  Entity Configurations | Migrations                      │    │
│  └───────────────────────────┬─────────────────────────────┘    │
└──────────────────────────────┼──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                   SQL Server Database                            │
│               (Existing Schema Preserved)                        │
└─────────────────────────────────────────────────────────────────┘
```

### Backend Layers

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Domain | `DnnMigration.Domain` | Entities, interfaces, enums |
| Application | `DnnMigration.Application` | Services, DTOs, mapping |
| Infrastructure | `DnnMigration.Infrastructure` | EF Core, repositories, identity |
| API | `DnnMigration.Api` | Controllers, middleware |

### Frontend Architecture

The Angular 19 frontend uses:

- **Standalone Components:** No NgModules, direct imports
- **Feature-based Organization:** portal/, module/, user/, role/ features
- **Signals:** Reactive state management with `signal()` and `computed()`
- **Control Flow Syntax:** `@if`, `@for`, `@switch` template directives
- **Lazy Loading:** Route-based code splitting with `loadComponent()`
- **Reactive Forms:** Typed `FormGroup<T>` for form handling

## Prerequisites

Ensure you have the following installed before proceeding:

| Tool | Minimum Version | Download |
|------|-----------------|----------|
| .NET SDK | 8.0.x LTS | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Node.js | 20.x LTS | [Download](https://nodejs.org/) |
| npm | 10.x | Included with Node.js |
| Angular CLI | 19.x | `npm install -g @angular/cli@19` |
| Docker | 24.x | [Download](https://www.docker.com/get-started) |
| Docker Compose | 2.x | Included with Docker Desktop |
| SQL Server | 2019+ | [Download](https://www.microsoft.com/sql-server) |

### Verify Installation

```bash
# Verify .NET SDK
dotnet --version
# Expected: 8.0.x

# Verify Node.js
node --version
# Expected: v20.x.x

# Verify npm
npm --version
# Expected: 10.x.x

# Verify Angular CLI
ng version
# Expected: Angular CLI: 19.x.x

# Verify Docker
docker --version
# Expected: Docker version 24.x.x

# Verify Docker Compose
docker compose version
# Expected: Docker Compose version v2.x.x
```

## Project Structure

```
DnnMigration/
├── backend/                          # .NET 8 Backend Solution
│   ├── src/
│   │   ├── DnnMigration.Domain/      # Domain Layer
│   │   │   ├── Entities/             # Portal, Module, User, Role, Tab, etc.
│   │   │   ├── Interfaces/           # Repository interfaces
│   │   │   └── Enums/                # Domain enumerations
│   │   │
│   │   ├── DnnMigration.Application/ # Application Layer
│   │   │   ├── Services/             # Business logic services
│   │   │   ├── DTOs/                 # Data transfer objects
│   │   │   ├── Interfaces/           # Service interfaces
│   │   │   └── Mapping/              # AutoMapper profiles
│   │   │
│   │   ├── DnnMigration.Infrastructure/ # Infrastructure Layer
│   │   │   ├── Data/                 # DbContext and configurations
│   │   │   ├── Repositories/         # Repository implementations
│   │   │   └── Identity/             # JWT and password services
│   │   │
│   │   └── DnnMigration.Api/         # API Layer (Entry Point)
│   │       ├── Controllers/          # REST API controllers
│   │       ├── Middleware/           # Exception handling, logging
│   │       ├── Program.cs            # Application configuration
│   │       └── appsettings.json      # Configuration files
│   │
│   ├── tests/
│   │   ├── DnnMigration.UnitTests/   # Unit tests
│   │   └── DnnMigration.IntegrationTests/ # Integration tests
│   │
│   └── DnnMigration.sln              # Solution file
│
├── frontend/                          # Angular 19 Frontend
│   ├── src/
│   │   ├── app/
│   │   │   ├── core/                 # Auth, services, models
│   │   │   ├── shared/               # Reusable components, pipes
│   │   │   ├── features/             # Feature modules
│   │   │   │   ├── portal/           # Portal management
│   │   │   │   ├── module/           # Module management
│   │   │   │   ├── user/             # User management
│   │   │   │   ├── role/             # Role management
│   │   │   │   └── auth/             # Authentication
│   │   │   ├── layout/               # Header, sidebar, footer
│   │   │   ├── app.component.ts      # Root component
│   │   │   ├── app.config.ts         # Application configuration
│   │   │   └── app.routes.ts         # Route definitions
│   │   │
│   │   ├── environments/             # Environment configurations
│   │   ├── index.html                # HTML entry point
│   │   ├── main.ts                   # Bootstrap entry point
│   │   └── styles.scss               # Global styles
│   │
│   ├── angular.json                  # Angular CLI configuration
│   ├── package.json                  # npm dependencies
│   └── tsconfig.json                 # TypeScript configuration
│
├── docker/                            # Docker Configuration
│   ├── api.Dockerfile                # Backend container build
│   ├── frontend.Dockerfile           # Frontend container build
│   ├── nginx.conf                    # nginx SPA routing + API proxy
│   └── docker-compose.yml            # Container orchestration
│
├── README.md                          # This file
└── MIGRATION_NOTES.md                 # Migration decisions and patterns
```

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/your-org/dnn-migration.git
cd dnn-migration
```

### 2. Configure the Database

Create a SQL Server database and update the connection string in `backend/src/DnnMigration.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=DotNetNuke;User Id=sa;Password=YourPassword;TrustServerCertificate=true"
  }
}
```

### 3. Apply Database Migrations

```bash
cd backend/src/DnnMigration.Api
dotnet ef database update
```

### 4. Run the Backend API

```bash
cd backend/src/DnnMigration.Api
dotnet run
```

The API will be available at `https://localhost:5001` and `http://localhost:5000`.

### 5. Run the Frontend

```bash
cd frontend
npm install
ng serve
```

The frontend will be available at `http://localhost:4200`.

## Development Setup

### Backend Development

```bash
# Navigate to backend solution
cd backend

# Restore NuGet packages
dotnet restore

# Build all projects
dotnet build

# Run the API with hot reload
cd src/DnnMigration.Api
dotnet watch run
```

### Frontend Development

```bash
# Navigate to frontend
cd frontend

# Install npm packages
npm install

# Start development server with hot reload
ng serve

# Open browser at http://localhost:4200
```

### IDE Recommendations

| IDE | Extension/Plugin |
|-----|-----------------|
| Visual Studio 2022 | C# Dev Kit, EditorConfig |
| VS Code | C# Dev Kit, Angular Language Service, ESLint |
| JetBrains Rider | Built-in .NET and Angular support |

## Build Commands

### Backend Build

```bash
# Navigate to backend
cd backend

# Debug build
dotnet build

# Release build
dotnet build --configuration Release

# Release build with warnings as errors (CI)
dotnet build --configuration Release --warnaserror

# Publish for deployment
dotnet publish --configuration Release --output ./publish
```

### Frontend Build

```bash
# Navigate to frontend
cd frontend

# Development build
ng build

# Production build (optimized, tree-shaking, AOT)
ng build --configuration production

# Production build with source maps for debugging
ng build --configuration production --source-map
```

### Docker Build

```bash
# Navigate to docker folder
cd docker

# Build all containers
docker-compose build

# Build individual containers
docker build -f api.Dockerfile -t dnn-migration-api ../backend
docker build -f frontend.Dockerfile -t dnn-migration-frontend ../frontend

# Build with no cache (fresh build)
docker-compose build --no-cache
```

## Test Commands

### Backend Tests

```bash
# Navigate to backend
cd backend

# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/DnnMigration.UnitTests

# Run tests with filter
dotnet test --filter "FullyQualifiedName~PortalService"

# CI mode (fail on any error)
dotnet test --configuration Release
```

### Frontend Tests

```bash
# Navigate to frontend
cd frontend

# Run tests once (CI mode)
ng test --watch=false --browsers=ChromeHeadless

# Run tests with code coverage
ng test --watch=false --code-coverage --browsers=ChromeHeadless

# Run tests in watch mode (development)
ng test

# Run specific test file
ng test --include="**/portal.service.spec.ts"
```

### End-to-End Tests (Optional)

```bash
# Using Cypress (if configured)
cd frontend
npx cypress run

# Using Playwright (if configured)
npx playwright test
```

## Docker Deployment

### Using Docker Compose

```bash
# Navigate to docker folder
cd docker

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down

# Stop and remove volumes
docker-compose down -v
```

### Container Access Points

| Service | URL | Description |
|---------|-----|-------------|
| Frontend (nginx) | http://localhost:8080 | Angular SPA |
| API | http://localhost:5000 | ASP.NET Core API |
| API (HTTPS) | https://localhost:5001 | Secure API endpoint |

### Environment Variables

The following environment variables can be configured in `docker-compose.yml`:

```yaml
services:
  api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Default=Server=db;Database=DotNetNuke;...
      - Jwt__Secret=your-256-bit-secret-key
      - Jwt__Issuer=DnnMigration
      - Jwt__Audience=DnnMigration
      - Jwt__ExpirationMinutes=60
```

## API Documentation

### Swagger/OpenAPI

When running in Development mode, Swagger UI is available at:

- **Swagger UI:** `https://localhost:5001/swagger`
- **OpenAPI JSON:** `https://localhost:5001/swagger/v1/swagger.json`

### API Endpoints Overview

#### Portal Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/portals` | List all portals (paginated) |
| GET | `/api/portals/{id}` | Get portal by ID |
| POST | `/api/portals` | Create new portal |
| PUT | `/api/portals/{id}` | Update existing portal |
| DELETE | `/api/portals/{id}` | Delete portal |

#### Module Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/modules` | List all modules (paginated) |
| GET | `/api/modules/{id}` | Get module by ID |
| POST | `/api/modules` | Create new module |
| PUT | `/api/modules/{id}` | Update existing module |
| DELETE | `/api/modules/{id}` | Delete module |

#### User Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users` | List all users (paginated) |
| GET | `/api/users/{id}` | Get user by ID |
| POST | `/api/users` | Create new user |
| PUT | `/api/users/{id}` | Update existing user |
| DELETE | `/api/users/{id}` | Delete user |

#### Role Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/roles` | List all roles |
| GET | `/api/roles/{id}` | Get role by ID |
| POST | `/api/roles` | Create new role |
| PUT | `/api/roles/{id}` | Update existing role |
| DELETE | `/api/roles/{id}` | Delete role |

#### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | Authenticate and receive JWT |
| POST | `/api/auth/refresh` | Refresh access token |
| POST | `/api/auth/logout` | Logout and invalidate token |
| GET | `/api/auth/me` | Get current authenticated user |

## Health Check Endpoints

The API provides health check endpoints for monitoring and container orchestration:

### Basic Health Check

```bash
# Check API health
curl -f http://localhost:5000/health

# Expected Response (HTTP 200 OK):
{
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Detailed Health Check

```bash
# Check detailed health with dependencies
curl -f http://localhost:5000/health/ready

# Expected Response (HTTP 200 OK):
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "memory": "Healthy"
  },
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Liveness vs Readiness

| Endpoint | Purpose | Usage |
|----------|---------|-------|
| `GET /health` | Basic liveness check | Kubernetes liveness probe |
| `GET /health/ready` | Readiness with dependencies | Kubernetes readiness probe |

### Docker Health Check Configuration

The `api.Dockerfile` includes a health check:

```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
```

### Using Health Checks in Docker Compose

```yaml
services:
  api:
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s
```

## Contributing

### Code Style

- **Backend:** Follow Microsoft C# coding conventions
- **Frontend:** Follow Angular style guide and use ESLint

### Branch Naming

- `feature/` - New features
- `bugfix/` - Bug fixes
- `hotfix/` - Production hotfixes

### Commit Messages

Follow conventional commits:

```
feat: add portal creation endpoint
fix: resolve user deletion cascade issue
docs: update API documentation
test: add user service unit tests
```

### Pull Request Process

1. Create feature branch from `main`
2. Implement changes with tests
3. Ensure all tests pass: `dotnet test` and `ng test --watch=false`
4. Ensure builds succeed: `dotnet build --warnaserror` and `ng build --configuration production`
5. Submit PR for review

## License

This project is licensed under the MIT License. See the LICENSE file for details.

---

## Quick Reference

### Common Commands

```bash
# Backend
cd backend
dotnet build                              # Build
dotnet test                               # Test
dotnet run --project src/DnnMigration.Api # Run

# Frontend
cd frontend
npm install                               # Install dependencies
ng serve                                  # Development server
ng build --configuration production       # Production build
ng test --watch=false                     # Run tests

# Docker
cd docker
docker-compose build                      # Build containers
docker-compose up -d                      # Start services
docker-compose down                       # Stop services
curl -f http://localhost:8080/health      # Health check
```

### Useful URLs (Development)

| Service | URL |
|---------|-----|
| Frontend | http://localhost:4200 |
| API | https://localhost:5001 |
| Swagger | https://localhost:5001/swagger |
| Health | http://localhost:5000/health |
