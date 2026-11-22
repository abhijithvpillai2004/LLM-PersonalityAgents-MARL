using UnityEngine;

namespace RelayCorridor
{
    public class HandDesignedReward : MonoBehaviour, IRewardCalculator
    {
        [Header("OCEAN Theory Coefficients (MDPI Baseline)")]
        [SerializeField] private float speedMatchScale = 0.006f;
        [SerializeField] private float padHoldScale = 0.012f;
        [SerializeField] private float cellProximityScale = 0.008f;
        [SerializeField] private float gateProximityScale = 0.010f;
        [SerializeField] private float receiverProximityScale = 0.015f;
        [SerializeField] private float idlePenalty = 0.002f;
        [SerializeField] private float pushPenaltyScale = 0.05f;

        [Header("Speed Dial Bounds")]
        [SerializeField] private float minTargetSpeed = 2.0f;
        [SerializeField] private float maxTargetSpeed = 6.0f;

        [Header("Event Rewards")]
        [SerializeField] private float padActivatedReward = 0.5f;
        [SerializeField] private float gateOpenedReward = 1.0f;
        [SerializeField] private float cellPickedUpReward = 0.8f;
        [SerializeField] private float cellDeliveredReward = 5.0f;
        [SerializeField] private float timeoutPenalty = 1.0f;

        public string CalculatorType => "HandDesigned";
        public string PersonaDescription => "Hand-Crafted MDPI Baseline (Gaussian Dial Shaping)";

        public float ComputeStepReward(RelayAgent agent, CorridorEnvironment env)
        {
            if (agent == null || env == null || agent.rb == null) return 0f;

            // STEP REWARDS — every step, all additive via AddReward()

            // Dynamic Phase Multipliers from 60-second curriculum cycle:
            // Minute 1: Personality Expression (pMult = 1.7x, tMult = 0.3x)
            // Minute 2: Task Objective Solving (pMult = 0.3x, tMult = 1.7x)
            float pMult = (env.PersonalityRewardWeight / 0.5f);
            float tMult = (env.TaskRewardWeight / 0.5f);

            // 1. Speed preference — Extraversion axis (Personality Scaled)
            // High-E agents are rewarded for moving fast; Low-E agents for moving slowly.
            float targetSpeed = Mathf.Lerp(minTargetSpeed, maxTargetSpeed, agent.extraversion);
            float currentSpeed = agent.rb.linearVelocity.magnitude;
            float speedMatch = 1f - Mathf.Clamp01(Mathf.Abs(currentSpeed - targetSpeed) / maxTargetSpeed);
            agent.AddReward(speedMatch * speedMatchScale * pMult); // STEP_REWARD

            // 2. Pad holding — Agreeableness axis, phase-gated (Personality Scaled)
            // High-A agents are more willing to hold pads for teammates.
            if (agent.IsStandingOnPad && PhaseRequiresPadHolder(env.CurrentPhase))
            {
                agent.AddReward(agent.agreeableness * padHoldScale * pMult); // STEP_REWARD
            }

            // 3. Phase-gated proximity rewards — only one phase active at a time (Task Scaled)
            // These guide all agents toward the current task goal regardless of personality.
            switch (env.CurrentPhase)
            {
                case CorridorEnvironment.PuzzlePhase.PadAPhase:
                    if (!agent.IsStandingOnPad)
                    {
                        agent.AddReward(Proximity(agent, env.PadAPosition, env.CorridorLength) * cellProximityScale * tMult); // STEP_REWARD
                    }
                    break;

                case CorridorEnvironment.PuzzlePhase.TransitABPhase:
                    if (!agent.HasCell)
                    {
                        agent.AddReward(Proximity(agent, env.CellPosition, env.CorridorLength) * cellProximityScale * tMult); // STEP_REWARD
                    }
                    else
                    {
                        agent.AddReward(Proximity(agent, env.Gate1Position, env.CorridorLength) * gateProximityScale * tMult); // STEP_REWARD
                    }
                    break;

                case CorridorEnvironment.PuzzlePhase.PadBCPhase:
                case CorridorEnvironment.PuzzlePhase.PadC_3PadsPhase:
                case CorridorEnvironment.PuzzlePhase.PadD_3PadsPhase:
                    if (agent.HasCell)
                    {
                        agent.AddReward(Proximity(agent, env.ChamberBWaitPoint, env.CorridorLength) * 0.006f * tMult); // STEP_REWARD
                    }
                    else if (!agent.IsStandingOnPad)
                    {
                        Vector3 nearestPad = env.GetNearestUnactivatedPad(agent.transform.position);
                        agent.AddReward(Proximity(agent, nearestPad, env.CorridorLength) * gateProximityScale * tMult); // STEP_REWARD
                    }
                    break;

                case CorridorEnvironment.PuzzlePhase.TransitBCPhase:
                case CorridorEnvironment.PuzzlePhase.TransitCDPhase:
                case CorridorEnvironment.PuzzlePhase.TransitDEPhase:
                    if (agent.HasCell)
                    {
                        agent.AddReward(Proximity(agent, env.ReceiverPosition, env.CorridorLength) * receiverProximityScale * tMult); // STEP_REWARD
                    }
                    break;

                case CorridorEnvironment.PuzzlePhase.DeliveryPhase:
                    if (agent.HasCell)
                    {
                        agent.AddReward(Proximity(agent, env.ReceiverPosition, env.CorridorLength) * (receiverProximityScale + 0.005f) * tMult); // STEP_REWARD
                    }
                    break;
            }

            // 4. Idle penalty
            if (currentSpeed < 0.2f && !agent.IsStandingOnPad)
            {
                agent.AddReward(-idlePenalty * tMult); // STEP_REWARD
            }

            return 0f;
        }

        public float ComputeEventReward(RelayAgent agent, CorridorEnvironment env, RewardEvent evt)
        {
            if (agent == null) return 0f;

            float pMult = env != null ? (env.PersonalityRewardWeight / 0.5f) : 1f;
            float tMult = env != null ? (env.TaskRewardWeight / 0.5f) : 1f;

            switch (evt)
            {
                case RewardEvent.PadActivated:
                    float padR = padActivatedReward * tMult;
                    agent.AddReward(padR);
                    return padR;

                case RewardEvent.GateOpened:
                    float gateR = gateOpenedReward * tMult;
                    agent.AddReward(gateR);
                    return gateR;

                case RewardEvent.CellPickedUp:
                    float cellR = cellPickedUpReward * tMult;
                    agent.AddReward(cellR);
                    return cellR;

                case RewardEvent.CellDelivered:
                    float timeBonus = env != null ? (float)(5000 - env.CurrentStep) / 5000f * 3.0f : 1.5f;
                    float totalDelivery = (cellDeliveredReward + Mathf.Max(0f, timeBonus)) * tMult;
                    agent.AddReward(totalDelivery);
                    return totalDelivery;

                case RewardEvent.EpisodeTimeout:
                    agent.AddReward(-timeoutPenalty);
                    return -timeoutPenalty;

                case RewardEvent.AgentPushed:
                    // Low agreeableness agents tolerate pushing more; high agreeableness agents are penalized more
                    float penalty = -(1f - agent.agreeableness) * pushPenaltyScale * pMult;
                    agent.AddReward(penalty);
                    return penalty;

                default:
                    return 0f;
            }
        }

        private float Proximity(RelayAgent agent, Vector3 target, float maxDistance)
        {
            float dist = Vector3.Distance(agent.transform.position, target);
            return Mathf.Clamp01(1f - (dist / Mathf.Max(1f, maxDistance)));
        }

        private bool PhaseRequiresPadHolder(CorridorEnvironment.PuzzlePhase phase)
        {
            return phase == CorridorEnvironment.PuzzlePhase.PadAPhase ||
                   phase == CorridorEnvironment.PuzzlePhase.TransitABPhase ||
                   phase == CorridorEnvironment.PuzzlePhase.PadBCPhase ||
                   phase == CorridorEnvironment.PuzzlePhase.TransitBCPhase ||
                   phase == CorridorEnvironment.PuzzlePhase.PadC_3PadsPhase ||
                   phase == CorridorEnvironment.PuzzlePhase.TransitCDPhase ||
                   phase == CorridorEnvironment.PuzzlePhase.PadD_3PadsPhase ||
                   phase == CorridorEnvironment.PuzzlePhase.TransitDEPhase;
        }
    }
}
