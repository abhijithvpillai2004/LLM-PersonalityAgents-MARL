"""
scripts/behavior_critique.py
Behavioral alignment evaluator (DESIGN PHASE ONLY)

Sends behavioral telemetry statistics from a completed PPO training run to Gemini 3.8 Flash.
Gemini evaluates alignment against the intended persona profile and baseline agent comparisons,
assigns an alignment score (1-10), critiques behavioral discrepancies, and proposes revised
reward coefficient magnitudes clamped to strictly valid ranges.
"""

import os
import json
import re
import sys
from typing import Dict, Any, Tuple

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
try:
    from scripts.gemini_client import call_gemini
    from scripts.coefficient_designer import clamp_coefficients_to_range, DEFAULT_BASELINE_TABLE
except ImportError:
    from gemini_client import call_gemini
    from coefficient_designer import clamp_coefficients_to_range, DEFAULT_BASELINE_TABLE

def clean_json_markdown(text: str) -> str:
    text = text.strip()
    match = re.search(r"```(?:json)?\s*([\s\S]*?)\s*```", text)
    if match:
        text = match.group(1).strip()
    return text

def critique_behavior(
    persona: Dict[str, Any],
    telemetry: Dict[str, Any],
    coefficients: Dict[str, Any],
    iteration: int,
    eval_episodes: int = 30,
    baseline_table: str = None
) -> Tuple[int, str, str, str, Dict[str, Any]]:
    """
    Sends behavioral telemetry to Gemini 3.8 Flash.
    Returns (score, what_matches, what_does_not_match, rationale, revised_coefficients).
    """
    b_table = baseline_table if baseline_table else DEFAULT_BASELINE_TABLE

    mean_speed = float(telemetry.get("mean_speed", 0.0))
    mean_pad_hold = float(telemetry.get("mean_pad_hold", telemetry.get("pad_hold_steps", 0.0)))
    mean_push = float(telemetry.get("mean_push", telemetry.get("push_count", 0.0)))
    mean_carry = float(telemetry.get("mean_carry", telemetry.get("cell_carry_steps", 0.0)))
    delivery_rate = float(telemetry.get("delivery_rate", 0.0))
    mean_phase = float(telemetry.get("mean_phase_reached", 1.0))

    expected = persona.get("expected_behavioral_profile", {})

    prompt = f"""You are evaluating the output of a PPO reinforcement learning training run.
A reward function was designed to produce an agent with a specific personality in a
cooperative relay corridor. You will assess whether the trained agent's behavior matches
the intended personality and revise the reward coefficients for the next training run.

You are acting as a reward designer, not a game player. You read evaluation statistics
and output revised coefficient magnitudes. You do not control the agent at runtime.

INTENDED PERSONA: {persona.get('natural_language_summary', '')}

EXPECTED BEHAVIORAL PROFILE:
- Speed rank among all agents:    {expected.get('mean_speed_rank', 'medium')}
- Pad hold steps rank:            {expected.get('pad_hold_steps_rank', 'medium')}
- Push count rank:                {expected.get('push_count_rank', 'medium')}
- Cell carry steps rank:          {expected.get('cell_carry_steps_rank', 'medium')}

HAND-DESIGNED BASELINE METRICS (the comparison standard):
{b_table}

BEHAVIORAL TELEMETRY FROM {eval_episodes} EVALUATION EPISODES OF THE TRAINED POLICY:
- mean_speed:         {mean_speed:.3f}
- pad_hold_steps:     {mean_pad_hold:.1f}
- push_count:         {mean_push:.1f}
- cell_carry_steps:   {mean_carry:.1f}
- delivery_rate:      {delivery_rate:.1%}
- mean_phase_reached: {mean_phase:.2f} / 5.0

CURRENT REWARD COEFFICIENTS (the specification for this training run):
{json.dumps(coefficients, indent=2)}

This is iteration {iteration} of the design loop.

Evaluate alignment between observed behavior and intended persona.
Consider: would a human observer watching this agent in a relay corridor episode
perceive the intended personality, or would the behavior be ambiguous or wrong?

Respond ONLY with valid JSON:
{{
  "score": integer 1-10,
  "what_matches": "behavioral aspects that already match the persona",
  "what_does_not_match": "gaps between intended and observed behavior, with reasoning",
  "coefficient_revision_rationale": "which coefficients to change, in which direction, and why",
  "revised_coefficients": {{ complete JSON with exactly the same keys as current }}
}}
"""

    raw_response = call_gemini(prompt, json_mode=True)
    raw_text = clean_json_markdown(raw_response)

    try:
        parsed = json.loads(raw_text)
    except json.JSONDecodeError as jde:
        raise ValueError(f"Gemini returned invalid critique JSON: {jde}\nResponse:\n{raw_text}")

    score = int(parsed.get("score", 5))
    what_matches = parsed.get("what_matches", "None specified.")
    what_does_not_match = parsed.get("what_does_not_match", "None specified.")
    rationale = parsed.get("coefficient_revision_rationale", "None specified.")
    revised = clamp_coefficients_to_range(parsed.get("revised_coefficients", coefficients))
    revised["persona_name"] = coefficients.get("persona_name", "LLM Designed Policy")

    return score, what_matches, what_does_not_match, rationale, revised

if __name__ == "__main__":
    print("[behavior_critique] Module ready.")
