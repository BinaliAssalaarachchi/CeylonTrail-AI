"""Internal HTTP boundary for the Travel Intelligence Agent."""

from fastapi import FastAPI, HTTPException

from .agent import TravelIntelligenceAgent
from .providers import create_recommendation_provider
from .schemas import TravelRecommendationOutput, TravelValidationInput

app = FastAPI(
    title="CeylonTrail Travel Intelligence Internal Service",
    version="1.0",
)
agent = TravelIntelligenceAgent(create_recommendation_provider())


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "travel-intelligence"}


@app.post(
    "/travel-intelligence/analyze",
    response_model=TravelRecommendationOutput,
)
def analyze(validation: TravelValidationInput) -> TravelRecommendationOutput:
    try:
        return agent.analyze(validation)
    except Exception as error:
        raise HTTPException(
            status_code=500,
            detail="Travel Intelligence analysis failed.",
        ) from error
