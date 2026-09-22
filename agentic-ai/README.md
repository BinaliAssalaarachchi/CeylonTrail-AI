# CeylonTrail AI - Agentic AI Service

## Overview
This directory houses the autonomous agent components for the CeylonTrail platform.

### Member 3: Booking & Action Agent (`bookings/`)
The **Booking & Action Agent** is responsible for:
1. Translating approved tourist itinerary items and slots into structured booking action intents (`CREATE_BOOKING_REQUEST`).
2. Performing deterministic validations:
   - Schedule overlap detection between activities/attractions.
   - Budget constraint validation against tourist preferences.
   - Party size and capacity bounds checking.
3. Enforcing **human-in-the-loop** safety workflows (`requires_approval = True`).
4. Synthesizing structured payloads matching the ASP.NET Core `CreateBookingRequest` DTO schema.

---

## Architectural Boundaries
In adherence to the CeylonTrail Architecture Decision Records (ADRs):
- AI agents **never** communicate directly with the PostgreSQL database.
- AI agents output structured action requests that are submitted through the authoritative **ASP.NET Core Web API** endpoints.

---

## Testing
Run unit tests for the Booking & Action Agent:

```powershell
python -m unittest discover -s agentic-ai -p "test_*.py"
```
