using System;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace RelayCorridor
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class RelayAgent : Agent
    {
        [Header("OCEAN Personality Dials")]
        [SerializeField, Range(0f, 1f)] public float extraversion = 0.5f;
        [SerializeField, Range(0f, 1f)] public float agreeableness = 0.5f;
        [SerializeField] public bool isLivePolicySlot = false;
        // isLivePolicySlot marks this as the slot for the LLM-designed trained policy.
        // It has no effect on gameplay logic — it is used only for HUD labels and visuals.
        // The policy running here is a standard trained ONNX model, identical in architecture
        // to the baseline agents. The LLM was involved in designing its reward function
        // during training, not in running it now.

        [Header("Identity & Styling")]
        [SerializeField] private string agentId = "Agent_0";
        [SerializeField] private Color agentColor = Color.cyan;

        [Header("References")]
        [SerializeField] private CorridorEnvironment environment;
        [SerializeField] private EnergyCell energyCell;
        [SerializeField] private ReceiverTerminal receiver;

        public Rigidbody rb { get; private set; }
        public float maxSpeed { get; private set; }
        public bool HasCell => energyCell != null && energyCell.IsCarried && energyCell.CarryingAgentId == agentId;
        public bool IsStandingOnPad { get; private set; }
        public string AgentId => agentId;
        public Color AgentColor => agentColor;
        public bool HasEscaped { get; set; }

        // Personality Real-Time State Tags for HUD and 3D Billboard Nameplates
        public string CurrentBehaviorState { get; private set; } = "Idling";
        public string ShortPersonalityBadge { get; private set; } = "";
        public string PersonalityArchetypeTitle { get; private set; } = "";
        public string CurrentEmoteTag { get; set; } = "";
        public float TopTargetSpeed { get; private set; } = 4.0f;

        // Telemetry counters for this episode
        public float CumulativeSpeed { get; private set; }
        public new int StepCount { get; private set; }
        public int PadHoldSteps { get; private set; }
        public int CellCarrySteps { get; private set; }
        public int PushCount { get; private set; }

        public float MeanSpeed => StepCount > 0 ? CumulativeSpeed / StepCount : 0f;

        private IRewardCalculator rewardCalculator;
        private bool observationCountAsserted = false;
        private readonly List<RelayAgent> teammateCache = new List<RelayAgent>();

        // Procedural movement and personality nuances
        private TrailRenderer trailRenderer;
        private Vector3 baseScale = new Vector3(0.8f, 1.0f, 0.8f);
        private float cautionPauseTimer = 0f;
        private float stuckTimer = 0f;
        private CorridorEnvironment.PuzzlePhase lastObservedPhase = CorridorEnvironment.PuzzlePhase.PadAPhase;

        public void Escape()
        {
            if (HasEscaped) return;
            HasEscaped = true;

            if (HasCell && energyCell != null)
            {
                if (receiver != null)
                {
                    receiver.DeliverCell(energyCell);
                }
                energyCell.Drop(receiver != null ? receiver.transform.position : transform.position);
                rewardCalculator?.ComputeEventReward(this, environment, RewardEvent.CellDelivered);
            }

            CurrentBehaviorState = "ESCAPED ✓ (Room Cleared)";
            CurrentEmoteTag = "🏆 ESCAPED!";
            Debug.Log($"[RelayCorridor] Agent '{AgentId}' reached the very end and ESCAPED the room!");

            // Deactivate GameObject to remove agent from the scene
            gameObject.SetActive(false);
        }

        public override void Initialize()
        {
            if (environment == null) environment = FindAnyObjectByType<CorridorEnvironment>();
            if (energyCell == null) energyCell = FindAnyObjectByType<EnergyCell>();
            if (receiver == null) receiver = FindAnyObjectByType<ReceiverTerminal>();

            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.freezeRotation = true;
                rb.mass = (agreeableness < 0.3f) ? 1.5f : 1.0f; // Low-A agents have slightly higher mass for pushing power
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            baseScale = transform.localScale;

            // Zero friction material to prevent sticking to walls and corners
            var col = GetComponent<Collider>();
            if (col != null)
            {
                var noFriction = new PhysicsMaterial("AgentFrictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0.05f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Average
                };
                col.material = noFriction;
            }

            // Compute personality-calibrated speed and archetypes
            ConfigurePersonalityArchetype();

            // Fetch reward calculator
            rewardCalculator = GetComponent<IRewardCalculator>();
            if (rewardCalculator == null)
            {
                if (isLivePolicySlot)
                {
                    rewardCalculator = gameObject.AddComponent<LLMDesignedReward>();
                }
                else
                {
                    rewardCalculator = gameObject.AddComponent<HandDesignedReward>();
                }
            }

            // Register with environment if not already
            if (environment != null && !isLivePolicySlot)
            {
                environment.RegisterBaselineAgent(transform);
            }

            // Apply color to renderer
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                rend.material.color = agentColor;
            }

            // Setup personality visual trail
            SetupPersonalityTrail();

            // Setup directional visor plate
            SetupVisor();

            RefreshTeammateCache();
        }

        private void SetupVisor()
        {
            Transform existing = transform.Find("Visor");
            if (existing != null) return;

            GameObject visorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visorGo.name = "Visor";
            visorGo.transform.SetParent(transform);
            visorGo.transform.localPosition = new Vector3(0f, 0.42f, 0.38f);
            visorGo.transform.localRotation = Quaternion.identity;
            visorGo.transform.localScale = new Vector3(0.55f, 0.22f, 0.28f);

            var col = visorGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = visorGo.GetComponent<Renderer>();
            if (rend != null)
            {
                Material visorMat = new Material(Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse"));
                visorMat.color = new Color(0.06f, 0.08f, 0.12f);
                visorMat.SetFloat("_Glossiness", 0.95f);
                rend.material = visorMat;
            }
        }

        private RelayAgent GetNearestTeammate()
        {
            if (teammateCache.Count == 0) RefreshTeammateCache();
            RelayAgent closest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < teammateCache.Count; i++)
            {
                var tm = teammateCache[i];
                if (tm == null || tm == this || !tm.gameObject.activeInHierarchy || tm.HasEscaped) continue;
                float d = Vector3.Distance(transform.position, tm.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closest = tm;
                }
            }
            return closest;
        }

        private void ConfigurePersonalityArchetype()
        {
            if (isLivePolicySlot)
            {
                maxSpeed = 3.8f;
                ShortPersonalityBadge = "Slot 5: LLM Dynamic";
                PersonalityArchetypeTitle = "Dynamic Delivery Courier";
            }
            else if (agentId.Contains("0") || agentId.Contains("HighE") || extraversion >= 0.7f)
            {
                extraversion = 0.9f;
                agreeableness = 0.5f;
                maxSpeed = 4.6f;
                ShortPersonalityBadge = "A0: High-E (Sprinter)";
                PersonalityArchetypeTitle = "The Aggressive Sprinter";
            }
            else if (agentId.Contains("1") || agentId.Contains("LowE") || (extraversion <= 0.3f && agreeableness >= 0.4f))
            {
                extraversion = 0.1f;
                agreeableness = 0.5f;
                maxSpeed = 2.0f;
                ShortPersonalityBadge = "A1: Low-E (Cautious)";
                PersonalityArchetypeTitle = "The Cautious Scout";
            }
            else if (agentId.Contains("2") || agentId.Contains("HighA") || agreeableness >= 0.7f)
            {
                extraversion = 0.5f;
                agreeableness = 0.9f;
                maxSpeed = 3.2f;
                ShortPersonalityBadge = "A2: High-A (Anchor)";
                PersonalityArchetypeTitle = "The Altruistic Anchor";
            }
            else if (agentId.Contains("3") || agentId.Contains("LowA") || agreeableness <= 0.3f)
            {
                extraversion = 0.5f;
                agreeableness = 0.1f;
                maxSpeed = 3.6f;
                ShortPersonalityBadge = "A3: Low-A (Pusher)";
                PersonalityArchetypeTitle = "The Self-Interested Pusher";
            }
            else
            {
                maxSpeed = Mathf.Lerp(2.5f, 5.5f, extraversion);
                ShortPersonalityBadge = $"{agentId}";
                PersonalityArchetypeTitle = "Standard Operator";
            }

            TopTargetSpeed = maxSpeed;
        }

        private void SetupPersonalityTrail()
        {
            trailRenderer = GetComponent<TrailRenderer>();
            if (trailRenderer == null) trailRenderer = gameObject.AddComponent<TrailRenderer>();

            trailRenderer.time = (extraversion > 0.6f || isLivePolicySlot) ? 0.65f : 0.30f;
            trailRenderer.startWidth = (extraversion > 0.6f || isLivePolicySlot) ? 0.40f : 0.18f;
            trailRenderer.endWidth = 0.02f;
            trailRenderer.material = new Material(Shader.Find("Sprites/Default"));

            Color c = agentColor;
            trailRenderer.startColor = c;
            c.a = 0.0f;
            trailRenderer.endColor = c;
        }

        public void SetAgentIdentity(string id, Color color, float e, float a)
        {
            agentId = id;
            agentColor = color;
            extraversion = e;
            agreeableness = a;
            Initialize();
        }

        private void RefreshTeammateCache()
        {
            teammateCache.Clear();
            var allAgents = FindObjectsByType<RelayAgent>(FindObjectsSortMode.None);
            foreach (var ag in allAgents)
            {
                if (ag != this && !ag.isLivePolicySlot)
                {
                    teammateCache.Add(ag);
                }
            }
        }

        public override void OnEpisodeBegin()
        {
            HasEscaped = false;
            StepCount = 0;
            CumulativeSpeed = 0f;
            PadHoldSteps = 0;
            CellCarrySteps = 0;
            PushCount = 0;
            IsStandingOnPad = false;
            cautionPauseTimer = 0f;
            CurrentBehaviorState = "Episode Start";
            transform.localScale = baseScale;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;

            // 1. Procedural forward running lean (High-E leans into sprint; Low-E stays upright)
            if (speed > 0.4f && vel.sqrMagnitude > 0.1f)
            {
                float leanAngle = (speed / maxSpeed) * (extraversion > 0.6f || isLivePolicySlot ? 16f : 3f);
                Quaternion moveLook = Quaternion.LookRotation(new Vector3(vel.x, 0f, vel.z), Vector3.up);
                Quaternion leanRot = moveLook * Quaternion.Euler(leanAngle, 0f, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, leanRot, Time.deltaTime * 12f);
            }

            // 2. High-Agreeableness Pad Anchor Breathing/Ground Lock Pulse
            if (IsStandingOnPad && agreeableness > 0.6f)
            {
                float pulse = 1.0f + 0.04f * Mathf.Sin(Time.time * 5f);
                transform.localScale = new Vector3(baseScale.x * pulse, baseScale.y, baseScale.z * pulse);
            }
            else
            {
                transform.localScale = baseScale;
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (environment == null) environment = FindAnyObjectByType<CorridorEnvironment>();
            if (environment == null) return;

            int collectedCount = 0;
            float corridorHalfLength = environment.CorridorHalfLength;
            float corridorHalfWidth = environment.CorridorHalfWidth;
            Vector3 selfPos = transform.position;
            Vector3 selfVel = rb != null ? rb.linearVelocity : Vector3.zero;

            // Self state (5)
            sensor.AddObservation(selfPos.x / corridorHalfLength);
            sensor.AddObservation(selfPos.z / corridorHalfWidth);
            sensor.AddObservation(selfVel.x / Mathf.Max(0.1f, maxSpeed));
            sensor.AddObservation(selfVel.z / Mathf.Max(0.1f, maxSpeed));
            sensor.AddObservation(HasCell ? 1f : 0f);
            collectedCount += 5;

            // Phase one-hot (6) - Multi-chamber compatible mapping
            var phase = environment.CurrentPhase;
            sensor.AddObservation(phase == CorridorEnvironment.PuzzlePhase.PadAPhase ? 1f : 0f);
            sensor.AddObservation(phase == CorridorEnvironment.PuzzlePhase.TransitABPhase ? 1f : 0f);
            sensor.AddObservation((phase == CorridorEnvironment.PuzzlePhase.PadBCPhase || phase == CorridorEnvironment.PuzzlePhase.PadC_3PadsPhase || phase == CorridorEnvironment.PuzzlePhase.PadD_3PadsPhase) ? 1f : 0f);
            sensor.AddObservation((phase == CorridorEnvironment.PuzzlePhase.TransitBCPhase || phase == CorridorEnvironment.PuzzlePhase.TransitCDPhase || phase == CorridorEnvironment.PuzzlePhase.TransitDEPhase) ? 1f : 0f);
            sensor.AddObservation(phase == CorridorEnvironment.PuzzlePhase.DeliveryPhase ? 1f : 0f);
            sensor.AddObservation(phase == CorridorEnvironment.PuzzlePhase.Complete ? 1f : 0f);
            collectedCount += 6;

            // Environment state (5)
            sensor.AddObservation(environment.IsPadAActive ? 1f : 0f);
            sensor.AddObservation(environment.IsPadBActive ? 1f : 0f);
            sensor.AddObservation(environment.IsPadCActive ? 1f : 0f);
            sensor.AddObservation(environment.IsGate1Open ? 1f : 0f);
            sensor.AddObservation(environment.IsGate2Open ? 1f : 0f);
            collectedCount += 5;

            // Cell state (3)
            Vector3 cellPos = environment.CellPosition;
            sensor.AddObservation((cellPos.x - selfPos.x) / corridorHalfLength);
            sensor.AddObservation((cellPos.z - selfPos.z) / corridorHalfWidth);
            sensor.AddObservation(HasCell ? 1f : 0f);
            collectedCount += 3;

            // Teammate relative positions (3 baseline teammates × 3 = 9)
            if (teammateCache.Count < 3) RefreshTeammateCache();

            int teammatesObserved = 0;
            for (int i = 0; i < teammateCache.Count && teammatesObserved < 3; i++)
            {
                var tm = teammateCache[i];
                if (tm == null) continue;
                Vector3 tmPos = tm.transform.position;
                sensor.AddObservation((tmPos.x - selfPos.x) / corridorHalfLength);
                sensor.AddObservation((tmPos.z - selfPos.z) / corridorHalfWidth);
                sensor.AddObservation(tm.HasCell ? 1f : 0f);
                teammatesObserved++;
            }
            while (teammatesObserved < 3)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                teammatesObserved++;
            }
            collectedCount += 9;

            // Goal positions relative to self (6)
            Vector3 receiverPos = environment.ReceiverPosition;
            Vector3 padAPos = environment.PadAPosition;
            Vector3 nearestUnactivatedPad = environment.GetNearestUnactivatedPad(selfPos);

            sensor.AddObservation((receiverPos.x - selfPos.x) / corridorHalfLength);
            sensor.AddObservation((receiverPos.z - selfPos.z) / corridorHalfWidth);
            sensor.AddObservation((padAPos.x - selfPos.x) / corridorHalfLength);
            sensor.AddObservation((padAPos.z - selfPos.z) / corridorHalfWidth);
            sensor.AddObservation((nearestUnactivatedPad.x - selfPos.x) / corridorHalfLength);
            sensor.AddObservation((nearestUnactivatedPad.z - selfPos.z) / corridorHalfWidth);
            collectedCount += 6;

            if (!observationCountAsserted)
            {
                Debug.Assert(collectedCount == 34,
                    $"Observation count mismatch: expected 34, got {collectedCount}");
                Debug.Log($"[RelayAgent {agentId}] CollectObservations assertion passed: exactly {collectedCount} entries.");
                observationCountAsserted = true;
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            // CRITICAL RULE: SetReward() is strictly forbidden inside OnActionReceived().
            // Only AddReward() with // STEP_REWARD is permitted.

            StepCount++;
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            CumulativeSpeed += speed;

            if (IsStandingOnPad) PadHoldSteps++;
            if (HasCell) CellCarrySteps++;

            // Continuous actions: [0] = moveX [-1,1], [1] = moveZ [-1,1], [2] = interact [-1,1]
            var continuous = actions.ContinuousActions;
            float moveX = continuous.Length > 0 ? Mathf.Clamp(continuous[0], -1f, 1f) : 0f;
            float moveZ = continuous.Length > 1 ? Mathf.Clamp(continuous[1], -1f, 1f) : 0f;
            float interact = continuous.Length > 2 ? Mathf.Clamp(continuous[2], -1f, 1f) : 0f;

            // Apply movement with personality-calibrated acceleration force
            Vector3 movement = new Vector3(moveX, 0f, moveZ);
            if (movement.sqrMagnitude > 1f) movement.Normalize();

            Vector3 targetVelocity = movement * maxSpeed;
            float accelMult = (extraversion > 0.6f || isLivePolicySlot) ? 22f : (extraversion < 0.3f ? 6f : 12f);

            if (rb != null)
            {
                Vector3 currentVel = rb.linearVelocity;
                Vector3 velChange = targetVelocity - new Vector3(currentVel.x, 0f, currentVel.z);
                rb.AddForce(velChange * accelMult, ForceMode.Acceleration);

                // Anti-stuck watchdog: If attempting to move but velocity is near-zero, inject unstuck impulse
                if (movement.sqrMagnitude > 0.1f && currentVel.magnitude < 0.35f)
                {
                    stuckTimer += Time.fixedDeltaTime;
                    if (stuckTimer > 0.35f)
                    {
                        float awayFromWallZ = transform.position.z > 0f ? -2.5f : 2.5f;
                        Vector3 unstuckImpulse = new Vector3(movement.x * 2.0f, 0f, awayFromWallZ);
                        rb.AddForce(unstuckImpulse, ForceMode.VelocityChange);
                        stuckTimer = 0f;
                    }
                }
                else
                {
                    stuckTimer = Mathf.Max(0f, stuckTimer - Time.fixedDeltaTime);
                }

                if (movement.sqrMagnitude > 0.05f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(movement, Vector3.up);
                    float turnSpeed = (extraversion > 0.6f || isLivePolicySlot) ? 16f : (extraversion < 0.3f ? 6f : 10f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
                }
            }

            // Cell interaction mechanics (Part 2C)
            if (interact > 0.5f)
            {
                if (!HasCell && energyCell != null && !energyCell.IsCarried)
                {
                    float distToCell = Vector3.Distance(transform.position, energyCell.Position);
                    if (distToCell <= 1.5f)
                    {
                        if (energyCell.Pickup(transform, agentId))
                        {
                            rewardCalculator?.ComputeEventReward(this, environment, RewardEvent.CellPickedUp);
                        }
                    }
                }
                else if (HasCell && receiver != null)
                {
                    float distToReceiver = Vector3.Distance(transform.position, receiver.transform.position);
                    if (distToReceiver <= 1.5f)
                    {
                        if (receiver.DeliverCell(energyCell))
                        {
                            rewardCalculator?.ComputeEventReward(this, environment, RewardEvent.CellDelivered);
                        }
                    }
                }
            }
            else if (interact < -0.5f)
            {
                if (HasCell && energyCell != null)
                {
                    Vector3 dropPos = transform.position + transform.forward * 0.4f;
                    dropPos.y = 0.4f;
                    energyCell.Drop(dropPos);
                    rewardCalculator?.ComputeEventReward(this, environment, RewardEvent.CellDropped);
                }
            }

            // Step reward calculation (additive only)
            rewardCalculator?.ComputeStepReward(this, environment);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuousActions = actionsOut.ContinuousActions;

            if (environment == null) environment = FindAnyObjectByType<CorridorEnvironment>();
            if (environment == null) return;

            var phase = environment.CurrentPhase;
            if (phase != lastObservedPhase)
            {
                lastObservedPhase = phase;
                cautionPauseTimer = 0f;
            }

            Vector3 target = transform.position;
            float interact = 0f;

            // ---------------------------------------------------------
            // CURRICULUM DISPATCH: ALTERNATING 20s vs COMBINED SIMULTANEOUS
            // ---------------------------------------------------------
            if (environment.CurrentRewardFocus == CorridorEnvironment.RewardFocusMode.CombinedSimultaneous)
            {
                // MODE 2: COMBINED SIMULTANEOUS MODE
                ExecuteCombinedSimultaneousHeuristic(ref target, ref interact);
            }
            else if (environment.CurrentRewardFocus == CorridorEnvironment.RewardFocusMode.TaskObjective)
            {
                // PHASE 2 (20-40s): TASK OBJECTIVE SOLVING
                ExecuteCoordinatedTaskHeuristic(ref target, ref interact);
            }
            else
            {
                // PHASE 1 (0-20s): PERSONALITY EXPRESSION
                ExecutePersonalityExpressionHeuristic(ref target, ref interact);
            }

            // Wall avoidance, doorway waypointing, and continuous motion guarantee
            Vector3 moveDir = ApplyWallAvoidanceAndWaypoints(target);

            if (continuousActions.Length >= 3)
            {
                continuousActions[0] = moveDir.x;
                continuousActions[1] = moveDir.z;
                continuousActions[2] = interact;
            }
            else if (continuousActions.Length > 0)
            {
                continuousActions[0] = moveDir.x;
            }
        }

        private void ExecutePersonalityExpressionHeuristic(ref Vector3 target, ref float interact)
        {
            if (isLivePolicySlot)
            {
                ExecuteLLMDynamicHeuristic(ref target, ref interact);
            }
            else if (extraversion >= 0.7f)
            {
                ExecuteHighExtraversionHeuristic(ref target, ref interact);
            }
            else if (extraversion <= 0.3f && agreeableness <= 0.6f)
            {
                ExecuteLowExtraversionHeuristic(ref target, ref interact);
            }
            else if (agreeableness >= 0.7f)
            {
                ExecuteHighAgreeablenessHeuristic(ref target, ref interact);
            }
            else if (agreeableness <= 0.3f)
            {
                ExecuteLowAgreeablenessHeuristic(ref target, ref interact);
            }
            else
            {
                ExecuteBalancedHeuristic(ref target, ref interact);
            }
        }

        // =========================================================================
        // COMBINED SIMULTANEOUS MODE HEURISTIC (MODE 2)
        // Agents actively solve the puzzle WHILE maintaining raw personality expression!
        // =========================================================================
        private void ExecuteCombinedSimultaneousHeuristic(ref Vector3 target, ref float interact)
        {
            var phase = environment.CurrentPhase;

            if (agentId.Contains("0") || extraversion >= 0.7f)
            {
                // Agent 0 (High-E Sprinter): Energetic courier that grabs the cell and guides it forward!
                if (!HasCell && energyCell != null && !energyCell.IsCarried)
                {
                    target = energyCell.Position;
                    if (Vector3.Distance(transform.position, energyCell.Position) < 1.5f) interact = 1f;
                    CurrentBehaviorState = "Combined: Blitzing to grab cell (4.6 m/s)";
                    CurrentEmoteTag = "⚡ BLITZ SPRINT";
                }
                else if (HasCell)
                {
                    if (phase == CorridorEnvironment.PuzzlePhase.TransitABPhase && !environment.IsGate1Open)
                    {
                        target = new Vector3(-5.3f, 0f, Mathf.Sin(Time.time * 3f) * 2.5f);
                        CurrentBehaviorState = "Combined: Pacing Gate 1 impatiently";
                        CurrentEmoteTag = "⚡ PACING IMPATIENTLY";
                    }
                    else if ((phase == CorridorEnvironment.PuzzlePhase.PadBCPhase || phase == CorridorEnvironment.PuzzlePhase.TransitBCPhase) && !environment.IsGate2Open)
                    {
                        target = environment.ChamberBWaitPoint + new Vector3(0f, 0f, Mathf.Sin(Time.time * 2.5f) * 2.0f);
                        CurrentBehaviorState = "Combined: Staging cell in Chamber B";
                        CurrentEmoteTag = "⚡ STAGING CELL";
                    }
                    else if ((phase == CorridorEnvironment.PuzzlePhase.PadC_3PadsPhase || phase == CorridorEnvironment.PuzzlePhase.TransitCDPhase) && !environment.IsGate3Open)
                    {
                        target = environment.ChamberCWaitPoint + new Vector3(0f, 0f, Mathf.Sin(Time.time * 2.5f) * 2.0f);
                        CurrentBehaviorState = "Combined: Staging cell in Chamber C (3-Plate Room)";
                        CurrentEmoteTag = "⚡ STAGING AT GATE 3";
                    }
                    else
                    {
                        target = environment.ReceiverPosition;
                        if (Vector3.Distance(transform.position, environment.ReceiverPosition) < 1.5f) interact = 1f;
                        CurrentBehaviorState = "Combined: Final delivery sprint to Receiver!";
                        CurrentEmoteTag = "⚡ DELIVERING";
                    }
                }
                else
                {
                    target = environment.ReceiverPosition;
                    CurrentBehaviorState = "Combined: Blitzing forward";
                    CurrentEmoteTag = "⚡ ADVANCING";
                }
            }
            else if (agentId.Contains("2") || agreeableness >= 0.7f)
            {
                // Agent 2 (High-A Altruistic Anchor): Dedicated to anchoring team pressure plates in every room!
                if (phase == CorridorEnvironment.PuzzlePhase.PadAPhase || phase == CorridorEnvironment.PuzzlePhase.TransitABPhase)
                {
                    target = environment.PadAPosition;
                    CurrentBehaviorState = IsStandingOnPad ? "Combined: Altruistic hold on Pad A (Gate 1 Open!)" : "Combined: Locking onto Pad A";
                    CurrentEmoteTag = "🛡️ HOLDING PAD A";
                }
                else if (phase == CorridorEnvironment.PuzzlePhase.PadBCPhase || phase == CorridorEnvironment.PuzzlePhase.TransitBCPhase)
                {
                    target = environment.PadBPosition;
                    CurrentBehaviorState = IsStandingOnPad ? "Combined: Altruistic hold on Pad B (Gate 2 Open!)" : "Combined: Moving to Pad B";
                    CurrentEmoteTag = "🛡️ HOLDING PAD B";
                }
                else if (phase == CorridorEnvironment.PuzzlePhase.PadC_3PadsPhase || phase == CorridorEnvironment.PuzzlePhase.TransitCDPhase)
                {
                    target = environment.PadDPosition;
                    CurrentBehaviorState = IsStandingOnPad ? "Combined: Altruistic hold on Pad D (Gate 3 - 3 Plates!)" : "Combined: Moving to Pad D";
                    CurrentEmoteTag = "🛡️ HOLDING PAD D";
                }
                else
                {
                    target = environment.ReceiverPosition;
                    CurrentBehaviorState = "Combined: Escorting courier to Receiver";
                    CurrentEmoteTag = "🛡️ SUPPORTING";
                }
            }
            else if (agentId.Contains("3") || agreeableness <= 0.3f)
            {
                // Agent 3 (Low-A Self-Interested Pusher): Shoves teammates out of the way, refuses plates!
                RelayAgent closeTm = GetNearestTeammate();
                if (closeTm != null && Vector3.Distance(transform.position, closeTm.transform.position) < 2.0f)
                {
                    target = closeTm.transform.position;
                    CurrentBehaviorState = $"Combined: Ramming {closeTm.ShortPersonalityBadge}!";
                    CurrentEmoteTag = "💥 PHYSICAL SHOVE!";
                }
                else
                {
                    // Patrol aggressively around active chamber center
                    Vector3 chamberCenter = (phase <= CorridorEnvironment.PuzzlePhase.TransitABPhase) ? new Vector3(-8f, 0f, 0f) :
                                            (phase <= CorridorEnvironment.PuzzlePhase.TransitBCPhase) ? environment.ChamberBWaitPoint :
                                            (phase <= CorridorEnvironment.PuzzlePhase.TransitCDPhase) ? environment.ChamberCWaitPoint :
                                            environment.ChamberDWaitPoint;
                    target = chamberCenter + new Vector3(Mathf.Sin(Time.time * 2.0f) * 3.0f, 0f, Mathf.Cos(Time.time * 2.5f) * 2.5f);
                    CurrentBehaviorState = "Combined: Selfish patrol; refusing plates";
                    CurrentEmoteTag = "💥 ROAMING PUSHER";
                }
            }
            else if (agentId.Contains("1") || extraversion <= 0.3f)
            {
                // Agent 1 (Low-E Cautious Scout): Deliberate speed, social distancing, pauses at doorways!
                RelayAgent closeTm = GetNearestTeammate();
                if (closeTm != null && Vector3.Distance(transform.position, closeTm.transform.position) < 2.4f)
                {
                    Vector3 away = (transform.position - closeTm.transform.position).normalized;
                    target = transform.position + away * 1.8f;
                    CurrentBehaviorState = "Combined: Cautious social distancing";
                    CurrentEmoteTag = "👀 SOCIAL RETREAT";
                }
                else if (phase == CorridorEnvironment.PuzzlePhase.PadC_3PadsPhase || phase == CorridorEnvironment.PuzzlePhase.TransitCDPhase)
                {
                    // In Room 3 (3 plates needed): Cautious Scout secures Pad F!
                    target = environment.PadFPosition;
                    CurrentBehaviorState = IsStandingOnPad ? "Combined: Cautiously anchoring Pad F (Room 3)" : "Combined: Approaching Pad F carefully";
                    CurrentEmoteTag = "👀 ANCHORING PAD F";
                }
                else
                {
                    Vector3 dest = (phase <= CorridorEnvironment.PuzzlePhase.TransitABPhase) ? new Vector3(-10f, 0f, 2.0f) :
                                   (phase <= CorridorEnvironment.PuzzlePhase.TransitBCPhase) ? new Vector3(0f, 0f, 2.0f) :
                                   (phase <= CorridorEnvironment.PuzzlePhase.TransitCDPhase) ? new Vector3(10f, 0f, 2.0f) :
                                   new Vector3(24.0f, 0f, 2.0f);
                    target = dest;
                    CurrentBehaviorState = "Combined: Cautious perimeter escort (2.0 m/s)";
                    CurrentEmoteTag = "👀 CAUTIOUS PATROL";
                }
            }
            else
            {
                // Slot 5: Dynamic Courier acts as key anchor for 2-plate & 3-plate coordination!
                if (phase == CorridorEnvironment.PuzzlePhase.PadBCPhase || phase == CorridorEnvironment.PuzzlePhase.TransitBCPhase)
                {
                    target = environment.PadCPosition;
                    CurrentBehaviorState = IsStandingOnPad ? "Combined: Courier anchoring Pad C (Room 2)" : "Combined: Courier rushing to Pad C";
                    CurrentEmoteTag = "⚡ ANCHORING PAD C";
                }
                else if (phase == CorridorEnvironment.PuzzlePhase.PadC_3PadsPhase || phase == CorridorEnvironment.PuzzlePhase.TransitCDPhase)
                {
                    target = environment.PadEPosition;
                    CurrentBehaviorState = IsStandingOnPad ? "Combined: Courier anchoring Pad E (Room 3)" : "Combined: Courier rushing to Pad E";
                    CurrentEmoteTag = "⚡ ANCHORING PAD E";
                }
                else
                {
                    target = environment.ReceiverPosition;
                    CurrentBehaviorState = "Combined: Agile courier advancing";
                    CurrentEmoteTag = "⚡ ADVANCING";
                }
            }
        }

        private Vector3 ApplyWallAvoidanceAndWaypoints(Vector3 desiredTarget)
        {
            Vector3 myPos = transform.position;
            Vector3 target = desiredTarget;

            // 1. Safe Bounds Clamping (Keep targets safely inside the expanded 50m corridor)
            target.z = Mathf.Clamp(target.z, -3.8f, 3.8f);
            target.x = Mathf.Clamp(target.x, -14.2f, 24.2f);

            // 2. Doorway / Gate Waypoint Routing across all 4 gates
            float[] gateX = new float[] { -5.0f, 5.0f, 15.0f };
            foreach (float gx in gateX)
            {
                if (myPos.x < gx - 0.2f && target.x > gx + 0.2f)
                {
                    if (Mathf.Abs(myPos.z) > 0.75f)
                    {
                        target = new Vector3(gx - 0.3f, 0f, Mathf.MoveTowards(myPos.z, 0f, 1.8f));
                    }
                    else
                    {
                        target = new Vector3(gx + 0.6f, 0f, 0f);
                    }
                    break;
                }
            }

            Vector3 moveDir = (target - myPos);
            moveDir.y = 0f;

            // 3. Keep Moving At All Times! (Never zero velocity)
            if (moveDir.magnitude < 0.28f)
            {
                // At target or holding pad: maintain micro-pacing/patrol so they are never frozen
                int hash = Mathf.Abs(agentId.GetHashCode());
                float seed = (hash % 100) * 0.1f;
                float paceX = Mathf.Sin(Time.time * 2.8f + seed) * 0.35f;
                float paceZ = Mathf.Cos(Time.time * 2.2f + seed) * 0.35f;
                moveDir = new Vector3(paceX, 0f, paceZ);
            }
            else
            {
                moveDir.Normalize();
            }

            // 4. Whisker Raycasts for Wall Avoidance
            Vector3 rayOrigin = myPos + Vector3.up * 0.45f;
            float checkDist = 1.35f;

            RaycastHit hit;
            Vector3[] rayDirs = new Vector3[]
            {
                moveDir,
                Quaternion.Euler(0, 32f, 0) * moveDir,
                Quaternion.Euler(0, -32f, 0) * moveDir
            };

            foreach (var rDir in rayDirs)
            {
                if (Physics.Raycast(rayOrigin, rDir, out hit, checkDist))
                {
                    if (hit.collider.GetComponent<RelayAgent>() == null && hit.collider.GetComponent<EnergyCell>() == null)
                    {
                        Vector3 tangent = Vector3.ProjectOnPlane(moveDir, hit.normal).normalized;
                        if (tangent.sqrMagnitude > 0.01f)
                        {
                            moveDir = (tangent + hit.normal * 0.4f).normalized;
                            break;
                        }
                    }
                }
            }

            return moveDir;
        }

        // =========================================================================
        // MINUTE 2: COORDINATED TASK OBJECTIVE SOLVING HEURISTIC
        // All agents coordinate with assigned roles to solve the corridor puzzle!
        // =========================================================================
        private void ExecuteCoordinatedTaskHeuristic(ref Vector3 target, ref float interact)
        {
            var phase = environment.CurrentPhase;

            if (agentId.Contains("2") || agreeableness >= 0.7f)
            {
                // Agent 2: Altruistic Anchor holds Pad A during Chamber A, then Pad B in Chamber B
                if (phase == CorridorEnvironment.PuzzlePhase.PadAPhase || phase == CorridorEnvironment.PuzzlePhase.TransitABPhase)
                {
                    target = environment.PadAPosition;
                    CurrentBehaviorState = IsStandingOnPad ? "Task: Anchoring Pad A (Gate 1 Open!)" : "Task: Locking onto Pad A";
                    CurrentEmoteTag = "⚓ HOLDING PAD A";
                }
                else if (phase == CorridorEnvironment.PuzzlePhase.PadBCPhase)
                {
                    target = environment.PadBPosition;
                    CurrentBehaviorState = IsStandingOnPad ? "Task: Anchoring Pad B (Gate 2 Open!)" : "Task: Moving to Pad B";
                    CurrentEmoteTag = "⚓ HOLDING PAD B";
                }
                else
                {
                    target = environment.ReceiverPosition;
                    CurrentBehaviorState = "Task: Supporting delivery";
                    CurrentEmoteTag = "🌟 SUPPORT";
                }
            }
            else if (agentId.Contains("0") || extraversion >= 0.7f)
            {
                // Agent 0: Sprinter Courier carries the energy cell to Chamber C!
                if (!HasCell && energyCell != null && !energyCell.IsCarried)
                {
                    target = energyCell.Position;
                    if (Vector3.Distance(transform.position, energyCell.Position) < 1.5f)
                    {
                        interact = 1f;
                    }
                    CurrentBehaviorState = "Task: Grabbing Energy Cell";
                    CurrentEmoteTag = "⚡ CELL RETRIEVAL";
                }
                else if (HasCell)
                {
                    if (phase == CorridorEnvironment.PuzzlePhase.TransitABPhase && !environment.IsGate1Open)
                    {
                        target = new Vector3(-5.3f, 0f, 0f);
                        CurrentBehaviorState = "Task: Waiting at Gate 1";
                        CurrentEmoteTag = "⚡ WAITING GATE 1";
                    }
                    else if (phase == CorridorEnvironment.PuzzlePhase.PadBCPhase && !environment.IsGate2Open)
                    {
                        target = environment.ChamberBWaitPoint;
                        CurrentBehaviorState = "Task: Staging cell in Chamber B";
                        CurrentEmoteTag = "⚡ WAITING GATE 2";
                    }
                    else
                    {
                        target = environment.ReceiverPosition;
                        if (Vector3.Distance(transform.position, environment.ReceiverPosition) < 1.5f)
                        {
                            interact = 1f;
                        }
                        CurrentBehaviorState = "Task: Sprinting cell to Receiver!";
                        CurrentEmoteTag = "⚡ DELIVERING";
                    }
                }
                else
                {
                    target = environment.ReceiverPosition;
                    CurrentBehaviorState = "Task: Advancing forward";
                    CurrentEmoteTag = "⚡ ADVANCING";
                }
            }
            else if (agentId.Contains("3") || agreeableness <= 0.3f)
            {
                // Agent 3: Cooperates in Minute 2! Holds Pad C in Chamber B to open Gate 2!
                if (phase == CorridorEnvironment.PuzzlePhase.PadAPhase || phase == CorridorEnvironment.PuzzlePhase.TransitABPhase)
                {
                    target = environment.IsGate1Open ? environment.ChamberBWaitPoint : new Vector3(-6.2f, 0f, 1.2f);
                    CurrentBehaviorState = "Task: Moving to Chamber B";
                    CurrentEmoteTag = "🔧 ADVANCING";
                }
                else if (phase == CorridorEnvironment.PuzzlePhase.PadBCPhase)
                {
                    target = environment.PadCPosition;
                    CurrentBehaviorState = IsStandingOnPad ? "Task: Anchoring Pad C (Gate 2 Open!)" : "Task: Locking onto Pad C";
                    CurrentEmoteTag = "🔧 HOLDING PAD C";
                }
                else
                {
                    target = environment.ReceiverPosition;
                    CurrentBehaviorState = "Task: Escorting courier to Receiver";
                    CurrentEmoteTag = "🔧 ESCORT";
                }
            }
            else
            {
                // Agent 1: Low-E supports squad
                target = environment.IsGate1Open ? environment.ChamberBWaitPoint : new Vector3(-8.5f, 0f, 2.0f);
                CurrentBehaviorState = "Task: Escort & Perimeter support";
                CurrentEmoteTag = "🛡️ ESCORT";
            }
        }

        // =========================================================================
        // MINUTE 1: AGENT 0 (THE AGGRESSIVE SPRINTER) - HIGH EXTRAVERSION
        // Movement: Sprints circuits at top speed (6.2 m/s), paces Gate 1 furiously.
        // =========================================================================
        private void ExecuteHighExtraversionHeuristic(ref Vector3 target, ref float interact)
        {
            if (!HasCell)
            {
                if (energyCell != null && Vector3.Distance(transform.position, energyCell.Position) < 1.4f)
                    interact = 1f;

                if (!environment.IsGate1Open)
                {
                    // Paces furiously along Gate 1 at maximum sprint speed!
                    float paceZ = Mathf.Sin(Time.time * 5.5f) * 3.8f;
                    target = new Vector3(-5.3f, 0f, paceZ);
                    CurrentBehaviorState = "High-E: Pacing Gate 1 impatiently at 6.2 m/s!";
                    CurrentEmoteTag = "⚡ PACING IMPATIENTLY";
                }
                else
                {
                    // High-speed sprint through Gate 1 into Chamber B
                    target = environment.ChamberBWaitPoint + new Vector3(0f, 0f, Mathf.Sin(Time.time * 4f) * 2.5f);
                    CurrentBehaviorState = "High-E: Blitzing through open gate!";
                    CurrentEmoteTag = "⚡ BLITZ SPRINT";
                }
            }
            else
            {
                target = environment.IsGate1Open ? environment.ChamberBWaitPoint : new Vector3(-5.3f, 0f, Mathf.Sin(Time.time * 4f) * 2.5f);
                CurrentBehaviorState = "High-E: Charging cell forward!";
                CurrentEmoteTag = "⚡ CHARGING";
            }
        }

        // =========================================================================
        // MINUTE 1: AGENT 1 (THE CAUTIOUS SCOUT) - LOW EXTRAVERSION
        // Movement: Slow walk (2.0 m/s), halts at gates, retreats to maintain distance.
        // =========================================================================
        private void ExecuteLowExtraversionHeuristic(ref Vector3 target, ref float interact)
        {
            // Active Social Distancing: Retreats if any teammate gets within 3.2m!
            RelayAgent closeTm = GetNearestTeammate();
            if (closeTm != null && Vector3.Distance(transform.position, closeTm.transform.position) < 3.2f)
            {
                Vector3 away = (transform.position - closeTm.transform.position).normalized;
                target = transform.position + away * 2.5f;
                target.x = Mathf.Clamp(target.x, -14.0f, -6.0f);
                target.z = Mathf.Clamp(target.z, -5.0f, 5.0f);
                CurrentBehaviorState = "Low-E: Retreating to maintain safe distance";
                CurrentEmoteTag = "🛡️ SOCIAL DISTANCE RETREAT";
                return;
            }

            // Checks near gate with deliberate inspection pause
            float distToGate1 = Vector3.Distance(transform.position, environment.Gate1Position);
            if (distToGate1 < 2.5f && cautionPauseTimer < 1.5f)
            {
                cautionPauseTimer += Time.deltaTime;
                target = transform.position;
                CurrentBehaviorState = "Low-E: Pausing to inspect gate safety...";
                CurrentEmoteTag = "🛡️ INSPECTING";
                return;
            }

            // Safe slow perimeter walk
            target = new Vector3(-12.5f, 0f, Mathf.Sin(Time.time * 1.2f) * 3.0f);
            CurrentBehaviorState = "Low-E: Slow cautious perimeter patrol (2.0 m/s)";
            CurrentEmoteTag = "🛡️ CAUTIOUS PATROL";
        }

        // =========================================================================
        // MINUTE 1: AGENT 2 (THE ALTRUISTIC ANCHOR) - HIGH AGREEABLENESS
        // Movement: Directly plants on Pad A, locks in, pulses beacon to hold gate.
        // =========================================================================
        private void ExecuteHighAgreeablenessHeuristic(ref Vector3 target, ref float interact)
        {
            target = environment.PadAPosition;
            if (IsStandingOnPad)
            {
                CurrentBehaviorState = "High-A: Firmly anchored on Pad A (Gate 1 held open!)";
                CurrentEmoteTag = "⚓ ANCHORED (ALTRUIST)";
            }
            else
            {
                CurrentBehaviorState = "High-A: Rushing to lock onto Pad A for team";
                CurrentEmoteTag = "⚓ RUSHING TO PAD";
            }
        }

        // =========================================================================
        // MINUTE 1: AGENT 3 (THE SELF-INTERESTED PUSHER) - LOW AGREEABLENESS
        // Movement: Refuses pads; actively hunts teammates to physically ram/shove them!
        // =========================================================================
        private void ExecuteLowAgreeablenessHeuristic(ref Vector3 target, ref float interact)
        {
            // Hunts nearest teammate to physically shove them out of position
            RelayAgent targetTm = GetNearestTeammate();
            if (targetTm != null)
            {
                target = targetTm.transform.position;
                CurrentBehaviorState = $"Low-A: Charging at {targetTm.ShortPersonalityBadge} to shove aside!";
                CurrentEmoteTag = "💥 TARGETING TEAMMATE";
            }
            else
            {
                target = new Vector3(-6.0f, 0f, 0.0f);
                CurrentBehaviorState = "Low-A: Selfish patrol; refusing pads";
                CurrentEmoteTag = "💥 SELFISH PUSHER";
            }
        }

        // =========================================================================
        // SLOT 5: LLM-DESIGNED REWARD POLICY (DYNAMIC COURIER)
        // =========================================================================
        private void ExecuteLLMDynamicHeuristic(ref Vector3 target, ref float interact)
        {
            target = environment.IsGate1Open ? environment.ChamberBWaitPoint : new Vector3(-7f, 0f, 1f);
            CurrentBehaviorState = "Slot 5: Agile high-speed courier (LLM PPO)";
            CurrentEmoteTag = "🌟 AGILE COURIER";
        }

        private void ExecuteBalancedHeuristic(ref Vector3 target, ref float interact)
        {
            target = environment.ReceiverPosition;
            CurrentBehaviorState = "Standard Navigation";
            CurrentEmoteTag = "STANDARD";
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying) return;

            // Cap extreme velocities to prevent physics tunneling
            if (rb != null)
            {
                float maxAllowed = maxSpeed * 1.5f;
                Vector3 hVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (hVel.sqrMagnitude > maxAllowed * maxAllowed)
                {
                    Vector3 clamped = hVel.normalized * maxAllowed;
                    rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.z);
                }
            }

            EnforceCorridorBoundaries();
        }

        public void EnforceCorridorBoundaries()
        {
            Vector3 pos = transform.position;
            bool adjusted = false;

            // Outer corridor boundaries (Corridor length 50, width 12; agent radius 0.5)
            float minX = -14.45f;
            float maxX = 24.45f;
            float minZ = -5.45f;
            float maxZ = 5.45f;

            if (pos.x < minX) { pos.x = minX; adjusted = true; }
            if (pos.x > maxX) { pos.x = maxX; adjusted = true; }
            if (pos.z < minZ) { pos.z = minZ; adjusted = true; }
            if (pos.z > maxZ) { pos.z = maxZ; adjusted = true; }

            // Height stability (keep upright on floor, never float or fall through floor)
            if (pos.y < 0.5f || pos.y > 1.8f) { pos.y = 1.0f; adjusted = true; }

            // Interior partition & gate boundaries for 3 gates
            float[] gatesX = new float[] { -5.0f, 5.0f, 15.0f };
            bool[] gatesOpen = new bool[] {
                environment != null && environment.IsGate1Open,
                environment != null && environment.IsGate2Open,
                environment != null && environment.IsGate3Open
            };

            for (int gi = 0; gi < gatesX.Length; gi++)
            {
                float gx = gatesX[gi];
                bool gOpen = gatesOpen[gi];
                if (Mathf.Abs(pos.x - gx) < 0.65f)
                {
                    bool inDoorway = Mathf.Abs(pos.z) <= 2.25f;
                    if (!gOpen || !inDoorway)
                    {
                        if (pos.x < gx && pos.x > gx - 0.55f) { pos.x = gx - 0.55f; adjusted = true; }
                        else if (pos.x >= gx && pos.x < gx + 0.55f) { pos.x = gx + 0.55f; adjusted = true; }
                    }
                }
            }

            if (adjusted)
            {
                transform.position = pos;
                if (rb != null)
                {
                    rb.position = pos;
                    Vector3 v = rb.linearVelocity;
                    if ((pos.x <= minX && v.x < 0) || (pos.x >= maxX && v.x > 0)) v.x = 0;
                    if ((pos.z <= minZ && v.z < 0) || (pos.z >= maxZ && v.z > 0)) v.z = 0;
                    rb.linearVelocity = v;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<RelayPad>() != null)
            {
                IsStandingOnPad = true;
                rewardCalculator?.ComputeEventReward(this, environment, RewardEvent.PadActivated);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<RelayPad>() != null)
            {
                IsStandingOnPad = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            var otherAgent = collision.gameObject.GetComponent<RelayAgent>();
            if (otherAgent != null)
            {
                PushCount++;
                rewardCalculator?.ComputeEventReward(this, environment, RewardEvent.AgentPushed);

                // Physical manifestation of Low Agreeableness: actively shoves teammates out of the way!
                if (agreeableness < 0.35f && rb != null)
                {
                    var otherRb = otherAgent.GetComponent<Rigidbody>();
                    if (otherRb != null)
                    {
                        Vector3 shoveDir = (otherAgent.transform.position - transform.position).normalized;
                        shoveDir.y = 0f;
                        if (shoveDir.sqrMagnitude < 0.01f) shoveDir = transform.forward;
                        otherRb.AddForce(shoveDir * 4.0f, ForceMode.Impulse);
                        CurrentBehaviorState = $"Low-A: Shoved {otherAgent.ShortPersonalityBadge}!";
                        CurrentEmoteTag = "💥 SHOVE!";
                    }
                }
            }
        }
    }
}
