"""Shared, bounded operational trace contracts for agent services.

This module deliberately models execution evidence rather than model reasoning.
It contains no prompt, secret, or free-form chain-of-thought fields.
"""

from typing import List, Optional

from pydantic import BaseModel, ConfigDict, Field


class AgentTraceStep(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    sequence: int = Field(gt=0, le=12)
    tool: str = Field(min_length=1, max_length=80)
    purpose: str = Field(min_length=1, max_length=300)
    status: str = Field(pattern=r"^(Completed|Failed|Skipped)$")
    result_summary: str = Field(alias="resultSummary", min_length=1, max_length=500)
    duration_ms: Optional[int] = Field(default=None, alias="durationMs", ge=0, le=120000)


class AgentExecutionTrace(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    agent: str = Field(min_length=1, max_length=80)
    responsibility: str = Field(min_length=1, max_length=300)
    input_summary: str = Field(alias="inputSummary", min_length=1, max_length=500)
    steps: List[AgentTraceStep] = Field(default_factory=list, max_length=12)
    decision: Optional[str] = Field(default=None, max_length=500)
    validation: Optional[str] = Field(default=None, max_length=500)
    output_summary: Optional[str] = Field(default=None, alias="outputSummary", max_length=500)
    safe_failure: Optional[str] = Field(default=None, alias="safeFailure", max_length=500)
    duration_ms: Optional[int] = Field(default=None, alias="durationMs", ge=0, le=120000)

