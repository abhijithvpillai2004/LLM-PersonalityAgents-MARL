"""
scripts/persona_parser.py
Natural language -> Structured intent (DESIGN PHASE ONLY)

Extracts an OCEAN-aligned structured profile from a plain English personality description
using Gemini 3.8 Flash, strictly validated against a Pydantic schema.
"""

import os
import json
import re
import sys
from typing import Literal
from pydantic import BaseModel, Field, ValidationError

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
try:
    from scripts.gemini_client import call_gemini
except ImportError:
    from gemini_client import call_gemini

class ExpectedBehavioralProfile(BaseModel):
    mean_speed_rank: Literal["lowest", "low", "medium", "high", "highest"]
    pad_hold_steps_rank: Literal["lowest", "low", "medium", "high", "highest"]
    push_count_rank: Literal["lowest", "low", "medium", "high", "highest"]
    cell_carry_steps_rank: Literal["lowest", "low", "medium", "high", "highest"]

class PersonaProfile(BaseModel):
    persona_name: str
    extraversion: float = Field(ge=0.0, le=1.0)
    agreeableness: float = Field(ge=0.0, le=1.0)
    speed_preference: Literal["slow", "moderate", "fast"]
    pad_holding_tendency: Literal["low", "medium", "high"]
    cell_carrying_tendency: Literal["low", "medium", "high"]
    push_tolerance: Literal["avoidant", "neutral", "aggressive"]
    exploration_tendency: Literal["low", "medium", "high"]
    expected_behavioral_profile: ExpectedBehavioralProfile
    natural_language_summary: str

def clean_json_markdown(text: str) -> str:
    text = text.strip()
    match = re.search(r"```(?:json)?\s*([\s\S]*?)\s*```", text)
    if match:
        text = match.group(1).strip()
    return text

def parse_persona(user_input: str, output_path: str = "config/reward_coefficients/persona_latest.json") -> dict:
    if not user_input or not user_input.strip():
        raise ValueError("User personality description cannot be empty.")

    prompt = f"""You are a reward engineering assistant for a multi-agent Unity ML-Agents relay corridor.
Agents cooperate to carry an energy cell through two gated chambers by activating relay
pads. You will read a personality description and extract a structured profile that a
reward designer will use to set reward coefficient magnitudes.

The agent you are designing for is a standard PPO reinforcement learning policy.
You are designing its reward function, not its runtime behavior. The agent will learn
its behavior by optimising the reward signal you help design. It will not call you
at runtime.

User description: "{user_input.strip()}"

Respond ONLY with valid JSON, no preamble, no markdown formatting:
{{
  "persona_name": "short name, 3-5 words",
  "extraversion": float 0.0-1.0,
  "agreeableness": float 0.0-1.0,
  "speed_preference": "slow" or "moderate" or "fast",
  "pad_holding_tendency": "low" or "medium" or "high",
  "cell_carrying_tendency": "low" or "medium" or "high",
  "push_tolerance": "avoidant" or "neutral" or "aggressive",
  "exploration_tendency": "low" or "medium" or "high",
  "expected_behavioral_profile": {{
    "mean_speed_rank": "lowest" or "low" or "medium" or "high" or "highest",
    "pad_hold_steps_rank": "lowest" or "low" or "medium" or "high" or "highest",
    "push_count_rank": "lowest" or "low" or "medium" or "high" or "highest",
    "cell_carry_steps_rank": "lowest" or "low" or "medium" or "high" or "highest"
  }},
  "natural_language_summary": "one sentence: what should a human observer see when watching this agent in the relay corridor?"
}}
"""

    raw_response = call_gemini(prompt, json_mode=True)
    raw_text = clean_json_markdown(raw_response)

    try:
        raw_dict = json.loads(raw_text)
    except json.JSONDecodeError as jde:
        raise ValueError(f"Gemini returned malformed JSON: {jde}\nResponse:\n{raw_text}")

    try:
        validated = PersonaProfile(**raw_dict)
    except ValidationError as ve:
        raise ValueError(f"Gemini output failed PersonaProfile schema validation: {ve}\nRaw dict: {raw_dict}")

    persona_dict = validated.model_dump()
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(persona_dict, f, indent=2)

    return persona_dict

if __name__ == "__main__":
    test_desc = " ".join(sys.argv[1:]) if len(sys.argv) > 1 else "Reckless Charger: extremely fast, impatient, pushes teammates out of the way to grab the energy cell and deliver it immediately."
    print(f"[PersonaParser] Parsing: {test_desc}", flush=True)
    res = parse_persona(test_desc)
    print(f"[PersonaParser] Extracted Persona: {res['persona_name']} (E={res['extraversion']}, A={res['agreeableness']})", flush=True)
    print(json.dumps(res, indent=2), flush=True)
