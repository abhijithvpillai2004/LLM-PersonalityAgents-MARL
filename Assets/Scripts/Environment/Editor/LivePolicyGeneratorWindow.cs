using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace RelayCorridor.Editor
{
    public class LivePolicyGeneratorWindow : EditorWindow
    {
        private string description = "Reckless Charger: extremely fast, impatient, pushes teammates out of the way to grab the energy cell and deliver it immediately.";
        private bool isRunning = false;
        private Process pipelineProcess = null;
        private Vector2 logScroll;
        private StringBuilder logBuffer = new StringBuilder();

        // Status tracking
        private int currentIteration = 0;
        private float currentScore = 0f;
        private string currentStatus = "IDLE";
        private string spawnedPersona = "";

        [MenuItem("Tools/Live Policy Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<LivePolicyGeneratorWindow>("Live Policy Generator");
            window.minSize = new Vector2(500, 650);
            window.Show();
        }

        private void OnGUI()
        {
            // 1. Header Banner
            EditorGUILayout.Space(6);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.95f, 0.65f, 0.1f) }
            };
            EditorGUILayout.LabelField("LLM-Assisted Reward Design → PPO Training → Live Policy", headerStyle);
            EditorGUILayout.HelpBox(
                "The LLM acts offline as a reward designer, iteratively refining reward coefficients. " +
                "The agent running in Unity is a trained PPO policy with ZERO LLM calls at runtime.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            // 2. Input description
            EditorGUILayout.LabelField("Describe the personality you want to train:", EditorStyles.boldLabel);
            GUI.enabled = !isRunning;
            description = EditorGUILayout.TextArea(description, GUILayout.Height(65));
            GUI.enabled = true;

            EditorGUILayout.Space(6);

            // 3. Action Buttons
            EditorGUILayout.BeginHorizontal();
            if (!isRunning)
            {
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
                if (GUILayout.Button("Generate and Train Policy", GUILayout.Height(32)))
                {
                    StartPipelineProcess();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
                if (GUILayout.Button("Cancel Pipeline Run", GUILayout.Height(32)))
                {
                    CancelPipelineProcess();
                }
                GUI.backgroundColor = Color.white;
            }

            if (GUILayout.Button("Load Best Policy Now", GUILayout.Height(32)))
            {
                TriggerManualLoad();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 4. Progress and Metrics Display
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("TRAINING & ALIGNMENT STATUS", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Status: {currentStatus}", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Iteration: {currentIteration}/10", EditorStyles.miniBoldLabel);
            
            // Score with color badge
            Color scoreColor = currentScore >= 8.0f ? Color.green : (currentScore >= 5.0f ? Color.yellow : Color.red);
            GUIStyle scoreStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = scoreColor } };
            EditorGUILayout.LabelField($"Alignment Score: {currentScore:0.#}/10", scoreStyle);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(spawnedPersona))
            {
                GUIStyle activeBadgeStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.cyan } };
                EditorGUILayout.LabelField($"✓ Policy spawned — [{spawnedPersona}] — Running PPO inference in scene", activeBadgeStyle);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 5. Live Log Box
            EditorGUILayout.LabelField("Live Pipeline Output Stream:", EditorStyles.boldLabel);
            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.Height(300));
            GUIStyle logStyle = new GUIStyle(EditorStyles.textArea)
            {
                fontSize = 11,
                font = Font.CreateDynamicFontFromOSFont("Consolas", 11)
            };
            EditorGUILayout.TextArea(logBuffer.ToString(), logStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);

            // 6. Architecture Footer Reminder
            EditorGUILayout.HelpBox(
                "Architecture Reminder: The LLM designs reward coefficients offline. " +
                "The running agent in Unity is a trained PPO policy.",
                MessageType.None);
        }

        private void StartPipelineProcess()
        {
            logBuffer.Clear();
            currentIteration = 0;
            currentScore = 0f;
            currentStatus = "STARTING PIPELINE...";
            spawnedPersona = "";
            isRunning = true;

            string pythonExe = @"C:\Users\SiD\miniconda3\envs\mlagents\python.exe";
            string scriptPath = Path.Combine(Application.dataPath, "..", "scripts", "generate_policy.py");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"-u \"{scriptPath}\" --fast --description \"{description.Replace("\"", "\\\"")}\"",
                WorkingDirectory = Path.Combine(Application.dataPath, ".."),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                pipelineProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                pipelineProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        ParseOutputLine(e.Data);
                    }
                };
                pipelineProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        logBuffer.AppendLine($"[STDERR] {e.Data}");
                    }
                };
                pipelineProcess.Exited += (s, e) =>
                {
                    isRunning = false;
                    currentStatus = "COMPLETED";
                    Repaint();
                };

                pipelineProcess.Start();
                pipelineProcess.BeginOutputReadLine();
                pipelineProcess.BeginErrorReadLine();
                logBuffer.AppendLine("[LivePolicyGenerator] Pipeline launched successfully.");
            }
            catch (Exception ex)
            {
                isRunning = false;
                currentStatus = "ERROR LAUNCHING";
                logBuffer.AppendLine($"[Launch Error] {ex.Message}");
                UnityEngine.Debug.LogError($"[LivePolicyGenerator] {ex}");
            }
        }

        private void ParseOutputLine(string line)
        {
            logBuffer.AppendLine(line);

            if (line.Contains("[CRITIQUE]") && line.Contains("Alignment Score:"))
            {
                var idx = line.IndexOf("Alignment Score:");
                var scoreSub = line.Substring(idx);
                // Parse score
                var match = System.Text.RegularExpressions.Regex.Match(scoreSub, @"(\d+)/10");
                if (match.Success)
                {
                    float.TryParse(match.Groups[1].Value, out currentScore);
                }
            }
            if (line.Contains("Iteration"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"Iteration (\d+)/(\d+)");
                if (match.Success)
                {
                    int.TryParse(match.Groups[1].Value, out currentIteration);
                }
            }
            if (line.Contains("[CONVERGED]"))
            {
                currentStatus = "CONVERGED";
            }
            if (line.Contains("[SPAWNING]"))
            {
                currentStatus = "SPAWNING IN UNITY";
            }

            // Check if load_signal was written
            string signalPath = Path.Combine(Application.dataPath, "Models/llm_designed/current/load_signal.json");
            if (File.Exists(signalPath))
            {
                try
                {
                    string json = File.ReadAllText(signalPath);
                    var signal = JsonUtility.FromJson<LoadSignal>(json);
                    if (signal != null)
                    {
                        spawnedPersona = signal.personaName;
                        currentScore = signal.alignmentScore;
                        currentIteration = signal.iteration;
                    }
                }
                catch { }
            }

            Repaint();
        }

        private void CancelPipelineProcess()
        {
            if (pipelineProcess != null && !pipelineProcess.HasExited)
            {
                try
                {
                    pipelineProcess.Kill();
                    logBuffer.AppendLine("[LivePolicyGenerator] Process cancelled by user.");
                }
                catch (Exception ex)
                {
                    logBuffer.AppendLine($"[Cancel Error] {ex.Message}");
                }
            }
            isRunning = false;
            currentStatus = "CANCELLED";
            Repaint();
        }

        private void TriggerManualLoad()
        {
            var loader = FindAnyObjectByType<LivePolicyLoader>();
            if (loader == null)
            {
                var platform = GameObject.Find("Environment/LivePolicySlot/LivePolicySlotPlatform") ?? GameObject.Find("LivePolicyAgent_Slot");
                if (platform != null)
                {
                    loader = platform.AddComponent<LivePolicyLoader>();
                }
                else
                {
                    var go = new GameObject("LivePolicyLoader");
                    loader = go.AddComponent<LivePolicyLoader>();
                }
            }

            if (loader != null)
            {
                loader.FindReferencesIfMissing();
                loader.PollForSignal();
                logBuffer.AppendLine("[LivePolicyGenerator] Triggered LivePolicyLoader successfully.");
            }
            else
            {
                logBuffer.AppendLine("[LivePolicyGenerator] Failed to instantiate LivePolicyLoader.");
            }
        }

        private void OnInspectorUpdate()
        {
            if (isRunning)
            {
                Repaint();
            }
        }
    }
}
