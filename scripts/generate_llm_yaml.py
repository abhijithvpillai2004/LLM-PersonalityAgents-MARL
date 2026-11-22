"""
scripts/generate_llm_yaml.py
Trainer YAML generator (DESIGN PHASE ONLY)

Generates config/trainer_configs/llm_iter{N}.yaml from the parsed persona and reward coefficients.
Configures beta, curiosity reward signal, and max_steps while preserving identical network
architecture (2 layers, 128 hidden units, 34 vector observations, 3 continuous actions) to baselines.
"""

import os
import json
import sys
import shutil

BETA_MAP = {
    "low": 0.003,
    "medium": 0.007,
    "high": 0.015
}

def generate_llm_yaml(persona: dict, coefficients: dict, iteration: int = 0, max_steps: int = 500000) -> str:
    exploration_tendency = persona.get("exploration_tendency", "medium")
    beta = BETA_MAP.get(exploration_tendency, 0.007)
    curiosity_strength = float(coefficients.get("exploration_bonus", 0.030))
    persona_name = persona.get("persona_name", "LLM Designed Policy")

    yaml_content = f"""# LLM-Designed Reward Policy Trainer Configuration — Iteration {iteration}
# Persona: {persona_name}
# Exploration Tendency: {exploration_tendency} (beta={beta})
# Curiosity Strength: {curiosity_strength} (exploration_bonus)
# Max Steps: {max_steps}

behaviors:
  RelayAgent:
    trainer_type: ppo
    hyperparameters:
      batch_size: 256
      buffer_size: 2048
      learning_rate: 0.0003
      beta: {beta}
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
      learning_rate_schedule: linear
    network_settings:
      normalize: true
      hidden_units: 128
      num_layers: 2
      vis_encode_type: simple
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
      curiosity:
        gamma: 0.99
        strength: {curiosity_strength}
        network_settings:
          hidden_units: 128
        learning_rate: 0.0003
    keep_checkpoints: 5
    max_steps: {max_steps}
    time_horizon: 64
    summary_freq: 5000
"""

    os.makedirs("config/trainer_configs", exist_ok=True)
    os.makedirs("config/trainer_configs/archive", exist_ok=True)

    yaml_path = f"config/trainer_configs/llm_iter{iteration}.yaml"
    arch_path = f"config/trainer_configs/archive/llm_iter{iteration}.yaml"

    with open(yaml_path, "w", encoding="utf-8") as f:
        f.write(yaml_content)
    with open(arch_path, "w", encoding="utf-8") as f:
        f.write(yaml_content)

    print(f"[generate_llm_yaml] Generated {yaml_path} (archived to {arch_path})", flush=True)
    return yaml_path

if __name__ == "__main__":
    persona_path = "config/reward_coefficients/persona_latest.json"
    coeff_path = "config/reward_coefficients/llm_current.json"

    if not os.path.exists(persona_path) or not os.path.exists(coeff_path):
        print("Missing persona or coefficients JSON. Generating sample YAML...", flush=True)
        sample_persona = {"persona_name": "Reckless Cell Charger", "exploration_tendency": "low"}
        sample_coeff = {"exploration_bonus": 0.020}
        path = generate_llm_yaml(sample_persona, sample_coeff, iteration=0, max_steps=500000)
    else:
        with open(persona_path, "r", encoding="utf-8") as f:
            p_data = json.load(f)
        with open(coeff_path, "r", encoding="utf-8") as f:
            c_data = json.load(f)
        path = generate_llm_yaml(p_data, c_data, iteration=0, max_steps=500000)

    print(f"Trainer YAML ready at: {path}")
