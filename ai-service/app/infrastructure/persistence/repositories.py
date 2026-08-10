from typing import Sequence

from pydantic import ValidationError as PydanticValidationError
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.application.repositories import (
    QuestionRepository,
    TutorPlanRepository,
    WeaknessRepository,
)
from app.domain.tutoring import (
    GeneratedQuestion,
    StoredGeneratedQuestion,
    StudyPlan,
    TopicScoreSummary,
)
from app.exceptions import DataCorruptionError

from .models import (
    GeneratedQuestionRecord,
    QuestionAttemptRecord,
    StudyPlanRecord,
    TopicScoreRecord,
)


class SqlTutorPlanRepository(TutorPlanRepository):
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def save_scores(
        self,
        user_id: str,
        scores: Sequence[TopicScoreSummary],
    ) -> None:
        records = [
            TopicScoreRecord(
                user_id=user_id,
                topic=score.topic,
                correct=score.correct,
                total=score.total,
                score_percent=score.percent,
                source="quiz",
            )
            for score in scores
        ]

        self._session.add_all(records)

    async def save_plan(
        self,
        user_id: str,
        plan: StudyPlan,
    ) -> None:
        self._session.add(
            StudyPlanRecord(
                user_id=user_id,
                plan_json=plan.model_dump(mode="json"),
            )
        )


class SqlQuestionRepository(QuestionRepository):
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def save_generated_questions(
        self,
        user_id: str,
        topic: str,
        difficulty: str,
        questions: Sequence[GeneratedQuestion],
    ) -> list[int]:
        question_ids: list[int] = []

        for question in questions:
            question_data = question.model_dump(mode="json")

            question_data["topic"] = topic
            question_data["difficulty"] = difficulty

            record = GeneratedQuestionRecord(
                user_id=user_id,
                topic=topic,
                difficulty=difficulty,
                question_json=question_data,
                review_status="ai_generated",
            )

            self._session.add(record)
            await self._session.flush()

            question_ids.append(record.id)

        return question_ids

    async def get_by_id(
        self,
        question_id: int,
    ) -> StoredGeneratedQuestion | None:
        record = await self._session.get(GeneratedQuestionRecord, question_id)

        if record is None:
            return None

        try:
            question = GeneratedQuestion.model_validate(record.question_json)
        except PydanticValidationError as exc:
            raise DataCorruptionError("Stored question data is invalid.") from exc

        return StoredGeneratedQuestion(
            id=record.id,
            user_id=record.user_id,
            topic=record.topic,
            difficulty=record.difficulty,
            question=question,
        )

    async def save_answer(
        self,
        stored_question: StoredGeneratedQuestion,
        selected_option_id: str,
        is_correct: bool,
    ) -> None:
        self._session.add(
            QuestionAttemptRecord(
                user_id=stored_question.user_id,
                question_id=stored_question.id,
                selected_option_id=selected_option_id,
                is_correct=is_correct,
            )
        )

        self._session.add(
            TopicScoreRecord(
                user_id=stored_question.user_id,
                topic=stored_question.topic,
                correct=1 if is_correct else 0,
                total=1,
                score_percent=100.0 if is_correct else 0.0,
                source="ai_question",
            )
        )


class SqlWeaknessRepository(WeaknessRepository):
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def get_topic_summaries(
        self,
        user_id: str,
    ) -> list[TopicScoreSummary]:
        statement = (
            select(
                TopicScoreRecord.topic,
                func.sum(TopicScoreRecord.correct).label("correct"),
                func.sum(TopicScoreRecord.total).label("total"),
            )
            .where(TopicScoreRecord.user_id == user_id)
            .group_by(TopicScoreRecord.topic)
        )

        result = await self._session.execute(statement)
        rows = result.all()

        summaries: list[TopicScoreSummary] = []

        for row in rows:
            correct = int(row.correct or 0)
            total = int(row.total or 0)

            percent = (
                round((correct / total) * 100, 2)
                if total > 0
                else 0.0
            )

            summaries.append(
                TopicScoreSummary(
                    topic=row.topic,
                    correct=correct,
                    total=total,
                    percent=percent,
                )
            )

        return sorted(summaries, key=lambda summary: summary.percent)
