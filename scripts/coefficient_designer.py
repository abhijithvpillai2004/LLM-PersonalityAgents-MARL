"""
scripts/coefficient_designer.py
Structured persona intent -> Reward coefficient magnitudes (DESIGN PHASE ONLY)

Prompts Gemini 3.8 Flash to design initial PPO reward coefficients from a structured persona
and baseline telemetry comparison table. Validates and strictly clamps all coefficients into valid ranges.
"""

import os
import json
import re
import sys
import shutil
from typing import Dict, Any

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
try:
    from scripts.gemini_client import call_gemini
except ImportError:
    from gemini_client import call_gemini

# Range constraints defined in research specification
COEFFICIENT_RANGES = {
    "speed_match_scale": (0.001, 0.020),
    "pad_hold_scale": (0.001, 0.030),
    "cell_proximity_scale": (0.001, 0.020),
    "gate_proximity_scale": (0.001, 0.020),
    "receiver_proximity_scale": (0.001, 0.025),
    "idle_penalty": (0.000, 0.010),
    "push_penalty_scale": (0.010, 0.100),
    "target_speed": (1.0, 6.0),
    "exploration_bonus": (0.010, 0.100),
    "event_rewards": {
        "pad_activated": (0.1, 2.0),
        "gate_opened": (0.5, 3.0),
        "cell_picked_up": (0.2, 2.0),
        "cell_delivered": (2.0, 10.0),
        "timeout_penalty": (0.2, 3.0),
    }
}

DEFAULT_BASELINE_TABLE = """| Agent Name | Extraversion | Agreeableness | Mean Speed (u/s) | Pad Hold Steps | Push Count | Cell Carry Steps |
|---|---|---|---|---|---|---|
| Agent_0_HighE | 0.9 | 0.5 | 0.687 | 1,842 | 16.3 | 21,987 |
| Agent_1_LowE  | 0.1 | 0.5 | 0.380 | 3,120 | 12.1 | 8,450 |
| Agent_2_HighA | 0.5 | 0.9 | 0.490 | 18,118 | 10.4 | 14,200 |
| Agent_3_LowA  | 0.5 | 0.1 | 0.610 | 2,450 | 26.8 | 19,500 |"""

def clean_json_markdown(text: str) -> str:
    text = text.strip()
    match = re.search(r"```(?:json)?\s*([\s\S]*?)\s*```", text)
    if match:
        text = match.group(1).strip()
    return text

def clamp_coefficients_to_range(coeffs: Dict[str, Any]) -> Dict[str, Any]:
    """Clamps all values into allowed ranges and logs warnings on violations."""
    clamped = dict(coeffs)

    for key, val in COEFFICIENT_RANGES.items():
        if key == "event_rewards":
            continue
        low, high = val
        if key in clamped:
            orig = float(clamped[key])
            c_val = max(low, min(high, orig))
            if c_val != orig:
                print(f"[Warning] Clamped {key}: {orig} -> {c_val} (Range: [{low}, {high}])", flush=True)
            clamped[key] = round(c_val, 4)

    if "event_rewards" in clamped and isinstance(clamped["event_rewards"], dict):
        event_dict = dict(clamped["event_rewards"])
        for evt_key, (low, high) in COEFFICIENT_RANGES["event_rewards"].items():
            if evt_key in event_dict:
                orig = float(event_dict[evt_key])
                c_val = max(low, min(high, orig))
                if c_val != orig:
                    print(f"[Warning] Clamped event_rewards.{evt_key}: {orig} -> {c_val} (Range: [{low}, {high}])", flush=True)
                event_dict[evt_key] = round(c_val, 4)
            else:
                event_dict[evt_key] = round(low, 4)
        clamped["event_rewards"] = event_dict
    else:
        clamped["event_rewards"] = {k: v[0] for k, v in COEFFICIENT_RANGES["event_rewards"].items()}

    return clamped

def design_initial_coefficients(persona: dict, baseline_summary: str = None) -> dict:
    baseline_table = baseline_summary if baseline_summary else DEFAULT_BASELINE_TABLE

    prompt = f"""You are a reward coefficient designer. You are setting the magnitudes of reward
components for a PPO reinforcement learning agent in a cooperative relay corridor.

IMPORTANT: You are designing a reward function that will shape a learning agent's
behavior during training. You are not programming the agent's decisions directly.
The agent will learn by optimising the reward signal you design over many training
steps. Small coefficient changes can have large behavioral effects after 500,000
training steps.

The phase-gated reward structure is fixed — you cannot change which reward is active
in which puzzle phase. You control only the magnitudes.

Allowed ranges:
speed_match_scale: 0.001–0.020
pad_hold_scale: 0.001–0.030
cell_proximity_scale: 0.001–0.020
gate_proximity_scale: 0.001–0.020
receiver_proximity_scale: 0.001–0.025
idle_penalty: 0.000–0.010
push_penalty_scale: 0.010–0.100
target_speed: 1.0–6.0
exploration_bonus: 0.010–0.100
event_rewards.pad_activated: 0.1–2.0
event_rewards.gate_opened: 0.5–3.0
event_rewards.cell_picked_up: 0.2–2.0
event_rewards.cell_delivered: 2.0–10.0
event_rewards.timeout_penalty: 0.2–3.0

Persona to implement:
{json.dumps(persona, indent=2)}

Baseline agent metrics for comparison:
{baseline_table}

Respond ONLY with valid JSON using exactly this schema:
{{
  "persona_name": "{persona.get('persona_name', 'LLM Designed Policy')}",
  "speed_match_scale": float,
  "pad_hold_scale": float,
  "cell_proximity_scale": float,
  "gate_proximity_scale": float,
  "receiver_proximity_scale": float,
  "idle_penalty": float,
  "push_penalty_scale": float,
  "target_speed": float,
  "exploration_bonus": float,
  "event_rewards": {{
    "pad_activated": float,
    "gate_opened": float,
    "cell_picked_up": float,
    "cell_delivered": float,
    "timeout_penalty": float
  }}
}}
"""

    raw_response = call_gemini(prompt, json_mode=True)
    raw_text = clean_json_markdown(raw_response)

    try:
        raw_dict = json.loads(raw_text)
    except json.JSONDecodeError as jde:
        raise ValueError(f"Gemini returned malformed coefficient JSON: {jde}\nResponse:\n{raw_text}")

    clamped_dict = clamp_coefficients_to_range(raw_dict)
    clamped_dict["persona_name"] = persona.get("persona_name", "LLM Designed Policy")

    # Save to disk
    os.makedirs("config/reward_coefficients", exist_ok=True)
    os.makedirs("config/reward_coefficients/archive", exist_ok=True)

    iter0_path = "config/reward_coefficients/llm_iter0.json"
    curr_path = "config/reward_coefficients/llm_current.json"
    arch_path = "config/reward_coefficients/archive/llm_iter0.json"

    with open(iter0_path, "w", encoding="utf-8") as f:
        json.dump(clamped_dict, f, indent=2)
    with open(curr_path, "w", encoding="utf-8") as f:
        json.dump(clamped_dict, f, indent=2)
    with open(arch_path, "w", encoding="utf-8") as f:
        json.dump(clamped_dict, f, indent=2)

    return clamped_dict

if __name__ == "__main__":
    persona_path = "config/reward_coefficients/persona_latest.json"
    if not os.path.exists(persona_path):
        print(f"Error: {persona_path} does not exist. Run persona_parser.py first.", flush=True)
        sys.exit(1)

    with open(persona_path, "r", encoding="utf-8") as f:
        persona_data = json.load(f)

    print(f"[CoefficientDesigner] Generating coefficients for: {persona_data.get('persona_name')}", flush=True)
    coeffs = design_initial_coefficients(persona_data)
    print(f"[CoefficientDesigner] Generated Coefficients:")
    print(json.dumps(coeffs, indent=2))
