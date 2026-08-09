from functools import lru_cache

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_prefix="AI_",
        env_file=".env",
        extra="ignore",
    )

    environment: str = "local"

    groq_api_key: str = Field(min_length=10)

    groq_model: str = "llama-3.1-70b-versatile"

    internal_api_key: str = Field(min_length=32)

    database_url: str = Field(pattern=r"^postgresql\+psycopg://")

    timeout_seconds: float = Field(default=60, gt=0)

    weak_threshold: float = Field(default=75.0, ge=0, le=100)

    readiness_threshold: float = Field(default=80.0, ge=0, le=100)


@lru_cache
def get_settings() -> Settings:
    return Settings()