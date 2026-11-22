# RelayCorridor Quick 300k Proof-of-Concept Baseline Training Pipeline

$mlagentsBin = "C:\Users\SiD\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe"

Write-Host "[RelayCorridor] Starting Quick High-E training (300k steps)..." -ForegroundColor Cyan
& $mlagentsBin config/trainer_configs_quick/high_e_300k.yaml --run-id=high_e_poc --no-graphics --force

Write-Host "[RelayCorridor] Starting Quick Low-E training (300k steps)..." -ForegroundColor Blue
& $mlagentsBin config/trainer_configs_quick/low_e_300k.yaml --run-id=low_e_poc --no-graphics --force

Write-Host "[RelayCorridor] Starting Quick High-A training (300k steps)..." -ForegroundColor Green
& $mlagentsBin config/trainer_configs_quick/high_a_300k.yaml --run-id=high_a_poc --no-graphics --force

Write-Host "[RelayCorridor] Starting Quick Low-A training (300k steps)..." -ForegroundColor Red
& $mlagentsBin config/trainer_configs_quick/low_a_300k.yaml --run-id=low_a_poc --no-graphics --force

Write-Host "[RelayCorridor] Quick Proof-of-Concept training complete!" -ForegroundColor Green