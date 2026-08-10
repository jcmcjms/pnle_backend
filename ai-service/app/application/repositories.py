from typing import Protocol, Sequence

from app.domain.tutoring import (
    GeneratedQuestion,
    StoredGeneratedQuestion,
    StudyPlan,
    TopicScoreSummary,
)


class UnitOfWork(Protocol):
    async def commit(self) -> None: ...

    async def rollback(self) -> None: ...


class TutorPlanRepository(Protocol):
    async def save_scores(
        self,
        user_id: str,
        scores: Sequence[TopicScoreSummary],
    ) -> None: ...

    async def save_plan(
        self,
        user_id: str,
        plan: StudyPlan,
    ) -> None: ...


class QuestionRepository(Protocol):
    async def save_generated_questions(
        self,
        user_id: str,
        topic: str,
        difficulty: str,
        questions: Sequence[GeneratedQuestion],
    ) -> list[int]: ...

    async def get_by_id(
        self,
        question_id: int,
    ) -> StoredGeneratedQuestion | None: ...

    async def save_answer(
        self,
        stored_question: StoredGeneratedQuestion,
        selected_option_id: str,
        is_correct: bool,
    ) -> None: ...


class WeaknessRepository(Protocol):
    async def get_topic_summaries(
        self,
        user_id: str,
    ) -> list[TopicScoreSummary]: ...
