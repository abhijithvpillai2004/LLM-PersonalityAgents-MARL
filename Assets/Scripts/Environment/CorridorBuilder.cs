using TMPro;
using UnityEngine;

namespace RelayCorridor
{
    public class CorridorBuilder : MonoBehaviour
    {
        [Header("Corridor Dimensions")]
        [SerializeField] private float corridorLength = 40.0f;
        [SerializeField] private float corridorWidth = 12.0f;
        [SerializeField] private float corridorHeight = 4.0f;
        [SerializeField] private float wallThickness = 0.5f;

        [Header("Colors & Styling")]
        [SerializeField] private Color floorColor = new Color(0.2f, 0.22f, 0.24f);
        [SerializeField] private Color wallColor = new Color(0.1f, 0.11f, 0.12f);
        [SerializeField] private Color ceilingColor = new Color(0.85f, 0.85f, 0.88f);
        [SerializeField] private Color gateColor = new Color(0.3f, 0.35f, 0.4f);

        [Header("Interactive Positions")]
        [SerializeField] private Vector3 padAPosition = new Vector3(-7.5f, 0.05f, -3.0f);
        [SerializeField] private Vector3 padBPosition = new Vector3(0.0f, 0.05f, -3.5f);
        [SerializeField] private Vector3 padCPosition = new Vector3(0.0f, 0.05f, 3.5f);

        // Room 3 (3 plates)
        [SerializeField] private Vector3 padDPosition = new Vector3(10.0f, 0.05f, -3.8f);
        [SerializeField] private Vector3 padEPosition = new Vector3(8.5f, 0.05f, 0.0f);
        [SerializeField] private Vector3 padFPosition = new Vector3(10.0f, 0.05f, 3.8f);

        // Room 4 (3 plates)
        [SerializeField] private Vector3 padGPosition = new Vector3(20.0f, 0.05f, -3.8f);
        [SerializeField] private Vector3 padHPosition = new Vector3(18.5f, 0.05f, 0.0f);
        [SerializeField] private Vector3 padIPosition = new Vector3(20.0f, 0.05f, 3.8f);

        [SerializeField] private Vector3 cellSpawnPosition = new Vector3(-13.0f, 0.6f, 0.0f);
        [SerializeField] private Vector3 receiverPosition = new Vector3(24.0f, 1.0f, 0.0f);
        [SerializeField] private Vector3 liveSlotPosition = new Vector3(-13.0f, 0.05f, 4.0f);

        [Header("Build Targets")]
        [SerializeField] private Transform environmentRoot;

        public CorridorEnvironment BuiltEnvironment { get; private set; }

        [ContextMenu("Build Environment From Scratch")]
        public CorridorEnvironment BuildEnvironment()
        {
            if (environmentRoot == null)
            {
                GameObject rootGo = GameObject.Find("Environment");
                if (rootGo != null) DestroyImmediate(rootGo);
                rootGo = new GameObject("Environment");
                environmentRoot = rootGo.transform;
            }
            else
            {
                for (int i = environmentRoot.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(environmentRoot.GetChild(i).gameObject);
                }
            }

            // Groups
            Transform geomGroup = CreateGroup("Geometry", environmentRoot);
            Transform gatesGroup = CreateGroup("Gates", environmentRoot);
            Transform padsGroup = CreateGroup("Pads", environmentRoot);
            Transform cellGroup = CreateGroup("Cell", environmentRoot);
            Transform receiverGroup = CreateGroup("Receiver", environmentRoot);
            Transform slotGroup = CreateGroup("LivePolicySlot", environmentRoot);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");

            float halfWidth = corridorWidth * 0.5f;
            float corridorCenterX = 5.0f; // Center of [-15, 35]

            // 1. Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(geomGroup);
            floor.transform.position = new Vector3(corridorCenterX, -0.25f, 0f);
            floor.transform.localScale = new Vector3(corridorLength, 0.5f, corridorWidth);
            SetMaterialColor(floor, litShader, floorColor);

            // 2. Ceiling
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(geomGroup);
            ceiling.transform.position = new Vector3(corridorCenterX, corridorHeight + 0.25f, 0f);
            ceiling.transform.localScale = new Vector3(corridorLength, 0.5f, corridorWidth);
            SetMaterialColor(ceiling, litShader, ceilingColor);
            ceiling.GetComponent<MeshRenderer>().enabled = false;

            // 3. Side Walls (North & South) with thickened colliders to prevent phasing
            GameObject wallNorth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallNorth.name = "Wall_North";
            wallNorth.transform.SetParent(geomGroup);
            wallNorth.transform.position = new Vector3(corridorCenterX, corridorHeight * 0.5f, halfWidth + wallThickness * 0.5f);
            wallNorth.transform.localScale = new Vector3(corridorLength, corridorHeight, wallThickness);
            var colN = wallNorth.GetComponent<BoxCollider>();
            if (colN != null) { colN.size = new Vector3(1.0f, 1.0f, 3.0f / wallThickness); colN.center = new Vector3(0f, 0f, 1.25f / wallThickness); }
            SetMaterialColor(wallNorth, litShader, wallColor);

            GameObject wallSouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallSouth.name = "Wall_South";
            wallSouth.transform.SetParent(geomGroup);
            wallSouth.transform.position = new Vector3(corridorCenterX, corridorHeight * 0.5f, -halfWidth - wallThickness * 0.5f);
            wallSouth.transform.localScale = new Vector3(corridorLength, corridorHeight, wallThickness);
            var colS = wallSouth.GetComponent<BoxCollider>();
            if (colS != null) { colS.size = new Vector3(1.0f, 1.0f, 3.0f / wallThickness); colN.center = new Vector3(0f, 0f, -1.25f / wallThickness); }
            SetMaterialColor(wallSouth, litShader, wallColor);

            // 4. End Walls (West at -15.25 & East at +35.25)
            GameObject wallWest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallWest.name = "Wall_West";
            wallWest.transform.SetParent(geomGroup);
            wallWest.transform.position = new Vector3(-15.0f - wallThickness * 0.5f, corridorHeight * 0.5f, 0f);
            wallWest.transform.localScale = new Vector3(wallThickness, corridorHeight, corridorWidth + wallThickness * 2f);
            var colW = wallWest.GetComponent<BoxCollider>();
            if (colW != null) { colW.size = new Vector3(3.0f / wallThickness, 1.0f, 1.0f); colW.center = new Vector3(-1.25f / wallThickness, 0f, 0f); }
            SetMaterialColor(wallWest, litShader, wallColor);

            GameObject wallEast = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallEast.name = "Wall_East";
            wallEast.transform.SetParent(geomGroup);
            wallEast.transform.position = new Vector3(25.0f + wallThickness * 0.5f, corridorHeight * 0.5f, 0f);
            wallEast.transform.localScale = new Vector3(wallThickness, corridorHeight, corridorWidth + wallThickness * 2f);
            var colE = wallEast.GetComponent<BoxCollider>();
            if (colE != null) { colE.size = new Vector3(3.0f / wallThickness, 1.0f, 1.0f); colE.center = new Vector3(1.25f / wallThickness, 0f, 0f); }
            SetMaterialColor(wallEast, litShader, wallColor);

            // 5. Partition walls & Gates for 3 thresholds (Room 1->2, Room 2->3, Room 3->4)
            SlidingGate gate1 = CreateGate("Gate1", new Vector3(-5.0f, 2.0f, 0.0f), gatesGroup, litShader);
            SlidingGate gate2 = CreateGate("Gate2", new Vector3(5.0f, 2.0f, 0.0f), gatesGroup, litShader);
            SlidingGate gate3 = CreateGate("Gate3", new Vector3(15.0f, 2.0f, 0.0f), gatesGroup, litShader);

            CreateDoorwayPartitions(-5.0f, geomGroup, litShader);
            CreateDoorwayPartitions(5.0f, geomGroup, litShader);
            CreateDoorwayPartitions(15.0f, geomGroup, litShader);

            // 6. Relay Pads
            // Room 1 (1 plate)
            RelayPad padA = CreatePad("RelayPad_A", padAPosition, new Color(0.1f, 0.5f, 1.0f), padsGroup, litShader);
            // Room 2 (2 plates)
            RelayPad padB = CreatePad("RelayPad_B", padBPosition, new Color(1.0f, 0.65f, 0.1f), padsGroup, litShader);
            RelayPad padC = CreatePad("RelayPad_C", padCPosition, new Color(1.0f, 0.65f, 0.1f), padsGroup, litShader);
            // Room 3 (3 plates: Pad D, Pad E, Pad F)
            RelayPad padD = CreatePad("RelayPad_D", padDPosition, new Color(0.75f, 0.25f, 1.0f), padsGroup, litShader);
            RelayPad padE = CreatePad("RelayPad_E", padEPosition, new Color(0.85f, 0.35f, 1.0f), padsGroup, litShader);
            RelayPad padF = CreatePad("RelayPad_F", padFPosition, new Color(0.75f, 0.25f, 1.0f), padsGroup, litShader);
            RelayPad padG = null;
            RelayPad padH = null;
            RelayPad padI = null;
            SlidingGate gate4 = null;

            // 7. Energy Cell in Chamber A
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "CellPedestal";
            pedestal.transform.SetParent(cellGroup);
            pedestal.transform.position = new Vector3(cellSpawnPosition.x, 0.2f, cellSpawnPosition.z);
            pedestal.transform.localScale = new Vector3(1.2f, 0.2f, 1.2f);
            SetMaterialColor(pedestal, litShader, new Color(0.25f, 0.25f, 0.3f));

            GameObject cellGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cellGo.name = "EnergyCell";
            cellGo.transform.SetParent(cellGroup);
            cellGo.transform.position = cellSpawnPosition;
            cellGo.transform.localScale = Vector3.one * 0.8f;
            SetMaterialColor(cellGo, litShader, new Color(0.2f, 1.0f, 0.9f), emissive: true, emissionMult: 3.5f);
            EnergyCell energyCell = cellGo.AddComponent<EnergyCell>();
            energyCell.InitializeSpawn(cellSpawnPosition);

            // 8. Receiver Terminal in Chamber E (Last Room)
            GameObject receiverGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            receiverGo.name = "ReceiverTerminal";
            receiverGo.transform.SetParent(receiverGroup);
            receiverGo.transform.position = receiverPosition;
            receiverGo.transform.localScale = new Vector3(1.0f, 2.0f, 3.0f);
            var recCol = receiverGo.GetComponent<BoxCollider>();
            recCol.isTrigger = true;
            SetMaterialColor(receiverGo, litShader, new Color(0.2f, 0.9f, 0.4f), emissive: true, emissionMult: 2.0f);
            ReceiverTerminal receiverTerminal = receiverGo.AddComponent<ReceiverTerminal>();

            // 9. Live Policy Slot Platform
            GameObject slotPlatform = CreateLivePolicySlot(slotGroup, litShader);

            // 10. Wire Environment controller on root
            CorridorEnvironment env = environmentRoot.gameObject.GetComponent<CorridorEnvironment>();
            if (env == null) env = environmentRoot.gameObject.AddComponent<CorridorEnvironment>();

            SetPrivateField(env, "padA", padA);
            SetPrivateField(env, "padB", padB);
            SetPrivateField(env, "padC", padC);
            SetPrivateField(env, "padD", padD);
            SetPrivateField(env, "padE", padE);
            SetPrivateField(env, "padF", padF);
            SetPrivateField(env, "padG", padG);
            SetPrivateField(env, "padH", padH);
            SetPrivateField(env, "padI", padI);

            SetPrivateField(env, "gate1", gate1);
            SetPrivateField(env, "gate2", gate2);
            SetPrivateField(env, "gate3", gate3);
            SetPrivateField(env, "gate4", gate4);

            SetPrivateField(env, "energyCell", energyCell);
            SetPrivateField(env, "receiverTerminal", receiverTerminal);
            SetPrivateField(env, "livePolicySlotTransform", slotPlatform.transform);

            BuiltEnvironment = env;
            Debug.Log("[CorridorBuilder] Doubled 5-Chamber Relay Corridor successfully built!");
            return env;
        }

        private SlidingGate CreateGate(string gateId, Vector3 position, Transform parent, Shader shader)
        {
            GameObject gateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateGo.name = gateId;
            gateGo.transform.SetParent(parent);
            gateGo.transform.position = position;
            gateGo.transform.localScale = new Vector3(0.4f, 4.0f, 5.0f);
            var col = gateGo.GetComponent<BoxCollider>();
            if (col != null) { col.size = Vector3.one; col.center = Vector3.zero; }
            SetMaterialColor(gateGo, shader, gateColor, emissive: true, emissionMult: 0.5f);
            SlidingGate gate = gateGo.AddComponent<SlidingGate>();
            SetPrivateField(gate, "gateId", gateId);
            SetPrivateField(gate, "openHeight", 4.0f);
            SetPrivateField(gate, "animationDuration", 1.2f);
            return gate;
        }

        private void CreateDoorwayPartitions(float xPos, Transform parent, Shader shader)
        {
            float sideWidth = (corridorWidth - 5.0f) * 0.5f;
            float posZNorth = 2.5f + sideWidth * 0.5f;
            float posZSouth = -2.5f - sideWidth * 0.5f;

            GameObject partNorth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            partNorth.name = $"Partition_{xPos}_North";
            partNorth.transform.SetParent(parent);
            partNorth.transform.position = new Vector3(xPos, corridorHeight * 0.5f, posZNorth);
            partNorth.transform.localScale = new Vector3(wallThickness, corridorHeight, sideWidth);
            var colN = partNorth.GetComponent<BoxCollider>();
            if (colN != null) { colN.size = Vector3.one; colN.center = Vector3.zero; }
            SetMaterialColor(partNorth, shader, wallColor);

            GameObject partSouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            partSouth.name = $"Partition_{xPos}_South";
            partSouth.transform.SetParent(parent);
            partSouth.transform.position = new Vector3(xPos, corridorHeight * 0.5f, posZSouth);
            partSouth.transform.localScale = new Vector3(wallThickness, corridorHeight, sideWidth);
            var colS = partSouth.GetComponent<BoxCollider>();
            if (colS != null) { colS.size = Vector3.one; colS.center = Vector3.zero; }
            SetMaterialColor(partSouth, shader, wallColor);
        }

        private RelayPad CreatePad(string name, Vector3 position, Color accentColor, Transform parent, Shader shader)
        {
            GameObject padGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padGo.name = name;
            padGo.transform.SetParent(parent);
            padGo.transform.position = position;
            padGo.transform.localScale = new Vector3(2.5f, 0.05f, 2.5f);
            var col = padGo.GetComponent<CapsuleCollider>();
            col.isTrigger = true;

            SetMaterialColor(padGo, shader, accentColor, emissive: true, emissionMult: 1.0f);
            RelayPad pad = padGo.AddComponent<RelayPad>();
            SetPrivateField(pad, "padId", name);
            SetPrivateField(pad, "baseColor", accentColor * 0.6f);
            SetPrivateField(pad, "activeColor", accentColor);
            SetPrivateField(pad, "baseEmission", 1.0f);
            SetPrivateField(pad, "activeEmission", 3.5f);
            return pad;
        }

        private GameObject CreateLivePolicySlot(Transform parent, Shader shader)
        {
            GameObject slotGo = new GameObject("LivePolicySlotPlatform");
            slotGo.transform.SetParent(parent);
            slotGo.transform.position = liveSlotPosition;

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "PlatformBase";
            platform.transform.SetParent(slotGo.transform);
            platform.transform.localPosition = Vector3.zero;
            platform.transform.localScale = new Vector3(2.8f, 0.05f, 2.8f);
            SetMaterialColor(platform, shader, new Color(0.15f, 0.18f, 0.22f));

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PulsingRing";
            ring.transform.SetParent(slotGo.transform);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(2.6f, 0.03f, 2.6f);
            DestroyImmediate(ring.GetComponent<Collider>());
            SetMaterialColor(ring, shader, new Color(0.2f, 0.8f, 1.0f), emissive: true, emissionMult: 2.0f);

            GameObject textGo = new GameObject("FloatingLabel");
            textGo.transform.SetParent(slotGo.transform);
            textGo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            TextMesh tm = textGo.AddComponent<TextMesh>();
            tm.text = "AWAITING TRAINED POLICY";
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontSize = 32;
            tm.characterSize = 0.050f;
            tm.color = new Color(0.2f, 0.8f, 1.0f);

            GameObject subTextGo = new GameObject("SubLabel");
            subTextGo.transform.SetParent(slotGo.transform);
            subTextGo.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            TextMesh subTm = subTextGo.AddComponent<TextMesh>();
            subTm.text = "LLM REWARD LOOP INACTIVE";
            subTm.alignment = TextAlignment.Center;
            subTm.anchor = TextAnchor.MiddleCenter;
            subTm.fontSize = 22;
            subTm.characterSize = 0.038f;
            subTm.color = new Color(0.75f, 0.8f, 0.85f);

            slotGo.AddComponent<LiveSlotVisuals>();
            slotGo.AddComponent<LivePolicyLoader>();
            return slotGo;
        }

        private Transform CreateGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent);
            group.transform.localPosition = Vector3.zero;
            return group.transform;
        }

        private Material SetMaterialColor(GameObject obj, Shader shader, Color color, bool emissive = false, float emissionMult = 1.0f)
        {
            var rend = obj.GetComponent<Renderer>();
            if (rend == null) return null;
            Material mat = new Material(shader);
            mat.color = color;
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emissionMult);
            }
            rend.material = mat;
            return mat;
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null) field.SetValue(target, value);
        }
    }
}
