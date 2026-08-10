# FAQ — Design Decisions Explained

This document explains *why* this project is built the way it is. It is a practice project, so every decision is documented honestly — including the trade-offs. The answers reference the actual packages and versions used in this repository.

For setup and usage, see the main [README.md](README.md).

---

## Table of Contents

1. [Why two services (microservices architecture)?](#1-why-two-services-microservices-architecture)
2. [Why Clean Architecture?](#2-why-clean-architecture)
3. [Why .NET 8 and minimal APIs?](#3-why-net-8-and-minimal-apis)
4. [Why EF Core + Npgsql?](#4-why-ef-core--npgsql)
5. [Why PostgreSQL?](#5-why-postgresql)
6. [Why JWT access tokens + refresh-token rotation in HttpOnly cookies?](#6-why-jwt-access-tokens--refresh-token-rotation-in-httponly-cookies)
7. [Why Google.Apis.Auth instead of rolling our own Google validation?](#7-why-googleapisauth-instead-of-rolling-our-own-google-validation)
8. [Why Swashbuckle/Swagger?](#8-why-swashbuckleswagger)
9. [Why fixed-window rate limiting on auth endpoints?](#9-why-fixed-window-rate-limiting-on-auth-endpoints)
10. [Why Python/FastAPI for the AI service?](#10-why-pythonfastapi-for-the-ai-service)
11. [Why LangChain + Groq?](#11-why-langchain--groq)
12. [Why async SQLAlchemy + psycopg?](#12-why-async-sqlalchemy--psycopg)
13. [Why uv?](#13-why-uv)
14. [Why separate databases per service?](#14-why-separate-databases-per-service)
15. [Why x-api-key for service-to-service auth?](#15-why-x-api-key-for-service-to-service-auth)
16. [Why is the backend→ai-service integration not wired yet?](#16-why-is-the-backendai-service-integration-not-wired-yet)
17. [Why does the backend have TopicScore when the ai-service also has topic_scores?](#17-why-does-the-backend-have-topicscore-when-the-ai-service-also-has-topic_scores)
18. [Why Alembic?](#18-why-alembic)
19. [Why .NET for auth and Python for AI instead of one language?](#19-why-net-for-auth-and-python-for-ai-instead-of-one-language)
20. [Why practice microservices at all — and when should you NOT use them?](#20-why-practice-microservices-at-all--and-when-should-you-not-use-them)
21. [How do I run both services together locally?](#21-how-do-i-run-both-services-together-locally)
22. [Where do secrets live, and how do I keep them out of git?](#22-where-do-secrets-live-and-how-do-i-keep-them-out-of-git)

---

## 1. Why two services (microservices architecture)?

The system is split into two independently deployed services: a .NET 8 backend (`Pnle.Api`) that owns identity and API composition, and a Python FastAPI AI service (`ai-service`) that owns tutoring intelligence. The split buys real separation of concerns: authentication is a crisp, well-understood problem best served by mature .NET middleware, while AI tutoring lives in the Python ecosystem where LangChain and the Groq SDK are first-class citizens.

The other classic microservice benefits apply too. Each service can scale independently — a spike in question generation shouldn't force more auth instances — and each can be released on its own cadence. There is also resilience: if the AI service goes down or the Groq provider has an outage, login and profile endpoints keep working, because the backend does not depend on the AI service for anything critical. For a practice project, the split also deliberately exercises distributed-system patterns: two databases, two deployment artifacts, service-to-service auth, and cross-service data ownership.

The trade-offs are real and worth naming. Two services means two deployments, two databases, two logging/observability surfaces, and added latency on every AI-tutoring call. None of that complexity is justified by the actual feature set yet. For a tiny project, a monolith is almost always the right call — this project is not a tiny project though; it is a *learning* project, and the split exists because the point is to practice the architecture, not to optimize the workload. See [question 20](#20-why-practice-microservices-at-all--and-when-should-you-not-use-them) for the honest version.

## 2. Why Clean Architecture?

Clean Architecture keeps the dependency rule: source-code dependencies point inward, toward the domain. In this repo, `Pnle.Api` depends on `Pnle.Application` and `Pnle.Infrastructure`; `Pnle.Infrastructure` depends on `Pnle.Application` and `Pnle.Domain`; `Pnle.Application` depends on `Pnle.Domain`; and `Pnle.Domain` depends on nothing. The compiler enforces this — a stray reference in the wrong project fails the build (each project also compiles with `TreatWarningsAsErrors=true`).

The payoff is testability and swap-ability. The application layer defines what the system *does* (use cases, the `Result<T>`/`Error` pattern for expected failures) without knowing whether data comes from PostgreSQL, an in-memory store, or a mock. The domain layer (`User`, `RefreshToken`, `TopicScore`) contains pure rules with zero dependencies — no EF Core, no HTTP, no JWT. Infrastructure (`EF Core + Npgsql`, `Google.Apis.Auth`, token services) is a replaceable detail: if you wanted SQL Server or Dapper or a different OAuth provider, you would change only `Pnle.Infrastructure`.

For a practice project this layering is also the point: it demonstrates the discipline of separating "what the app wants" from "how it happens." The cost is more projects, more indirection, and more boilerplate than a single-project API needs. That is an accepted cost here, deliberately paid to build the habit before building real systems.

## 3. Why .NET 8 and minimal APIs?

.NET 8 is an LTS release, which means years of supported, stable updates — a sensible foundation for a project meant to be referenced later as a portfolio artifact. The platform ships first-party solutions for nearly everything this backend needs: dependency injection, configuration binding with validation, CORS, JWT bearer authentication via `Microsoft.AspNetCore.Authentication.JwtBearer 8.*`, and built-in rate limiting. None of that required adding a framework; it is all part of the runtime.

Minimal APIs were chosen over MVC controllers because this API surface is small — five endpoints, one health check, a handful of auth routes. Minimal APIs let each endpoint be defined in a few lines at the composition root, with the handler logic kept in application-layer use cases. Controllers would have added classes, attributes, and ceremony without adding value at this scope. If the API grows to dozens of resource endpoints with full CRUD, migrating to controllers (or splitting minimal endpoints into groups) is a straightforward, well-trodden path — another reason the choice is low-risk.

## 4. Why EF Core + Npgsql?

EF Core provides what raw SQL or a micro-ORM like Dapper would require hand-rolling: strongly typed LINQ queries, change tracking, and a migration story. The backend stores `User`, `RefreshToken`, and `TopicScore` with relationships (a user has many refresh tokens), and EF Core's change tracking keeps that state manipulation simple and readable. `Npgsql.EntityFrameworkCore.PostgreSQL 8.*` is the official Npgsql provider, which means first-class PostgreSQL support: `JSONB`, `citext`, and the Npgsql-specific types work out of the box.

Why PostgreSQL instead of SQL Server? PostgreSQL is free and cross-platform, which keeps the dev experience on Windows with zero licensing friction, and it offers modern features like `JSONB` (the ai-service stores `plan_json` and `question_json` as JSONB). Why not MongoDB? The data here is fundamentally relational — users, refresh tokens, question attempts, and foreign keys between them — and the auth flows need transactions and referential integrity. A document store would fight the shape of the data. For the AI service the same logic applies, with async SQLAlchemy 2.0.51 in the Python stack.

## 5. Why PostgreSQL?

PostgreSQL is open source, battle-tested, and free for both development and production — the default choice for a practice project that shouldn't cost money to run. Its `JSONB` column type is genuinely useful here: the ai-service stores AI-generated artifacts (`plan_json`, `question_json`) as JSONB, which gives the flexibility of schemaless documents *and* the ability to index and query into them when needed. Reliability matters too — losing auth data or study plans to a flaky database would make the whole project untrustworthy.

The project deliberately uses **two databases**: `pnle` for the backend and `pnle_ai` for the AI service. This is data isolation between bounded contexts: the backend owns identity and user data, the AI service owns tutoring data. Each service can migrate its schema on its own cadence (EF Core in .NET, Alembic in Python) without coordinating with the other, and neither can accidentally read the other's tables. The cost is no cross-database joins — any combined query must be composed in application code or via an API call. See [question 14](#14-why-separate-databases-per-service).

## 6. Why JWT access tokens + refresh-token rotation in HttpOnly cookies?

The access token is a stateless JWT: the backend issues it signed with a secret (dev placeholder in `appsettings.json`, `Auth:SigningKey`), and any resource server can validate it without a database lookup. Because it is short-lived (15 minutes, `Auth:AccessTokenMinutes`), a stolen access token is only useful for a short window. The backend configures the JWT bearer with `MapInboundClaims=false` (clean, predictable claim names) and a 30-second clock skew for legitimate time drift between servers.

The refresh token is the long-lived credential (30 days) and it is the sensitive one. It is stored hashed in the database and delivered as the `pnle_refresh_token` HttpOnly cookie scoped to `/auth`. HttpOnly means JavaScript cannot read it, so an XSS vulnerability cannot exfiltrate it directly — a token kept in `localStorage` would be stealable by any injected script. Every refresh rotates the token: the old one is revoked and a new pair is issued, so a replayed token is detected. Cookie `SameSite=Lax` plus the fact that refresh is a POST-only endpoint provides reasonable CSRF posture in development; `Auth:CookieSecure` is `false` in dev and must be `true` in production over HTTPS.

Why not sessions-only? Server-side sessions work, but they are stateful by design: every request hits the session store, scaling requires a shared store, and the browser gets an opaque cookie that leaks nothing but requires that lookup. The hybrid here gives the statelessness of JWT for the hot path (`/auth/me` and future resource endpoints) with the revocation capability of server-side state for the long-lived credential. That is the best of both — at the price of implementing rotation, which this repo does deliberately.

## 7. Why Google.Apis.Auth instead of rolling our own Google validation?

Validating a Google ID token correctly requires fetching Google's JSON Web Key Set (JWKS), checking the token's signature against the right key, validating the audience against your configured client IDs, checking the issuer, and handling key rotation. That is subtle cryptography — and getting any of it wrong creates a login bypass. `Google.Apis.Auth 1.*` is Google's own library; it implements all of that correctly and keeps it maintained.

The deeper rule here: **never trust the client.** A frontend can send any string as an "idToken", and a client-side Google sign-in result is not proof of identity. The server must independently validate the token with Google's public keys. Rolling your own version would mean reimplementing signature verification with no benefit and high risk. The library also returns the token payload (email, verified status) so the backend can enforce `AUTH_EMAIL_NOT_VERIFIED` before issuing tokens. The remaining work is configuration — providing real client IDs via `Google:ClientIds` (the committed value is a placeholder) — not cryptography.

## 8. Why Swashbuckle/Swagger?

Swashbuckle generates an OpenAPI description of the API and serves an interactive UI at `/swagger`. For a practice project this is nearly free documentation: every endpoint, request body, and response schema is visible and *testable* from the browser, which is how most development of the auth flows happens here — clicking through `POST /auth/google` and `POST /auth/refresh` without a frontend client.

It also enforces a useful habit: the OpenAPI document is a machine-readable contract. Whatever frontend eventually consumes this API (React on 5173 or Angular on 4200) can generate typed clients from the spec. The cost is negligible (a single package, `Swashbuckle.AspNetCore 6.*`), and the alternative — hand-maintained endpoint documentation — drifts out of date within a week. The ai-service gets the same benefit from FastAPI, which generates OpenAPI natively (`/docs` for Swagger UI, `/redoc` for ReDoc).

## 9. Why fixed-window rate limiting on auth endpoints?

Auth endpoints are the only attack surface worth hammering: an attacker can try to abuse `POST /auth/google`, `POST /auth/refresh`, and `POST /auth/logout` with forged or replayed tokens. The "auth" policy is a fixed window of 20 permits per minute per client, returning `429 Too Many Requests` when exceeded. Twenty per minute is far more than a legitimate user generates while logging in or refreshing tokens, but far less than a brute-force or credential-stuffing script needs.

Why fixed-window rather than global? A global limit would throttle the whole API — including `GET /healthz`, which must stay open for load balancers and orchestrators — just because one client misbehaved. Limiting only the auth group contains the damage to exactly the endpoints that need it. Fixed-window was chosen over sliding-window or token-bucket for this scope because it is built into ASP.NET Core, requires no external state, and the burst tolerance is acceptable for a dev-facing practice API. The policy name, permits, and window are all visible in the backend composition root.

## 10. Why Python/FastAPI for the AI service?

The AI service's entire job is to talk to large language models and process their output. That ecosystem is Python: LangChain (orchestration), the Groq SDK, Pydantic for schema validation, and the wider ML tooling all treat Python as their home language. Building the same service in .NET would mean working against the grain — thinner LLM libraries, fewer maintained integrations, more glue code. This is a case of picking the right tool per job rather than dogmatism.

FastAPI specifically was chosen for three reasons: it is natively async (matching the async SQLAlchemy stack), it generates OpenAPI docs automatically (`/docs`, `/redoc`), and it validates request/response payloads with Pydantic 2.13.4 at the boundary — which is exactly where validation belongs. The service is small (five v1 endpoints) and FastAPI keeps it small. Python's GIL and slower baseline performance are irrelevant here because the dominant cost is the LLM round-trip, not the framework.

## 11. Why LangChain + Groq?

LangChain provides the orchestration glue for LLM work: `ChatPromptTemplate`s for building prompts, and structured output so the model returns valid JSON that the domain layer can consume. That structured output is critical — the service generates questions with specific fields and evaluates answers with rationale and common-mistake text; unvalidated free text would leak into the database. `langchain-groq 1.1.3` is the LangChain integration for Groq.

Groq was chosen for its inference speed: `llama-3.1-70b-versatile` runs on Groq's LPU hardware with response times that make interactive tutoring feel responsive, and the free tier is generous enough for practice and portfolio use. Why not OpenAI-only? Cost and latency for a practice project, plus the desire to demonstrate that the LLM provider is a swappable detail behind the LangChain integration — the same prompts and structured-output pipeline would work with another provider by changing configuration (`AI_GROQ_MODEL`) and the gateway implementation. The `infrastructure/` layer exists precisely so that swap is one class, not a rewrite.

## 12. Why async SQLAlchemy + psycopg?

FastAPI is async end-to-end, so blocking the event loop with synchronous database calls would waste the entire reason for choosing it: while one request waits on a DB round-trip, every other request on that worker stalls. Async SQLAlchemy 2.0.51 (`sqlalchemy[asyncio]`) with the async `psycopg[binary]` driver (psycopg 3.3.4) keeps the whole stack non-blocking, and SQLAlchemy's async engine provides connection pooling out of the box.

Why not sync SQLAlchemy? It would work — FastAPI runs sync handlers in a threadpool — but it adds a thread-hop per query and makes the "one async stack" story muddier for a service whose models and repositories are written async-first anyway. psycopg 3 over psycopg2 was chosen because psycopg 3 is the current generation, supports async natively, and is what `AI_DATABASE_URL` (`postgresql+psycopg://`) targets.

## 13. Why uv?

uv is the modern Python dependency manager: it resolves and installs packages dramatically faster than pip or poetry, and it produces a `uv.lock` lockfile that pins every transitive dependency. Reproducibility is the real win — `uv sync` on any machine installs byte-identical dependencies, which matters for a practice project that will be cloned and demonstrated later. The Python version itself is pinned via `.python-version` (3.12/3.13).

Why not pip + requirements.txt alone? `requirements.txt` (which this repo also keeps, for Docker) is fine for a frozen list but has weak resolution semantics. Why not poetry? Poetry is heavier, slower, and its lockfile format is more complex than needed here. uv runs `pip install -r requirements.txt` and `uv sync` from `pyproject.toml` with the same commands, so it was adopted early and consistently: the local dev loop is `uv sync && uv run uvicorn app.main:app --reload`, and tests run with `uv run pytest`.

## 14. Why separate databases per service?

Each service owns its data outright: the backend owns identity (`pnle` database: users, refresh tokens), the AI service owns tutoring (`pnle_ai` database: `topic_scores`, `study_plans`, `generated_questions`, `question_attempts`). This is bounded-context data ownership — the microservice rule that a service should never reach into another service's tables, because shared schemas become hidden coupling that silently breaks deployments.

The concrete benefits: independent migrations (EF Core for .NET, Alembic for Python, no shared migration history), independent scaling of storage, and a hard wall preventing accidental cross-service queries. The trade-off is that any question combining both datasets must be answered by calling the other service or by duplicating a slice of data — there are no cross-database joins. That is a *feature* here: it forces the team to design service APIs rather than ad-hoc queries, which is exactly the discipline a microservices practice project should build.

## 15. Why x-api-key for service-to-service auth?

The planned backend→ai-service calls will send an internal API key in the `x-api-key` header, matching `AI_INTERNAL_API_KEY` (minimum 32 characters, configured via `AI_INTERNAL_API_KEY`). All `/v1/*` endpoints in the ai-service already enforce this. The tests verify the behavior: a missing or wrong key returns `401`.

An API key is the right mechanism at this stage because the caller is a single, trusted backend — there is no user context being delegated, no third party, and no need for token issuance, expiry, or scopes. Full OAuth2 between the services (e.g., client-credentials flow) would add an authorization server, token endpoints, and key rotation infrastructure for zero added security here, since both services are deployed and controlled together. The key's job is to stop casual or accidental access, not to defend against a compromised network. If the services ever become externally reachable or multi-tenant, the upgrade path is mTLS, a service mesh, or OAuth2 client credentials — all behind the same header contract.

## 16. Why is the backend→ai-service integration not wired yet?

Honest answer: staged delivery. The repository deliberately lands two independently working services first — the auth backend and the AI service — each fully runnable and testable on its own, and the integration is deferred by design rather than forgotten. This sequencing makes debugging tractable: when auth works and the AI service works standalone, any future failure in the combined system can be attributed to the wiring, not the parts.

What remains is concrete: a typed HTTP client in the backend (via `IHttpClientFactory`), configuration for the ai-service base URL, attaching the `x-api-key` header (the value would come from configuration, not code), retry/timeout policies, and mapping ai-service responses into backend domain models. The contract on the ai-service side already exists — the `/v1/*` endpoints and their request/response schemas are documented and key-protected. Until that wiring lands, the two services run side by side, and the [README](README.md#current-status--roadmap) says so plainly.

## 17. Why does the backend have TopicScore when the ai-service also has topic_scores?

Both services own a slice of the same concept. In the backend domain, `TopicScore` is a placeholder entity representing per-topic exam scores — the backend's future job is to accept scores from the frontend and ultimately hand them to the AI service when requesting a study plan. In the ai-service, the `topic_scores` table is where tutoring analytics actually live, alongside `study_plans`, `generated_questions`, and `question_attempts`.

This duplication is a known, accepted trade-off at this stage — it reflects reality: the integration isn't wired yet, so each service models the slice it needs. The intended future sync strategy is that the backend remains the write path (frontend → backend), and the AI service reads what it needs over the API (backend → ai-service), with the backend's `TopicScore` acting as the source-of-truth boundary model. Documented honestly: today the two are duplicates; the integration step will define the single authoritative flow.

## 18. Why Alembic?

The ai-service needs schema migrations for its own database (`pnle_ai`), and in the Python ecosystem the standard tool is Alembic (`alembic 1.19.1`), the migration framework from the SQLAlchemy project. It integrates with the existing async SQLAlchemy models, versions the schema, and supports both upgrade and downgrade paths.

Why not EF Core migrations? EF Core is a .NET tool — it cannot run against Python's SQLAlchemy models, and trying to share migrations between two ecosystems would create a cross-language dependency nobody wants. Each service migrates its own schema with its own tool: EF Core (via the design-time package `Microsoft.EntityFrameworkCore.Design 8.*`) for `pnle`, Alembic for `pnle_ai`. Note that the backend currently uses dev-only `EnsureCreated` instead of EF migrations — replacing that with real migrations is an explicit roadmap item.

## 19. Why .NET for auth and Python for AI instead of one language?

Because they are different jobs. The auth/API backend is about correctness, security middleware, and framework maturity: ASP.NET Core's DI, configuration validation (`ValidateOnStart`), JWT bearer handling, rate limiting, and CORS are all production-grade and battle-tested. The AI service is about talking to LLMs: the Python ecosystem has LangChain, the Groq SDK, and the fastest-moving AI integrations. Choosing one language for both would mean compromising on one of the two.

There is a team-skills argument too: a practice project like this is often the first place a developer deliberately learns both ecosystems. Running the split here — rather than in a production system under deadline pressure — is the low-risk way to gain exposure to .NET and Python microservices, their build tools, their test frameworks, and their operational quirks (like the OneDrive `-p:EnableSourceControlManagerQueries=false` workaround in the README). The cost is context switching and two toolchains; the payoff is the ability to choose languages deliberately in the future.

## 20. Why practice microservices at all — and when should you NOT use them?

The point of this project is learning. Building two services, two databases, an internal API-key contract, and clean architecture in two languages forces hands-on experience with distributed-system problems — deployment, service-to-service auth, data ownership, staged integration — that a monolith never surfaces. Those are marketable, transferable skills, and this repo is the evidence of them.

The honest counterpoint: for a real PNLE prep product at this scale, microservices would be the wrong default. The complexity tax is real — two deployments, two databases, network latency, distributed debugging, and more moving parts to secure. The standard advice applies: start with a modular monolith, split only when a boundary is proven (different scaling needs, different release cadence, different team ownership). The split here is *deliberately* exercised as practice, not because the workload demands it. If you're building something small for production, copy the clean architecture and the auth design — not the two-service topology.

## 21. How do I run both services together locally?

Create both databases (`pnle` and `pnle_ai`), then in two terminals: run the backend with `cd backend && dotnet run --project Pnle.Api -p:EnableSourceControlManagerQueries=false` (Swagger at `http://localhost:5097/swagger`), and the ai-service with `cd ai-service && uv sync && uv run uvicorn app.main:app --reload` (docs at `http://localhost:8000/docs`). The OneDrive flag is required because this repo lives under OneDrive and `dotnet` fails with a `Microsoft.Build.Tasks.Git` error otherwise.

Remember the current integration status: the backend does **not** call the ai-service yet, so "running together" means running side by side. Both health endpoints (`/healthz`) are public and are the first thing to check. Full step-by-step instructions, including the `.env` setup and Docker option, are in the [README's Getting Started section](README.md#getting-started).

## 22. Where do secrets live, and how do I keep them out of git?

Secrets live in local, gitignored files: `ai-service/.env` (ignored by the repo's `.gitignore`) for the AI service, and `backend/Pnle.Api/appsettings.json` for the backend (with the committed values being dev placeholders, not real secrets). The template `ai-service/.env.example` is committed and is the safe reference for which variables exist.

The rules practiced here: never commit a real key or password — the placeholder Google client ID, signing key, and database password in `appsettings.json` are explicitly dev-only placeholders; the `.env` file is never committed (only `.env.example` is); and the Docker image does not bake secrets in — it expects `--env-file .env` at run time. If a real key ever lands in git history, treat it as compromised, rotate it, and remove it from history.
