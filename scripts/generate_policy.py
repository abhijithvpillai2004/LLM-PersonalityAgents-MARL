"""
scripts/generate_policy.py
Option A: Python CLI entry point for LLM-assisted reward design pipeline.

Interactive CLI with terminal streaming and ASCII architecture banners.
"""

import os
import sys
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

import argparse
import time

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
try:
    from scripts.llm_reward_loop import run_pipeline
except ImportError:
    from llm_reward_loop import run_pipeline

BANNER = """
==================================================================
        RELAY CORRIDOR -- LLM-ASSISTED POLICY GENERATOR                  
==================================================================
"""

def cli_callback(stage: str, message: str, data: any = None):
    timestamp = time.strftime("%H:%M:%S")
    prefix = f"[{timestamp}] [{stage.upper()}]"
    
    if stage == "parsing":
        print(f"\n{prefix} {message}")
        if data and isinstance(data, dict):
            print(f"  --> Extracted: \"{data.get('persona_name')}\" — E={data.get('extraversion')}, A={data.get('agreeableness')}")
            print(f"  --> Profile: Speed={data.get('speed_preference')} | Pad={data.get('pad_holding_tendency')} | Push={data.get('push_tolerance')}")
    elif stage == "coefficients":
        print(f"\n{prefix} {message}")
        if data and isinstance(data, dict):
            print(f"  --> target_speed={data.get('target_speed')} | pad_hold_scale={data.get('pad_hold_scale')} | push_penalty={data.get('push_penalty_scale')}")
    elif stage == "training":
        if isinstance(data, dict) and "steps" in data:
            print(f"\n{prefix} {message}")
        else:
            print(f"  {message}")
    elif stage == "evaluating":
        print(f"\n{prefix} {message}")
        if data and isinstance(data, dict):
            print(f"  --> Telemetry: speed={data.get('mean_speed')} u/s, pad_hold={data.get('mean_pad_hold')} steps, pushes={data.get('mean_push')}, deliveries={data.get('delivery_rate'):.1%}")
    elif stage == "critique":
        print(f"\n{prefix} {message}")
        if data and isinstance(data, dict):
            score = data.get("score", 0)
            color_badge = "★" * score + "☆" * (10 - score)
            print(f"  --> Alignment Score: [{score}/10] {color_badge}")
            if data.get("what_does_not_match"):
                print(f"  --> Critique: {data.get('what_does_not_match')}")
            if data.get("rationale"):
                print(f"  --> Rationale: {data.get('rationale')}")
    elif stage == "converged":
        print(f"\n══════════════════════════════════════════════════════════")
        print(f"  [CONVERGED] {message}")
        print(f"══════════════════════════════════════════════════════════")
    elif stage == "spawning":
        print(f"\n{prefix} {message}")
        print("\n[READY] The LLM design phase is complete. Runtime is PPO inference only.")
        print("Watch the Unity scene: the policy will materialize and begin behaving immediately.\n")
    elif stage == "failed":
        print(f"\n[ERROR] {message}")

def main():
    parser = argparse.ArgumentParser(description="LLM-Assisted PPO Reward Policy Generator")
    parser.add_argument("--description", "-d", type=str, default="", help="Natural language personality description")
    parser.add_argument("--steps", "-s", type=int, default=500000, help="Training steps per iteration")
    parser.add_argument("--max-iter", "-m", type=int, default=10, help="Maximum design loop iterations")
    parser.add_argument("--fast", action="store_true", help="Run in quick demonstration mode (10,000 steps per iter)")
    args = parser.parse_args()

    print(BANNER)

    user_desc = args.description.strip()
    if not user_desc:
        print("Describe your agent's personality in plain English:\n")
        try:
            user_desc = input("> ").strip()
        except EOFError:
            user_desc = "Reckless Charger: extremely fast, impatient, pushes teammates out of the way to grab the energy cell and deliver it immediately."

    if not user_desc:
        user_desc = "Reckless Charger: extremely fast, impatient, pushes teammates out of the way to grab the energy cell and deliver it immediately."

    print(f"\n[TARGET PERSONA] \"{user_desc}\"")
    run_pipeline(
        user_input=user_desc,
        progress_callback=cli_callback,
        max_iterations=args.max_iter,
        steps_per_iter=args.steps,
        fast_mode=args.fast
    )

if __name__ == "__main__":
    main()
