@echo off
REM RelayCorridor Quick 300k Proof-of-Concept Baseline Training Pipeline

set MLAGENTS_BIN=C:\Users\SiD\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe

echo [RelayCorridor] Starting Quick High-E training (300k steps)...
"%MLAGENTS_BIN%" config\trainer_configs_quick\high_e_300k.yaml --run-id=high_e_poc --no-graphics --force

echo [RelayCorridor] Starting Quick Low-E training (300k steps)...
"%MLAGENTS_BIN%" config\trainer_configs_quick\low_e_300k.yaml --run-id=low_e_poc --no-graphics --force

echo [RelayCorridor] Starting Quick High-A training (300k steps)...
"%MLAGENTS_BIN%" config\trainer_configs_quick\high_a_300k.yaml --run-id=high_a_poc --no-graphics --force

echo [RelayCorridor] Starting Quick Low-A training (300k steps)...
"%MLAGENTS_BIN%" config\trainer_configs_quick\low_a_300k.yaml --run-id=low_a_poc --no-graphics --force

echo [RelayCorridor] Quick Proof-of-Concept training complete!