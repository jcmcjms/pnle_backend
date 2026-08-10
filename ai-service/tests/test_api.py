from contextlib import asynccontextmanager

import pytest
from fastapi.testclient import TestClient

from app.api.dependencies import (
    get_create_tutor_plan_use_case,
    get_get_readiness_use_case,
    get_get_weaknesses_use_case,
    get_submit_answer_use_case,
)
from app.main import app

# Must match AI_INTERNAL_API_KEY in .env.
API_KEY = "change_this_to_a_long_random_secret_at_least_32_chars"
WRONG_KEY = "definitely_not_the_right_key_0123456789abcdef"


class FakeCreateTutorPlanUseCase:
    async def execute(self, command):
        return {
            "summary": "Test plan",
            "priority_topics": [],
            "weekly_actions": [],
            "test_taking_strategy": [],
        }


class FakeGetWeaknessesUseCase:
    async def execute(self, user_id):
        return {
            "user_id": user_id,
            "topics": [],
            "weak_topics": [],
            "generated_at": "2026-08-10T12:00:00Z",
        }


class FakeGetReadinessUseCase:
    async def execute(self, user_id):
        return {
            "user_id": user_id,
            "overall_score": 0.0,
            "status": "not_started",
            "topics": [],
            "weak_topics": [],
            "generated_at": "2026-08-10T12:00:00Z",
        }


class FakeSubmitAnswerUseCase:
    async def execute(self, command):
        return {
            "question_id": command.question_id,
            "is_correct": True,
            "correct_option_id": "B",
            "rationale": "The right option is B.",
            "common_mistake": "Choosing A without reading the stem.",
        }


class FakeEngine:
    """No-op engine so the app lifespan never touches live Postgres in tests."""

    class _FakeConnection:
        async def run_sync(self, fn, *args, **kwargs):
            return None

    @asynccontextmanager
    async def begin(self):
        yield self._FakeConnection()

    async def dispose(self):
        return None


@pytest.fixture()
def client(monkeypatch):
    app.dependency_overrides.clear()

    monkeypatch.setattr("app.main.engine", FakeEngine())

    with TestClient(app) as test_client:
        yield test_client

    app.dependency_overrides.clear()


def test_create_tutor_plan(client):
    app.dependency_overrides[get_create_tutor_plan_use_case] = (
        lambda: FakeCreateTutorPlanUseCase()
    )

    response = client.post(
        "/v1/tutor/plan",
        headers={"x-api-key": API_KEY},
        json={
            "user_id": "user_123",
            "scores": [
                {
                    "topic": "Pharmacology",
                    "correct": 4,
                    "total": 20,
                }
            ],
        },
    )

    assert response.status_code == 200
    assert response.json()["summary"] == "Test plan"


def test_healthz_is_public(client):
    response = client.get("/healthz")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_weaknesses_requires_api_key(client):
    response = client.get("/v1/users/user_123/weaknesses")

    assert response.status_code == 401


def test_weaknesses_rejects_wrong_api_key(client):
    response = client.get(
        "/v1/users/user_123/weaknesses",
        headers={"x-api-key": WRONG_KEY},
    )

    assert response.status_code == 401


@pytest.mark.parametrize("count", [0, 11])
def test_generate_questions_rejects_invalid_count(client, count):
    response = client.post(
        "/v1/questions/generate",
        headers={"x-api-key": API_KEY},
        json={
            "user_id": "user_123",
            "topic": "Pharmacology",
            "count": count,
        },
    )

    assert response.status_code == 422


def test_weaknesses_with_valid_key(client):
    app.dependency_overrides[get_get_weaknesses_use_case] = (
        lambda: FakeGetWeaknessesUseCase()
    )

    response = client.get(
        "/v1/users/user_123/weaknesses",
        headers={"x-api-key": API_KEY},
    )

    assert response.status_code == 200

    data = response.json()
    assert set(data) == {"user_id", "topics", "weak_topics", "generated_at"}
    assert data["user_id"] == "user_123"
    assert data["topics"] == []
    assert data["weak_topics"] == []


def test_readiness_with_valid_key(client):
    app.dependency_overrides[get_get_readiness_use_case] = (
        lambda: FakeGetReadinessUseCase()
    )

    response = client.get(
        "/v1/users/user_123/readiness",
        headers={"x-api-key": API_KEY},
    )

    assert response.status_code == 200

    data = response.json()
    assert data["status"] == "not_started"
    assert data["overall_score"] == 0.0


def test_submit_answer_happy_path(client):
    app.dependency_overrides[get_submit_answer_use_case] = (
        lambda: FakeSubmitAnswerUseCase()
    )

    response = client.post(
        "/v1/questions/42/answer",
        headers={"x-api-key": API_KEY},
        json={
            "user_id": "user_123",
            "selected_option_id": "B",
        },
    )

    assert response.status_code == 200

    data = response.json()
    assert set(data) == {
        "question_id",
        "is_correct",
        "correct_option_id",
        "rationale",
        "common_mistake",
    }
    assert data["question_id"] == 42
    assert data["is_correct"] is True
    assert data["correct_option_id"] == "B"
