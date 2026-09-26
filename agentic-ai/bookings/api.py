"""Internal proposal-only Booking/Action Agent boundary."""

from fastapi import FastAPI

from .booking_agent import BookingProposalAgent
from .schemas import BookingActionExecutionRequest, BookingActionOutput


app = FastAPI(title="CeylonTrail Booking Action Agent", version="1.0")
booking_agent = BookingProposalAgent()


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "booking-action"}


@app.post("/booking/prepare", response_model=BookingActionOutput)
def prepare_booking(request: BookingActionExecutionRequest) -> BookingActionOutput:
    return booking_agent.prepare(request)
