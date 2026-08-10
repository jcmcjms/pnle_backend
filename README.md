# Microservice Practice — PNLE Exam Prep Platform

> A two-service practice project for PNLE (Philippine Nursing Licensure Exam) board-exam prep: a .NET 8 backend for authentication and API composition, and a Python FastAPI AI-tutoring microservice powered by LangChain + Groq.

This repository is a deliberate learning exercise in building, connecting, and documenting real microservices. It demonstrates clean architecture in two languages, Google OAuth with JWT refresh-token rotation, service-to-service API-key auth (planned), and AI-powered tutoring features.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Repository Structure](#repository-structure)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Authentication Flow](#authentication-flow)
- [Testing](#testing)
- [Current Status & Roadmap](#current-status--roadmap)
- [Design Decisions](#design-decisions)

---

## Overview

**What this is**: A microservices practice project implementing a PNLE board-exam prep platform. Students log in with Google, and an AI service generates study plans, produces practice questions, evaluates answers, and tracks readiness.

**Why it exists**: The project exists to practice — and document — the decisions behind a real-world microservice system:

- A **.NET 8 backend** (`Pnle.Api`) that owns identity: Google OAuth login, JWT access tokens, rotating refresh tokens in HttpOnly cookies, and health endpoints.
- A **Python FastAPI AI service** (`ai-service`) that owns AI tutoring: study plans, question generation, answer evaluation, weakness analysis, and readiness scoring, using LangChain with Groq (llama-3.1-70b-versatile).
- **Clean architecture in both services**, two PostgreSQL databases with clear ownership, and a planned service-to-service integration via an internal API key.

The backend-to-AI-service integration is **not wired yet** — both services currently run independently. This is a deliberate staging decision; see [Current Status & Roadmap](#current-status--roadmap).

---

## Architecture

```
                        ┌──────────────────────────────────────┐
                        │            Frontend                  │
                        │   React (localhost:5173)             │
                        │   Angular (localhost:4200)           │
                        └──────────────────┬───────────────────┘
                                           │ Google ID token (POST /auth/google)
                                           │ JWT bearer (GET /auth/me, ...)
                                           ▼
                        ┌──────────────────────────────────────────────────┐
                        │              Pnle.Api (backend)                  │
                        │   .NET 8 minimal API · Clean Architecture        │
                        │   Google OAuth → JWT + refresh cookie            │
                        │   Rate limiting · Swagger · ProblemDetails       │
                        └─────────┬──────────────────────────┬─────────────┘
                                  │ EF Core + Npgsql         │ planned call
                                  │                          │ (x-api-key header)
                                  ▼                          ▼
                        ┌────────────────────┐   ┌───────────────────────────────────┐
                        │  PostgreSQL        │   │    ai-service (FastAPI)           │
                        │  database: pnle    │   │    LangChain + Groq               │
                        └────────────────────┘   │    async SQLAlchemy + Alembic     │
                                                 └──────────────────┬────────────────┘
                                                                    │ Npgsql
                                                                    ▼
                                                 ┌───────────────────────────────────┐
                                                 │  PostgreSQL                       │
                                                 │  database: pnle_ai                │
                                                 └───────────────────────────────────┘
```

**Backend clean-architecture layers** (dependencies point inward; `Pnle.Domain` depends on nothing):

```
        ┌────────────────┐
        │   Pnle.Api     │  composition root: DI, middleware, endpoints
        └───────┬────────┘
       ┌────────┴─────────┐
       ▼                  ▼
┌──────────────┐  ┌──────────────────────┐
│ Pnle.Application │  │ Pnle.Infrastructure │  EF Core + Npgsql,
└──────┬───────┘  │ Google.Apis.Auth, JWT │
       │          └──────────┬───────────┘
       ▼                     ▼
┌────────────────────────────────────────┐
│            Pnle.Domain                 │  User, RefreshToken, TopicScore
└────────────────────────────────────────┘
```

**ai-service clean-architecture layers**:

```
        ┌────────────────┐
        │     api/       │  routes, schemas, security, DI
        └───────┬────────┘
                ▼
        ┌────────────────┐
        │ application/   │  use cases, protocol definitions
        └───────┬────────┘
                ▼
        ┌────────────────┐
        │    domain/     │  pure models + rules (WeaknessAnalyzer)
        └───────┬────────┘
                ▼
        ┌────────────────┐
        │ infrastructure/ │  Groq AI gateway, async SQLAlchemy
        └────────────────┘
```

---

## Repository Structure

| Path | Description |
|------|-------------|
| `backend/` | .NET 8 solution (`PnleBackend.slnx`) — the auth/API backend |
| `backend/Pnle.Api` | ASP.NET Core minimal API host: composition root, Swagger, auth endpoints |
| `backend/Pnle.Application` | Application layer: use cases, `Result<T>`/`Error` pattern |
| `backend/Pnle.Domain` | Domain layer: `User`, `RefreshToken`, `TopicScore`; zero dependencies |
| `backend/Pnle.Infrastructure` | Infrastructure: EF Core + Npgsql, Google.Apis.Auth, JWT services |
| `ai-service/` | Python FastAPI AI-tutoring microservice (uv-managed, Python 3.12/3.13) |
| `ai-service/app` | The service package: `api/`, `application/`, `domain/`, `infrastructure/` |
| `ai-service/tests` | Pytest suite (`tests/test_api.py`, 8 tests) |
| `ai-service/Dockerfile` | Container image: `python:3.12-slim`, uvicorn on port 8000 |
| `ai-service/requirements.txt`, `pyproject.toml`, `uv.lock` | Dependency manifests and lockfile |
| `ai-service/.env.example` | Template for local environment configuration |
| `FAQ.md` | Deep dives into every design decision (why this, why not that) |
| `README.md` | This file |

---

## Tech Stack

| Layer | Technology | Why |
|-------|------------|-----|
| Backend runtime | .NET 8 (`net8.0`, all projects) | LTS platform with first-party DI, auth, and rate limiting |
| API style | ASP.NET Core minimal APIs | Concise endpoint definitions for a small, focused API surface |
| Architecture | Clean Architecture (4 projects) | Dependency rule, testability, swappable infrastructure |
| ORM | EF Core `8.*` + `Npgsql.EntityFrameworkCore.PostgreSQL 8.*` | LINQ, migrations, change tracking; first-class Npgsql provider |
| Database | PostgreSQL 18 (database `pnle`) | Free, reliable, JSONB support |
| JWT | `Microsoft.AspNetCore.Authentication.JwtBearer 8.*`, `System.IdentityModel.Tokens.Jwt 7.*`, `Microsoft.IdentityModel.Tokens 7.*` | Standard, well-audited bearer-token validation |
| Google sign-in | `Google.Apis.Auth 1.*` | Server-side ID-token validation (JWKS, signature, audience, issuer) |
| Refresh tokens | HttpOnly cookie `pnle_refresh_token` (path `/auth`) | XSS-safe storage with rotation and revocation |
| API docs | `Swashbuckle.AspNetCore 6.*` | OpenAPI generation and interactive UI |
| Error handling | ProblemDetails + `GlobalExceptionHandler` | Consistent RFC 7807 error responses |
| Rate limiting | ASP.NET Core fixed-window policy `auth` (20 permits/min) | Brute-force protection on auth endpoints |
| Logging | `Microsoft.Extensions.Logging.Abstractions 8.*` | Abstracted logging across layers |
| AI service | Python 3.12/3.13 + FastAPI `0.141.1` | Native async, Pydantic validation, rich AI ecosystem |
| LLM orchestration | LangChain `1.3.14` + `langchain-groq 1.1.3` | Prompt templates and structured output |
| LLM provider | Groq (`groq 0.37.1`, model `llama-3.1-70b-versatile`) | Fast inference, generous free tier for practice |
| AI ORM | SQLAlchemy `2.0.51` (async) + `psycopg 3.3.4` | Async end-to-end, connection pooling |
| AI migrations | Alembic `1.19.1` | Schema migrations for the `pnle_ai` database |
| Dependency management | uv (`uv.lock`) | Fast installs, reproducible lockfile |
| Containerization | Docker (`python:3.12-slim`) | Consistent runtime with a `/healthz` healthcheck |
| Tests | pytest, pytest-asyncio, httpx | FastAPI `TestClient` with dependency overrides |

---

## Getting Started

### Prerequisites

| Requirement | Version / Notes |
|-------------|-----------------|
| Windows | This repo is developed on Windows (OneDrive note below) |
| .NET SDK | 8.x |
| Python | 3.12 or 3.13 (ai-service) |
| uv | Latest (see [docs](https://docs.astral.sh/uv/)) |
| PostgreSQL | 18, running locally on `localhost:5432` |
| Docker | Optional, for running the ai-service as a container |

> **OneDrive note (Windows)**: this repo lives under OneDrive, and `dotnet build`/`dotnet run` fail with a `Microsoft.Build.Tasks.Git` error unless you pass `-p:EnableSourceControlManagerQueries=false`. All backend commands below include this flag.

### Step 1 — Create the databases

Both databases must exist before either service runs. Connect to your local PostgreSQL and create them:

```sql
CREATE DATABASE pnle;
CREATE DATABASE pnle_ai;
```

The backend uses `pnle`; the ai-service uses `pnle_ai`. They are intentionally separate — see [FAQ.md](FAQ.md#14-why-separate-databases-per-service).

### Step 2 — Run the backend (.NET 8)

1. Open `backend/Pnle.Api/appsettings.json` and confirm:
   - `ConnectionStrings:Default` points at `localhost:5432`, database `pnle`, user `postgres`, with your local password (the committed value is a dev placeholder — replace it).
   - `Google:ClientIds` contains a **real Google OAuth Client ID** (e.g. from the [Google Cloud Console](https://console.cloud.google.com/)). The committed placeholder (`YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com`) will reject every login until replaced.
2. Run with the `http` launch profile:

```powershell
cd backend
dotnet run --project Pnle.Api -p:EnableSourceControlManagerQueries=false
```

3. Open the Swagger UI at <http://localhost:5097/swagger>.

Other launch profiles: `https` (`https://localhost:7244` + `http://localhost:5097`) and IIS Express (`http://localhost:3753`); all launch the Swagger UI. The dev-only schema initialization uses `EnsureCreated` — no migrations are applied (see [Roadmap](#current-status--roadmap)).

### Step 3 — Run the ai-service (FastAPI)

1. Copy the environment template and fill in the required keys:

```powershell
cd ai-service
Copy-Item .env.example .env
# edit .env: set AI_GROQ_API_KEY and AI_INTERNAL_API_KEY (min 32 chars)
```

2. Sync dependencies and start the server:

```powershell
cd ai-service
uv sync
uv run uvicorn app.main:app --reload
```

3. Open the interactive docs at <http://localhost:8000/docs> (ReDoc at <http://localhost:8000/redoc>).

### Step 4 (optional) — Run the ai-service with Docker

Secrets are **not** baked into the image — pass them at run time with `--env-file`:

```powershell
cd ai-service
docker build -t pnle-ai-service .
docker run --env-file .env -p 8000:8000 pnle-ai-service
```

The image exposes port 8000 and runs a `/healthz` healthcheck.

---

## Configuration

### Backend — `backend/Pnle.Api/appsettings.json`

| Key | Purpose | Dev default |
|-----|---------|-------------|
| `ConnectionStrings:Default` | PostgreSQL connection for the backend | `localhost:5432`, database `pnle`, user `postgres`, dev placeholder password |
| `Cors:AllowedOrigins` | Allowed frontend origins (credentials allowed) | `http://localhost:5173`, `http://localhost:4200` |
| `Auth:Issuer` | JWT issuer claim | `pnle-api` |
| `Auth:Audience` | JWT audience claim | `pnle-app` |
| `Auth:SigningKey` | HMAC key for signing tokens | dev placeholder (>64 chars) |
| `Auth:AccessTokenMinutes` | Access-token lifetime | `15` |
| `Auth:RefreshTokenDays` | Refresh-token lifetime | `30` |
| `Auth:CookieSecure` | `Secure` flag on the refresh cookie | `false` (dev; must be `true` over HTTPS) |
| `Auth:CookieSameSite` | SameSite policy on the refresh cookie | `Lax` |
| `Google:ClientIds` | Allowed Google OAuth client IDs | placeholder `YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com` |

All options are validated at startup (`ValidateOnStart`), so a misconfigured `appsettings.json` fails fast.

### ai-service — `.env` (all keys prefixed `AI_`)

| Variable | Purpose | Dev default |
|----------|---------|-------------|
| `AI_ENVIRONMENT` | Runtime environment label | `local` |
| `AI_GROQ_API_KEY` | Groq API key (required) | none |
| `AI_GROQ_MODEL` | Groq model for generation | `llama-3.1-70b-versatile` |
| `AI_INTERNAL_API_KEY` | Internal API key for service-to-service calls (min 32 chars, required) | none |
| `AI_DATABASE_URL` | PostgreSQL connection for the ai-service | `postgresql+psycopg://...`, database `pnle_ai` (dev placeholder credentials in `.env.example`) |
| `AI_TIMEOUT_SECONDS` | Timeout for Groq calls | `60` |
| `AI_WEAK_THRESHOLD` | Score below which a topic counts as weak (%) | `75` |
| `AI_READINESS_THRESHOLD` | Score at which a user is "ready" (%) | `80` |

`.env` is gitignored; `.env.example` is the committed template.

---

## API Reference

### Backend (`Pnle.Api`) — base URL `http://localhost:5097`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/healthz` | Anonymous | Liveness check; returns `{"status":"ok"}` |
| POST | `/auth/google` | Anonymous, rate-limited | Validates a Google ID token, finds or creates the user, issues a 15-minute JWT access token and a rotating 30-day refresh token in the `pnle_refresh_token` HttpOnly cookie |
| POST | `/auth/refresh` | Anonymous, rate-limited | Rotates the refresh token: revokes the old one and issues a new access/refresh pair |
| POST | `/auth/logout` | Anonymous, rate-limited | Revokes the refresh token and deletes the cookie; idempotent; returns `204` |
| GET | `/auth/me` | JWT bearer | Returns the current user's claims |

All `/auth/*` endpoints are covered by the fixed-window rate-limit policy `auth` (20 permits per minute; `429` on rejection) and are tagged `Auth` in Swagger. Expected failures (invalid Google token, unverified email, invalid refresh token, missing user) return structured errors via the `Result<T>`/`Error` pattern with codes such as `AUTH_INVALID_GOOGLE_TOKEN`, `AUTH_EMAIL_NOT_VERIFIED`, `AUTH_INVALID_REFRESH_TOKEN`, and `AUTH_USER_NOT_FOUND` — no exceptions for control flow.

### ai-service — base URL `http://localhost:8000`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/` | Public | Redirects to `/redoc` |
| GET | `/healthz` | Public | Liveness check |
| POST | `/v1/tutor/plan` | `x-api-key` | Creates a study plan from topic scores (`user_id`, `scores[]`, optional `exam_date`, `notes`) |
| POST | `/v1/questions/generate` | `x-api-key` | Generates practice questions: `topic`, `difficulty` (`easy`/`medium`/`hard`), `count` 1–10 (default 5); response intentionally excludes the correct answer and rationale |
| POST | `/v1/questions/{question_id}/answer` | `x-api-key` | Evaluates an answer (`user_id`, `selected_option_id` `A`/`B`/`C`/`D`); returns `is_correct`, the correct option, rationale, and common mistake |
| GET | `/v1/users/{user_id}/weaknesses` | `x-api-key` | Aggregated weakness analysis (threshold from `AI_WEAK_THRESHOLD`, default 75) |
| GET | `/v1/users/{user_id}/readiness` | `x-api-key` | Readiness score plus status: `not_started` / `needs_work` / `ready` (threshold default 80) |
| GET | `/docs` | — | Swagger UI |
| GET | `/redoc` | — | ReDoc UI |

All `/v1/*` endpoints require the `x-api-key` header matching `AI_INTERNAL_API_KEY`.

---

## Authentication Flow

1. **Frontend obtains a Google ID token** via Google Identity Services (GIS) and posts it to `POST /auth/google`.
2. **Backend validates the token server-side** with `Google.Apis.Auth` — signature, audience (must match one of `Google:ClientIds`), and issuer. Client-side validation alone is never trusted.
3. **Backend finds or creates the user**, then issues a **15-minute JWT access token** (returned to the client) and a **30-day refresh token** stored as a hash in the database, delivered as the `pnle_refresh_token` HttpOnly cookie scoped to path `/auth`.
4. **The client sends the access token** as a `Bearer` header for protected endpoints such as `GET /auth/me`.
5. **On expiry**, the client calls `POST /auth/refresh`, which rotates the token: the old refresh token is revoked and a new pair is issued.
6. **On logout**, `POST /auth/logout` revokes the refresh token and deletes the cookie (idempotent, `204`).

Refresh-token rotation means a stolen refresh token is usable at most once before the server detects the replay.

---

## Testing

### ai-service (pytest)

```powershell
cd ai-service
uv run pytest
```

The suite (`tests/test_api.py`) contains 8 tests using FastAPI's `TestClient` with dependency overrides: happy paths for tutor plans, public `/healthz`, missing and wrong API keys (401), invalid question counts (422), weaknesses and readiness endpoints, and answer submission.

### Backend

The backend has **no automated tests yet** — this is a known gap and a roadmap item.

---

## Current Status & Roadmap

**Done**

- Backend: Google OAuth login, JWT access tokens, rotating refresh tokens in HttpOnly cookies, logout, `/auth/me`, health check, CORS for the two dev frontends, fixed-window rate limiting on auth endpoints, `ValidateOnStart` options, ProblemDetails error handling, clean-architecture solution.
- ai-service: standalone AI tutoring service — study plans, question generation, answer evaluation, weakness analysis, readiness scoring, internal API-key auth, Docker image, 8 pytest tests.

**Next**

- Wire the backend → ai-service integration: the .NET backend currently has no HTTP client calling the ai-service. The planned contract is an HTTP call with the `x-api-key` header; the design is in place, the call is deferred.
- Replace dev-only `EnsureCreated` with EF Core migrations.
- Frontend integration (React on 5173 / Angular on 4200 are the allowed CORS origins).
- Automated test coverage for the backend.

---

## Design Decisions

Every significant choice — two services vs. one, clean architecture, JWT + refresh rotation, PostgreSQL, FastAPI + LangChain, API keys, and the deferred integration — is explained with its trade-offs in [FAQ.md](FAQ.md).
