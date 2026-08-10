# ai-service — AI Tutoring Microservice

> The AI tutoring microservice for the PNLE (Philippine Nursing Licensure Exam) prep platform: generates study plans, creates practice questions, evaluates answers, analyzes weaknesses, and scores readiness — powered by LangChain and Groq.

This is a Python FastAPI service (Python 3.12/3.13, managed with uv) that owns all AI tutoring functionality for the platform. It runs as an independent microservice with its own PostgreSQL database (`pnle_ai`). It is designed to be called by the .NET backend (`Pnle.Api`) using an internal API key; that integration is planned but not yet wired — see [Backend Integration](#backend-integration).

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Docker](#docker)
- [API Reference](#api-reference)
- [Configuration](#configuration)
- [Project Layout](#project-layout)
- [Database](#database)
- [Testing](#testing)
- [Backend Integration](#backend-integration)

---

## Overview

The ai-service provides four tutoring capabilities:

- **Study plans** (`/v1/tutor/plan`) — builds a personalized study plan from the user's topic scores.
- **Question generation** (`/v1/questions/generate`) — produces multiple-choice practice questions with difficulty control.
- **Answer evaluation** (`/v1/questions/{question_id}/answer`) — grades an attempt and returns the correct option, rationale, and common mistake.
- **Weakness & readiness analytics** (`/v1/users/{user_id}/weaknesses`, `/v1/users/{user_id}/readiness`) — aggregates performance and reports readiness status.

All AI generation runs through LangChain with the Groq provider (`llama-3.1-70b-versatile`), using structured output so model responses are validated before they touch the database. All `/v1/*` endpoints require an internal API key (`x-api-key` header). The service is stateless beyond its database and can be scaled horizontally behind a load balancer.

---

## Architecture

The service follows clean architecture with dependencies pointing inward:

```
        ┌────────────────┐
        │     api/       │  routes, schemas, security, dependency injection
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
        │ infrastructure/ │  Groq AI gateway (ChatGroq + structured output),
        │                │  async SQLAlchemy models, repositories, unit of work
        └────────────────┘
```

- `api/` owns HTTP concerns: routing, request/response schemas, `x-api-key` security, and wiring.
- `application/` owns use cases and defines the protocols the infrastructure layer must implement.
- `domain/` contains pure models and rules — e.g. the `WeaknessAnalyzer` that applies the weakness threshold — with no framework dependencies.
- `infrastructure/` implements the Groq gateway via `ChatGroq` with `ChatPromptTemplate`s and structured output, plus the async SQLAlchemy models, repositories, and unit of work.

---

## Quick Start

### Prerequisites

- Python 3.12 or 3.13
- [uv](https://docs.astral.sh/uv/) (dependency management)
- PostgreSQL 18 running locally on `localhost:5432`

### Step 1 — Create the database

The ai-service uses its own database, `pnle_ai` (separate from the backend's `pnle`):

```sql
CREATE DATABASE pnle_ai;
```

### Step 2 — Configure the environment

```powershell
Copy-Item .env.example .env
```

Edit `.env` and set the two required keys:

| Variable | What to set |
|----------|-------------|
| `AI_GROQ_API_KEY` | Your Groq API key from [console.groq.com](https://console.groq.com) |
| `AI_INTERNAL_API_KEY` | A random secret of at least 32 characters (used by the backend later) |

The remaining variables already have sensible dev defaults. `.env` is gitignored; never commit real keys.

### Step 3 — Sync and run

```powershell
uv sync
uv run uvicorn app.main:app --reload
```

The service is now at <http://localhost:8000>:

- Interactive docs (Swagger UI): <http://localhost:8000/docs>
- ReDoc: <http://localhost:8000/redoc> (also the destination of `GET /`)
- Health check: <http://localhost:8000/healthz>

---

## Docker

Build and run the image with secrets passed at run time (they are never baked into the image):

```powershell
docker build -t pnle-ai-service .
docker run --env-file .env -p 8000:8000 pnle-ai-service
```

The image is based on `python:3.12-slim`, exposes port 8000, and includes a `HEALTHCHECK` against `/healthz`. The container runs `uvicorn app.main:app --host 0.0.0.0 --port 8000`.

---

## API Reference

Base URL: `http://localhost:8000`. All `/v1/*` endpoints require the `x-api-key` header set to `AI_INTERNAL_API_KEY` (missing or wrong key → `401`).

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/` | Public | Redirects to `/redoc` |
| GET | `/healthz` | Public | Liveness check (used by the Docker HEALTHCHECK) |
| POST | `/v1/tutor/plan` | `x-api-key` | Creates a study plan from topic scores. Body: `user_id`, `scores[]`, optional `exam_date`, optional `notes`. |
| POST | `/v1/questions/generate` | `x-api-key` | Generates practice questions. Body: `topic`, `difficulty` (`easy`/`medium`/`hard`), `count` 1–10 (default 5). The response intentionally excludes the correct answer and rationale (those are only revealed when answering). |
| POST | `/v1/questions/{question_id}/answer` | `x-api-key` | Evaluates an answer. Body: `user_id`, `selected_option_id` (`A`/`B`/`C`/`D`). Returns `is_correct`, the correct option, rationale, and common mistake. |
| GET | `/v1/users/{user_id}/weaknesses` | `x-api-key` | Aggregated weakness analysis. A topic is weak when its score is below `AI_WEAK_THRESHOLD` (default 75). |
| GET | `/v1/users/{user_id}/readiness` | `x-api-key` | Readiness score plus status: `not_started` / `needs_work` / `ready` (threshold from `AI_READINESS_THRESHOLD`, default 80). |
| GET | `/docs` | — | Swagger UI |
| GET | `/redoc` | — | ReDoc UI |

---

## Configuration

All configuration comes from environment variables with the `AI_` prefix (loaded via a `.env` file). Template: `.env.example`.

| Variable | Purpose | Dev default |
|----------|---------|-------------|
| `AI_ENVIRONMENT` | Runtime environment label | `local` |
| `AI_GROQ_API_KEY` | Groq API key (**required**) | none |
| `AI_GROQ_MODEL` | Groq model used for generation | `llama-3.1-70b-versatile` |
| `AI_INTERNAL_API_KEY` | Internal API key for service-to-service calls, min 32 chars (**required**) | none |
| `AI_DATABASE_URL` | PostgreSQL connection string (async psycopg driver) | `postgresql+psycopg://...`, database `pnle_ai` (dev placeholder credentials in `.env.example`) |
| `AI_TIMEOUT_SECONDS` | Timeout for Groq calls | `60` |
| `AI_WEAK_THRESHOLD` | Score below which a topic is classified as weak | `75` |
| `AI_READINESS_THRESHOLD` | Score at which a user is classified as ready | `80` |

---

## Project Layout

| Path | Description |
|------|-------------|
| `app/main.py` | FastAPI application entry point |
| `app/api/` | Routes, request/response schemas, `x-api-key` security, dependency injection |
| `app/application/` | Use cases and protocol (interface) definitions |
| `app/domain/` | Pure domain models and rules (e.g. `WeaknessAnalyzer`) |
| `app/infrastructure/` | Groq AI gateway (`ChatGroq`, prompt templates, structured output), async SQLAlchemy models, repositories, unit of work |
| `tests/test_api.py` | Pytest suite (8 tests) using FastAPI `TestClient` with dependency overrides |
| `requirements.txt` | Pinned dependency list (also used by the Docker build) |
| `pyproject.toml`, `uv.lock` | uv project definition and lockfile |
| `.env.example` | Committed template for local configuration |
| `.python-version` | Python version pin (3.12/3.13) |
| `Dockerfile` | Container image definition |

---

## Database

The service owns the `pnle_ai` database with these tables:

| Table | Purpose |
|-------|---------|
| `topic_scores` | Per-topic performance scores used for weakness/readiness analytics |
| `study_plans` | Generated study plans stored as `plan_json` (JSONB) |
| `generated_questions` | AI-generated questions stored as `question_json` (JSONB), with `review_status` defaulting to `ai_generated` |
| `question_attempts` | User answer attempts, with a foreign key to `generated_questions` |

Schema management uses Alembic 1.19.1. The ai-service never touches the backend's `pnle` database.

---

## Testing

Run the suite with uv:

```powershell
uv run pytest
```

`tests/test_api.py` contains 8 tests using FastAPI's `TestClient` with dependency overrides (no live Groq or database required): happy paths for study plans and answer submission, public `/healthz`, missing and wrong `x-api-key` (401), invalid question `count` (422), and weaknesses/readiness behavior.

---

## Backend Integration

The ai-service is designed to be called by the .NET backend (`Pnle.Api`) and expects that contract:

- **Header**: every `/v1/*` request must include `x-api-key: <AI_INTERNAL_API_KEY>`.
- **Base URL**: the backend will target `http://localhost:8000` in development.
- **Flow (planned)**: the frontend sends topic scores to the backend → the backend calls `/v1/tutor/plan` (or `/v1/questions/generate`) with the API key → results are returned to the frontend.

**Current status**: this integration is **not wired yet**. The backend has no HTTP client calling this service, and the two services run independently. The `x-api-key` contract, endpoints, and schemas documented here are the agreed interface — the backend wiring is a planned next step. See the repository root [README](../README.md) for the full project context and roadmap.
