import json
import logging

from langchain_core.prompts import ChatPromptTemplate
from langchain_groq import ChatGroq

from app.application.ai_gateway import (
    QuestionGenerationAiInput,
    TutorAiGateway,
    TutorPlanAiInput,
)
from app.config import Settings
from app.domain.tutoring import GeneratedQuestion, QuestionSet, StudyPlan
from app.exceptions import AiProviderError

logger = logging.getLogger(__name__)


_STUDY_PLAN_PROMPT = ChatPromptTemplate.from_messages(
    [
        (
            "system",
            """
You are a PNLE nursing board exam tutor.

Your job is to analyze a nursing student's recent quiz scores and create a practical study plan.

Rules:
- Focus on weak topics first.
- Be specific and actionable.
- Use safe, educational nursing content only.
- Do not give real clinical advice for actual patients.
- Do not invent facts.
- Output must follow the required schema exactly.
""",
        ),
        (
            "human",
            """
Exam date: {exam_date}
Student notes: {notes}

Recent quiz scores JSON:
{scores_json}

Create a structured study plan.
Prioritize topics with lower scores.
Include focus areas, daily recommended minutes, weekly actions, and test-taking strategies.
""",
        ),
    ]
)

_QUESTION_GENERATION_PROMPT = ChatPromptTemplate.from_messages(
    [
        (
            "system",
            """
You are a PNLE nursing board exam item writer.

Create high-quality nursing board exam practice questions.

Rules:
- Use safe, educational nursing content only.
- Do not give real clinical advice for actual patients.
- Make questions realistic for nursing board exam preparation.
- Use clear English.
- Avoid ambiguous wording.
- Exactly one option must be correct.
- Provide a concise rationale for the correct answer.
- Provide a common mistake students make.
- Output must follow the required schema exactly.
""",
        ),
        (
            "human",
            """
Create {count} {difficulty} multiple-choice questions about:
{topic}

Each question must have:
- stem
- four options: A, B, C, D
- correct_option_id
- rationale
- common_mistake
""",
        ),
    ]
)


class GroqTutorAiGateway(TutorAiGateway):
    def __init__(self, settings: Settings) -> None:
        self._chat_model = ChatGroq(
            model=settings.groq_model,
            temperature=0,
            api_key=settings.groq_api_key,
        )

    async def generate_study_plan(
        self,
        input_data: TutorPlanAiInput,
    ) -> StudyPlan:
        chain = _STUDY_PLAN_PROMPT | self._chat_model.with_structured_output(StudyPlan)

        scores_payload = [score.model_dump() for score in input_data.scores]

        try:
            plan = await chain.ainvoke(
                {
                    "exam_date": input_data.exam_date.isoformat()
                    if input_data.exam_date
                    else "Not provided",
                    "notes": input_data.notes or "None",
                    "scores_json": json.dumps(scores_payload, ensure_ascii=False),
                }
            )
        except Exception as exc:
            logger.exception("Failed to generate study plan.")
            raise AiProviderError("Failed to generate study plan.") from exc

        if plan is None:
            raise AiProviderError("AI provider returned an empty study plan.")

        return plan

    async def generate_questions(
        self,
        input_data: QuestionGenerationAiInput,
    ) -> list[GeneratedQuestion]:
        chain = _QUESTION_GENERATION_PROMPT | self._chat_model.with_structured_output(
            QuestionSet
        )

        try:
            question_set = await chain.ainvoke(
                {
                    "count": input_data.count,
                    "difficulty": input_data.difficulty,
                    "topic": input_data.topic,
                }
            )
        except Exception as exc:
            logger.exception("Failed to generate questions.")
            raise AiProviderError("Failed to generate questions.") from exc

        if question_set is None:
            raise AiProviderError("AI provider returned an empty question set.")

        return question_set.questions[: input_data.count]
