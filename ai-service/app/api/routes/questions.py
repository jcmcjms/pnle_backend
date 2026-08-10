from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.dependencies import (
    get_generate_questions_use_case,
    get_submit_answer_use_case,
)
from app.api.routes.schemas import (
    ErrorDetail,
    GenerateQuestionsRequest,
    GenerateQuestionsResponse,
    SubmitAnswerRequest,
    SubmitAnswerResponse,
)
from app.api.security import require_internal_api_key
from app.application.use_cases import (
    GenerateQuestionsCommand,
    GenerateQuestionsUseCase,
    SubmitAnswerCommand,
    SubmitAnswerUseCase,
)

router = APIRouter(
    prefix="/v1/questions",
    tags=["Questions"],
    dependencies=[Depends(require_internal_api_key)],
)


@router.post(
    "/generate",
    response_model=GenerateQuestionsResponse,
    summary="Generate AI practice questions",
    description="""
Generates AI practice questions for a topic.

The response does not include the correct answer or rationale.
Those are returned only after submitting an answer.
""",
    responses={
        400: {"model": ErrorDetail},
        401: {"model": ErrorDetail},
        502: {"model": ErrorDetail},
    },
)
async def generate_questions(
    request: GenerateQuestionsRequest,
    use_case: Annotated[GenerateQuestionsUseCase, Depends(get_generate_questions_use_case)],
) -> GenerateQuestionsResponse:
    command = GenerateQuestionsCommand(
        user_id=request.user_id,
        topic=request.topic,
        difficulty=request.difficulty,
        count=request.count,
    )

    return await use_case.execute(command)


@router.post(
    "/{question_id}/answer",
    response_model=SubmitAnswerResponse,
    summary="Submit answer",
    description="""
Submits an answer for a generated question.

Returns whether the answer was correct, the correct option, and the rationale.
""",
    responses={
        400: {"model": ErrorDetail},
        401: {"model": ErrorDetail},
        404: {"model": ErrorDetail},
    },
)
async def submit_answer(
    question_id: int,
    request: SubmitAnswerRequest,
    use_case: Annotated[SubmitAnswerUseCase, Depends(get_submit_answer_use_case)],
) -> SubmitAnswerResponse:
    command = SubmitAnswerCommand(
        user_id=request.user_id,
        question_id=question_id,
        selected_option_id=request.selected_option_id,
    )

    return await use_case.execute(command)
