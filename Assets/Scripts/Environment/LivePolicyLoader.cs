using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Unity.MLAgents.Policies;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RelayCorridor
{
    [System.Serializable]
    public class LoadSignal
    {
        public string modelPath;
        public string coefficientPath;
        public string personaName;
        public string naturalLanguageSummary;
        public float alignmentScore;
        public int iteration;
        public bool converged;
        public int totalIterations;
        public double timestamp;
    }

    [System.Serializable]
    public class PersonaProfileData
    {
        public string persona_name;
        public float extraversion;
        public float agreeableness;
        public string natural_language_summary;
    }

    /// <summary>
    /// Watches for a load signal file written by the Python design pipeline.
    /// When a signal appears, loads the trained ONNX model into the live policy slot
    /// and activates the agent in the current running episode.
    ///
    /// This class is the boundary between the design phase and the inference phase.
    /// After loading, the LLM is no longer involved. The agent runs its PPO policy.
    /// </summary>
    public class LivePolicyLoader : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RelayAgent livePolicySlot;
        [SerializeField] private LiveSlotVisuals slotVisuals;
        [SerializeField] private CorridorHUD hud;
        [SerializeField] private CorridorEnvironment environment;

        [Header("Settings")]
        [SerializeField] private string signalFilePath = "Assets/Models/llm_designed/current/load_signal.json";
        [SerializeField] private float pollIntervalSeconds = 1.0f;

        private float _lastPoll;
        private string _lastLoadedTimestamp = "";

        private void Start()
        {
            FindReferencesIfMissing();
            PollForSignal();
        }

        private void Update()
        {
            if (Time.time - _lastPoll < pollIntervalSeconds) return;
            _lastPoll = Time.time;
            PollForSignal();
        }

        public void FindReferencesIfMissing()
        {
            if (livePolicySlot == null)
            {
                var go = GameObject.Find("LivePolicyAgent_Slot");
                if (go != null) livePolicySlot = go.GetComponent<RelayAgent>();
            }
            if (slotVisuals == null)
            {
                slotVisuals = FindAnyObjectByType<LiveSlotVisuals>();
            }
            if (hud == null)
            {
                hud = FindAnyObjectByType<CorridorHUD>();
            }
            if (environment == null)
            {
                environment = FindAnyObjectByType<CorridorEnvironment>();
            }
        }

        public void PollForSignal()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", signalFilePath);
            if (!File.Exists(fullPath)) return;

            LoadSignal signal;
            try
            {
                string json = File.ReadAllText(fullPath);
                signal = JsonUtility.FromJson<LoadSignal>(json);
            }
            catch
            {
                return; // File being written; try next poll
            }

            if (signal == null || string.IsNullOrEmpty(signal.modelPath)) return;
            if (signal.timestamp.ToString() == _lastLoadedTimestamp) return;
            _lastLoadedTimestamp = signal.timestamp.ToString();

            if (Application.isPlaying)
            {
                StartCoroutine(LoadAndSpawnPolicyCoroutine(signal));
            }
            else
            {
                ExecuteLoadAndSpawn(signal);
            }
        }

        private IEnumerator LoadAndSpawnPolicyCoroutine(LoadSignal signal)
        {
            Debug.Log($"[LivePolicyLoader] Loading trained policy: {signal.personaName} " +
                      $"(alignment score {signal.alignmentScore}/10, iteration {signal.iteration})");
            Debug.Log("[LivePolicyLoader] The LLM design phase is complete. " +
                      "This policy runs PPO inference only. No LLM calls at runtime.");

            FindReferencesIfMissing();

            if (slotVisuals != null)
            {
                slotVisuals.PlayActivationSequence(signal.personaName);
            }
            yield return new WaitForSeconds(0.6f);

            ExecuteLoadAndSpawn(signal);
        }

        public void ExecuteLoadAndSpawn(LoadSignal signal)
        {
            FindReferencesIfMissing();

            // Load ONNX from disk into a Unity ModelAsset
            Unity.InferenceEngine.ModelAsset model = null;
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(signal.modelPath, ImportAssetOptions.ForceUpdate);
            model = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(signal.modelPath);
#endif

            if (model == null)
            {
                Debug.LogError($"[LivePolicyLoader] Model load failed: {signal.modelPath}");
                if (slotVisuals != null) slotVisuals.ShowError("Policy load failed");
                return;
            }

            if (livePolicySlot != null)
            {
                // Apply trained model to BehaviorParameters
                var bp = livePolicySlot.GetComponent<BehaviorParameters>();
                if (bp != null)
                {
                    bp.Model = model;
                    bp.BehaviorType = BehaviorType.InferenceOnly;
                }

                // Load static coefficient JSON
                var reward = livePolicySlot.GetComponent<LLMDesignedReward>();
                if (reward != null)
                {
                    string coeffPath = Path.Combine(Application.dataPath, "..", signal.coefficientPath);
                    reward.LoadCoefficients(coeffPath);
                }

                // Apply E and A values from the parsed persona
                string personaPath = Path.Combine(Application.dataPath, "..", "config/reward_coefficients/persona_latest.json");
                if (File.Exists(personaPath))
                {
                    try
                    {
                        string personaJson = File.ReadAllText(personaPath);
                        var persona = JsonUtility.FromJson<PersonaProfileData>(personaJson);
                        if (persona != null)
                        {
                            livePolicySlot.extraversion = persona.extraversion;
                            livePolicySlot.agreeableness = persona.agreeableness;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LivePolicyLoader] Could not read persona traits: {ex.Message}");
                    }
                }

                livePolicySlot.SetAgentIdentity(signal.personaName, new Color(1.0f, 0.85f, 0.2f), livePolicySlot.extraversion, livePolicySlot.agreeableness);
                livePolicySlot.Initialize();

                // Join the CURRENT episode in progress — do NOT reset.
                // The audience watches the policy appear and immediately begin behaving.
                // This is the demo moment.
                livePolicySlot.gameObject.SetActive(true);
            }

            if (environment != null)
            {
                environment.RegisterLivePolicy(signal.personaName);
            }

            if (hud != null)
            {
                hud.UpdateLivePolicyPanel(
                    signal.personaName,
                    signal.naturalLanguageSummary,
                    signal.alignmentScore,
                    signal.iteration,
                    signal.converged,
                    signal.totalIterations);
            }

            if (slotVisuals != null)
            {
                slotVisuals.SetActive(signal.personaName, signal.alignmentScore, signal.iteration, signal.totalIterations);
            }

            Debug.Log($"[LivePolicyLoader] {signal.personaName} is now running as a live PPO policy in the scene.");
        }
    }
}
