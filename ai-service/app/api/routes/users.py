from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.dependencies import (
    get_get_readiness_use_case,
    get_get_weaknesses_use_case,
)
from app.api.routes.schemas import (
    ErrorDetail,
    GetReadinessResponse,
    GetWeaknessesResponse,
)
from app.api.security import require_internal_api_key
from app.application.use_cases import GetReadinessUseCase, GetWeaknessesUseCase

router = APIRouter(
    prefix="/v1/users",
    tags=["Users"],
    dependencies=[Depends(require_internal_api_key)],
)


@router.get(
    "/{user_id}/weaknesses",
    response_model=GetWeaknessesResponse,
    summary="Get user weaknesses",
    description="Returns aggregated topic weaknesses for a user.",
    responses={
        401: {"model": ErrorDetail},
    },
)
async def get_weaknesses(
    user_id: str,
    use_case: Annotated[GetWeaknessesUseCase, Depends(get_get_weaknesses_use_case)],
) -> GetWeaknessesResponse:
    return await use_case.execute(user_id)


@router.get(
    "/{user_id}/readiness",
    response_model=GetReadinessResponse,
    summary="Get exam readiness",
    description="Returns estimated PNLE readiness score based on stored topic performance.",
    responses={
        401: {"model": ErrorDetail},
    },
)
async def get_readiness(
    user_id: str,
    use_case: Annotated[GetReadinessUseCase, Depends(get_get_readiness_use_case)],
) -> GetReadinessResponse:
    return await use_case.execute(user_id)
