using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.MLAgents.Policies;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using TMPro;

namespace RelayCorridor.Editor
{
    public static class CorridorSceneSetup
    {
        [MenuItem("RelayCorridor/Setup Complete Scene")]
        public static void SetupCompleteScene()
        {
            // 1. Create or open scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RelayCorridor";

            // 2. Main Camera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.transform.position = new Vector3(5.0f, 27.0f, -20.0f);
            camGo.transform.rotation = Quaternion.Euler(52.0f, 0f, 0f); cam.fieldOfView = 52f;
            camGo.AddComponent<AudioListener>();

            // 3. Directional Light
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(0.95f, 0.95f, 1.0f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 4. Builder GameObject & Build Environment
            GameObject builderGo = new GameObject("CorridorBuilder");
            CorridorBuilder builder = builderGo.AddComponent<CorridorBuilder>();
            CorridorEnvironment env = builder.BuildEnvironment();
            if (env != null && env.GetComponent<CorridorTelemetryLogger>() == null)
            {
                env.gameObject.AddComponent<CorridorTelemetryLogger>();
            }

            // 5. Spawn 4 Baseline Agents in Chamber A
            Transform agentsGroup = GameObject.Find("Environment/Agents")?.transform;
            if (agentsGroup == null) agentsGroup = new GameObject("Agents").transform;

            Color[] agentColors = new Color[]
            {
                new Color(0.2f, 0.8f, 1.0f),  // Agent 0: High-E (Cyan)
                new Color(0.4f, 0.5f, 0.8f),  // Agent 1: Low-E (Slate Blue)
                new Color(0.2f, 0.9f, 0.4f),  // Agent 2: High-A (Emerald Green)
                new Color(0.9f, 0.3f, 0.2f)   // Agent 3: Low-A (Crimson)
            };

            float[] eDials = new float[] { 0.9f, 0.1f, 0.1f, 0.1f };
            float[] aDials = new float[] { 0.1f, 0.1f, 0.9f, 0.1f };
            string[] agentNames = new string[] { "Agent_0_HighE", "Agent_1_LowE", "Agent_2_HighA", "Agent_3_LowA" };
            float[] zSpawns = new float[] { -3.0f, -1.0f, 1.0f, 3.0f };

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");
            EnergyCell cell = Object.FindAnyObjectByType<EnergyCell>();
            ReceiverTerminal receiver = Object.FindAnyObjectByType<ReceiverTerminal>();

            for (int i = 0; i < 4; i++)
            {
                GameObject agentGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                agentGo.name = agentNames[i];
                agentGo.transform.SetParent(agentsGroup);
                agentGo.transform.position = new Vector3(-11.0f, 1.0f, zSpawns[i]);
                agentGo.transform.localScale = new Vector3(0.8f, 1.0f, 0.8f);

                // Material
                Material mat = new Material(litShader);
                mat.color = agentColors[i];
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", agentColors[i] * 0.5f);
                    mat.EnableKeyword("_EMISSION");
                }
                agentGo.GetComponent<Renderer>().material = mat;

                // Physics
                Rigidbody rb = agentGo.GetComponent<Rigidbody>();
                if (rb == null) rb = agentGo.AddComponent<Rigidbody>();
                rb.mass = 1.0f;
                rb.freezeRotation = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // HandDesignedReward first
                agentGo.AddComponent<HandDesignedReward>();

                // RelayAgent component added FIRST before BehaviorParameters
                RelayAgent agent = agentGo.AddComponent<RelayAgent>();
                agent.isLivePolicySlot = false;
                agent.SetAgentIdentity(agentNames[i], agentColors[i], eDials[i], aDials[i]);

                // Wire dependencies via reflection
                SetField(agent, "environment", env);
                SetField(agent, "energyCell", cell);
                SetField(agent, "receiver", receiver);

                // ML-Agents components
                var bps = agentGo.GetComponents<BehaviorParameters>();
                BehaviorParameters bp = bps.Length > 0 ? bps[0] : agentGo.AddComponent<BehaviorParameters>();
                for (int b = 1; b < bps.Length; b++) Object.DestroyImmediate(bps[b]);
                bp.BehaviorName = "RelayAgent";
                bp.BrainParameters.VectorObservationSize = 34;
                bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(3);
                bp.BehaviorType = BehaviorType.HeuristicOnly;

                DecisionRequester dr = agentGo.AddComponent<DecisionRequester>();
                dr.DecisionPeriod = 5;
                dr.TakeActionsBetweenDecisions = true;

                env.RegisterBaselineAgent(agentGo.transform);
            }

            // 6. Spawn Live Policy Slot Agent on the glowing platform
            Transform slotPlatform = GameObject.Find("Environment/LivePolicySlot/LivePolicySlotPlatform")?.transform;
            Vector3 slotPos = slotPlatform != null ? slotPlatform.position + Vector3.up * 1.0f : new Vector3(-13.0f, 1.0f, 4.0f);

            GameObject liveAgentGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            liveAgentGo.name = "LivePolicyAgent_Slot";
            liveAgentGo.transform.SetParent(agentsGroup);
            liveAgentGo.transform.position = slotPos;
            liveAgentGo.transform.localScale = new Vector3(0.8f, 1.0f, 0.8f);

            Material liveMat = new Material(litShader);
            Color gold = new Color(1.0f, 0.85f, 0.2f);
            liveMat.color = gold;
            if (liveMat.HasProperty("_EmissionColor"))
            {
                liveMat.SetColor("_EmissionColor", gold * 1.0f);
                liveMat.EnableKeyword("_EMISSION");
            }
            liveAgentGo.GetComponent<Renderer>().material = liveMat;

            Rigidbody liveRb = liveAgentGo.GetComponent<Rigidbody>();
            if (liveRb == null) liveRb = liveAgentGo.AddComponent<Rigidbody>();
            liveRb.mass = 1.0f;
            liveRb.freezeRotation = true;
            liveRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            liveAgentGo.AddComponent<LLMDesignedReward>();

            RelayAgent liveAgent = liveAgentGo.AddComponent<RelayAgent>();
            liveAgent.isLivePolicySlot = true;
            liveAgent.SetAgentIdentity("LivePolicyAgent", gold, 0.5f, 0.5f);

            SetField(liveAgent, "environment", env);
            SetField(liveAgent, "energyCell", cell);
            SetField(liveAgent, "receiver", receiver);
            env.RegisterLivePolicyAgent(liveAgent);

            var liveBps = liveAgentGo.GetComponents<BehaviorParameters>();
            BehaviorParameters liveBp = liveBps.Length > 0 ? liveBps[0] : liveAgentGo.AddComponent<BehaviorParameters>();
            for (int b = 1; b < liveBps.Length; b++) Object.DestroyImmediate(liveBps[b]);
            liveBp.BehaviorName = "RelayAgent";
            liveBp.BrainParameters.VectorObservationSize = 34;
            liveBp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(3);
            liveBp.BehaviorType = BehaviorType.HeuristicOnly;

            DecisionRequester liveDr = liveAgentGo.AddComponent<DecisionRequester>();
            liveDr.DecisionPeriod = 5;
            liveDr.TakeActionsBetweenDecisions = true;

            // 7. Add HUD & Beautifier
            GameObject hudGo = new GameObject("CorridorHUD");
            CorridorHUD hud = hudGo.AddComponent<CorridorHUD>();

            CorridorBeautifier beautifier = builderGo.GetComponent<CorridorBeautifier>();
            if (beautifier == null) beautifier = builderGo.AddComponent<CorridorBeautifier>();
            beautifier.ApplyBeautification();

            // Ensure LivePolicyLoader exists in scene
            if (Object.FindAnyObjectByType<LivePolicyLoader>() == null)
            {
                GameObject loaderHost = GameObject.Find("Environment/LivePolicySlot/LivePolicySlotPlatform") ?? slotPlatform?.gameObject ?? liveAgentGo;
                var loader = loaderHost.AddComponent<LivePolicyLoader>();
                loader.FindReferencesIfMissing();
            }

            // 8. Save scene
            string scenePath = "Assets/Scenes/RelayCorridor.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[CorridorSceneSetup] Scene successfully created and saved to {scenePath}");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null) field.SetValue(target, value);
        }
    }
}
