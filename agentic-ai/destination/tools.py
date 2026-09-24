"""Narrow, deterministic tools over ASP.NET-supplied authoritative data."""

from datetime import date
from typing import Iterable, Optional

from .schemas import DestinationRequest, TrustedAttraction


class DestinationToolError(RuntimeError):
    pass


class DestinationTools:
    def __init__(self, attractions: Iterable[TrustedAttraction]):
        self._attractions = list(attractions)

    def search_attractions(self, request: DestinationRequest) -> list[TrustedAttraction]:
        """Search only the allow-listed records supplied by ASP.NET."""
        category_ids = set(request.category_ids)
        interests = {value.casefold() for value in request.interests}
        district = request.district.casefold() if request.district else None
        results = []
        for attraction in self._attractions:
            if not attraction.is_active or attraction.status.casefold() != "approved":
                continue
            if district and attraction.district.casefold() != district:
                continue
            if category_ids and attraction.category_id not in category_ids:
                continue
            if interests and attraction.category.casefold() not in interests:
                continue
            if request.max_budget is not None and attraction.price > request.max_budget:
                continue
            if request.date and not self._available_on(attraction, request.date):
                continue
            results.append(attraction)
        return results

    def get_attraction_details(self, attraction_id: str) -> TrustedAttraction:
        for attraction in self._attractions:
            if attraction.attraction_id == attraction_id:
                return attraction
        raise DestinationToolError("Attraction is not present in the trusted tool result set.")

    def get_attraction_schedule(self, attraction_id: str):
        return self.get_attraction_details(attraction_id).schedules

    def get_attraction_availability(self, attraction_id: str, requested_date: Optional[date] = None):
        slots = self.get_attraction_details(attraction_id).experience_slots
        return [slot for slot in slots if requested_date is None or slot.date == requested_date]

    @staticmethod
    def _available_on(attraction: TrustedAttraction, requested_date: date) -> bool:
        return any(
            slot.date == requested_date and slot.available_capacity > 0
            for slot in attraction.experience_slots
        )
