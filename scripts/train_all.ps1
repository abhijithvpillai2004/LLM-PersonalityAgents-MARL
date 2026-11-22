# RelayCorridor Baseline Training Pipeline (Paradigm A: Hand-Designed MDPI Baseline)
# DO NOT RUN WITHOUT EXPLICIT APPROVAL

$mlagentsBin = "C:\Users\SiD\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe"

Write-Host "[RelayCorridor] Starting High-E training (Agent 0)..." -ForegroundColor Cyan
& $mlagentsBin config/trainer_configs/high_e.yaml --run-id=high_e_v1 --no-graphics

Write-Host "[RelayCorridor] Starting Low-E training (Agent 1)..." -ForegroundColor Blue
& $mlagentsBin config/trainer_configs/low_e.yaml --run-id=low_e_v1 --no-graphics

Write-Host "[RelayCorridor] Starting High-A training (Agent 2)..." -ForegroundColor Green
& $mlagentsBin config/trainer_configs/high_a.yaml --run-id=high_a_v1 --no-graphics

Write-Host "[RelayCorridor] Starting Low-A training (Agent 3)..." -ForegroundColor Red
& $mlagentsBin config/trainer_configs/low_a.yaml --run-id=low_a_v1 --no-graphics

Write-Host "[RelayCorridor] Baseline training complete." -ForegroundColor Green