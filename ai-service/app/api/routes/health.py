from fastapi import APIRouter

router = APIRouter(tags=["Health"])


@router.get(
    "/healthz",
    summary="Health check",
    description="Returns OK when the service is running.",
)
async def healthz() -> dict[str, str]:
    return {"status": "ok"}
