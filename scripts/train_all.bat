@echo off
REM RelayCorridor Baseline Training Pipeline (Paradigm A: Hand-Designed MDPI Baseline)
REM DO NOT RUN WITHOUT EXPLICIT APPROVAL

set MLAGENTS_BIN=C:\Users\SiD\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe

"%MLAGENTS_BIN%" config\trainer_configs\high_e.yaml --run-id=high_e_v1 --no-graphics
"%MLAGENTS_BIN%" config\trainer_configs\low_e.yaml  --run-id=low_e_v1  --no-graphics
"%MLAGENTS_BIN%" config\trainer_configs\high_a.yaml --run-id=high_a_v1 --no-graphics
"%MLAGENTS_BIN%" config\trainer_configs\low_a.yaml  --run-id=low_a_v1  --no-graphics