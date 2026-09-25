"""FastAPI boundary for the internal Destination Agent."""

from fastapi import FastAPI

from .agent import DestinationAgent
from .schemas import DestinationInput, DestinationOutput


app = FastAPI(title="CeylonTrail Destination Agent", version="1.0")
destination_agent = DestinationAgent()


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "destination"}


@app.post("/destination/select", response_model=DestinationOutput)
def select_destination(request: DestinationInput) -> DestinationOutput:
    return destination_agent.select(request)

