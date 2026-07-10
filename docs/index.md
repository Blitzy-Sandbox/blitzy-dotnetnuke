# blitzy-dotnetnuke

A complete migration of the legacy DotNetNuke 4.x portal framework (VB.NET on .NET Framework 2.0) to a modern, containerized two-tier application: a C# 12 / .NET 8 LTS ASP.NET Core Web API following the Backend-for-Frontend (BFF) pattern and an Angular 19 single-page application (SPA). Data access is modernized to Entity Framework Core 8 mapped to the existing database schema, with Docker multi-container deployment.

## Documentation

- [Project Guide](project-guide.md) — operational setup, deployment, and delivery-status guide.
- [Technical Specifications](technical-specifications.md) — architecture contract and migration design.
