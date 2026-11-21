using System.Collections.Generic;
using UnityEngine;

namespace RelayCorridor
{
    /// <summary>
    /// Part 7: Clean, collapsible HUD and scene telemetry UI.
    /// Features:
    /// 1. Top Collapsible Telemetry Panel (Episode stats, Baseline policies, Live LLM Policy, Comparison View)
    /// 2. Dedicated Bottom Personality Dashboard (Explicitly mentions each agent's personality and movement cues)
    /// 3. Overhead 3D World-Space Nameplates (Floating above each agent in 3D scene)
    /// </summary>
    public class CorridorHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CorridorEnvironment environment;
        [SerializeField] private List<RelayAgent> baselineAgents = new List<RelayAgent>();
        [SerializeField] private RelayAgent livePolicyAgent;

        [Header("UI State")]
        [SerializeField] private bool isCollapsed = true;
        [SerializeField] private bool showComparison = false;
        [SerializeField] private bool showBottomPersonalityBar = true;
        [SerializeField] private bool showOverheadNameplates = false;

        public bool IsCollapsed { get => isCollapsed; set => isCollapsed = value; }
        public bool ShowComparison { get => showComparison; set => showComparison = value; }
        public bool ShowBottomPersonalityBar { get => showBottomPersonalityBar; set => showBottomPersonalityBar = value; }
        public bool ShowOverheadNameplates { get => showOverheadNameplates; set => showOverheadNameplates = value; }

        // Live policy status (Paradigm B)
        private string livePersonaName = "AWAITING TRAINED POLICY";
        private string liveSummary = "LLM reward design loop offline. Awaiting pipeline start.";
        private float liveAlignmentScore = 0f;
        private int liveIteration = 0;
        private bool liveConverged = false;
        private int liveTotalIterations = 0;
        private bool isDesigning = false;

        private GUIStyle boxStyle;
        private GUIStyle headerStyle;
        private GUIStyle agentRowStyle;
        private GUIStyle subLabelStyle;
        private GUIStyle statStyle;
        private GUIStyle columnHeaderStyle;
        private GUIStyle cardHeaderStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardDescStyle;
        private GUIStyle cardLiveStyle;
        private GUIStyle nameplateStyle;
        private GUIStyle liveBadgeStyle;
        private Texture2D barTex;

        private void Awake()
        {
            isCollapsed = true;
            if (environment == null) environment = FindAnyObjectByType<CorridorEnvironment>();

            // Discover agents if not explicitly assigned
            if (baselineAgents.Count == 0)
            {
                var all = FindObjectsByType<RelayAgent>(FindObjectsSortMode.None);
                foreach (var a in all)
                {
                    if (a.isLivePolicySlot) livePolicyAgent = a;
                    else baselineAgents.Add(a);
                }
                // Sort baseline agents by AgentId
                baselineAgents.Sort((a, b) => string.Compare(a.AgentId, b.AgentId, System.StringComparison.Ordinal));
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.hKey.wasPressedThisFrame)
                {
                    showBottomPersonalityBar = !showBottomPersonalityBar;
                }
                if (kb.rKey.wasPressedThisFrame)
                {
                    if (environment != null) environment.ResetEnvironment();
                }
            }
            else
            {
                try 
                { 
                    if (Input.GetKeyDown(KeyCode.H)) showBottomPersonalityBar = !showBottomPersonalityBar; 
                    if (Input.GetKeyDown(KeyCode.R)) environment?.ResetEnvironment();
                } 
                catch {}
            }
        }

        public void UpdateLivePolicyPanel(string personaName, string summary, float score, int iter, bool converged, int totalIter)
        {
            livePersonaName = personaName;
            liveSummary = summary;
            liveAlignmentScore = score;
            liveIteration = iter;
            liveConverged = converged;
            liveTotalIterations = totalIter;
            isDesigning = false;
        }

        public void SetDesigningState(int iter, float score, string status)
        {
            isDesigning = true;
            liveIteration = iter;
            liveAlignmentScore = score;
            liveSummary = status;
        }

        private void InitStyles()
        {
            if (boxStyle != null) return;

            barTex = new Texture2D(1, 1);
            barTex.SetPixel(0, 0, Color.white);
            barTex.Apply();

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = barTex;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.95f, 0.98f) }
            };

            agentRowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            subLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.72f, 0.78f, 0.85f) }
            };

            statStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.85f, 0.88f, 0.92f) }
            };

            columnHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.4f, 0.8f, 1.0f) }
            };

            cardHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.4f, 0.85f, 1.0f) }
            };

            cardDescStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 7,
                wordWrap = true,
                normal = { textColor = new Color(0.80f, 0.84f, 0.90f) }
            };

            cardLiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1.0f, 0.9f, 0.4f) }
            };

            nameplateStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            liveBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(1.0f, 0.25f, 0.25f) }
            };
        }

        private void OnGUI()
        {
            InitStyles();

            // 1. Top Telemetry & Control Panel
            DrawTopTelemetryPanel();

            // 2. Dynamic 60-Second Curriculum Phase Banner (User Request)
            DrawCurriculumPhaseBanner();

            // 3. 3D Overhead Nameplates removed per user request (displayed in bottom dashboard)
            // Overhead text removed per user request (already displayed at bottom)
            // if (showOverheadNameplates) DrawOverheadNameplates();

            // 4. Dedicated Bottom Personality Dashboard
            if (showBottomPersonalityBar)
            {
                DrawBottomPersonalityBar();
            }
            else
            {
                // Floating on-screen restore button when bottom bar is hidden
                float btnW = 175f;
                float btnH = 24f;
                float btnX = Screen.width - btnW - 14f;
                float btnY = Screen.height - btnH - 8f;

                GUI.color = new Color(0.04f, 0.07f, 0.12f, 0.92f);
                GUI.Box(new Rect(btnX, btnY, btnW, btnH), GUIContent.none, boxStyle);
                GUI.color = new Color(0.25f, 0.75f, 1.0f);
                GUI.DrawTexture(new Rect(btnX, btnY, btnW, 1.5f), barTex);
                GUI.color = Color.white;

                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "[+] Show Agent Profiles (H)", cardLiveStyle))
                {
                    showBottomPersonalityBar = true;
                }
            }
        }

        private void DrawTopTelemetryPanel()
        {
            float maxAvailWidth = (Screen.width - 28f) * 0.54f;
            float panelWidth = showComparison ? 880f : Mathf.Min(695f, (Screen.width - 28f) * 0.65f);
            float panelHeight = isCollapsed ? 44f : (showComparison ? 430f : 400f);

            // Background panel
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
            GUI.Box(new Rect(12, 12, panelWidth, panelHeight), GUIContent.none, boxStyle);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(20, 16, panelWidth - 16, panelHeight - 10));

            // Title Bar with Collapse and Comparison Toggles
            GUILayout.BeginHorizontal();
            GUILayout.Label(isCollapsed ? "RELAY CORRIDOR" : "RELAY CORRIDOR — MULTI-AGENT OCEAN", headerStyle);
            GUILayout.FlexibleSpace();

            string modeBtn = (environment != null && environment.CurrentCurriculumMode == CorridorEnvironment.CurriculumMode.CombinedSimultaneous)
                ? "[Combined Mode (C)]"
                : "[20s Cycle (C)]";
            if (GUILayout.Button(modeBtn, GUILayout.Width(130), GUILayout.Height(20)))
            {
                if (environment != null) environment.ToggleCurriculumMode();
            }

            GUI.backgroundColor = new Color(0.95f, 0.35f, 0.15f);
            if (GUILayout.Button("[Reset Game (R)]", GUILayout.Width(115), GUILayout.Height(20)))
            {
                if (environment != null) environment.ResetEnvironment();
            }
            GUI.backgroundColor = Color.white;

            string bottomToggleText = showBottomPersonalityBar ? "Hide Profiles (H)" : "Show Profiles (H)";
            if (GUILayout.Button(bottomToggleText, GUILayout.Width(115), GUILayout.Height(20)))
            {
                showBottomPersonalityBar = !showBottomPersonalityBar;
            }

            if (GUILayout.Button(showComparison ? "Standard View" : "Comparison View", GUILayout.Width(110), GUILayout.Height(20)))
            {
                showComparison = !showComparison;
            }

            if (GUILayout.Button(isCollapsed ? "Expand" : "Collapse", GUILayout.Width(65), GUILayout.Height(20)))
            {
                isCollapsed = !isCollapsed;
            }
            GUILayout.EndHorizontal();

            if (isCollapsed)
            {
                GUILayout.EndArea();
                return;
            }

            // Episode Info & Global Telemetry Panel
            if (environment != null)
            {
                GUILayout.Space(3);
                string phaseStr = environment.CurrentPhase.ToString();
                GUILayout.Label($"Episode: {environment.EpisodeCount}  |  Step: {environment.CurrentStep}/5000  |  Phase: {phaseStr}  |  Deliveries: {environment.DeliveryCount}  |  Timeouts: {environment.TimeoutCount}", statStyle);
                GUILayout.Space(4);
            }

            if (showComparison)
            {
                DrawComparisonView(panelWidth - 32);
            }
            else
            {
                DrawStandardView(panelWidth - 32);
            }

            GUILayout.EndArea();
        }

        private void DrawStandardView(float width)
        {
            // Section 1: Baseline Agents (Hand-Designed)
            GUILayout.Label("PARADIGM A: HAND-DESIGNED REWARD (PPO POLICIES)", subLabelStyle);

            for (int i = 0; i < baselineAgents.Count; i++)
            {
                var ag = baselineAgents[i];
                if (ag == null) continue;

                DrawAgentRow(ag, $"{ag.AgentId} ({ag.PersonalityArchetypeTitle})",
                    "HAND-DESIGNED REWARD — PPO POLICY", ag.AgentColor);
            }

            GUILayout.Space(4);
            DrawDivider(width);
            GUILayout.Space(3);

            // Section 2: Live Policy Slot (LLM-Designed)
            GUILayout.Label("PARADIGM B: LLM-DESIGNED REWARD (LIVE PPO POLICY)", subLabelStyle);
            DrawLivePolicyRow();
        }

        private void DrawComparisonView(float width)
        {
            GUILayout.Label("PARADIGM A vs PARADIGM B: SIDE-BY-SIDE ARCHITECTURAL COMPARISON", subLabelStyle);
            GUILayout.Space(4);

            float colWidth = (width - 16) * 0.5f;

            GUILayout.BeginHorizontal();

            // Column 1: Baseline Aggregates
            GUILayout.BeginVertical(GUILayout.Width(colWidth));
            GUILayout.Label("PARADIGM A (MDPI BASELINE)", columnHeaderStyle);
            GUILayout.Label("Hand-Crafted OCEAN Linear Formulas", subLabelStyle);
            GUILayout.Space(3);

            float totalSpeed = 0f;
            int totalPads = 0;
            int totalPushes = 0;
            int validCount = 0;

            foreach (var ag in baselineAgents)
            {
                if (ag == null) continue;
                totalSpeed += ag.rb != null ? ag.rb.linearVelocity.magnitude : 0f;
                totalPads += ag.PadHoldSteps;
                totalPushes += ag.PushCount;
                validCount++;
            }

            float avgSpeed = validCount > 0 ? totalSpeed / validCount : 0f;

            GUILayout.Label($"Active Agents: {validCount} (A0, A1, A2, A3)", statStyle);
            GUILayout.Label($"Average Speed: {avgSpeed:0.2} m/s", statStyle);
            GUILayout.Label($"Total Pad Hold Steps: {totalPads}", statStyle);
            GUILayout.Label($"Total Cell Pushes: {totalPushes}", statStyle);
            GUILayout.Label("Reward Formula: Hand-crafted heuristic coefficients", subLabelStyle);
            GUILayout.Label("Inference: Sentis ONNX PPO Policy", subLabelStyle);
            GUILayout.EndVertical();

            // Vertical divider
            GUI.color = new Color(0.3f, 0.35f, 0.45f, 0.6f);
            GUILayout.Box(GUIContent.none, boxStyle, GUILayout.Width(2), GUILayout.Height(115));
            GUI.color = Color.white;
            GUILayout.Space(8);

            // Column 2: LLM-Designed Policy
            GUILayout.BeginVertical(GUILayout.Width(colWidth));
            GUILayout.Label("PARADIGM B (PROPOSED METHOD)", columnHeaderStyle);
            GUILayout.Label("LLM-Designed Closed-Loop (Gemini 3.8 Flash)", subLabelStyle);
            GUILayout.Space(3);

            if (livePolicyAgent != null && environment != null && environment.IsLivePolicyActive)
            {
                float liveSpd = livePolicyAgent.rb != null ? livePolicyAgent.rb.linearVelocity.magnitude : 0f;
                GUILayout.Label($"Active Policy: {livePersonaName}", statStyle);
                GUILayout.Label($"Live Agent Speed: {liveSpd:0.2} m/s", statStyle);
                GUILayout.Label($"Pad Hold Steps: {livePolicyAgent.PadHoldSteps}", statStyle);
                GUILayout.Label($"Cell Pushes: {livePolicyAgent.PushCount}", statStyle);
                GUILayout.Label($"Alignment Score: {liveAlignmentScore:0.#}/10 (Iter {liveIteration})", statStyle);
                GUILayout.Label("Inference: Sentis ONNX PPO Policy (Trained)", subLabelStyle);
            }
            else if (isDesigning)
            {
                GUILayout.Label($"Designing Reward: Iteration {liveIteration}/10", statStyle);
                GUILayout.Label($"Current Alignment Score: {liveAlignmentScore:0.#}/10", statStyle);
                GUILayout.Label($"Status: {liveSummary}", subLabelStyle);
            }
            else
            {
                GUILayout.Label("Live Policy Slot: Awaiting Policy Load", statStyle);
                GUILayout.Label("Loop Status: Offline", subLabelStyle);
                GUILayout.Label("Load via CLI, Web UI, or LivePolicyGeneratorWindow", subLabelStyle);
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            DrawDivider(width);
            GUILayout.Space(3);

            GUILayout.Label("LIVE AGENT TELEMETRY (SLOT 5)", subLabelStyle);
            DrawLivePolicyRow();
        }

        private void DrawLivePolicyRow()
        {
            if (livePolicyAgent != null && environment != null && environment.IsLivePolicyActive)
            {
                string personaTitle = $"{livePersonaName} (E={livePolicyAgent.extraversion:0.1}, A={livePolicyAgent.agreeableness:0.1})";
                string subText = $"LLM-DESIGNED REWARD — PPO POLICY — Score {liveAlignmentScore:0.#}/10 — Iter {liveIteration}";
                DrawAgentRow(livePolicyAgent, personaTitle, subText, new Color(1.0f, 0.85f, 0.2f));
                GUILayout.Label($"Summary: \"{liveSummary}\"", subLabelStyle);
            }
            else if (isDesigning)
            {
                GUI.color = new Color(1.0f, 0.70f, 0.15f);
                GUILayout.Label($"DESIGNING REWARD — ITER {liveIteration}/10 — SCORE {liveAlignmentScore:0.#}/10", agentRowStyle);
                GUILayout.Label($"Telemetry evaluation in progress: {liveSummary}", subLabelStyle);
                GUI.color = Color.white;
            }
            else
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.realtimeSinceStartup * 4f);
                Color pulsingAmber = new Color(1.0f * pulse, 0.75f * pulse, 0.2f * pulse);

                GUI.color = pulsingAmber;
                GUILayout.Label("LIVE SLOT — AWAITING TRAINED POLICY", agentRowStyle);
                GUI.color = Color.white;
                GUILayout.Label("LLM reward design phase inactive. Generate policy via CLI, Web UI, or Tools -> Live Policy Generator.", subLabelStyle);
            }
        }

        private void DrawAgentRow(RelayAgent agent, string title, string architectureTag, Color accent)
        {
            GUILayout.BeginHorizontal();

            // Accent color chip
            GUI.color = accent;
            GUILayout.Box(GUIContent.none, boxStyle, GUILayout.Width(7), GUILayout.Height(30));
            GUI.color = Color.white;
            GUILayout.Space(4);

            GUILayout.BeginVertical();
            GUILayout.Label(title, agentRowStyle);

            string goal = agent.CurrentBehaviorState;
            GUILayout.Label($"{architectureTag}  |  Behavior: {goal}", subLabelStyle);

            // Speed bar as fraction of maxSpeed
            float curSpeed = agent.rb != null ? agent.rb.linearVelocity.magnitude : 0f;
            float maxSpd = Mathf.Max(0.1f, agent.maxSpeed);
            float ratio = Mathf.Clamp01(curSpeed / maxSpd);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Speed: {curSpeed:0.2}/{maxSpd:0.1} m/s", statStyle, GUILayout.Width(110));

            Rect barRect = GUILayoutUtility.GetRect(110, 8);
            GUI.color = new Color(0.2f, 0.25f, 0.3f);
            GUI.DrawTexture(barRect, barTex);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height), barTex);
            GUI.color = Color.white;

            GUILayout.Label($"  Pads: {agent.PadHoldSteps}  Push: {agent.PushCount}", statStyle);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        // =========================================================================
        // DEDICATED BOTTOM PERSONALITY BAR (USER REQUIREMENT)
        // Explicitly displays all personalities and tells the observer what movement
        // behaviors to look for in real time!
        // =========================================================================
                        private void DrawBottomPersonalityBar()
        {
            float barHeight = 138f;
            float barWidth = Screen.width - 16f;
            float barX = 8f;
            float barY = Screen.height - barHeight - 4f;

            // Outer dark container
            GUI.color = new Color(0.04f, 0.055f, 0.085f, 0.96f);
            GUI.Box(new Rect(barX, barY, barWidth, barHeight), GUIContent.none, boxStyle);

            // Glowing top accent line
            GUI.color = new Color(0.20f, 0.55f, 0.90f, 0.80f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth, 2), barTex);
            GUI.color = Color.white;

            // Header line
            bool isCombined = environment != null && environment.CurrentCurriculumMode == CorridorEnvironment.CurriculumMode.CombinedSimultaneous;
            bool isPersonality = environment != null && environment.CurrentRewardFocus == CorridorEnvironment.RewardFocusMode.PersonalityExpression;
            string subBanner = isCombined
                ? "MODE: COMBINED SIMULTANEOUS (All agents express personalities WHILE solving puzzle)"
                : (isPersonality
                    ? "PHASE: PERSONALITY EXPRESSION (Notice individual traits: sprint pacing, pad anchor, shoving)"
                    : "PHASE: TASK OBJECTIVE SOLVING (Notice synchronized coordination: pad holds, rapid delivery)");

            GUI.Label(new Rect(barX + 10f, barY + 4f, 260f, 18f), "MULTI-AGENT PERSONALITY PROFILES", headerStyle);
            GUI.color = new Color(0.70f, 0.78f, 0.88f);
            GUI.Label(new Rect(barX + 275f, barY + 4f, barWidth - 415f, 18f), subBanner, subLabelStyle);
            GUI.color = Color.white;

            // Direct button on bottom bar to hide
            if (GUI.Button(new Rect(barX + barWidth - 128f, barY + 3f, 120f, 18f), "[–] Hide Profiles (H)"))
            {
                showBottomPersonalityBar = false;
            }

            // 5 Cards across bottom
            float cardsStartY = barY + 24f;
            float cardSpacing = 6f;
            float cardWidth = (barWidth - 20f - 4 * cardSpacing) / 5f;
            float cardHeight = barHeight - 30f;

            RelayAgent a0 = GetAgentByIndexOrId(0, "Agent_0_HighE");
            RelayAgent a1 = GetAgentByIndexOrId(1, "Agent_1_LowE");
            RelayAgent a2 = GetAgentByIndexOrId(2, "Agent_2_HighA");
            RelayAgent a3 = GetAgentByIndexOrId(3, "Agent_3_LowA");

            // Agent 0: Cyan
            DrawSingleCard(barX + 10f + 0 * (cardWidth + cardSpacing), cardsStartY, cardWidth, cardHeight,
                "Agent 0", "HIGH EXTRAVERSION (E=0.9, A=0.1)", "The Aggressive Sprinter",
                "- Top speed (6.2 m/s), fast accel\n- Aggressive lean; charges cell",
                new Color(0.0f, 0.85f, 1.0f), a0, false);

            // Agent 1: Green
            DrawSingleCard(barX + 10f + 1 * (cardWidth + cardSpacing), cardsStartY, cardWidth, cardHeight,
                "Agent 1", "LOW EXTRAVERSION (E=0.1, A=0.5)", "The Cautious Scout",
                "- Slow walk (2.0 m/s), upright posture\n- Pauses at gates to inspect passage",
                new Color(0.1f, 0.90f, 0.45f), a1, false);

            // Agent 2: Orange
            DrawSingleCard(barX + 10f + 2 * (cardWidth + cardSpacing), cardsStartY, cardWidth, cardHeight,
                "Agent 2", "HIGH AGREEABLENESS (E=0.5, A=0.9)", "The Altruistic Anchor",
                "- Team helper (3.8 m/s), pad pulse glow\n- Anchors pads until team clears",
                new Color(1.0f, 0.58f, 0.05f), a2, false);

            // Agent 3: Magenta
            DrawSingleCard(barX + 10f + 3 * (cardWidth + cardSpacing), cardsStartY, cardWidth, cardHeight,
                "Agent 3", "LOW AGREEABLENESS (E=0.5, A=0.1)", "The Self-Interested Pusher",
                "- Refuses relay pads; rushes cell (4.8 m/s)\n- Physically shoves teammates aside!",
                new Color(1.0f, 0.15f, 0.65f), a3, false);

            // Slot 5: White/Gold with LIVE badge
            string llmTitle = livePolicyAgent != null && environment != null && environment.IsLivePolicyActive
                ? livePersonaName : "Awaiting Policy";
            DrawSingleCard(barX + 10f + 4 * (cardWidth + cardSpacing), cardsStartY, cardWidth, cardHeight,
                "Slot 5", "LLM-DESIGNED REWARD (PROPOSED)", llmTitle,
                "- Agile courier (6.5 m/s), golden trail\n- Dynamic goal transitions (LLM PPO)",
                new Color(1.0f, 0.88f, 0.35f), livePolicyAgent, true);
        }

        private void DrawSingleCard(float x, float y, float width, float height,
            string name, string traitTag, string title, string movementDesc, Color accent, RelayAgent agent, bool isLiveSlot)
        {
            Rect cardRect = new Rect(x, y, width, height);

            // 1. Background
            GUI.color = isLiveSlot ? new Color(0.07f, 0.08f, 0.13f, 0.95f) : new Color(0.05f, 0.065f, 0.095f, 0.92f);
            GUI.Box(cardRect, GUIContent.none, boxStyle);

            // 2. Thin colored left border matching agent identity color
            GUI.color = accent;
            GUI.DrawTexture(new Rect(x, y, 3f, height), barTex);

            // 3. Slot 5 gold/white border and animated LIVE badge
            if (isLiveSlot)
            {
                GUI.color = new Color(1.0f, 0.88f, 0.35f, 0.85f);
                GUI.DrawTexture(new Rect(x, y, width, 1.5f), barTex); // top
                GUI.DrawTexture(new Rect(x, y + height - 1.5f, width, 1.5f), barTex); // bottom
                GUI.DrawTexture(new Rect(x + width - 1.5f, y, 1.5f, height), barTex); // right

                // Animated LIVE badge in top right corner
                float livePulse = 0.65f + 0.35f * Mathf.Sin(Time.realtimeSinceStartup * 6f);
                GUI.color = new Color(1.0f, 0.25f, 0.25f, livePulse);
                GUI.Label(new Rect(x + width - 42f, y + 2f, 38f, 14f), "● LIVE", liveBadgeStyle);
            }
            GUI.color = Color.white;

            // 4. Content inside card
            float innerX = x + 8f;
            float innerY = y + 3f;
            float innerW = width - 12f;

            // Header line
            GUI.color = Color.white;
            GUI.Label(new Rect(innerX, innerY, innerW - (isLiveSlot ? 38f : 0f), 14f), $"{name}: {title}", cardHeaderStyle);

            // Trait line
            GUI.color = accent;
            GUI.Label(new Rect(innerX, innerY + 14f, innerW, 13f), traitTag, cardTitleStyle);
            GUI.color = Color.white;

            // Description lines
            GUI.Label(new Rect(innerX, innerY + 28f, innerW, 36f), movementDesc, cardDescStyle);

            // Live status
            if (agent != null && agent.HasEscaped)
            {
                GUI.color = new Color(0.2f, 1.0f, 0.45f);
                GUI.Label(new Rect(innerX, innerY + 68f, innerW, 14f), "LIVE: ESCAPED ✓ (Room Cleared)", cardLiveStyle);
                GUI.color = Color.white;
            }
            else
            {
                float spd = agent != null && agent.rb != null && agent.gameObject.activeInHierarchy ? agent.rb.linearVelocity.magnitude : 0f;
                string state = agent != null ? agent.CurrentBehaviorState : "Offline";
                if (state.Length > 20) state = state.Substring(0, 18) + "..";

                GUI.color = accent;
                GUI.Label(new Rect(innerX, innerY + 68f, innerW, 14f), $"LIVE: {spd:0.1} m/s | {state}", cardLiveStyle);
                GUI.color = Color.white;
            }
        }

        // =========================================================================
        // DYNAMIC 60-SECOND CURRICULUM PHASE BANNER (USER REQUIREMENT)
        // Explicitly announces Minute 1 (Personality Expression) vs Minute 2 (Task Solving)
        // with live countdown progress bar and reward scaling!
        // =========================================================================
        private void DrawCurriculumPhaseBanner()
        {
            if (environment == null) environment = FindAnyObjectByType<CorridorEnvironment>();
            if (environment == null) return;

            float maxAvailWidth = (Screen.width - 28f) * 0.49f;
            float bannerWidth = Mathf.Min(470f, maxAvailWidth);
            float bannerHeight = 44f;
            float bannerX = Screen.width - bannerWidth - 12f;
            float bannerY = 12f;

            bool isCombined = environment.CurrentCurriculumMode == CorridorEnvironment.CurriculumMode.CombinedSimultaneous;
            bool isPersonality = environment.CurrentRewardFocus == CorridorEnvironment.RewardFocusMode.PersonalityExpression;
            Color themeColor = isCombined 
                ? new Color(0.85f, 0.40f, 1.0f) 
                : (isPersonality ? new Color(0.25f, 0.85f, 1.0f) : new Color(1.0f, 0.72f, 0.20f));

            // Container Box
            GUI.color = new Color(0.04f, 0.06f, 0.10f, 0.94f);
            GUI.Box(new Rect(bannerX, bannerY, bannerWidth, bannerHeight), GUIContent.none, boxStyle);

            // Glowing top accent
            GUI.color = themeColor;
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerWidth, 2), barTex);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(bannerX + 10, bannerY + 4, bannerWidth - 20, bannerHeight - 8));

            GUILayout.BeginHorizontal();
            GUI.color = themeColor;
            string phaseTitle = isCombined
                ? "SIMULTANEOUS MODE: [PERSONALITY + TASK SOLVING]"
                : (isPersonality 
                    ? $"PHASE {environment.CurrentCycleMinute}: [PERSONALITY EXPRESSION]" 
                    : $"PHASE {environment.CurrentCycleMinute}: [TASK OBJECTIVE SOLVING]");
            GUILayout.Label(phaseTitle, cardHeaderStyle);
            GUILayout.FlexibleSpace();
            int secs = Mathf.CeilToInt(environment.CycleTimeRemaining);
            string timerText = isCombined ? "⚡ ACTIVE (Press 'C')" : $"⏳ {secs}s (Press 'C')";
            GUILayout.Label(timerText, cardTitleStyle);
            GUILayout.EndHorizontal();

            string rewardDesc = isCombined
                ? "Rewarding: Personality Dial Congruence (50%) + Task Objective (50%) SIMULTANEOUSLY"
                : (isPersonality
                    ? "Rewarding: Personality Dial Congruence 85% (3.0x)  |  Task Solving: 15% (0.2x)"
                    : "Rewarding: Task Objective & Solving: 85% (3.0x)  |  Personality: 15% (0.2x)");
            GUI.color = new Color(0.85f, 0.90f, 0.95f);
            GUILayout.Label(rewardDesc, cardDescStyle);
            GUI.color = Color.white;

            // Countdown progress bar
            float progress = isCombined ? 1.0f : Mathf.Clamp01(environment.CycleTimeRemaining / 20f);
            Rect progressRect = GUILayoutUtility.GetRect(bannerWidth - 20, 3);
            GUI.color = new Color(0.15f, 0.20f, 0.25f);
            GUI.DrawTexture(progressRect, barTex);
            GUI.color = themeColor;
            GUI.DrawTexture(new Rect(progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height), barTex);
            GUI.color = Color.white;

            GUILayout.EndArea();
        }

        // =========================================================================
        // 3D FLOATING OVERHEAD BILLBOARD NAMEPLATES
        // =========================================================================
        private void DrawOverheadNameplates()
        {
            var cam = Camera.main;
            if (cam == null) return;

            List<RelayAgent> allToDraw = new List<RelayAgent>(baselineAgents);
            if (livePolicyAgent != null && environment != null && environment.IsLivePolicyActive)
            {
                allToDraw.Add(livePolicyAgent);
            }

            foreach (var agent in allToDraw)
            {
                if (agent == null || !agent.gameObject.activeInHierarchy) continue;

                Vector3 worldHead = agent.transform.position + Vector3.up * 1.55f;
                Vector3 screenPoint = cam.WorldToScreenPoint(worldHead);

                if (screenPoint.z > 0f)
                {
                    float guiX = screenPoint.x;
                    float guiY = Screen.height - screenPoint.y;

                    float spd = agent.rb != null ? agent.rb.linearVelocity.magnitude : 0f;
                    string tag = string.IsNullOrEmpty(agent.ShortPersonalityBadge) ? agent.AgentId : agent.ShortPersonalityBadge;
                    string emote = !string.IsNullOrEmpty(agent.CurrentEmoteTag) ? $" | {agent.CurrentEmoteTag}" : "";
                    string badgeText = $"{tag} [{spd:0.1} m/s]{emote}";

                    Vector2 size = nameplateStyle.CalcSize(new GUIContent(badgeText));
                    float pillW = size.x + 12f;
                    float pillH = size.y + 4f;

                    Rect bgRect = new Rect(guiX - pillW * 0.5f, guiY - pillH * 0.5f, pillW, pillH);

                    // Dark pill background
                    GUI.color = new Color(0.04f, 0.06f, 0.10f, 0.88f);
                    GUI.Box(bgRect, GUIContent.none, boxStyle);

                    // Colored border/accent
                    GUI.color = agent.AgentColor;
                    GUI.DrawTexture(new Rect(bgRect.x, bgRect.yMax - 2, bgRect.width, 2), barTex);

                    // Text
                    GUI.Label(bgRect, badgeText, nameplateStyle);
                    GUI.color = Color.white;
                }
            }
        }

        private RelayAgent GetAgentByIndexOrId(int index, string id)
        {
            foreach (var a in baselineAgents)
            {
                if (a != null && a.AgentId == id) return a;
            }
            if (index >= 0 && index < baselineAgents.Count) return baselineAgents[index];
            return null;
        }

        private void DrawDivider(float width)
        {
            Rect r = GUILayoutUtility.GetRect(width, 1);
            GUI.color = new Color(0.3f, 0.35f, 0.45f, 0.6f);
            GUI.DrawTexture(r, barTex);
            GUI.color = Color.white;
        }
    }
}
