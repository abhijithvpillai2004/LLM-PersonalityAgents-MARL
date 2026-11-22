# Baseline Evaluation Report (Paradigm A: Hand-Designed MDPI Baseline)

- **Total Episodes Evaluated**: 30
- **Team Delivery Rate**: 10/30 (33.3%)
- **Mean Episode Length**: 3842.7 steps

## Per-Agent Behavioral Metrics (Mean ± Std)

| Agent ID | Personality Profile | Mean Speed | Pad Hold Steps | Cell Carry Steps | Push Count |
|---|---|---|---|---|---|
| Agent_0_HighE | High-E | 0.687 ± 0.194 | 3574.3 ± 1368.9 | 21987.0 ± 14829.9 | 186.5 ± 58.5 |
| Agent_1_LowE | Low-E | 0.396 ± 0.066 | 102.4 ± 104.9 | 15860.4 ± 6993.7 | 188.1 ± 57.8 |
| Agent_2_HighA | High-A | 0.063 ± 0.006 | 18118.0 ± 11697.3 | 0.0 ± 0.0 | 10.4 ± 4.0 |
| Agent_3_LowA | Low-A | 0.247 ± 0.041 | 2872.4 ± 1991.9 | 17959.7 ± 10616.2 | 20.7 ± 11.0 |
| LivePolicyAgent | LLM-Designed Policy | 0.109 ± 0.051 | 0.0 ± 0.0 | 0.0 ± 0.0 | 7.9 ± 2.8 |

## Acceptance Criteria Summary

1. **Agent 0 High-E highest mean_speed**: PASS
2. **Agent 1 Low-E lowest mean_speed**: FAIL
3. **Agent 2 High-A highest pad_hold_steps**: PASS
4. **Agent 3 Low-A highest push_count**: FAIL
5. **Team delivery rate > 50%**: FAIL (33.3%)
