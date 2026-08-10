from typing import Annotated

from fastapi import Depends
from sqlalchemy.ext.asyncio import AsyncSession

from app.application.ai_gateway import TutorAiGateway
from app.application.repositories import (
    QuestionRepository,
    TutorPlanRepository,
    WeaknessRepository,
)
from app.application.use_cases import (
    CreateTutorPlanUseCase,
    GenerateQuestionsUseCase,
    GetReadinessUseCase,
    GetWeaknessesUseCase,
    SubmitAnswerUseCase,
)
from app.config import Settings, get_settings
from app.database import get_db
from app.domain.tutoring import Clock, SystemClock, WeaknessAnalyzer
from app.infrastructure.ai.groq_tutor_ai_gateway import GroqTutorAiGateway
from app.infrastructure.persistence.repositories import (
    SqlQuestionRepository,
    SqlTutorPlanRepository,
    SqlWeaknessRepository,
)
from app.infrastructure.persistence.unit_of_work import SqlUnitOfWork


def get_clock() -> Clock:
    return SystemClock()


def get_unit_of_work(
    db: Annotated[AsyncSession, Depends(get_db)],
) -> SqlUnitOfWork:
    return SqlUnitOfWork(db)


def get_tutor_plan_repository(
    db: Annotated[AsyncSession, Depends(get_db)],
) -> TutorPlanRepository:
    return SqlTutorPlanRepository(db)


def get_question_repository(
    db: Annotated[AsyncSession, Depends(get_db)],
) -> QuestionRepository:
    return SqlQuestionRepository(db)


def get_weakness_repository(
    db: Annotated[AsyncSession, Depends(get_db)],
) -> WeaknessRepository:
    return SqlWeaknessRepository(db)


def get_ai_gateway(
    settings: Annotated[Settings, Depends(get_settings)],
) -> TutorAiGateway:
    return GroqTutorAiGateway(settings)


def get_weakness_analyzer(
    settings: Annotated[Settings, Depends(get_settings)],
) -> WeaknessAnalyzer:
    return WeaknessAnalyzer(settings.weak_threshold)


def get_create_tutor_plan_use_case(
    repository: Annotated[TutorPlanRepository, Depends(get_tutor_plan_repository)],
    ai_gateway: Annotated[TutorAiGateway, Depends(get_ai_gateway)],
    weakness_analyzer: Annotated[WeaknessAnalyzer, Depends(get_weakness_analyzer)],
    unit_of_work: Annotated[SqlUnitOfWork, Depends(get_unit_of_work)],
) -> CreateTutorPlanUseCase:
    return CreateTutorPlanUseCase(
        repository=repository,
        ai_gateway=ai_gateway,
        weakness_analyzer=weakness_analyzer,
        unit_of_work=unit_of_work,
    )


def get_generate_questions_use_case(
    repository: Annotated[QuestionRepository, Depends(get_question_repository)],
    ai_gateway: Annotated[TutorAiGateway, Depends(get_ai_gateway)],
    unit_of_work: Annotated[SqlUnitOfWork, Depends(get_unit_of_work)],
) -> GenerateQuestionsUseCase:
    return GenerateQuestionsUseCase(
        repository=repository,
        ai_gateway=ai_gateway,
        unit_of_work=unit_of_work,
    )


def get_submit_answer_use_case(
    repository: Annotated[QuestionRepository, Depends(get_question_repository)],
    unit_of_work: Annotated[SqlUnitOfWork, Depends(get_unit_of_work)],
) -> SubmitAnswerUseCase:
    return SubmitAnswerUseCase(
        repository=repository,
        unit_of_work=unit_of_work,
    )


def get_get_weaknesses_use_case(
    repository: Annotated[WeaknessRepository, Depends(get_weakness_repository)],
    weakness_analyzer: Annotated[WeaknessAnalyzer, Depends(get_weakness_analyzer)],
    clock: Annotated[Clock, Depends(get_clock)],
) -> GetWeaknessesUseCase:
    return GetWeaknessesUseCase(
        repository=repository,
        weakness_analyzer=weakness_analyzer,
        clock=clock,
    )


def get_get_readiness_use_case(
    repository: Annotated[WeaknessRepository, Depends(get_weakness_repository)],
    weakness_analyzer: Annotated[WeaknessAnalyzer, Depends(get_weakness_analyzer)],
    clock: Annotated[Clock, Depends(get_clock)],
    settings: Annotated[Settings, Depends(get_settings)],
) -> GetReadinessUseCase:
    return GetReadinessUseCase(
        repository=repository,
        weakness_analyzer=weakness_analyzer,
        clock=clock,
        readiness_threshold=settings.readiness_threshold,
    )
