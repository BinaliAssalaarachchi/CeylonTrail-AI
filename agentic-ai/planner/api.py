"""Dedicated HTTP application for the Planner Agent."""

from fastapi import FastAPI, HTTPException

from .agent import PlannerAgent, PlannerError, PlannerValidationError
from .providers import PlannerConfigurationError, PlannerProviderError
from .schemas import PlannerInput, PlannerOutput


app = FastAPI(title="CeylonTrail Planner Agent", version="1.0")
planner_agent = PlannerAgent()


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "planner"}


@app.post("/planner/generate", response_model=PlannerOutput)
def generate_planner_itinerary(request: PlannerInput) -> PlannerOutput:
    try:
        return planner_agent.generate(request)
    except PlannerConfigurationError as error:
        raise HTTPException(status_code=503, detail="Planner Agent is not configured.") from error
    except PlannerValidationError as error:
        raise HTTPException(status_code=422, detail="Planner output failed deterministic validation.") from error
    except PlannerProviderError as error:
        raise HTTPException(status_code=502, detail="Planner Agent provider failed.") from error
    except PlannerError as error:
        raise HTTPException(status_code=502, detail="Planner Agent failed.") from error
