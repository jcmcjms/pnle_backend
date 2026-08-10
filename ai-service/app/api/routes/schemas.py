from datetime import date

from pydantic import BaseModel, Field

from app.domain.tutoring import (
    AnswerEvaluation,
    Difficulty,
    OptionId,
    PublicGeneratedQuestion,
    ReadinessReport,
    ScoreInput,
    StudyPlan,
    WeaknessReport,
)


class ErrorDetail(BaseModel):
    detail: str


class CreateTutorPlanRequest(BaseModel):
    user_id: str = Field(min_length=1, max_length=64)
    scores: list[ScoreInput] = Field(min_length=1)
    exam_date: date | None = None
    notes: str | None = Field(default=None, max_length=2000)


class GenerateQuestionsRequest(BaseModel):
    user_id: str = Field(min_length=1, max_length=64)
    topic: str = Field(min_length=1, max_length=120)
    difficulty: Difficulty = "medium"
    count: int = Field(default=5, ge=1, le=10)


class SubmitAnswerRequest(BaseModel):
    user_id: str = Field(min_length=1, max_length=64)
    selected_option_id: OptionId


CreateTutorPlanResponse = StudyPlan
GenerateQuestionsResponse = list[PublicGeneratedQuestion]
SubmitAnswerResponse = AnswerEvaluation
GetWeaknessesResponse = WeaknessReport
GetReadinessResponse = ReadinessReport
