using System;
using System.Collections.Generic;
using UnityEngine;

namespace RelayCorridor
{
    public class CorridorEnvironment : MonoBehaviour
    {
        public enum PuzzlePhase
        {
            PadAPhase,          // Room 1: Pad A (1 plate)
            TransitABPhase,     // Gate 1 Open: Moving to Room 2
            PadBCPhase,         // Room 2: Pad B & Pad C (2 plates)
            TransitBCPhase,     // Gate 2 Open: Moving to Room 3
            PadC_3PadsPhase,    // Room 3: Pad D, Pad E, Pad F (3 plates)
            TransitCDPhase,     // Gate 3 Open: Moving to Room 4
            PadD_3PadsPhase,    // Room 4: Pad G, Pad H, Pad I (3 plates)
            TransitDEPhase,     // Gate 4 Open: Moving to Room 5 (Last Room)
            DeliveryPhase,      // Room 5: Delivering to Receiver
            Complete            // Episode complete
        }

        public enum RewardFocusMode
        {
            PersonalityExpression, // Alternating Phase 1 (0-20s, 40-60s)
            TaskObjective,         // Alternating Phase 2 (20-40s, 60-80s)
            CombinedSimultaneous   // Combined Simultaneous Mode (Both active together)
        }

        public enum CurriculumMode
        {
            Alternating20s,
            CombinedSimultaneous
        }

        [Header("Corridor Geometry")]
        [SerializeField] private float corridorLength = 40.0f;
        [SerializeField] private float corridorWidth = 12.0f;
        [SerializeField] private float chamberBLimitX = -5.0f;
        [SerializeField] private float chamberCLimitX = 5.0f;
        [SerializeField] private float chamberDLimitX = 15.0f;
        // chamberELimitX removed (4 chambers total)

        [Header("Components - Room 1 & 2")]
        [SerializeField] private RelayPad padA;
        [SerializeField] private RelayPad padB;
        [SerializeField] private RelayPad padC;
        [SerializeField] private SlidingGate gate1;
        [SerializeField] private SlidingGate gate2;

        [Header("Components - Room 3 (3 Plates)")]
        [SerializeField] private RelayPad padD;
        [SerializeField] private RelayPad padE;
        [SerializeField] private RelayPad padF;
        [SerializeField] private SlidingGate gate3;

        [Header("Components - Room 4 (3 Plates)")]
        [SerializeField] private RelayPad padG;
        [SerializeField] private RelayPad padH;
        [SerializeField] private RelayPad padI;
        [SerializeField] private SlidingGate gate4;

        [Header("Components - Core & Receiver")]
        [SerializeField] private EnergyCell energyCell;
        [SerializeField] private ReceiverTerminal receiverTerminal;
        [SerializeField] private Transform livePolicySlotTransform;

        [Header("Episode Control")]
        [SerializeField] private int maxSteps = 5000;
        [SerializeField] private bool autoResetOnTimeout = false; // User requested: manual reset on-screen
        [SerializeField] private bool autoResetOnDelivery = false;
        private bool isGate1PermanentlyOpen = false;
        private bool isGate2PermanentlyOpen = false;
        private bool isGate3PermanentlyOpen = false;
        public bool IsGate1PermanentlyOpen => isGate1PermanentlyOpen;
        public bool IsGate2PermanentlyOpen => isGate2PermanentlyOpen;
        public bool IsGate3PermanentlyOpen => isGate3PermanentlyOpen;
        [SerializeField] private RelayAgent livePolicyAgent;
        public bool AutoResetOnTimeout => autoResetOnTimeout;
        public RelayAgent LivePolicyAgent => livePolicyAgent;
        private Vector3 livePolicySpawnDefault = new Vector3(-13.0f, 1.0f, 4.0f);

        [Header("Curriculum Mode (Toggle via 'C' key or HUD)")]
        [SerializeField] private CurriculumMode currentCurriculumMode = CurriculumMode.CombinedSimultaneous;
        public CurriculumMode CurrentCurriculumMode => currentCurriculumMode;

        [Header("Dynamic 20-Second Reward Cycle (Curriculum)")]
        [SerializeField] private float cycleDurationSeconds = 40f;
        [SerializeField] private float phaseDurationSeconds = 20f;

        // Dynamic Reward Cycle state
        public RewardFocusMode CurrentRewardFocus { get; private set; } = RewardFocusMode.CombinedSimultaneous;
        public float CycleTimeRemaining { get; private set; } = 0f;
        public float PersonalityRewardWeight { get; private set; } = 0.50f;
        public float TaskRewardWeight { get; private set; } = 0.50f;
        public int CurrentCycleMinute { get; private set; } = 1;
        public event Action<RewardFocusMode> OnRewardFocusChanged;

        // Public state properties
        public bool IsPadAActive => padA != null && padA.IsActive;
        public bool IsPadBActive => padB != null && padB.IsActive;
        public bool IsPadCActive => padC != null && padC.IsActive;
        public bool IsPadDActive => padD != null && padD.IsActive;
        public bool IsPadEActive => padE != null && padE.IsActive;
        public bool IsPadFActive => padF != null && padF.IsActive;
        public bool IsPadGActive => padG != null && padG.IsActive;
        public bool IsPadHActive => padH != null && padH.IsActive;
        public bool IsPadIActive => padI != null && padI.IsActive;

        public bool IsGate1Open => gate1 != null && gate1.IsOpen;
        public bool IsGate2Open => gate2 != null && gate2.IsOpen;
        public bool IsGate3Open => gate3 != null && gate3.IsOpen;
        public bool IsGate4Open => gate4 != null && gate4.IsOpen;

        public bool IsCellDelivered => receiverTerminal != null && receiverTerminal.IsDelivered;
        public Vector3 CellPosition => energyCell != null ? energyCell.Position : Vector3.zero;
        public string CellHolderAgentId => energyCell != null ? energyCell.CarryingAgentId : null;
        public PuzzlePhase CurrentPhase { get; private set; }
        public bool IsLivePolicyActive { get; private set; }
        public string LivePolicyPersonaName { get; private set; } = "None";

        public float CorridorLength => corridorLength;
        public float CorridorWidth => corridorWidth;
        public float CorridorHalfLength => corridorLength * 0.5f;
        public float CorridorHalfWidth => corridorWidth * 0.5f;

        public Vector3 PadAPosition => padA != null ? padA.transform.position : new Vector3(-7.5f, 0f, -3f);
        public Vector3 PadBPosition => padB != null ? padB.transform.position : new Vector3(0f, 0f, -3.5f);
        public Vector3 PadCPosition => padC != null ? padC.transform.position : new Vector3(0f, 0f, 3.5f);
        public Vector3 PadDPosition => padD != null ? padD.transform.position : new Vector3(10f, 0f, -3.8f);
        public Vector3 PadEPosition => padE != null ? padE.transform.position : new Vector3(8.5f, 0f, 0f);
        public Vector3 PadFPosition => padF != null ? padF.transform.position : new Vector3(10f, 0f, 3.8f);
        public Vector3 PadGPosition => padG != null ? padG.transform.position : new Vector3(20f, 0f, -3.8f);
        public Vector3 PadHPosition => padH != null ? padH.transform.position : new Vector3(18.5f, 0f, 0f);
        public Vector3 PadIPosition => padI != null ? padI.transform.position : new Vector3(20f, 0f, 3.8f);

        public Vector3 Gate1Position => gate1 != null ? gate1.transform.position : new Vector3(-5f, 0f, 0f);
        public Vector3 Gate2Position => gate2 != null ? gate2.transform.position : new Vector3(5f, 0f, 0f);
        public Vector3 Gate3Position => gate3 != null ? gate3.transform.position : new Vector3(15f, 0f, 0f);
        public Vector3 Gate4Position => gate4 != null ? gate4.transform.position : new Vector3(25f, 0f, 0f);

        public Vector3 ReceiverPosition => receiverTerminal != null ? receiverTerminal.transform.position : new Vector3(24f, 0f, 0f);
        public Vector3 ChamberBWaitPoint => new Vector3(0.0f, 0f, 0f);
        public Vector3 ChamberCWaitPoint => new Vector3(10.0f, 0f, 0f);
        public Vector3 ChamberDWaitPoint => new Vector3(20.0f, 0f, 0f);
        public Vector3 ChamberEWaitPoint => new Vector3(30.0f, 0f, 0f);

        public int CurrentStep { get; private set; }
        public int EpisodeCount { get; private set; }
        public int DeliveryCount { get; private set; }
        public int TimeoutCount { get; private set; }

        public event Action<PuzzlePhase, PuzzlePhase> OnPhaseChanged;
        public event Action OnEpisodeCompleted;

        private Vector3 defaultCellSpawn;
        private readonly List<Vector3> baselineSpawnDefaults = new List<Vector3>();
        private readonly List<Transform> baselineAgents = new List<Transform>();

        public void InitializeEnvironmentReferences(
            RelayPad padA, RelayPad padB, RelayPad padC,
            SlidingGate gate1, SlidingGate gate2,
            EnergyCell cell, ReceiverTerminal receiver,
            RelayPad padD = null, RelayPad padE = null, RelayPad padF = null,
            RelayPad padG = null, RelayPad padH = null, RelayPad padI = null,
            SlidingGate gate3 = null, SlidingGate gate4 = null)
        {
            this.padA = padA;
            this.padB = padB;
            this.padC = padC;
            this.padD = padD;
            this.padE = padE;
            this.padF = padF;
            this.padG = padG;
            this.padH = padH;
            this.padI = padI;

            this.gate1 = gate1;
            this.gate2 = gate2;
            this.gate3 = gate3;
            this.gate4 = gate4;

            this.energyCell = cell;
            this.receiverTerminal = receiver;

            if (energyCell != null && energyCell.transform.position.x < -6.0f)
            {
                defaultCellSpawn = energyCell.transform.position;
            }
            else
            {
                defaultCellSpawn = new Vector3(-13.0f, 0.6f, 0.0f);
            }
        }

        public void ToggleCurriculumMode()
        {
            if (currentCurriculumMode == CurriculumMode.Alternating20s)
            {
                currentCurriculumMode = CurriculumMode.CombinedSimultaneous;
                CurrentRewardFocus = RewardFocusMode.CombinedSimultaneous;
                PersonalityRewardWeight = 0.50f;
                TaskRewardWeight = 0.50f;
            }
            else
            {
                currentCurriculumMode = CurriculumMode.Alternating20s;
                float timeInCycle = Time.time % cycleDurationSeconds;
                CurrentRewardFocus = (timeInCycle < phaseDurationSeconds) 
                    ? RewardFocusMode.PersonalityExpression 
                    : RewardFocusMode.TaskObjective;
                PersonalityRewardWeight = (CurrentRewardFocus == RewardFocusMode.PersonalityExpression) ? 0.85f : 0.15f;
                TaskRewardWeight = 1.0f - PersonalityRewardWeight;
            }

            OnRewardFocusChanged?.Invoke(CurrentRewardFocus);
            Debug.Log($"[CorridorEnvironment] Curriculum mode toggled to: {currentCurriculumMode} (Focus: {CurrentRewardFocus})");
        }

        public void Awake()
        {
            cycleDurationSeconds = 40f;
            phaseDurationSeconds = 20f;

            if (currentCurriculumMode == CurriculumMode.CombinedSimultaneous)
            {
                CurrentRewardFocus = RewardFocusMode.CombinedSimultaneous;
                PersonalityRewardWeight = 0.50f;
                TaskRewardWeight = 0.50f;
                CycleTimeRemaining = 0f;
            }

            if (GetComponent<CorridorBeautifier>() == null)
            {
                gameObject.AddComponent<CorridorBeautifier>();
            }

            if (energyCell != null && energyCell.transform.position.x < -6.0f)
            {
                defaultCellSpawn = energyCell.transform.position;
            }
            else
            {
                defaultCellSpawn = new Vector3(-13.0f, 0.6f, 0.0f);
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                bool togglePressed = false;
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.cKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame) togglePressed = true;
                }
                else
                {
                    if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.M)) togglePressed = true;
                }

                if (togglePressed)
                {
                    ToggleCurriculumMode();
                }

                if (currentCurriculumMode == CurriculumMode.Alternating20s)
                {
                    UpdateRewardCycle();
                }
                else
                {
                    CurrentRewardFocus = RewardFocusMode.CombinedSimultaneous;
                    PersonalityRewardWeight = 0.50f;
                    TaskRewardWeight = 0.50f;
                    CycleTimeRemaining = 0f;
                }
            }
        }

        private void UpdateRewardCycle()
        {
            float elapsedTime = Time.time;
            float timeInCycle = elapsedTime % cycleDurationSeconds;
            CurrentCycleMinute = Mathf.FloorToInt(elapsedTime / phaseDurationSeconds) + 1;

            RewardFocusMode prevMode = CurrentRewardFocus;
            if (timeInCycle < phaseDurationSeconds)
            {
                CurrentRewardFocus = RewardFocusMode.PersonalityExpression;
                CycleTimeRemaining = phaseDurationSeconds - timeInCycle;
                PersonalityRewardWeight = 0.85f;
                TaskRewardWeight = 0.15f;
            }
            else
            {
                CurrentRewardFocus = RewardFocusMode.TaskObjective;
                CycleTimeRemaining = cycleDurationSeconds - timeInCycle;
                PersonalityRewardWeight = 0.15f;
                TaskRewardWeight = 0.85f;
            }

            if (prevMode != CurrentRewardFocus)
            {
                OnRewardFocusChanged?.Invoke(CurrentRewardFocus);
                Debug.Log($"[CorridorEnvironment] Reward focus switched to: {CurrentRewardFocus} (Minute {CurrentCycleMinute}). PersonalityWeight={PersonalityRewardWeight:0.00}, TaskWeight={TaskRewardWeight:0.00}");
            }
        }

        private void Start()
        {
            if (padA != null) padA.OnPadStateChanged += HandlePadAStateChanged;
            if (padB != null) padB.OnPadStateChanged += HandlePadBCStateChanged;
            if (padC != null) padC.OnPadStateChanged += HandlePadBCStateChanged;
            if (padD != null) padD.OnPadStateChanged += HandlePadDEFStateChanged;
            if (padE != null) padE.OnPadStateChanged += HandlePadDEFStateChanged;
            if (padF != null) padF.OnPadStateChanged += HandlePadDEFStateChanged;
            if (padG != null) padG.OnPadStateChanged += HandlePadGHIStateChanged;
            if (padH != null) padH.OnPadStateChanged += HandlePadGHIStateChanged;
            if (padI != null) padI.OnPadStateChanged += HandlePadGHIStateChanged;

            if (receiverTerminal != null) receiverTerminal.OnCellDelivered += HandleCellDelivered;

            ResetEnvironment();
        }

                public void RegisterLivePolicyAgent(RelayAgent agent)
        {
            livePolicyAgent = agent;
            if (agent != null)
            {
                livePolicySpawnDefault = agent.transform.position;
            }
        }

        public void RegisterBaselineAgent(Transform agentTransform)
        {
            if (!baselineAgents.Contains(agentTransform))
            {
                baselineAgents.Add(agentTransform);
                baselineSpawnDefaults.Add(agentTransform.position);
            }
        }

        public void RegisterLivePolicy(string personaName)
        {
            IsLivePolicyActive = true;
            LivePolicyPersonaName = string.IsNullOrEmpty(personaName) ? "LLM Dynamic Policy" : personaName;
            Debug.Log($"[CorridorEnvironment] Live policy registered: {LivePolicyPersonaName}");
        }

        public void ClearLivePolicy()
        {
            IsLivePolicyActive = false;
            LivePolicyPersonaName = "None";
        }

        private void FixedUpdate()
        {
            if (Application.isPlaying)
            {
                StepEnvironment();
            }
        }

        public void StepEnvironment()
        {
            if (CurrentPhase == PuzzlePhase.Complete)
            {
                if (autoResetOnDelivery)
                {
                    OnEpisodeCompleted?.Invoke();
                    ResetEnvironment();
                    return;
                }
            }

            CurrentStep++;

            // Check plate activations and latch doors permanently open once pressed simultaneously
            if (IsPadAActive) isGate1PermanentlyOpen = true;
            if (IsPadBActive && IsPadCActive) isGate2PermanentlyOpen = true;
            if (IsPadDActive && IsPadEActive && IsPadFActive) isGate3PermanentlyOpen = true;

            // Gate states tracking:
            // Gate 1: Room 1 (Chamber A -> B)
            if (gate1 != null)
            {
                bool targetGate1Open = isGate1PermanentlyOpen || (CurrentPhase >= PuzzlePhase.PadBCPhase) || IsPadAActive;
                if (gate1.IsOpen != targetGate1Open)
                {
                    gate1.SetOpen(targetGate1Open);
                }
            }

            // Gate 2: Room 2 (Chamber B -> C)
            if (gate2 != null)
            {
                bool targetGate2Open = isGate2PermanentlyOpen || (CurrentPhase >= PuzzlePhase.PadC_3PadsPhase) || (IsPadBActive && IsPadCActive);
                if (gate2.IsOpen != targetGate2Open)
                {
                    gate2.SetOpen(targetGate2Open);
                }
            }

            // Gate 3: Room 3 (Chamber C -> D, requires 3 plates: D, E, F)
            if (gate3 != null)
            {
                bool targetGate3Open = isGate3PermanentlyOpen || (CurrentPhase >= PuzzlePhase.DeliveryPhase) || (IsPadDActive && IsPadEActive && IsPadFActive);
                if (gate3.IsOpen != targetGate3Open)
                {
                    gate3.SetOpen(targetGate3Open);
                }
            }

            // Gate 4: Room 4 (Chamber D -> E, requires 3 plates: G, H, I)
            if (gate4 != null)
            {
                bool targetGate4Open = (CurrentPhase >= PuzzlePhase.DeliveryPhase) || (IsPadGActive && IsPadHActive && IsPadIActive);
                if (gate4.IsOpen != targetGate4Open)
                {
                    gate4.SetOpen(targetGate4Open);
                }
            }

            // Reversion guards if plates released before cell transit (disabled if door permanently latched)
            if (!isGate1PermanentlyOpen && CellPosition.x < chamberBLimitX && !IsPadAActive && CurrentPhase == PuzzlePhase.TransitABPhase)
            {
                CurrentPhase = PuzzlePhase.PadAPhase;
            }
            if (!isGate2PermanentlyOpen && CellPosition.x >= chamberBLimitX && CellPosition.x < chamberCLimitX && (!IsPadBActive || !IsPadCActive) && CurrentPhase == PuzzlePhase.TransitBCPhase)
            {
                CurrentPhase = PuzzlePhase.PadBCPhase;
            }
            if (!isGate3PermanentlyOpen && gate3 != null && CellPosition.x >= chamberCLimitX && CellPosition.x < chamberDLimitX && (!IsPadDActive || !IsPadEActive || !IsPadFActive) && CurrentPhase == PuzzlePhase.TransitCDPhase)
            {
                CurrentPhase = PuzzlePhase.PadC_3PadsPhase;
            }


            // Check if any agent has reached the very end (escaped the room)
            CheckAgentEscapes();

            // Hard boundary and partition wall enforcement for all agents
            for (int i = 0; i < baselineAgents.Count; i++)
            {
                if (baselineAgents[i] == null) continue;
                var ag = baselineAgents[i].GetComponent<RelayAgent>();
                if (ag != null) ag.EnforceCorridorBoundaries();
            }
            if (livePolicySlotTransform != null)
            {
                var liveAg = livePolicySlotTransform.GetComponent<RelayAgent>();
                if (liveAg != null) liveAg.EnforceCorridorBoundaries();
            }

            // Evaluate state transitions that depend on cell position
            EvaluatePhaseTransitions();

            if (autoResetOnTimeout && CurrentStep >= maxSteps && CurrentPhase != PuzzlePhase.Complete)
            {
                TimeoutCount++;
                Debug.Log($"[CorridorEnvironment] Episode timeout reached ({maxSteps} steps). Timeouts: {TimeoutCount}");
                OnEpisodeCompleted?.Invoke();
                ResetEnvironment();
            }
        }

        private void CheckAgentEscapes()
        {
            float escapeThresholdX = 23.2f;
            Vector3 recPos = ReceiverPosition;

            for (int i = 0; i < baselineAgents.Count; i++)
            {
                if (baselineAgents[i] == null) continue;
                var ag = baselineAgents[i].GetComponent<RelayAgent>();
                if (ag != null && ag.gameObject.activeSelf && !ag.HasEscaped)
                {
                    if (ag.transform.position.x >= escapeThresholdX || Vector3.Distance(ag.transform.position, recPos) <= 1.8f)
                    {
                        ag.Escape();
                    }
                }
            }

            if (livePolicyAgent != null && livePolicyAgent.gameObject.activeSelf && !livePolicyAgent.HasEscaped)
            {
                if (livePolicyAgent.transform.position.x >= escapeThresholdX || Vector3.Distance(livePolicyAgent.transform.position, recPos) <= 1.8f)
                {
                    livePolicyAgent.Escape();
                }
            }
        }

        public void EvaluatePhaseTransitions()
        {
            PuzzlePhase previous = CurrentPhase;

            switch (CurrentPhase)
            {
                case PuzzlePhase.PadAPhase:
                    if (IsPadAActive || isGate1PermanentlyOpen)
                    {
                        isGate1PermanentlyOpen = true;
                        TransitionPhase(PuzzlePhase.TransitABPhase);
                    }
                    else if (gate1 != null && gate1.IsOpen && !isGate1PermanentlyOpen)
                    {
                        gate1.SetOpen(false);
                    }
                    break;

                case PuzzlePhase.TransitABPhase:
                    if (!isGate1PermanentlyOpen && !IsPadAActive && CellPosition.x < chamberBLimitX)
                    {
                        if (gate1 != null && gate1.IsOpen) gate1.SetOpen(false);
                        CurrentPhase = PuzzlePhase.PadAPhase;
                    }
                    else if (CellPosition.x >= chamberBLimitX)
                    {
                        TransitionPhase(PuzzlePhase.PadBCPhase);
                    }
                    break;

                case PuzzlePhase.PadBCPhase:
                    // Pad B AND Pad C simultaneously active (2 plates)
                    if ((IsPadBActive && IsPadCActive) || isGate2PermanentlyOpen)
                    {
                        isGate2PermanentlyOpen = true;
                        TransitionPhase(PuzzlePhase.TransitBCPhase);
                    }
                    break;

                case PuzzlePhase.TransitBCPhase:
                    // Cell enters Chamber C bounds
                    if (CellPosition.x >= chamberCLimitX)
                    {
                        if (gate3 != null)
                        {
                            TransitionPhase(PuzzlePhase.PadC_3PadsPhase);
                        }
                        else
                        {
                            TransitionPhase(PuzzlePhase.DeliveryPhase);
                        }
                    }
                    break;

                case PuzzlePhase.PadC_3PadsPhase:
                    // Room 3: Requires THREE pressure plates simultaneously (Pad D, Pad E, Pad F)
                    if ((IsPadDActive && IsPadEActive && IsPadFActive) || isGate3PermanentlyOpen)
                    {
                        isGate3PermanentlyOpen = true;
                        TransitionPhase(PuzzlePhase.DeliveryPhase);
                    }
                    break;

                case PuzzlePhase.TransitCDPhase:
                    if (CellPosition.x >= chamberDLimitX)
                    {
                        TransitionPhase(PuzzlePhase.PadD_3PadsPhase);
                    }
                    break;

                case PuzzlePhase.PadD_3PadsPhase:
                    // Room 4: Requires THREE pressure plates simultaneously (Pad G, Pad H, Pad I)
                    if (IsPadGActive && IsPadHActive && IsPadIActive)
                    {
                        TransitionPhase(PuzzlePhase.TransitDEPhase);
                    }
                    break;

                case PuzzlePhase.TransitDEPhase:
                    TransitionPhase(PuzzlePhase.DeliveryPhase);
                    break;

                case PuzzlePhase.DeliveryPhase:
                    if (IsCellDelivered)
                    {
                        TransitionPhase(PuzzlePhase.Complete);
                    }
                    break;

                case PuzzlePhase.Complete:
                    break;
            }
        }

        private void TransitionPhase(PuzzlePhase nextPhase)
        {
            if (CurrentPhase == nextPhase) return;
            PuzzlePhase oldPhase = CurrentPhase;
            CurrentPhase = nextPhase;

            bool immediate = !Application.isPlaying;

            // Apply phase side-effects (gates opening)
            if (gate1 != null)
            {
                bool targetGate1 = isGate1PermanentlyOpen || (CurrentPhase >= PuzzlePhase.PadBCPhase) || IsPadAActive;
                if (gate1.IsOpen != targetGate1) gate1.SetOpen(targetGate1, immediate);
            }

            if (gate2 != null)
            {
                bool targetGate2 = isGate2PermanentlyOpen || (CurrentPhase >= PuzzlePhase.PadC_3PadsPhase) || (IsPadBActive && IsPadCActive);
                if (gate2.IsOpen != targetGate2) gate2.SetOpen(targetGate2, immediate);
            }

            if (gate3 != null)
            {
                bool targetGate3 = isGate3PermanentlyOpen || (CurrentPhase >= PuzzlePhase.DeliveryPhase) || (IsPadDActive && IsPadEActive && IsPadFActive);
                if (gate3.IsOpen != targetGate3) gate3.SetOpen(targetGate3, immediate);
            }

            if (gate4 != null)
            {
                bool targetGate4 = (CurrentPhase >= PuzzlePhase.DeliveryPhase) || (IsPadGActive && IsPadHActive && IsPadIActive);
                if (gate4.IsOpen != targetGate4) gate4.SetOpen(targetGate4, immediate);
            }

            Debug.Log($"[PhaseTransition] Step {CurrentStep}: {oldPhase} -> {CurrentPhase}");
            OnPhaseChanged?.Invoke(oldPhase, CurrentPhase);

            if (CurrentPhase == PuzzlePhase.Complete)
            {
                DeliveryCount++;
                OnEpisodeCompleted?.Invoke();
            }
        }

        private void HandlePadAStateChanged(RelayPad pad, bool active)
        {
            bool immediate = !Application.isPlaying;
            if (active)
            {
                isGate1PermanentlyOpen = true;
                if (gate1 != null && !gate1.IsOpen)
                {
                    gate1.SetOpen(true, immediate);
                }
                if (CurrentPhase == PuzzlePhase.PadAPhase)
                {
                    TransitionPhase(PuzzlePhase.TransitABPhase);
                }
            }
            else
            {
                // Once pressed, Gate 1 remains open forever! Do not close.
                if (!isGate1PermanentlyOpen && CurrentPhase < PuzzlePhase.PadBCPhase)
                {
                    if (gate1 != null && gate1.IsOpen) gate1.SetOpen(false, immediate);
                    if (CellPosition.x < chamberBLimitX && CurrentPhase == PuzzlePhase.TransitABPhase)
                    {
                        CurrentPhase = PuzzlePhase.PadAPhase;
                    }
                }
            }
        }

        private void HandlePadBCStateChanged(RelayPad pad, bool active)
        {
            if (IsPadBActive && IsPadCActive)
            {
                isGate2PermanentlyOpen = true;
                if (gate2 != null && !gate2.IsOpen)
                {
                    gate2.SetOpen(true, !Application.isPlaying);
                }
                if (CurrentPhase == PuzzlePhase.PadBCPhase)
                {
                    TransitionPhase(PuzzlePhase.TransitBCPhase);
                }
            }
        }

        private void HandlePadDEFStateChanged(RelayPad pad, bool active)
        {
            if (IsPadDActive && IsPadEActive && IsPadFActive)
            {
                isGate3PermanentlyOpen = true;
                if (gate3 != null && !gate3.IsOpen)
                {
                    gate3.SetOpen(true, !Application.isPlaying);
                }
                if (CurrentPhase == PuzzlePhase.PadC_3PadsPhase)
                {
                    TransitionPhase(PuzzlePhase.DeliveryPhase);
                }
            }
        }

        private void HandlePadGHIStateChanged(RelayPad pad, bool active)
        {
            if (CurrentPhase == PuzzlePhase.PadD_3PadsPhase && IsPadGActive && IsPadHActive && IsPadIActive)
            {
                TransitionPhase(PuzzlePhase.TransitDEPhase);
            }
        }

        private void HandleCellDelivered()
        {
            TransitionPhase(PuzzlePhase.Complete);
        }

        public void ResetEnvironment()
        {
            CurrentStep = 0;
            EpisodeCount++;

            // Reset phase
            CurrentPhase = PuzzlePhase.PadAPhase;

            // Reset pads
            if (padA != null) padA.ResetPad();
            if (padB != null) padB.ResetPad();
            if (padC != null) padC.ResetPad();
            if (padD != null) padD.ResetPad();
            if (padE != null) padE.ResetPad();
            if (padF != null) padF.ResetPad();
            if (padG != null) padG.ResetPad();
            if (padH != null) padH.ResetPad();
            if (padI != null) padI.ResetPad();

            // Reset gates and latching flags
            isGate1PermanentlyOpen = false;
            isGate2PermanentlyOpen = false;
            isGate3PermanentlyOpen = false;
            if (gate1 != null) gate1.ResetGate();
            if (gate2 != null) gate2.ResetGate();
            if (gate3 != null) gate3.ResetGate();
            if (gate4 != null) gate4.ResetGate();

            // Reset receiver terminal
            if (receiverTerminal != null) receiverTerminal.ResetTerminal();

            // Reset cell with randomization +-0.5 units in Chamber A
            if (defaultCellSpawn == Vector3.zero || defaultCellSpawn.x > -6.0f)
            {
                if (energyCell != null && energyCell.transform.position.x < -6.0f)
                {
                    defaultCellSpawn = energyCell.transform.position;
                }
                else
                {
                    defaultCellSpawn = new Vector3(-13.0f, 0.6f, 0.0f);
                }
            }
            Vector3 randomizedCell = defaultCellSpawn + new Vector3(
                UnityEngine.Random.Range(-0.5f, 0.5f), 0f, UnityEngine.Random.Range(-0.5f, 0.5f));
            if (energyCell != null)
            {
                energyCell.ResetCell(randomizedCell);
            }

            // Reset baseline agents with randomization +-1.5 units in Chamber A
            for (int i = 0; i < baselineAgents.Count; i++)
            {
                if (baselineAgents[i] == null) continue;
                if (!baselineAgents[i].gameObject.activeSelf)
                {
                    baselineAgents[i].gameObject.SetActive(true);
                }
                Vector3 basePos = i < baselineSpawnDefaults.Count ? baselineSpawnDefaults[i] : baselineAgents[i].position;
                Vector3 randomizedPos = basePos + new Vector3(
                    UnityEngine.Random.Range(-1.5f, 1.5f), 0f, UnityEngine.Random.Range(-1.5f, 1.5f));
                randomizedPos.x = Mathf.Clamp(randomizedPos.x, -14.5f, -6.0f);
                randomizedPos.z = Mathf.Clamp(randomizedPos.z, -5.5f, 5.5f);
                baselineAgents[i].position = randomizedPos;

                var rb = baselineAgents[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                var ag = baselineAgents[i].GetComponent<RelayAgent>();
                if (ag != null) ag.OnEpisodeBegin();
            }

            // Reset LLM-mediated agent (Slot 5) back to Chamber A platform!
            if (livePolicyAgent == null)
            {
                var allAgents = FindObjectsByType<RelayAgent>(FindObjectsSortMode.None);
                foreach (var a in allAgents)
                {
                    if (a.isLivePolicySlot)
                    {
                        livePolicyAgent = a;
                        break;
                    }
                }
            }

            if (livePolicyAgent != null)
            {
                if (!livePolicyAgent.gameObject.activeSelf)
                {
                    livePolicyAgent.gameObject.SetActive(true);
                }
                livePolicyAgent.HasEscaped = false;
                Vector3 basePos = livePolicySpawnDefault != Vector3.zero ? livePolicySpawnDefault : new Vector3(-13.0f, 1.0f, 4.0f);
                Vector3 randomizedPos = basePos + new Vector3(
                    UnityEngine.Random.Range(-0.5f, 0.5f), 0f, UnityEngine.Random.Range(-0.5f, 0.5f));
                randomizedPos.x = Mathf.Clamp(randomizedPos.x, -14.5f, -6.0f);
                randomizedPos.z = Mathf.Clamp(randomizedPos.z, -5.5f, 5.5f);
                livePolicyAgent.transform.position = randomizedPos;

                var rb = livePolicyAgent.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                livePolicyAgent.OnEpisodeBegin();
            }
        }

        public Vector3 GetNearestUnactivatedPad(Vector3 fromPosition)
        {
            if (CurrentPhase == PuzzlePhase.PadAPhase || CurrentPhase == PuzzlePhase.TransitABPhase)
            {
                return PadAPosition;
            }

            if (CurrentPhase == PuzzlePhase.PadBCPhase || CurrentPhase == PuzzlePhase.TransitBCPhase)
            {
                bool bActive = IsPadBActive;
                bool cActive = IsPadCActive;
                if (!bActive && !cActive)
                {
                    float distB = Vector3.Distance(fromPosition, PadBPosition);
                    float distC = Vector3.Distance(fromPosition, PadCPosition);
                    return distB <= distC ? PadBPosition : PadCPosition;
                }
                if (!bActive) return PadBPosition;
                if (!cActive) return PadCPosition;
            }

            if (CurrentPhase == PuzzlePhase.PadC_3PadsPhase || CurrentPhase == PuzzlePhase.TransitCDPhase)
            {
                List<Vector3> unactivated = new List<Vector3>();
                if (!IsPadDActive) unactivated.Add(PadDPosition);
                if (!IsPadEActive) unactivated.Add(PadEPosition);
                if (!IsPadFActive) unactivated.Add(PadFPosition);

                if (unactivated.Count > 0)
                {
                    Vector3 closest = unactivated[0];
                    float minDist = Vector3.Distance(fromPosition, closest);
                    for (int i = 1; i < unactivated.Count; i++)
                    {
                        float d = Vector3.Distance(fromPosition, unactivated[i]);
                        if (d < minDist) { minDist = d; closest = unactivated[i]; }
                    }
                    return closest;
                }
            }

            if (CurrentPhase == PuzzlePhase.PadD_3PadsPhase || CurrentPhase == PuzzlePhase.TransitDEPhase)
            {
                List<Vector3> unactivated = new List<Vector3>();
                if (!IsPadGActive) unactivated.Add(PadGPosition);
                if (!IsPadHActive) unactivated.Add(PadHPosition);
                if (!IsPadIActive) unactivated.Add(PadIPosition);

                if (unactivated.Count > 0)
                {
                    Vector3 closest = unactivated[0];
                    float minDist = Vector3.Distance(fromPosition, closest);
                    for (int i = 1; i < unactivated.Count; i++)
                    {
                        float d = Vector3.Distance(fromPosition, unactivated[i]);
                        if (d < minDist) { minDist = d; closest = unactivated[i]; }
                    }
                    return closest;
                }
            }

            return ReceiverPosition;
        }

        // Scripted transition helpers for unit testing
        public void ScriptedForcePadA(bool active) { if (padA != null) padA.ForceSetOccupied(active); }
        public void ScriptedForcePadB(bool active) { if (padB != null) padB.ForceSetOccupied(active); }
        public void ScriptedForcePadC(bool active) { if (padC != null) padC.ForceSetOccupied(active); }
        public void ScriptedForcePadD(bool active) { if (padD != null) padD.ForceSetOccupied(active); }
        public void ScriptedForcePadE(bool active) { if (padE != null) padE.ForceSetOccupied(active); }
        public void ScriptedForcePadF(bool active) { if (padF != null) padF.ForceSetOccupied(active); }
        public void ScriptedForcePadG(bool active) { if (padG != null) padG.ForceSetOccupied(active); }
        public void ScriptedForcePadH(bool active) { if (padH != null) padH.ForceSetOccupied(active); }
        public void ScriptedForcePadI(bool active) { if (padI != null) padI.ForceSetOccupied(active); }

        public void ScriptedSetCellPosition(Vector3 pos)
        {
            if (energyCell != null)
            {
                energyCell.transform.position = pos;
            }
            EvaluatePhaseTransitions();
        }

        public void ScriptedDeliverCell()
        {
            if (receiverTerminal != null && energyCell != null)
            {
                receiverTerminal.DeliverCell(energyCell);
                EvaluatePhaseTransitions();
            }
        }
    }
}
