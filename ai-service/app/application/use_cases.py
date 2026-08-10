from datetime import date

from pydantic import BaseModel, Field

from app.application.ai_gateway import (
    QuestionGenerationAiInput,
    TutorAiGateway,
    TutorPlanAiInput,
)
from app.application.repositories import (
    QuestionRepository,
    TutorPlanRepository,
    UnitOfWork,
    WeaknessRepository,
)
from app.domain.tutoring import (
    AnswerEvaluation,
    Clock,
    Difficulty,
    GeneratedQuestion,
    OptionId,
    PublicGeneratedQuestion,
    ReadinessReport,
    ScoreInput,
    StoredGeneratedQuestion,
    StudyPlan,
    WeaknessAnalyzer,
    WeaknessReport,
)
from app.exceptions import NotFoundError, ValidationError


class CreateTutorPlanCommand(BaseModel):
    user_id: str = Field(min_length=1, max_length=64)
    scores: list[ScoreInput]
    exam_date: date | None = None
    notes: str | None = Field(default=None, max_length=2000)


class GenerateQuestionsCommand(BaseModel):
    user_id: str = Field(min_length=1, max_length=64)
    topic: str = Field(min_length=1, max_length=120)
    difficulty: Difficulty = "medium"
    count: int = Field(default=5, ge=1, le=10)


class SubmitAnswerCommand(BaseModel):
    user_id: str = Field(min_length=1, max_length=64)
    question_id: int
    selected_option_id: OptionId


class CreateTutorPlanUseCase:
    def __init__(
        self,
        repository: TutorPlanRepository,
        ai_gateway: TutorAiGateway,
        weakness_analyzer: WeaknessAnalyzer,
        unit_of_work: UnitOfWork,
    ) -> None:
        self._repository = repository
        self._ai_gateway = ai_gateway
        self._weakness_analyzer = weakness_analyzer
        self._unit_of_work = unit_of_work

    async def execute(self, command: CreateTutorPlanCommand) -> StudyPlan:
        if not command.scores:
            raise ValidationError("At least one score is required.")

        score_summaries = self._weakness_analyzer.analyze(command.scores)

        await self._repository.save_scores(command.user_id, score_summaries)

        plan = await self._ai_gateway.generate_study_plan(
            TutorPlanAiInput(
                scores=score_summaries,
                exam_date=command.exam_date,
                notes=command.notes,
            )
        )

        await self._repository.save_plan(command.user_id, plan)
        await self._unit_of_work.commit()

        return plan


class GenerateQuestionsUseCase:
    def __init__(
        self,
        repository: QuestionRepository,
        ai_gateway: TutorAiGateway,
        unit_of_work: UnitOfWork,
    ) -> None:
        self._repository = repository
        self._ai_gateway = ai_gateway
        self._unit_of_work = unit_of_work

    async def execute(
        self,
        command: GenerateQuestionsCommand,
    ) -> list[PublicGeneratedQuestion]:
        questions = await self._ai_gateway.generate_questions(
            QuestionGenerationAiInput(
                topic=command.topic,
                difficulty=command.difficulty,
                count=command.count,
            )
        )

        if not questions:
            raise ValidationError("No questions were generated.")

        question_ids = await self._repository.save_generated_questions(
            user_id=command.user_id,
            topic=command.topic,
            difficulty=command.difficulty,
            questions=questions,
        )

        await self._unit_of_work.commit()

        return [
            PublicGeneratedQuestion(
                id=question_id,
                topic=command.topic,
                difficulty=command.difficulty,
                stem=question.stem,
                options=question.options,
            )
            for question_id, question in zip(question_ids, questions)
        ]


class SubmitAnswerUseCase:
    def __init__(
        self,
        repository: QuestionRepository,
        unit_of_work: UnitOfWork,
    ) -> None:
        self._repository = repository
        self._unit_of_work = unit_of_work

    async def execute(self, command: SubmitAnswerCommand) -> AnswerEvaluation:
        stored_question = await self._repository.get_by_id(command.question_id)

        if stored_question is None:
            raise NotFoundError("Question not found.")

        if stored_question.user_id != command.user_id:
            raise NotFoundError("Question not found.")

        is_correct = (
            command.selected_option_id
            == stored_question.question.correct_option_id
        )

        await self._repository.save_answer(
            stored_question=stored_question,
            selected_option_id=command.selected_option_id,
            is_correct=is_correct,
        )

        await self._unit_of_work.commit()

        return AnswerEvaluation(
            question_id=stored_question.id,
            is_correct=is_correct,
            correct_option_id=stored_question.question.correct_option_id,
            rationale=stored_question.question.rationale,
            common_mistake=stored_question.question.common_mistake,
        )


class GetWeaknessesUseCase:
    def __init__(
        self,
        repository: WeaknessRepository,
        weakness_analyzer: WeaknessAnalyzer,
        clock: Clock,
    ) -> None:
        self._repository = repository
        self._weakness_analyzer = weakness_analyzer
        self._clock = clock

    async def execute(self, user_id: str) -> WeaknessReport:
        topics = await self._repository.get_topic_summaries(user_id)
        weak_topics = self._weakness_analyzer.weak_topics(topics)

        return WeaknessReport(
            user_id=user_id,
            topics=topics,
            weak_topics=weak_topics,
            generated_at=self._clock.now(),
        )


class GetReadinessUseCase:
    def __init__(
        self,
        repository: WeaknessRepository,
        weakness_analyzer: WeaknessAnalyzer,
        clock: Clock,
        readiness_threshold: float,
    ) -> None:
        self._repository = repository
        self._weakness_analyzer = weakness_analyzer
        self._clock = clock
        self._readiness_threshold = readiness_threshold

    async def execute(self, user_id: str) -> ReadinessReport:
        topics = await self._repository.get_topic_summaries(user_id)
        weak_topics = self._weakness_analyzer.weak_topics(topics)

        if not topics:
            overall_score = 0.0
            status = "not_started"
        else:
            overall_score = round(
                sum(topic.percent for topic in topics) / len(topics),
                2,
            )

            if overall_score >= self._readiness_threshold and not weak_topics:
                status = "ready"
            else:
                status = "needs_work"

        return ReadinessReport(
            user_id=user_id,
            overall_score=overall_score,
            status=status,
            topics=topics,
            weak_topics=weak_topics,
            generated_at=self._clock.now(),
        )
