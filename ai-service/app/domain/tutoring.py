from datetime import datetime, timezone
from typing import Literal, Protocol

from pydantic import BaseModel, Field, field_validator, model_validator

OptionId = Literal["A", "B", "C", "D"]
Difficulty = Literal["easy", "medium", "hard"]
Priority = Literal["high", "medium", "low"]
ReadinessStatus = Literal["not_started", "needs_work", "ready"]


class Clock(Protocol):
    def now(self) -> datetime: ...


class SystemClock:
    def now(self) -> datetime:
        return datetime.now(timezone.utc)


class ScoreInput(BaseModel):
    topic: str = Field(min_length=1, max_length=120)
    correct: int = Field(ge=0)
    total: int = Field(gt=0)

    @model_validator(mode="after")
    def validate_correct(self) -> "ScoreInput":
        if self.correct > self.total:
            raise ValueError("correct cannot be greater than total.")
        return self


class TopicScoreSummary(BaseModel):
    topic: str
    correct: int
    total: int
    percent: float


class StudyTopicPlan(BaseModel):
    topic: str
    priority: Priority
    reason: str
    focus_areas: list[str]
    recommended_minutes_per_day: int = Field(ge=5, le=240)


class StudyPlan(BaseModel):
    summary: str
    priority_topics: list[StudyTopicPlan]
    weekly_actions: list[str]
    test_taking_strategy: list[str]


class QuestionOption(BaseModel):
    id: OptionId
    text: str


class GeneratedQuestion(BaseModel):
    topic: str
    difficulty: Difficulty
    stem: str
    options: list[QuestionOption] = Field(min_length=4, max_length=4)
    correct_option_id: OptionId
    rationale: str
    common_mistake: str

    @field_validator("options")
    @classmethod
    def validate_options(cls, options: list[QuestionOption]) -> list[QuestionOption]:
        ids = [option.id for option in options]

        if len(set(ids)) != len(ids):
            raise ValueError("Option ids must be unique.")

        return options


class QuestionSet(BaseModel):
    questions: list[GeneratedQuestion] = Field(min_length=1, max_length=10)


class PublicGeneratedQuestion(BaseModel):
    id: int
    topic: str
    difficulty: Difficulty
    stem: str
    options: list[QuestionOption]


class StoredGeneratedQuestion(BaseModel):
    id: int
    user_id: str
    topic: str
    difficulty: Difficulty
    question: GeneratedQuestion


class AnswerEvaluation(BaseModel):
    question_id: int
    is_correct: bool
    correct_option_id: OptionId
    rationale: str
    common_mistake: str


class WeaknessReport(BaseModel):
    user_id: str
    topics: list[TopicScoreSummary]
    weak_topics: list[TopicScoreSummary]
    generated_at: datetime


class ReadinessReport(BaseModel):
    user_id: str
    overall_score: float
    status: ReadinessStatus
    topics: list[TopicScoreSummary]
    weak_topics: list[TopicScoreSummary]
    generated_at: datetime


class WeaknessAnalyzer:
    def __init__(self, weak_threshold: float) -> None:
        self._weak_threshold = weak_threshold

    def analyze(self, scores: list[ScoreInput]) -> list[TopicScoreSummary]:
        summaries = [
            TopicScoreSummary(
                topic=score.topic,
                correct=score.correct,
                total=score.total,
                percent=round((score.correct / score.total) * 100, 2),
            )
            for score in scores
        ]

        return sorted(summaries, key=lambda summary: summary.percent)

    def weak_topics(self, summaries: list[TopicScoreSummary]) -> list[TopicScoreSummary]:
        return [summary for summary in summaries if summary.percent < self._weak_threshold]
