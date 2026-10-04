import json
import os
import tempfile
import unittest
from decimal import Decimal
from pathlib import Path

from google.genai import types
from pydantic import ValidationError

from config import load_dotenv_file
from planner.agent import DeterministicPlannerFixture, PlannerAgent, PlannerError, PlannerValidationError
from planner.providers import FallbackPlannerModelProvider, GEMINI_PLANNER_RESPONSE_SCHEMA, GeminiPlannerModelProvider, MissingPlannerProvider, PlannerConfigurationError, PlannerProviderError, create_planner_provider
from agent_trace import AgentExecutionTrace
from planner.schemas import PlannerInput, PlannerOutput
from planner.tools import PlannerToolError, PlannerTools
from planner.prompts import PLANNER_SYSTEM_POLICY


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


class FakeGeminiResponse:
    parsed = valid_output()
    text = None


class FakeGeminiModels:
    def __init__(self):
        self.calls = []

    def generate_content(self, **kwargs):
        self.calls.append(kwargs)
        return FakeGeminiResponse()


class FakeGeminiClient:
    def __init__(self):
        self.models = FakeGeminiModels()


class PlannerTests(unittest.TestCase):
    def test_valid_mocked_structured_result_passes(self):
        result = PlannerAgent(RecordingProvider(valid_output())).generate(request())
        self.assertEqual(result.status, "Generated")
        self.assertEqual(result.days[0].items[0].attraction_id, "a1")

    def test_multiple_days_with_two_unique_attractions_are_preserved(self):
        output = {
            "days": [
                {"dayNumber": 1, "date": "2026-10-10", "items": [
                    {"attractionId": "a1", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 2500},
                ]},
                {"dayNumber": 2, "date": "2026-10-11", "items": [
                    {"attractionId": "a2", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 1500},
                ]},
                {"dayNumber": 3, "date": "2026-10-12", "items": []},
            ],
            "estimatedCost": 4000,
            "status": "Generated",
        }

        result = PlannerAgent(RecordingProvider(output), max_retries=0).generate(request())

        self.assertEqual(
            [item.attraction_id for day in result.days for item in day.items],
            ["a1", "a2"],
        )
        self.assertEqual(result.days[2].items, [])
        self.assertEqual(result.estimated_cost, Decimal("4000"))

    def test_duplicate_attraction_proposed_across_days_is_skipped(self):
        output = {
            "days": [
                {"dayNumber": 1, "date": "2026-10-10", "items": [
                    {"attractionId": "a1", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 2500},
                ]},
                {"dayNumber": 2, "date": "2026-10-11", "items": [
                    {"attractionId": "a1", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 2500},
                    {"attractionId": "a2", "startTime": "11:00", "endTime": "12:00", "estimatedCost": 1500},
                ]},
                {"dayNumber": 3, "date": "2026-10-12", "items": []},
            ],
            "estimatedCost": 6500,
            "status": "Generated",
        }

        result = PlannerAgent(RecordingProvider(output), max_retries=0).generate(request())

        self.assertEqual(
            [item.attraction_id for day in result.days for item in day.items],
            ["a1", "a2"],
        )
        self.assertEqual(result.estimated_cost, Decimal("4000"))

    def test_only_one_suitable_attraction_leaves_other_days_empty(self):
        output = {
            "days": [
                {"dayNumber": 1, "date": "2026-10-10", "items": [
                    {"attractionId": "a1", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 2500},
                ]},
                {"dayNumber": 2, "date": "2026-10-11", "items": [
                    {"attractionId": "a1", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 2500},
                ]},
                {"dayNumber": 3, "date": "2026-10-12", "items": [
                    {"attractionId": "a1", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 2500},
                ]},
            ],
            "estimatedCost": 7500,
            "status": "Generated",
        }
        one_candidate_request = request(
            candidateAttractions=[
                {"id": "a1", "name": "Temple", "category": "Culture", "region": "Kandy", "price": 2500},
            ]
        )

        result = PlannerAgent(RecordingProvider(output), max_retries=0).generate(one_candidate_request)

        self.assertEqual(sum(len(day.items) for day in result.days), 1)
        self.assertEqual([len(day.items) for day in result.days], [1, 0, 0])
        self.assertEqual(result.estimated_cost, Decimal("2500"))

    def test_normalization_does_not_bypass_budget_validation(self):
        output = {
            "days": [{"dayNumber": 1, "date": "2026-10-10", "items": [
                {"attractionId": "a1", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 2500},
                {"attractionId": "a2", "startTime": "11:00", "endTime": "12:00", "estimatedCost": 1500},
            ]}],
            "estimatedCost": 4000,
            "status": "Generated",
        }

        with self.assertRaises(PlannerValidationError) as raised:
            PlannerAgent(RecordingProvider(output), max_retries=0).generate(request(budget=3000))

        self.assertIn("budget", raised.exception.diagnostic_message)

    def test_successful_result_contains_exact_bounded_operational_trace(self):
        result = PlannerAgent(RecordingProvider(valid_output())).generate(request())
        self.assertIsInstance(result.trace, AgentExecutionTrace)
        self.assertEqual(
            [step.tool for step in result.trace.steps],
            [
                "inspect_trip_constraints",
                "inspect_candidate_attractions",
                "calculate_budget_usage",
                "check_schedule_conflicts",
                "validate_plan_constraints",
            ],
        )
        self.assertLessEqual(len(result.trace.steps), 5)
        self.assertTrue(all(step.status == "Completed" for step in result.trace.steps))
        self.assertIsNone(result.trace.safe_failure)

    def test_trace_rejects_unbounded_or_private_fields(self):
        with self.assertRaises(ValidationError):
            AgentExecutionTrace(
                agent="Planner",
                responsibility="Plan",
                inputSummary="Input",
                prompt="must not be accepted",
            )

    def test_each_planner_tool_normal_path(self):
        planner_request = request()
        planner_output = PlannerOutput.model_validate(valid_output())
        self.assertIn("budget", PlannerTools.inspect_trip_constraints(planner_request).result_summary)
        self.assertIn("2 trusted", PlannerTools.inspect_candidate_attractions(planner_request).result_summary)
        self.assertIn("2500", PlannerTools.calculate_budget_usage(planner_request, planner_output).result_summary)
        self.assertIn("no conflicts", PlannerTools.check_schedule_conflicts(planner_request, planner_output).result_summary)
        self.assertIn("passed", PlannerTools.validate_plan_constraints(planner_request, planner_output).result_summary)

    def test_policy_prefers_multiple_grounded_candidates_without_forcing_a_count(self):
        policy = " ".join(PLANNER_SYSTEM_POLICY.lower().split())
        self.assertIn("prefer a useful multi-item day or multi-day itinerary", policy)
        self.assertIn("one item is correct when only one candidate is suitable", policy)
        self.assertIn("do not invent duration, availability, travel", policy)

    def test_candidate_inspection_rejects_duplicate_ids(self):
        planner_request = request(candidateAttractions=[
            {"id": "a1", "name": "One", "price": 100},
            {"id": "a1", "name": "Duplicate", "price": 100},
        ])
        with self.assertRaises(PlannerToolError):
            PlannerTools.inspect_candidate_attractions(planner_request)

    def test_budget_tool_rejects_budget_violation(self):
        planner_request = request(budget=100)
        planner_output = PlannerOutput.model_validate(valid_output())
        with self.assertRaises(PlannerToolError):
            PlannerTools.calculate_budget_usage(planner_request, planner_output)

    def test_schedule_tool_rejects_unknown_candidate(self):
        planner_request = request()
        invalid = valid_output()
        invalid["days"][0]["items"][0]["attractionId"] = "unknown"
        planner_output = PlannerOutput.model_validate(invalid)
        with self.assertRaises(PlannerToolError):
            PlannerTools.check_schedule_conflicts(planner_request, planner_output)

    def test_validation_tool_rejects_generated_output_without_days(self):
        planner_request = request()
        planner_output = PlannerOutput.model_validate({"days": [], "estimatedCost": 0, "status": "Generated"})
        with self.assertRaises(ValueError):
            PlannerTools.validate_plan_constraints(planner_request, planner_output)

    def test_invalid_planner_output_exposes_bounded_safe_failure_trace(self):
        invalid = valid_output()
        invalid["days"][0]["items"][0]["attractionId"] = "unknown"
        with self.assertRaises(PlannerValidationError) as raised:
            PlannerAgent(RecordingProvider(invalid), max_retries=0).generate(request())
        self.assertIsNotNone(raised.exception.trace)
        self.assertEqual(raised.exception.trace.safe_failure, "Itinerary item references an unknown candidate attraction.")
        self.assertLessEqual(len(raised.exception.trace.steps), 3)

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

    def test_system_policy_describes_strict_gemini_json_contract(self):
        provider = RecordingProvider(valid_output())
        PlannerAgent(provider).generate(request())
        prompt = provider.calls[0][1]

        for required_text in (
            "Return exactly one JSON object and nothing else",
            "Do not use Markdown fences",
            "dayNumber",
            "attractionId",
            "startTime",
            "endTime",
            "estimatedCost",
            "Use estimatedCost, never cost",
            "Do not output name",
            "extra properties are rejected",
            "status must be exactly",
            '"Generated" or "NoPlan"',
        ):
            self.assertIn(required_text, prompt)

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

    def test_local_dotenv_loads_values_without_overwriting_process_environment(self):
        with tempfile.TemporaryDirectory() as directory:
            env_file = Path(directory) / ".env"
            env_file.write_text(
                "GEMINI_API_KEY=local-secret\nGEMINI_MODEL=local-model\n",
                encoding="utf-8",
            )
            old_key = os.environ.get("GEMINI_API_KEY")
            old_model = os.environ.get("GEMINI_MODEL")
            try:
                os.environ["GEMINI_API_KEY"] = "deployment-secret"
                os.environ["GEMINI_MODEL"] = "deployment-model"
                load_dotenv_file(env_file)
                self.assertEqual(os.environ["GEMINI_API_KEY"], "deployment-secret")
                self.assertEqual(os.environ["GEMINI_MODEL"], "deployment-model")
            finally:
                if old_key is None:
                    os.environ.pop("GEMINI_API_KEY", None)
                else:
                    os.environ["GEMINI_API_KEY"] = old_key
                if old_model is None:
                    os.environ.pop("GEMINI_MODEL", None)
                else:
                    os.environ["GEMINI_MODEL"] = old_model

    def test_gemini_model_is_preferred_with_legacy_planner_model_fallback(self):
        old_key = os.environ.get("GEMINI_API_KEY")
        old_model = os.environ.get("GEMINI_MODEL")
        old_planner_model = os.environ.get("PLANNER_MODEL")
        os.environ["GEMINI_API_KEY"] = "test-key"
        os.environ["GEMINI_MODEL"] = "gemini-configured-model"
        os.environ["PLANNER_MODEL"] = "legacy-model"
        try:
            provider = create_planner_provider()
            self.assertEqual(provider.model, "gemini-configured-model")
        finally:
            for name, value in (("GEMINI_API_KEY", old_key), ("GEMINI_MODEL", old_model), ("PLANNER_MODEL", old_planner_model)):
                if value is None:
                    os.environ.pop(name, None)
                else:
                    os.environ[name] = value

    def test_model_fallback_only_handles_retryable_provider_failures(self):
        class RetryableProvider:
            def __init__(self):
                self.calls = 0

            def generate(self, planner_request, system_prompt):
                self.calls += 1
                raise PlannerProviderError("model unavailable", retryable=True, status_code=503)

        class SuccessfulProvider:
            def generate(self, planner_request, system_prompt):
                return valid_output()

        first = RetryableProvider()
        result = FallbackPlannerModelProvider([first, SuccessfulProvider()]).generate(request(), "policy")
        self.assertEqual(result["status"], "Generated")
        self.assertEqual(first.calls, 1)

        class NonRetryableProvider:
            def generate(self, planner_request, system_prompt):
                raise PlannerProviderError("invalid request", retryable=False, status_code=400)

        class ShouldNotBeCalledProvider:
            def __init__(self):
                self.calls = 0

            def generate(self, planner_request, system_prompt):
                self.calls += 1
                return valid_output()

        second = ShouldNotBeCalledProvider()
        with self.assertRaises(PlannerProviderError):
            FallbackPlannerModelProvider([NonRetryableProvider(), second]).generate(request(), "policy")
        self.assertEqual(second.calls, 0)

    def test_gemini_provider_uses_configured_model_and_gemini_compatible_schema(self):
        old_key = os.environ.get("GEMINI_API_KEY")
        old_gemini_model = os.environ.get("GEMINI_MODEL")
        old_model = os.environ.get("PLANNER_MODEL")
        os.environ["GEMINI_API_KEY"] = "test-key"
        os.environ["GEMINI_MODEL"] = "gemini-test-model"
        try:
            provider = create_planner_provider()
            self.assertIsInstance(provider, GeminiPlannerModelProvider)
            client = FakeGeminiClient()
            provider._client = client
            provider.generate(request(), "policy")
            call = client.models.calls[0]
            self.assertEqual(call["model"], "gemini-test-model")
            config = call["config"]
            self.assertIsInstance(config, types.GenerateContentConfig)
            self.assertEqual(config.response_mime_type, "application/json")
            self.assertIsNone(config.response_schema)
            self.assertEqual(config.response_json_schema, GEMINI_PLANNER_RESPONSE_SCHEMA)
            self.assertNotEqual(
                config.response_json_schema,
                PlannerOutput.model_json_schema(by_alias=True, mode="serialization"),
            )
            self.assertTrue(config.automatic_function_calling.disable)
            self.assertIsNone(config.tools)
        finally:
            if old_key is None:
                os.environ.pop("GEMINI_API_KEY", None)
            else:
                os.environ["GEMINI_API_KEY"] = old_key
            if old_gemini_model is None:
                os.environ.pop("GEMINI_MODEL", None)
            else:
                os.environ["GEMINI_MODEL"] = old_gemini_model
            if old_model is None:
                os.environ.pop("PLANNER_MODEL", None)
            else:
                os.environ["PLANNER_MODEL"] = old_model

    def test_gemini_json_is_validated_by_planner_output_contract(self):
        class Response:
            parsed = None
            text = json.dumps(valid_output())

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        result = PlannerAgent(provider, max_retries=0).generate(request())
        self.assertEqual(result.estimated_cost, Decimal("2500"))

    def test_gemini_missing_required_fields_fail_local_validation(self):
        class Response:
            parsed = None
            text = json.dumps({"days": []})

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        with self.assertRaises(PlannerValidationError):
            PlannerAgent(provider, max_retries=0).generate(request())

    def test_gemini_400_provider_failure_is_not_retried(self):
        class InvalidArgumentError(Exception):
            status_code = 400

        class Models:
            calls = 0

            def generate_content(self, **kwargs):
                self.calls += 1
                raise InvalidArgumentError("INVALID_ARGUMENT")

        class Client:
            models = Models()

        client = Client()
        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = client
        with self.assertRaises(PlannerProviderError) as context:
            provider.generate(request(), "policy")
        self.assertFalse(context.exception.retryable)
        self.assertEqual(client.models.calls, 1)

    def test_gemini_request_schema_is_explicit_and_uses_supported_subset(self):
        forbidden = {
            "$defs", "$ref", "additionalProperties", "pattern", "format",
            "title", "default", "anyOf", "oneOf", "allOf",
        }

        def walk(value):
            if isinstance(value, dict):
                for key, child in value.items():
                    self.assertNotIn(key, forbidden)
                    walk(child)
            elif isinstance(value, list):
                for child in value:
                    walk(child)

        walk(GEMINI_PLANNER_RESPONSE_SCHEMA)
        self.assertEqual(GEMINI_PLANNER_RESPONSE_SCHEMA["required"], ["days", "estimatedCost", "status"])
        day_schema = GEMINI_PLANNER_RESPONSE_SCHEMA["properties"]["days"]["items"]
        self.assertEqual(day_schema["required"], ["dayNumber", "date", "items"])
        item_schema = day_schema["properties"]["items"]["items"]
        self.assertEqual(
            item_schema["required"],
            ["attractionId", "startTime", "endTime", "estimatedCost"],
        )
        self.assertEqual(item_schema["properties"]["estimatedCost"], {"type": "number"})

    def test_gemini_schema_omits_nullable_optional_notes_and_message(self):
        self.assertNotIn("message", GEMINI_PLANNER_RESPONSE_SCHEMA["properties"])
        item_properties = GEMINI_PLANNER_RESPONSE_SCHEMA["properties"]["days"]["items"]["properties"]["items"]["items"]["properties"]
        self.assertNotIn("notes", item_properties)

    def test_nullable_structured_output_values_pass_planner_contract(self):
        output = valid_output()
        output["days"][0]["items"][0]["notes"] = None
        output["message"] = None

        result = PlannerAgent(RecordingProvider(output), max_retries=0).generate(request())
        self.assertIsNone(result.days[0].items[0].notes)
        self.assertIsNone(result.message)

    def test_extra_structured_output_fields_fail_final_planner_validation(self):
        output = valid_output()
        output["unexpected"] = "must be rejected"

        with self.assertRaises(PlannerValidationError):
            PlannerAgent(RecordingProvider(output), max_retries=0).generate(request())

    def test_configuration_failure_does_not_fallback_to_another_model(self):
        class ConfigurationFailureProvider:
            def __init__(self):
                self.calls = 0

            def generate(self, planner_request, system_prompt):
                self.calls += 1
                raise PlannerProviderError(
                    "invalid structured-output schema",
                    retryable=False,
                    stage="provider_configuration",
                )

        class SuccessfulProvider:
            def __init__(self):
                self.calls = 0

            def generate(self, planner_request, system_prompt):
                self.calls += 1
                return valid_output()

        first = ConfigurationFailureProvider()
        second = SuccessfulProvider()
        with self.assertRaises(PlannerProviderError):
            FallbackPlannerModelProvider([first, second]).generate(request(), "policy")
        self.assertEqual(first.calls, 1)
        self.assertEqual(second.calls, 0)

    def test_empty_sdk_parsed_value_falls_back_to_response_text(self):
        class Response:
            parsed = {}
            text = json.dumps(valid_output())

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        result = provider.generate(request(), "policy")
        self.assertEqual(result["status"], "Generated")

    def test_empty_json_response_is_provider_failure(self):
        class Response:
            parsed = {}
            text = "{}"
            candidates = []

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        with self.assertRaises(PlannerProviderError):
            provider.generate(request(), "policy")

    def test_missing_response_content_is_provider_failure(self):
        class Response:
            parsed = None
            text = ""
            candidates = []

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        with self.assertRaises(PlannerProviderError):
            provider.generate(request(), "policy")

    def test_malformed_json_is_provider_failure(self):
        class Response:
            parsed = None
            text = "{not-json}"

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        with self.assertRaises(PlannerProviderError):
            provider.generate(request(), "policy")

    def test_non_object_json_is_provider_failure(self):
        class Response:
            parsed = None
            text = "[1, 2, 3]"

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        with self.assertRaises(PlannerProviderError):
            provider.generate(request(), "policy")

    def test_nested_wrapper_is_not_unwrapped(self):
        wrapped = {"plannerOutput": valid_output()}

        class Response:
            parsed = None
            text = json.dumps(wrapped)

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        self.assertEqual(provider.generate(request(), "policy"), wrapped)

    def test_valid_json_text_object_passes_through(self):
        class Response:
            parsed = None
            text = json.dumps(valid_output())

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        self.assertEqual(provider.generate(request(), "policy"), valid_output())

    def test_one_json_markdown_fence_is_supported(self):
        class Response:
            parsed = None
            text = f"```json\n{json.dumps(valid_output())}\n```"

        class Models:
            def generate_content(self, **kwargs):
                return Response()

        class Client:
            models = Models()

        provider = GeminiPlannerModelProvider("test-key", "gemini-test-model")
        provider._client = Client()
        self.assertEqual(provider.generate(request(), "policy"), valid_output())

    def test_output_contract_still_requires_estimated_cost_and_status(self):
        with self.assertRaises(ValidationError):
            PlannerOutput.model_validate({"days": []})

    def test_deterministic_fixture_is_explicitly_test_only(self):
        result = DeterministicPlannerFixture().generate(request())
        self.assertEqual(result.status, "Generated")
        self.assertEqual(result.days[0].items[0].attraction_id, "a1")

    def test_deterministic_fixture_uses_multiple_grounded_candidates_when_budget_allows(self):
        result = DeterministicPlannerFixture().generate(request())
        self.assertEqual(len(result.days[0].items), 2)
        self.assertEqual({item.attraction_id for item in result.days[0].items}, {"a1", "a2"})
        self.assertLess(result.days[0].items[0].end_time, result.days[0].items[1].start_time)

    def test_deterministic_fixture_keeps_one_candidate_when_budget_allows_only_one(self):
        result = DeterministicPlannerFixture().generate(request(budget=2500))
        self.assertEqual(sum(len(day.items) for day in result.days), 1)
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
