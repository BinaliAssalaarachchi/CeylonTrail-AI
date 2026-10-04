"""Dedicated HTTP application for the Planner Agent."""

import logging

from fastapi import FastAPI, HTTPException

from .agent import PlannerAgent, PlannerError, PlannerValidationError
from .providers import PlannerConfigurationError, PlannerProviderError
from .schemas import PlannerInput, PlannerOutput


server_logger = logging.getLogger("uvicorn.error")


app = FastAPI(title="CeylonTrail Planner Agent", version="1.0")
planner_agent = PlannerAgent()


def _log_failure(error: Exception) -> None:
    message = (
        "Planner failure stage=%s provider_exception_type=%s provider_status_code=%s message=%s"
    )
    values = (
        getattr(error, "stage", "unknown"),
        getattr(error, "provider_exception_type", type(error).__name__),
        getattr(error, "status_code", None),
        getattr(error, "diagnostic_message", str(error))[:500],
    )
    server_logger.error(message, *values)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "planner"}


@app.post("/planner/generate", response_model=PlannerOutput)
def generate_planner_itinerary(request: PlannerInput) -> PlannerOutput:
    try:
        return planner_agent.generate(request)
    except PlannerConfigurationError as error:
        _log_failure(error)
        raise HTTPException(status_code=503, detail="Planner Agent is not configured.") from error
    except PlannerValidationError as error:
        _log_failure(error)
        detail = {
            "message": "Planner output failed deterministic validation.",
            "stage": error.stage,
            "safeFailure": error.trace.safe_failure if error.trace else error.diagnostic_message,
        }
        if error.trace is not None:
            detail["trace"] = error.trace.model_dump(mode="json", by_alias=True)
        raise HTTPException(status_code=422, detail=detail) from error
    except PlannerProviderError as error:
        _log_failure(error)
        raise HTTPException(status_code=502, detail="Planner Agent provider failed.") from error
    except PlannerError as error:
        _log_failure(error)
        status_code = getattr(error, "status_code", None)
        if status_code not in {429, 500, 502, 503, 504}:
            status_code = 502
        raise HTTPException(status_code=status_code, detail="Planner Agent provider unavailable.") from error
