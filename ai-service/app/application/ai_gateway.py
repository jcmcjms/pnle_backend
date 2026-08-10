from dataclasses import dataclass
from datetime import date
from typing import Protocol

from app.domain.tutoring import GeneratedQuestion, StudyPlan, TopicScoreSummary


@dataclass(slots=True)
class TutorPlanAiInput:
    scores: list[TopicScoreSummary]
    exam_date: date | None
    notes: str | None


@dataclass(slots=True)
class QuestionGenerationAiInput:
    topic: str
    difficulty: str
    count: int


class TutorAiGateway(Protocol):
    async def generate_study_plan(
        self,
        input_data: TutorPlanAiInput,
    ) -> StudyPlan: ...

    async def generate_questions(
        self,
        input_data: QuestionGenerationAiInput,
    ) -> list[GeneratedQuestion]: ...
