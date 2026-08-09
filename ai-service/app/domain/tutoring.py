from datetime import date, datetime, timezone
from typing import Literal, Protocol

from pydantic import BaseModel, Field, field_validator, model_validator

OptionId = Literal["A", "B", "C", "D"]
Difficulty = Literal["easy", "medium", "hard"]
Priority = Literal["high", "medium", "low"]
ReadinessStatus = Literal["not_started","needs_work", "ready"]

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
        if self