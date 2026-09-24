import os
import unittest
from decimal import Decimal

from pydantic import ValidationError

from planner.agent import DeterministicPlannerFixture, PlannerAgent, PlannerError, PlannerValidationError
from planner.providers import MissingPlannerProvider, PlannerConfigurationError, PlannerProviderError, create_planner_provider
from planner.schemas import PlannerInput


def request(**overrides):
    data = {
        "tripId": "trip-1", "startDate": "2026-10-10", "endDate": "2026-10-12", "duration": 3,
        "budget": 60000, "interests": ["culture"], "preferredRegions": ["Kandy"],
        "preferences": [{"type": "TravelStyle", "value": "Relaxed"}],
        "candidateAttractions": [
            {"id": "a1", "name": "Temple", "category": "Culture", "region": "Kandy", "price": 2500},
            {"id": "a2", "name": "Lake", "category": "Nature", "region": "Kandy", "price": 1500},
        ],
    }
    data.update(overrides)
    return PlannerInput.model_validate(data)


def valid_output():
    return {"days": [{"dayNumber": 1, "date": "2026-10-10", "items": [{
        "attractionId": "a1", "startTime": "09:00:00", "endTime": "10:00:00",
        "estimatedCost": "2500", "notes": "Culture stop",
    }]}], "estimatedCost": "2500", "status": "Generated"}


class RecordingProvider:
    def __init__(self, response=None, error=None, responses=None):
        self.response = response
        self.error = error
        self.responses = list(responses or [])
        self.calls = []

    def generate(self, planner_request, system_prompt):
        self.calls.append((planner_request, system_prompt))
        if self.error:
            raise self.error
        if self.responses:
            return self.responses.pop(0)
        return self.response


class PlannerTests(unittest.TestCase):
    def test_valid_mocked_structured_result_passes(self):
        result = PlannerAgent(RecordingProvider(valid_output())).generate(request())
        self.assertEqual(result.status, "Generated")
        self.assertEqual(result.days[0].items[0].attraction_id, "a1")

    def test_provider_receives_full_trusted_context_and_policy(self):
        provider = RecordingProvider(valid_output())
        PlannerAgent(provider).generate(request())
        received, prompt = provider.calls[0]
        self.assertEqual(received.budget, Decimal("60000"))
        self.assertEqual(received.interests, ["culture"])
        self.assertEqual(received.preferred_regions, ["Kandy"])
        self.assertEqual(received.preferences[0].value, "Relaxed")
        self.assertEqual(received.candidate_attractions[0].id, "a1")
        self.assertIn("closed allow-list", prompt)

    def assert_invalid_output(self, output, **kwargs):
        with self.assertRaises(PlannerValidationError):
            PlannerAgent(RecordingProvider(output), max_retries=0).generate(request(**kwargs))

    def test_unknown_and_invented_ids_rejected(self):
        output = valid_output()
        output["days"][0]["items"][0]["attractionId"] = "unknown"
        self.assert_invalid_output(output)

    def test_duplicate_attraction_rejected(self):
        output = {"days": [{"dayNumber": 1, "date": "2026-10-10", "items": [
            {"attractionId": "a1", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 2500},
            {"attractionId": "a1", "startTime": "11:00", "endTime": "12:00", "estimatedCost": 2500},
        ]}], "estimatedCost": 5000, "status": "Generated"}
        self.assert_invalid_output(output)

    def test_duplicate_day_rejected(self):
        output = valid_output()
        output["days"].append({"dayNumber": 1, "date": "2026-10-11", "items": []})
        self.assert_invalid_output(output)

    def test_out_of_range_date_rejected(self):
        output = valid_output()
        output["days"][0]["date"] = "2026-10-20"
        self.assert_invalid_output(output)

    def test_invalid_time_range_rejected(self):
        output = valid_output()
        output["days"][0]["items"][0]["endTime"] = "08:00"
        self.assert_invalid_output(output)

    def test_overlapping_items_rejected(self):
        output = valid_output()
        output["days"][0]["items"].append({"attractionId": "a2", "startTime": "09:30", "endTime": "11:00", "estimatedCost": 1500})
        output["estimatedCost"] = 4000
        self.assert_invalid_output(output)

    def test_negative_cost_rejected_by_schema(self):
        output = valid_output()
        output["days"][0]["items"][0]["estimatedCost"] = -1
        output["estimatedCost"] = -1
        self.assert_invalid_output(output)

    def test_incorrect_total_and_over_budget_rejected(self):
        output = valid_output()
        output["estimatedCost"] = 1
        self.assert_invalid_output(output)
        output = valid_output()
        output["days"][0]["items"][0]["estimatedCost"] = 1000
        output["estimatedCost"] = 1000
        self.assert_invalid_output(output, budget=100)

    def test_malformed_or_schema_invalid_model_result_retries_then_rejects(self):
        provider = RecordingProvider(responses=["not-json", {"status": "Generated"}])
        with self.assertRaises(PlannerValidationError):
            PlannerAgent(provider, max_retries=1).generate(request())
        self.assertEqual(len(provider.calls), 2)

    def test_provider_failure_is_controlled_and_retry_limit_is_respected(self):
        provider = RecordingProvider(error=PlannerProviderError("temporary"))
        with self.assertRaises(PlannerError):
            PlannerAgent(provider, max_retries=2).generate(request())
        self.assertEqual(len(provider.calls), 3)

    def test_missing_api_key_provider_fails_at_generation_not_import(self):
        with self.assertRaises(PlannerConfigurationError):
            MissingPlannerProvider().generate(request(), "policy")
        old = os.environ.pop("GEMINI_API_KEY", None)
        try:
            self.assertIsInstance(create_planner_provider(), MissingPlannerProvider)
        finally:
            if old is not None:
                os.environ["GEMINI_API_KEY"] = old

    def test_deterministic_fixture_is_explicitly_test_only(self):
        result = DeterministicPlannerFixture().generate(request())
        self.assertEqual(result.status, "Generated")
        self.assertEqual(result.days[0].items[0].attraction_id, "a1")

    def test_no_candidates_fixture_returns_noplan(self):
        result = DeterministicPlannerFixture().generate(request(candidateAttractions=[]))
        self.assertEqual(result.status, "NoPlan")
        self.assertEqual(result.days, [])

    def test_input_extra_field_rejected(self):
        with self.assertRaises(ValidationError):
            request(unexpected="not accepted")


if __name__ == "__main__":
    unittest.main()
