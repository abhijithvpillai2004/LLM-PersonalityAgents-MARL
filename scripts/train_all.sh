#!/bin/bash
# RelayCorridor Baseline Training Pipeline (Paradigm A: Hand-Designed MDPI Baseline)
# DO NOT RUN WITHOUT EXPLICIT APPROVAL

mlagents-learn config/trainer_configs/high_e.yaml --run-id=high_e_v1 --no-graphics
mlagents-learn config/trainer_configs/low_e.yaml  --run-id=low_e_v1  --no-graphics
mlagents-learn config/trainer_configs/high_a.yaml --run-id=high_a_v1 --no-graphics
mlagents-learn config/trainer_configs/low_a.yaml  --run-id=low_a_v1  --no-graphics
