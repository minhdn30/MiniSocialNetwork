# CloudM Backend API

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![SignalR](https://img.shields.io/badge/Realtime-SignalR-F47C20)](https://learn.microsoft.com/aspnet/core/signalr/introduction)

CloudM is a social platform backend built with ASP.NET Core, PostgreSQL, Redis, and SignalR. It supports the full backend surface of a modern social product: authentication, profile management, posts, comments, follow privacy, stories, notifications, presence, and realtime messaging.

This repository is meant to represent practical backend engineering for product-scale features, not just isolated endpoint work. The key strength of the codebase is the way business logic, realtime behavior, infrastructure integration, and security-sensitive configuration are organized into a maintainable system.

## Table of Contents

- [Overview](#overview)
- [Product Scope](#product-scope)
- [Architecture](#architecture)
- [Request and Domain Flow](#request-and-domain-flow)
- [Realtime and Background Work](#realtime-and-background-work)
- [Security and Reliability](#security-and-reliability)
- [Technology Stack](#technology-stack)
- [Repository Structure](#repository-structure)
- [Testing](#testing)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Deployment](#deployment)
- [Engineering Notes](#engineering-notes)

## Overview

The backend is split into dedicated layers so that API concerns, application orchestration, domain rules, persistence, and tests evolve independently:

- `CloudM.API`
  API host, controllers, middleware, SignalR hubs, Swagger, startup composition
- `CloudM.Application`
  DTOs, business services, mapping, validation helpers, application-level orchestration
- `CloudM.Domain`
  entities, enums, exceptions, and core domain types
- `CloudM.Infrastructure`
  EF Core, repositories, migrations, Redis integration, Cloudinary, email delivery, and external infrastructure
- `CloudM.Tests`
  service-level and repository-level tests for critical behavior

The intent is simple: keep controllers thin, keep domain behavior explicit, and keep infrastructure details from leaking into core business logic.

## Product Scope

The repository currently covers the following product areas:

| Area | Scope |
| --- | --- |
| Authentication | Register, login, logout, refresh token, Google login, email verification, forgot password, password reset |
| Accounts | Profile details, account settings, status-aware flows, reactivation |
| Social Graph | Follow, unfollow, follow requests, privacy-aware relationship behavior |
| Content | Posts, comments, reactions, saves, post tagging |
| Stories | Story creation, viewers, reactions, archive, highlights |
| Messaging | Private chat, group chat, reactions, hidden messages, pinned messages, media flows |
| Notifications | Notification generation, unread state, outbox processing |
| Presence | Online presence snapshots and cleanup |
| Realtime | Chat updates, post activity updates, user-driven SignalR flows |

## Architecture

This codebase follows a layered service-and-repository style with a clean separation between runtime delivery and business orchestration.

### API Layer

The API project hosts:

- controllers for the main product domains
- global middleware for exception and account-status handling
- SignalR hubs for realtime communication
- JWT authentication and Swagger configuration
- startup wiring for repositories, services, background jobs, and configuration

Primary controllers:

- `AccountsController`
- `AuthsController`
- `CommentsController`
- `ConversationsController`
- `FollowsController`
- `MessagesController`
- `NotificationsController`
- `PostsController`
- `PresenceController`
- `StoriesController`

Realtime hubs:

- `ChatHub`
- `PostHub`
- `UserHub`

### Application Layer

The application layer is where most of the product behavior lives. It contains services for:

- authentication and external identity
- account and account settings
- follows and follow requests
- posts, post reactions, saves, and tagging
- comments and comment reactions
- conversations, members, messages, media, and pinned messages
- stories, story views, and story highlights
- notifications and realtime dispatching
- presence coordination

This is the layer that makes the repository feel like a product backend rather than a controller-driven demo.

### Infrastructure Layer

Infrastructure contains:

- EF Core `AppDbContext`
- repositories and unit-of-work style coordination
- PostgreSQL migrations
- Redis integration
- Cloudinary media services
- email services
- hosted workers for cleanup and asynchronous processing

## Request and Domain Flow

Most request flows follow this pattern:

1. A controller receives and validates input.
2. The controller delegates to an application service.
3. The application service enforces business rules and coordinates repositories.
4. Infrastructure persists state and resolves external integrations where needed.
5. Realtime or notification side effects are dispatched when required.
6. Middleware standardizes error handling before a response is returned.

This approach keeps request handling predictable and makes business behavior easier to test in isolation.

## Realtime and Background Work

Realtime is not bolted on as an afterthought. It is part of the product design.

- SignalR hubs are used for chat, post, and user-related event delivery.
- JWT bearer tokens are supported for hub connections through query-string access token handling.
- Realtime services are registered explicitly in startup and coordinated through application services.

The backend also includes hosted services for operational work:

- `EmailVerificationCleanupHostedService`
- `OnlinePresenceCleanupHostedService`
- `NotificationOutboxWorkerHostedService`
- `CloudinaryDeleteWorkerHostedService`

This combination of request/response APIs, SignalR, and background jobs is one of the stronger engineering signals in the repository.

## Security and Reliability

The repository includes several practical hardening choices:

- JWT authentication with issuer, audience, and signing-key validation
- refresh-token flows instead of access-token-only auth
- Google external authentication support
- email verification and password reset flows with OTP pepper support
- Redis-backed rate-limiting services for login and email verification paths
- fail-fast configuration for sensitive values such as DB connection, Redis, JWT key, and OTP pepper
- global exception middleware so the API does not leak raw server errors to clients
- environment-aware CORS behavior that supports controlled local development without public hard-coded fallback secrets

A recent cleanup also removed public fallback secrets and local-only committed config values from the repository.

## Technology Stack

- ASP.NET Core 8
- Entity Framework Core 8
- PostgreSQL
- Redis / StackExchange.Redis
- SignalR
- Cloudinary
- Swagger / OpenAPI
- xUnit
- Moq
- FluentAssertions

## Repository Structure

```text
CloudM/
|-- CloudM.API/
|-- CloudM.Application/
|-- CloudM.Domain/
|-- CloudM.Infrastructure/
|-- CloudM.Tests/
|-- CloudM.sln
`-- Dockerfile
```

## Testing

The test project currently includes:

- service tests for accounts, auth, comments, conversations, follows, posts, notifications, stories, and messaging
- repository tests for selected persistence behavior
- utility and parser tests where shared behavior matters

Representative test files:

- `AuthServiceTests.cs`
- `ConversationServiceTests.cs`
- `MessageServiceTests.cs`
- `NotificationServiceTests.cs`
- `StoryServiceTests.cs`
- `StoryHighlightServiceTests.cs`
- `PostServiceTests.cs`

Run tests with:

```bash
dotnet test CloudM.Tests/CloudM.Tests.csproj
```

Note: the test project currently targets .NET 9, while the runtime API targets .NET 8.

## Getting Started

### Prerequisites

- .NET SDK 9.x recommended
- PostgreSQL
- Redis

### Restore and Build

```bash
dotnet restore CloudM.sln
dotnet build CloudM.sln
```

### Run the API

```bash
dotnet run --project CloudM.API/CloudM.API.csproj
```

Swagger is available through the local launch profile defined in `CloudM.API/Properties/launchSettings.json`.

### Apply Database Migrations

```bash
dotnet ef database update --project CloudM.Infrastructure --startup-project CloudM.API
```

## Configuration

Do not put real secrets in `appsettings.json`.

For local development, use `CloudM.API/appsettings.Local.json`. A typical local setup looks like this:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=cloudm;Username=postgres;Password=your_password",
    "Redis": "localhost:6379,abortConnect=false"
  },
  "Jwt": {
    "Key": "replace-with-a-long-random-local-secret",
    "Issuer": "CloudM",
    "Audience": "CloudMClient"
  },
  "EmailVerification": {
    "OtpPepper": "replace-with-a-local-otp-pepper"
  },
  "ExternalAuth": {
    "Google": {
      "AllowedClientIds": [
        "your-google-client-id"
      ]
    }
  },
  "Cloudinary": {
    "CloudName": "",
    "ApiKey": "",
    "ApiSecret": ""
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5500",
      "http://127.0.0.1:5500"
    ]
  }
}
```

Sensitive configuration can also be supplied through environment variables:

- `ConnectionStrings__Default`
- `ConnectionStrings__Redis`
- `Redis__ConnectionString`
- `Jwt__Key`
- `EmailVerification__OtpPepper`
- `DATABASE_URL`
- `PORT`

## Deployment

The repository includes a root `Dockerfile` for containerized build and runtime packaging.

```bash
docker build -t cloudm-api .
docker run -p 10000:10000 cloudm-api
```

For deployed environments, configuration should come from secure environment-specific sources rather than checked-in files.

## Engineering Notes

This repository is intentionally product-oriented:

- business logic lives in services instead of controllers
- infrastructure is explicit rather than hidden behind magical abstractions
- realtime behavior is treated as a first-class architectural concern
- configuration is hardened to avoid unsafe public defaults
- the codebase is organized for ongoing feature growth rather than a one-off assignment

If you are reviewing this repository as part of my work, the strongest signal is the breadth of coherent backend engineering across authentication, social features, realtime communication, notifications, presence, media workflows, and operational reliability.
