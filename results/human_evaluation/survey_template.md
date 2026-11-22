# RelayCorridor: Human Perceptual Evaluation Survey Instrument

**Study Title**: Perceptual Legibility of Cooperative Multi-Agent Reinforcement Learning Policies  
**Design**: Double-Blind Between/Within-Subjects Evaluation  
**Condition Codes**: `A0`, `A1`, `A2`, `A3`, `LLM` (Participants are blinded to all condition names, OCEAN mappings, and training paradigms).

---

## Instructions for Participants

Welcome to the multi-agent behavior study. You will watch short video clips of autonomous agents operating in a cooperative corridor. In each clip, four agents collaborate to move an energy cell through security chambers by activating relay pads.

For each video, focus your attention on the agent indicated on screen (identified by its neutral visual position or target marker). Please evaluate that specific agent's behavior honestly based on how it appears to you.

There are no right or wrong answers. We are interested strictly in your immediate human perception of how the agents move and interact.

---

## Participant Demographics (Optional)

1. **Participant ID**: `P___`
2. **Age Range**: `[ ] 18–24  [ ] 25–34  [ ] 35–44  [ ] 45+`
3. **Experience with Video Games**: `[ ] None  [ ] Casual  [ ] Frequent  [ ] Expert`
4. **Familiarity with AI / Robotics**: `[ ] None  [ ] Basic  [ ] Advanced`

---

## Evaluation Sheet: Video `[CODE] - Episode [N]`

*Watch the video clip completely before answering.*

### Section A: Behavioral Agreement Scale
Rate how strongly you agree with each statement on a scale from **1 (Strongly Disagree)** to **5 (Strongly Agree)**:

| Item Code | Statement | 1 (Strongly Disagree) | 2 (Disagree) | 3 (Neutral) | 4 (Agree) | 5 (Strongly Agree) | Target Trait (Internal) |
|---|---|:---:|:---:|:---:|:---:|:---:|---|
| **E1** | "This agent seems energetic and active." | [ ] | [ ] | [ ] | [ ] | [ ] | Extraversion (+) |
| **E2** | "This agent seems cautious and hesitant." | [ ] | [ ] | [ ] | [ ] | [ ] | Extraversion (-) |
| **A1** | "This agent seems to help its teammates." | [ ] | [ ] | [ ] | [ ] | [ ] | Agreeableness (+) |
| **A2** | "This agent seems to act in its own interest." | [ ] | [ ] | [ ] | [ ] | [ ] | Agreeableness (-) |
| **L1** | "This agent's behavior was distinctive and recognisable." | [ ] | [ ] | [ ] | [ ] | [ ] | Legibility |
| **L2** | "I could tell this agent apart from the others." | [ ] | [ ] | [ ] | [ ] | [ ] | Differentiation |

### Section B: Open-Ended Personality Attribution
In one or two sentences, how would you describe this agent's personality in your own words?

> *Write your description here:*  
> `__________________________________________________________________________________________`  
> `__________________________________________________________________________________________`

---

## Video Index Key (For Researchers Only — Keep Confidential)

| Video Code Prefix | Actual Policy Identity | OCEAN Trait Profile | Paradigm |
|---|---|---|---|
| `A0_ep[1-10].mp4` | Agent_0_HighE | Extraversion=0.9, Agreeableness=0.5 | Paradigm A (MDPI Baseline) |
| `A1_ep[1-10].mp4` | Agent_1_LowE | Extraversion=0.1, Agreeableness=0.5 | Paradigm A (MDPI Baseline) |
| `A2_ep[1-10].mp4` | Agent_2_HighA | Extraversion=0.5, Agreeableness=0.9 | Paradigm A (MDPI Baseline) |
| `A3_ep[1-10].mp4` | Agent_3_LowA | Extraversion=0.5, Agreeableness=0.1 | Paradigm A (MDPI Baseline) |
| `LLM_ep[1-10].mp4` | LivePolicyAgent | Designed by Gemini Closed-Loop | Paradigm B (Proposed Method) |

---

## Data Collection Schema (`results/human_evaluation/survey_responses.csv`)

Responses collected through this instrument are recorded with the following schema:
```csv
participant_id,video_code,config_code,episode,E1_energetic,E2_cautious,A1_helping,A2_self_interest,L1_distinctive,L2_differentiable,free_text_description
P01,A0_ep1,A0,1,5,1,3,3,4,4,"Fast runner rushing toward the cell."
P01,A2_ep1,A2,1,1,4,5,1,5,4,"Very patient and stood waiting on the pad for everyone."
```
