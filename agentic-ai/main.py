"""Unified FastAPI application hosting all CeylonTrail autonomous AI agents."""

from __future__ import annotations

import logging
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import ValidationError

from config import load_local_environment
from planner.agent import PlannerAgent
from planner.schemas import PlannerInput, PlannerOutput
from destination.agent import DestinationAgent
from destination.schemas import DestinationExecutionRequest, DestinationOutput
from bookings.booking_agent import BookingProposalAgent
from bookings.schemas import BookingActionExecutionRequest, BookingActionOutput
from travel_intelligence.agent import DeterministicExecutionError, TravelIntelligenceAgent
from travel_intelligence.providers import create_recommendation_provider
from travel_intelligence.schemas import TravelRecommendationOutput, TravelValidationInput

load_local_environment()

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("ceylontrail.agents")

app = FastAPI(
    title="CeylonTrail AI - Autonomous Multi-Agent Service",
    description="Unified cloud runtime hosting Planner, Destination, Booking Action, and Travel Intelligence agents.",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Initialize Agent Instances
planner_agent = PlannerAgent()
destination_agent = DestinationAgent()
booking_agent = BookingProposalAgent()
travel_intelligence_agent = TravelIntelligenceAgent(create_recommendation_provider())


@app.get("/")
@app.get("/health")
def health() -> dict[str, str]:
    return {
        "status": "ok",
        "service": "ceylontrail-agentic-ai-unified",
        "agents": "planner, destination, bookings, travel_intelligence"
    }


# 1. Planner Agent Endpoint
@app.post("/planner/generate", response_model=PlannerOutput, tags=["Planner Agent"])
def generate_planner_itinerary(request: PlannerInput) -> PlannerOutput:
    try:
        return planner_agent.generate(request)
    except Exception as error:
        logger.exception("Planner Agent execution failed: %s", str(error))
        raise HTTPException(status_code=500, detail=f"Planner Agent error: {error}") from error


# 2. Destination Agent Endpoint
@app.post("/destination/recommend", response_model=DestinationOutput, tags=["Destination Agent"])
def recommend_destinations(request: DestinationExecutionRequest) -> DestinationOutput:
    try:
        return destination_agent.recommend(request)
    except (ValidationError, ValueError) as error:
        logger.warning("Destination validation failed: %s", str(error)[:500])
        raise HTTPException(status_code=422, detail="Destination request or output failed validation.") from error
    except Exception as error:
        logger.exception("Destination Agent failed: %s", str(error))
        raise HTTPException(status_code=502, detail="Destination Agent is unavailable.") from error


# 3. Booking Action Agent Endpoint
@app.post("/booking/prepare", response_model=BookingActionOutput, tags=["Booking Action Agent"])
def prepare_booking(request: BookingActionExecutionRequest) -> BookingActionOutput:
    try:
        return booking_agent.prepare(request)
    except Exception as error:
        logger.exception("Booking Agent execution failed: %s", str(error))
        raise HTTPException(status_code=500, detail=f"Booking Agent error: {error}") from error


# 4. Travel Intelligence Agent Endpoint
@app.post("/travel-intelligence/analyze", response_model=TravelRecommendationOutput, tags=["Travel Intelligence Agent"])
def analyze_travel_intelligence(validation: TravelValidationInput) -> TravelRecommendationOutput:
    try:
        return travel_intelligence_agent.analyze(validation)
    except DeterministicExecutionError as error:
        logger.warning("Travel Intelligence deterministic error: %s", str(error))
        raise HTTPException(
            status_code=503,
            detail="Travel Intelligence analysis is temporarily unavailable.",
        ) from error
    except Exception as error:
        logger.exception("Travel Intelligence analysis failed: %s", str(error))
        raise HTTPException(
            status_code=500,
            detail="Travel Intelligence analysis failed.",
        ) from error
