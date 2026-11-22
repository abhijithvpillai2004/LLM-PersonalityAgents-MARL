using System;
using System.IO;
using UnityEngine;

namespace RelayCorridor
{
    /// <summary>
    /// Reward calculator that uses coefficients produced by the LLM-assisted design loop.
    ///
    /// ARCHITECTURE NOTE: The LLM (Gemini) is NOT called at runtime. This class reads a
    /// static JSON file of reward coefficients that were generated during the offline
    /// design phase. The agent running this calculator is a standard PPO policy trained
    /// with those coefficients. All in-game decisions are made by the trained policy.
    /// The LLM's involvement ended when training began.
    ///
    /// This is intentionally different from architectures where an LLM makes runtime
    /// NPC decisions (e.g. Tarigan et al. 2025). Here the LLM is a reward designer,
    /// not a decision-maker.
    /// </summary>
    public class LLMDesignedReward : MonoBehaviour, IRewardCalculator
    {
        [System.Serializable]
        public class EventRewardData
        {
            public float pad_activated = 0.5f;
            public float gate_opened = 1.0f;
            public float cell_picked_up = 0.8f;
            public float cell_delivered = 5.0f;
            public float timeout_penalty = 1.0f;
        }

        [System.Serializable]
        public class RewardCoefficients
        {
            public string persona_name = "LLM Designed Policy";
            public float speed_match_scale = 0.006f;
            public float pad_hold_scale = 0.012f;
            public float cell_proximity_scale = 0.008f;
            public float gate_proximity_scale = 0.010f;
            public float receiver_proximity_scale = 0.015f;
            public float idle_penalty = 0.002f;
            public float push_penalty_scale = 0.050f;
            public float target_speed = 4.0f;
            public float exploration_bonus = 0.030f;
            public EventRewardData event_rewards = new EventRewardData();
        }

        [SerializeField] private string coefficientFilePath = "config/reward_coefficients/llm_current.json";

        private RewardCoefficients _coefficients = new RewardCoefficients();
        private FileSystemWatcher _watcher;

        public string CalculatorType => "LLMDesigned";
        public string PersonaDescription => _coefficients?.persona_name ?? "Loading...";
        public RewardCoefficients Coefficients => _coefficients;

        private void Awake()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", coefficientFilePath);
            string dir = Path.GetDirectoryName(fullPath);
            string file = Path.GetFileName(fullPath);

            if (Directory.Exists(dir))
            {
                _watcher = new FileSystemWatcher(dir, file);
                _watcher.Changed += (_, _) => LoadCoefficients(fullPath);
                _watcher.EnableRaisingEvents = true;
            }

            if (File.Exists(fullPath))
            {
                LoadCoefficients(fullPath);
            }
        }

        public void LoadCoefficients(string path = null)
        {
            string targetPath = path ?? Path.Combine(Application.dataPath, "..", coefficientFilePath);
            if (!File.Exists(targetPath)) return;

            try
            {
                string json = File.ReadAllText(targetPath);
                _coefficients = JsonUtility.FromJson<RewardCoefficients>(json);
                Debug.Log($"[LLMDesignedReward] Loaded coefficients for: {_coefficients.persona_name}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LLMDesignedReward] Could not parse coefficient file: {ex.Message}");
            }
        }

        // Phase gating logic is IDENTICAL to HandDesignedReward.
        // Only coefficient magnitudes differ.
        // This is the key architectural point: the LLM controls reward strength,
        // not reward structure. Structure is fixed and shared across both paradigms.
        public float ComputeStepReward(RelayAgent agent, CorridorEnvironment env)
        {
            if (agent == null || env == null || agent.rb == null || _coefficients == null) return 0f;

            // 1. Speed preference
            float currentSpeed = agent.rb.linearVelocity.magnitude;
            float speedMatch = 1f - Mathf.Clamp01(Mathf.Abs(currentSpeed - _coefficients.target_speed) / 6.0f);
            agent.AddReward(speedMatch * _coefficients.speed_match_scale); // STEP_REWARD

            // 2. Pad holding
            if (agent.IsStandingOnPad && PhaseRequiresPadHolder(env.CurrentPhase))
            {
                agent.AddReward(agent.agreeableness * _coefficients.pad_hold_scale); // STEP_REWARD
            }

            // 3. Phase-gated proximity rewards
            switch (env.CurrentPhase)
            {
                case CorridorEnvironment.PuzzlePhase.PadAPhase:
                    if (!agent.IsStandingOnPad)
                        agent.AddReward(Proximity(agent, env.PadAPosition, env.CorridorLength) * _coefficients.cell_proximity_scale); // STEP_REWARD
                    break;

                case CorridorEnvironment.PuzzlePhase.TransitABPhase:
                    if (!agent.HasCell)
                        agent.AddReward(Proximity(agent, env.CellPosition, env.CorridorLength) * _coefficients.cell_proximity_scale); // STEP_REWARD
                    else
                        agent.AddReward(Proximity(agent, env.Gate1Position, env.CorridorLength) * _coefficients.gate_proximity_scale); // STEP_REWARD
                    break;

                case CorridorEnvironment.PuzzlePhase.PadBCPhase:
                case CorridorEnvironment.PuzzlePhase.PadC_3PadsPhase:
                case CorridorEnvironment.PuzzlePhase.PadD_3PadsPhase:
                    if (agent.HasCell)
                        agent.AddReward(Proximity(agent, env.ChamberBWaitPoint, env.CorridorLength) * 0.006f); // STEP_REWARD
                    else if (!agent.IsStandingOnPad)
                        agent.AddReward(Proximity(agent, env.GetNearestUnactivatedPad(agent.transform.position), env.CorridorLength) * _coefficients.gate_proximity_scale); // STEP_REWARD
                    break;

                case CorridorEnvironment.PuzzlePhase.TransitBCPhase:
                case CorridorEnvironment.PuzzlePhase.TransitCDPhase:
                case CorridorEnvironment.PuzzlePhase.TransitDEPhase:
                    if (agent.HasCell)
                        agent.AddReward(Proximity(agent, env.ReceiverPosition, env.CorridorLength) * _coefficients.receiver_proximity_scale); // STEP_REWARD
                    break;

                case CorridorEnvironment.PuzzlePhase.DeliveryPhase:
                    if (agent.HasCell)
                        agent.AddReward(Proximity(agent, env.ReceiverPosition, env.CorridorLength) * (_coefficients.receiver_proximity_scale + 0.005f)); // STEP_REWARD
                    break;
            }

            // 4. Idle penalty
            if (currentSpeed < 0.2f && !agent.IsStandingOnPad)
            {
                agent.AddReward(-_coefficients.idle_penalty); // STEP_REWARD
            }

            return 0f;
        }

        public float ComputeEventReward(RelayAgent agent, CorridorEnvironment env, RewardEvent evt)
        {
            if (agent == null || _coefficients == null) return 0f;

            switch (evt)
            {
                case RewardEvent.PadActivated:
                    agent.AddReward(_coefficients.event_rewards.pad_activated);
                    return _coefficients.event_rewards.pad_activated;

                case RewardEvent.GateOpened:
                    agent.AddReward(_coefficients.event_rewards.gate_opened);
                    return _coefficients.event_rewards.gate_opened;

                case RewardEvent.CellPickedUp:
                    agent.AddReward(_coefficients.event_rewards.cell_picked_up);
                    return _coefficients.event_rewards.cell_picked_up;

                case RewardEvent.CellDelivered:
                    float timeBonus = env != null ? (float)(5000 - env.CurrentStep) / 5000f * 3.0f : 1.5f;
                    float total = _coefficients.event_rewards.cell_delivered + Mathf.Max(0f, timeBonus);
                    agent.AddReward(total);
                    return total;

                case RewardEvent.EpisodeTimeout:
                    agent.AddReward(-_coefficients.event_rewards.timeout_penalty);
                    return -_coefficients.event_rewards.timeout_penalty;

                case RewardEvent.AgentPushed:
                    float penalty = -(1f - agent.agreeableness) * _coefficients.push_penalty_scale;
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
