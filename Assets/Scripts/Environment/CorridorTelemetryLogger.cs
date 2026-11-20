using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RelayCorridor
{
    public class CorridorTelemetryLogger : MonoBehaviour
    {
        [Header("Telemetry Settings")]
        [SerializeField] private bool enableLogging = true;
        [SerializeField] private int targetEpisodes = 30;
        [SerializeField] private string outputRelativePath = "results/hand_designed/eval_baseline.csv";
        [SerializeField] private bool autoStopOnTargetReached = true;

        private CorridorEnvironment environment;
        private List<RelayAgent> agents = new List<RelayAgent>();
        private StringBuilder csvBuffer = new StringBuilder();
        private int recordedEpisodes = 0;
        private bool headerWritten = false;

        public int RecordedEpisodes => recordedEpisodes;
        public int TargetEpisodes => targetEpisodes;

        private void Awake()
        {
            environment = GetComponent<CorridorEnvironment>();
            if (environment == null) environment = FindAnyObjectByType<CorridorEnvironment>();
        }

        private void OnEnable()
        {
            if (environment != null)
            {
                environment.OnEpisodeCompleted += HandleEpisodeCompleted;
            }
        }

        private void OnDisable()
        {
            if (environment != null)
            {
                environment.OnEpisodeCompleted -= HandleEpisodeCompleted;
            }
            FlushToDisk();
        }

        private void Start()
        {
            RefreshAgentList();
            InitCsv();
        }

        private void RefreshAgentList()
        {
            agents.Clear();
            var found = FindObjectsByType<RelayAgent>(FindObjectsSortMode.None);
            foreach (var a in found)
            {
                if (a != null && a.gameObject.activeInHierarchy)
                {
                    agents.Add(a);
                }
            }
            agents.Sort((a, b) => string.Compare(a.AgentId, b.AgentId, StringComparison.Ordinal));
        }

        private void InitCsv()
        {
            if (headerWritten) return;
            csvBuffer.AppendLine("episode,agent_id,personality,extraversion,agreeableness,mean_speed,pad_hold_steps,cell_carry_steps,push_count,phase_reached,delivered,episode_length");
            headerWritten = true;
        }

        private void HandleEpisodeCompleted()
        {
            if (!enableLogging || environment == null) return;

            RefreshAgentList();
            recordedEpisodes++;

            int ep = recordedEpisodes;
            string phase = environment.CurrentPhase.ToString();
            bool delivered = environment.IsCellDelivered;
            int length = environment.CurrentStep;

            foreach (var agent in agents)
            {
                if (agent == null) continue;
                string persona = GetPersonalityLabel(agent);
                string line = $"{ep},{agent.AgentId},{persona},{agent.extraversion:F2},{agent.agreeableness:F2}," +
                              $"{agent.MeanSpeed:F3},{agent.PadHoldSteps},{agent.CellCarrySteps},{agent.PushCount}," +
                              $"{phase},{delivered.ToString().ToLower()},{length}";
                csvBuffer.AppendLine(line);
            }

            Debug.Log($"[CorridorTelemetryLogger] Episode {ep}/{targetEpisodes} logged. Phase={phase}, Delivered={delivered}, Length={length}");

            if (recordedEpisodes >= targetEpisodes)
            {
                FlushToDisk();
                Debug.Log($"[CorridorTelemetryLogger] Target {targetEpisodes} episodes recorded! File: {outputRelativePath}");
                if (autoStopOnTargetReached && Application.isPlaying)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                }
            }
            else if (recordedEpisodes % 5 == 0)
            {
                FlushToDisk();
            }
        }

        public void FlushToDisk()
        {
            if (csvBuffer.Length == 0) return;
            try
            {
                string projectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
                string fullPath = Path.Combine(projectPath, outputRelativePath);
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(fullPath, csvBuffer.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CorridorTelemetryLogger] Failed to write CSV: {ex.Message}");
            }
        }

        private string GetPersonalityLabel(RelayAgent agent)
        {
            if (agent.isLivePolicySlot) return "LLM-Designed Policy";
            if (agent.extraversion > 0.6f) return "High-E";
            if (agent.agreeableness > 0.6f) return "High-A";
            if (agent.extraversion < 0.4f && agent.agreeableness < 0.4f)
            {
                if (agent.AgentId.Contains("LowA") || agent.AgentId.Contains("3")) return "Low-A";
                return "Low-E";
            }
            return "Neutral";
        }
    }
}