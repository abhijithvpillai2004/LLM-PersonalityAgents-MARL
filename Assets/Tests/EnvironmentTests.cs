using NUnit.Framework;
using UnityEngine;

namespace RelayCorridor.Tests
{
    public class EnvironmentTests
    {
        private GameObject envGo;
        private CorridorEnvironment env;
        private RelayPad padA;
        private RelayPad padB;
        private RelayPad padC;
        private SlidingGate gate1;
        private SlidingGate gate2;
        private EnergyCell cell;
        private ReceiverTerminal receiver;
        private GameObject agent1Go;
        private GameObject agent2Go;

        [SetUp]
        public void SetUp()
        {
            envGo = new GameObject("TestEnvironment");
            env = envGo.AddComponent<CorridorEnvironment>();

            // Pads
            padA = CreateTestPad("PadA", new Vector3(-7.5f, 0f, -3f));
            padB = CreateTestPad("PadB", new Vector3(0f, 0f, -3.5f));
            padC = CreateTestPad("PadC", new Vector3(0f, 0f, 3.5f));

            // Gates
            gate1 = CreateTestGate("Gate1", new Vector3(-5f, 2f, 0f));
            gate2 = CreateTestGate("Gate2", new Vector3(5f, 2f, 0f));

            // Energy Cell
            GameObject cellGo = new GameObject("TestCell");
            cellGo.transform.SetParent(envGo.transform);
            cellGo.transform.position = new Vector3(-13f, 0.6f, 0f);
            cellGo.AddComponent<Rigidbody>();
            cellGo.AddComponent<SphereCollider>();
            cell = cellGo.AddComponent<EnergyCell>();
            cell.EnsureComponents();
            cell.InitializeSpawn(cellGo.transform.position);

            // Receiver
            GameObject recGo = new GameObject("TestReceiver");
            recGo.transform.SetParent(envGo.transform);
            recGo.transform.position = new Vector3(14f, 1f, 0f);
            recGo.AddComponent<BoxCollider>().isTrigger = true;
            receiver = recGo.AddComponent<ReceiverTerminal>();

            // Agents
            agent1Go = new GameObject("Agent1");
            agent1Go.AddComponent<CapsuleCollider>();
            agent2Go = new GameObject("Agent2");
            agent2Go.AddComponent<CapsuleCollider>();

            // Wire references
            env.InitializeEnvironmentReferences(padA, padB, padC, gate1, gate2, cell, receiver);

            // Subscribe event listeners and initialize
            typeof(CorridorEnvironment).GetMethod("Start",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(env, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (envGo != null) Object.DestroyImmediate(envGo);
            if (agent1Go != null) Object.DestroyImmediate(agent1Go);
            if (agent2Go != null) Object.DestroyImmediate(agent2Go);
        }

        private RelayPad CreateTestPad(string id, Vector3 pos)
        {
            GameObject go = new GameObject(id);
            go.transform.SetParent(envGo.transform);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().isTrigger = true;
            var pad = go.AddComponent<RelayPad>();
            SetPrivateField(pad, "padId", id);
            return pad;
        }

        private SlidingGate CreateTestGate(string id, Vector3 pos)
        {
            GameObject go = new GameObject(id);
            go.transform.SetParent(envGo.transform);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>();
            var gate = go.AddComponent<SlidingGate>();
            SetPrivateField(gate, "gateId", id);
            SetPrivateField(gate, "openHeight", 4f);
            SetPrivateField(gate, "animationDuration", 0.01f);
            gate.EnsureInitialized();
            return gate;
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null) field.SetValue(target, value);
        }

        [Test]
        public void Gate1_OpensOnPadAActivation_RemainsOpenWhenDeactivated()
        {
            Assert.IsFalse(env.IsGate1Open, "Gate 1 should initially be closed.");

            // Activate Pad A
            env.ScriptedForcePadA(true);
            Assert.IsTrue(env.IsPadAActive, "Pad A should be active.");
            Assert.IsTrue(env.IsGate1Open, "Gate 1 should open when Pad A is activated.");

            // Deactivate Pad A - Gate 1 must REMAIN OPEN forever!
            env.ScriptedForcePadA(false);
            Assert.IsFalse(env.IsPadAActive, "Pad A should be inactive.");
            Assert.IsTrue(env.IsGate1Open, "Gate 1 must remain open forever once activated.");

            // Reset environment - Gate 1 closes
            env.ResetEnvironment();
            Assert.IsFalse(env.IsGate1Open, "Gate 1 should reset to closed on episode reset.");
        }

        [Test]
        public void Gate2_OpensOnlyWhenBothPadBAndCSimultaneouslyActive()
        {
            // First progress through Chamber A
            env.ScriptedForcePadA(true);
            env.ScriptedSetCellPosition(new Vector3(-4f, 0.6f, 0f)); // Chamber B bounds
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.PadBCPhase, env.CurrentPhase);

            Assert.IsFalse(env.IsGate2Open, "Gate 2 should be closed initially in PadBCPhase.");

            // Activate only Pad B
            env.ScriptedForcePadB(true);
            Assert.IsFalse(env.IsGate2Open, "Gate 2 must NOT open when only Pad B is active.");

            // Deactivate Pad B, activate only Pad C
            env.ScriptedForcePadB(false);
            env.ScriptedForcePadC(true);
            Assert.IsFalse(env.IsGate2Open, "Gate 2 must NOT open when only Pad C is active.");

            // Activate both Pad B AND Pad C simultaneously
            env.ScriptedForcePadB(true);
            Assert.IsTrue(env.IsGate2Open, "Gate 2 must open when both Pad B and C are simultaneously active.");
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.TransitBCPhase, env.CurrentPhase);
        }

        [Test]
        public void Cell_PickupDropAndPassBetweenAgents()
        {
            Assert.IsFalse(cell.IsCarried, "Cell should start uncarried.");

            // Agent 1 pickup
            bool picked1 = cell.Pickup(agent1Go.transform, "Agent1");
            Assert.IsTrue(picked1, "Agent 1 should successfully pick up the cell.");
            Assert.IsTrue(cell.IsCarried, "Cell state should be carried.");
            Assert.AreEqual("Agent1", cell.CarryingAgentId);
            Assert.AreEqual(agent1Go.transform, cell.CarryingAgent);

            // Agent 1 drop
            Vector3 dropPos = new Vector3(-6f, 0.4f, 0f);
            cell.Drop(dropPos);
            Assert.IsFalse(cell.IsCarried, "Cell should be uncarried after drop.");
            Assert.IsNull(cell.CarryingAgentId);

            // Agent 2 pickup (pass)
            bool picked2 = cell.Pickup(agent2Go.transform, "Agent2");
            Assert.IsTrue(picked2, "Agent 2 should successfully pick up the dropped cell (pass).");
            Assert.IsTrue(cell.IsCarried, "Cell state should be carried by Agent 2.");
            Assert.AreEqual("Agent2", cell.CarryingAgentId);
            Assert.AreEqual(agent2Go.transform, cell.CarryingAgent);
        }

        [Test]
        public void CellDelivery_SetsIsCellDeliveredTrue()
        {
            // Progress to delivery phase
            env.ScriptedForcePadA(true);
            env.ScriptedSetCellPosition(new Vector3(-4f, 0.6f, 0f));
            env.ScriptedForcePadB(true);
            env.ScriptedForcePadC(true);
            env.ScriptedSetCellPosition(new Vector3(6f, 0.6f, 0f)); // Chamber C bounds
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.DeliveryPhase, env.CurrentPhase);

            Assert.IsFalse(env.IsCellDelivered, "Cell should not be delivered yet.");

            // Deliver
            env.ScriptedDeliverCell();
            Assert.IsTrue(env.IsCellDelivered, "IsCellDelivered must be true after delivery.");
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.Complete, env.CurrentPhase, "Phase must become Complete.");
        }

        [Test]
        public void EpisodeReset_ClearsAllStateExceptLivePolicySlot()
        {
            // Register live policy
            env.RegisterLivePolicy("Reckless Charger");
            Assert.IsTrue(env.IsLivePolicyActive);

            // Modify states
            env.ScriptedForcePadA(true);
            env.ScriptedSetCellPosition(new Vector3(-4f, 0.6f, 0f));
            env.ScriptedForcePadB(true);
            env.ScriptedForcePadC(true);

            // Reset environment
            env.ResetEnvironment();

            // Verify clean reset
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.PadAPhase, env.CurrentPhase, "Phase must reset to PadAPhase.");
            Assert.IsFalse(env.IsPadAActive, "Pad A must be reset to inactive.");
            Assert.IsFalse(env.IsPadBActive, "Pad B must be reset to inactive.");
            Assert.IsFalse(env.IsPadCActive, "Pad C must be reset to inactive.");
            Assert.IsFalse(env.IsGate1Open, "Gate 1 must be closed.");
            Assert.IsFalse(env.IsGate2Open, "Gate 2 must be closed.");
            Assert.IsFalse(env.IsCellDelivered, "IsCellDelivered must be false.");

            // Verify live policy slot persists across reset
            Assert.IsTrue(env.IsLivePolicyActive, "Live policy slot MUST persist across episode resets.");
            Assert.AreEqual("Reckless Charger", env.LivePolicyPersonaName, "Persona name must persist across reset.");
        }

        [Test]
        public void PhaseTransitions_OccurInCorrectOrderUnderScriptedInput()
        {
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.PadAPhase, env.CurrentPhase);

            // 1. Pad A activated -> TransitABPhase
            env.ScriptedForcePadA(true);
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.TransitABPhase, env.CurrentPhase);

            // 2. Cell reaches Chamber B -> PadBCPhase
            env.ScriptedSetCellPosition(new Vector3(-4f, 0.6f, 0f));
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.PadBCPhase, env.CurrentPhase);

            // 3. Both Pad B & C active -> TransitBCPhase
            env.ScriptedForcePadB(true);
            env.ScriptedForcePadC(true);
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.TransitBCPhase, env.CurrentPhase);

            // 4. Cell reaches Chamber C -> DeliveryPhase
            env.ScriptedSetCellPosition(new Vector3(6f, 0.6f, 0f));
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.DeliveryPhase, env.CurrentPhase);

            // 5. Delivery -> Complete
            env.ScriptedDeliverCell();
            Assert.AreEqual(CorridorEnvironment.PuzzlePhase.Complete, env.CurrentPhase);
        }

        [Test]
        public void LivePolicySlot_StartsInactive_BecomesActiveOnRegistration()
        {
            Assert.IsFalse(env.IsLivePolicyActive, "Live policy slot should initially be inactive.");
            Assert.AreEqual("None", env.LivePolicyPersonaName);

            env.RegisterLivePolicy("Cautious Helper");
            Assert.IsTrue(env.IsLivePolicyActive, "Live policy slot should become active after RegisterLivePolicy.");
            Assert.AreEqual("Cautious Helper", env.LivePolicyPersonaName);
        }
    }
}
