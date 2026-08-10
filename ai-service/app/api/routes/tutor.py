from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.dependencies import get_create_tutor_plan_use_case
from app.api.routes.schemas import (
    CreateTutorPlanRequest,
    CreateTutorPlanResponse,
    ErrorDetail,
)
from app.api.security import require_internal_api_key
from app.application.use_cases import CreateTutorPlanCommand, CreateTutorPlanUseCase

router = APIRouter(
    prefix="/v1/tutor",
    tags=["Tutor"],
    dependencies=[Depends(require_internal_api_key)],
)


@router.post(
    "/plan",
    response_model=CreateTutorPlanResponse,
    summary="Create AI study plan",
    description="""
Creates an AI-generated study plan based on the student's recent scores.

This endpoint is intended to be called by the .NET backend.
""",
    responses={
        400: {"model": ErrorDetail},
        401: {"model": ErrorDetail},
        502: {"model": ErrorDetail},
    },
)
async def create_tutor_plan(
    request: CreateTutorPlanRequest,
    use_case: Annotated[CreateTutorPlanUseCase, Depends(get_create_tutor_plan_use_case)],
) -> CreateTutorPlanResponse:
    command = CreateTutorPlanCommand(
        user_id=request.user_id,
        scores=request.scores,
        exam_date=request.exam_date,
        notes=request.notes,
    )

    return await use_case.execute(command)
