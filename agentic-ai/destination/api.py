"""Internal FastAPI surface for the Destination Agent."""

import logging

from fastapi import FastAPI, HTTPException
from pydantic import ValidationError

from .agent import DestinationAgent
from .schemas import DestinationExecutionRequest, DestinationOutput

logger = logging.getLogger("uvicorn.error")
app = FastAPI(title="CeylonTrail Destination Agent", version="1.0")
destination_agent = DestinationAgent()


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "destination"}


@app.post("/destination/recommend", response_model=DestinationOutput)
def recommend(request: DestinationExecutionRequest) -> DestinationOutput:
    try:
        return destination_agent.recommend(request)
    except (ValidationError, ValueError) as error:
        logger.warning("Destination validation failed: %s", str(error)[:500])
        raise HTTPException(status_code=422, detail="Destination request or output failed validation.") from error
    except Exception as error:
        logger.exception("Destination Agent failed safely: %s", type(error).__name__)
        raise HTTPException(status_code=502, detail="Destination Agent is unavailable.") from error
