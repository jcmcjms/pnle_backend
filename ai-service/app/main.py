from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse, RedirectResponse

from app.api.routes import health, questions, tutor, users
from app.config import get_settings
from app.database import engine
from app.exceptions import AppError
from app.infrastructure.persistence.models import Base

settings = get_settings()

tags_metadata = [
    {
        "name": "Health",
        "description": "Service health checks.",
    },
    {
        "name": "Tutor",
        "description": "AI tutoring and study plan generation.",
    },
    {
        "name": "Questions",
        "description": "AI-generated PNLE practice questions.",
    },
    {
        "name": "Users",
        "description": "User weakness and readiness analytics.",
    },
]


@asynccontextmanager
async def lifespan(app: FastAPI):
    # For local development only.
    # Use Alembic migrations in production.
    if settings.environment.lower() in {"local", "dev", "development"}:
        async with engine.begin() as conn:
            await conn.run_sync(Base.metadata.create_all)

    yield

    await engine.dispose()


app = FastAPI(
    title="PNLE AI Tutoring API",
    version="1.0.0",
    summary="AI tutoring service for PNLE board exam preparation.",
    description="""
This is the FastAPI AI service used by the .NET backend.

## Documentation

- Swagger UI: `/docs`
- ReDoc: `/redoc`
- OpenAPI JSON: `/openapi.json`

## Authentication

Protected endpoints require the internal API key header:

```
x-api-key: your-secret-key
```

Click the Authorize button in Swagger UI and enter the API key.
""",
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_url="/openapi.json",
    openapi_tags=tags_metadata,
    lifespan=lifespan,
)


@app.exception_handler(AppError)
async def app_error_handler(request: Request, exc: AppError) -> JSONResponse:
    return JSONResponse(
        status_code=exc.status_code,
        content={"detail": exc.detail},
    )


@app.get("/", include_in_schema=False)
async def root() -> RedirectResponse:
    return RedirectResponse(url="/redoc")


app.include_router(health.router)
app.include_router(tutor.router)
app.include_router(questions.router)
app.include_router(users.router)
