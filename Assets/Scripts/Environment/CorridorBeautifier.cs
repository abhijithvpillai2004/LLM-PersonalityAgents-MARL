using System.Collections.Generic;
using UnityEngine;

namespace RelayCorridor
{
    [ExecuteAlways]
    public class CorridorBeautifier : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private Color cameraBackgroundColor = new Color(0.82f, 0.85f, 0.89f);

        // Core references
        private EnergyCell cellRef;
        private Renderer receiverRend;
        private Material receiverMat;
        private Material cellMat;
        private Transform cellRingTr;
        private Light dirLight;

        // Dynamic Pads expanding rings
        private struct PadVisualData
        {
            public RelayPad pad;
            public GameObject ringGo;
            public Material ringMat;
            public Color color;
        }
        private readonly List<PadVisualData> padVisuals = new List<PadVisualData>();

        // Gate glowing floor strips
        private Material gate1FloorMat;
        private Material gate2FloorMat;
        private Material gate3FloorMat;
        private Material gate4FloorMat;

        // Agent visual tracking
        private struct AgentVisualData
        {
            public RelayAgent agent;
            public Transform haloTr;
            public GameObject carrierAuraGo;
            public Material carrierAuraMat;
            public Color color;
        }
        private readonly List<AgentVisualData> agentVisuals = new List<AgentVisualData>();

        // Shared materials & procedural meshes
        private Material floorMat;
        private Material frostedWallMat;
        private Material gate1Mat;
        private Material gate2Mat;
        private Material gate3Mat;
        private Material gate4Mat;
        private Mesh ringMesh3D;
        private Mesh padExpandingRingMesh;

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyBeautification();
            }
        }

        [ContextMenu("Apply Beautification Now")]
        public void ApplyBeautification()
        {
            CleanUpOldObjects();
            BeautifyCamera();
            BeautifyLighting();
            CreateProceduralMeshes();
            BeautifyFloorAndWalls();
            CreateGateBoundaryGlows();
            BeautifyGates();
            BeautifyReceiver();
            SetupRelayPadsVisuals();
            SetupEnergyCellVisuals();
            SetupAgentsVisuals();
        }

        private void CleanUpOldObjects()
        {
            string[] cleanupNames = new string[]
            {
                "Neon_Floor_Tracks",
                "Environment_Beautified_Lighting",
                "Gate_Boundary_Glows",
                "Environment_Neutral_Lighting"
            };

            foreach (var n in cleanupNames)
            {
                var go = GameObject.Find(n);
                if (go != null) DestroyImmediate(go);
            }

            cellRef = FindAnyObjectByType<EnergyCell>();
            if (cellRef != null)
            {
                Transform oldLight = cellRef.transform.Find("CellLight");
                if (oldLight != null) DestroyImmediate(oldLight.gameObject);
                Transform oldRing = cellRef.transform.Find("Cell_OrbitRing");
                if (oldRing != null) DestroyImmediate(oldRing.gameObject);
            }
        }

        private void BeautifyCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cameraBackgroundColor;
            // Center of doubled 50m corridor [-15, 35] is x = 10.0
            cam.transform.position = new Vector3(5.0f, 27.0f, -20.0f);
            cam.transform.rotation = Quaternion.Euler(52.0f, 0f, 0f);
            cam.fieldOfView = 50f;
        }

        private void BeautifyLighting()
        {
            // 1. Soft Directional Light from above at low intensity (no harsh shadows)
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    dirLight = l;
                    break;
                }
            }

            if (dirLight != null)
            {
                dirLight.color = new Color(0.92f, 0.94f, 0.98f);
                dirLight.intensity = 0.50f;
                dirLight.shadows = LightShadows.Soft;
                dirLight.shadowStrength = 0.30f;
                dirLight.transform.rotation = Quaternion.Euler(60f, -25f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.10f, 0.13f);

            // 2. Subtle Point Light in each of the 5 chambers in neutral cool white at low intensity
            GameObject lightRoot = new GameObject("Environment_Neutral_Lighting");
            lightRoot.transform.SetParent(transform);

            CreateChamberLight("Light_ChamberA", new Vector3(-10.0f, 3.2f, 0f), lightRoot.transform);
            CreateChamberLight("Light_ChamberB", new Vector3(0.0f, 3.2f, 0f), lightRoot.transform);
            CreateChamberLight("Light_ChamberC", new Vector3(10.0f, 3.2f, 0f), lightRoot.transform);
            CreateChamberLight("Light_ChamberD", new Vector3(20.0f, 3.2f, 0f), lightRoot.transform);
        }

        private void CreateChamberLight(string name, Vector3 pos, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;

            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.88f, 0.92f, 0.98f);
            l.intensity = 0.30f;
            l.range = 14f;
            l.shadows = LightShadows.None;
        }

        private void CreateProceduralMeshes()
        {
            // 1. 3D Ring Mesh with top, bottom, and side faces
            int segments = 32;
            ringMesh3D = new Mesh { name = "Procedural_Ring3D" };

            float rIn = 0.44f;
            float rOut = 0.62f;
            float halfH = 0.045f;

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector3> norms = new List<Vector3>();

            // Top ring (y = +halfH)
            int topStart = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * rIn, halfH, Mathf.Sin(a) * rIn));
                verts.Add(new Vector3(Mathf.Cos(a) * rOut, halfH, Mathf.Sin(a) * rOut));
                norms.Add(Vector3.up);
                norms.Add(Vector3.up);

                int next = (i + 1) % segments;
                tris.Add(topStart + i * 2);
                tris.Add(topStart + i * 2 + 1);
                tris.Add(topStart + next * 2 + 1);

                tris.Add(topStart + i * 2);
                tris.Add(topStart + next * 2 + 1);
                tris.Add(topStart + next * 2);
            }

            // Outer bevel sides
            int sideStart = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(new Vector3(Mathf.Cos(a) * rOut, halfH, Mathf.Sin(a) * rOut));
                verts.Add(new Vector3(Mathf.Cos(a) * rOut, -halfH, Mathf.Sin(a) * rOut));
                norms.Add(outward);
                norms.Add(outward);

                int next = (i + 1) % segments;
                tris.Add(sideStart + i * 2);
                tris.Add(sideStart + i * 2 + 1);
                tris.Add(sideStart + next * 2 + 1);

                tris.Add(sideStart + i * 2);
                tris.Add(sideStart + next * 2 + 1);
                tris.Add(sideStart + next * 2);
            }

            ringMesh3D.SetVertices(verts);
            ringMesh3D.SetTriangles(tris, 0);
            ringMesh3D.SetNormals(norms);

            // 2. Flat Pad Expanding Ring Mesh
            padExpandingRingMesh = new Mesh { name = "Procedural_PadRing" };
            Vector3[] pVerts = new Vector3[segments * 2];
            int[] pTris = new int[segments * 6];

            float prIn = 0.70f;
            float prOut = 0.85f;
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                pVerts[i * 2] = new Vector3(Mathf.Cos(a) * prIn, 0f, Mathf.Sin(a) * prIn);
                pVerts[i * 2 + 1] = new Vector3(Mathf.Cos(a) * prOut, 0f, Mathf.Sin(a) * prOut);

                int next = (i + 1) % segments;
                int bIdx = i * 6;
                pTris[bIdx] = i * 2;
                pTris[bIdx + 1] = i * 2 + 1;
                pTris[bIdx + 2] = next * 2 + 1;

                pTris[bIdx + 3] = i * 2;
                pTris[bIdx + 4] = next * 2 + 1;
                pTris[bIdx + 5] = next * 2;
            }
            padExpandingRingMesh.vertices = pVerts;
            padExpandingRingMesh.triangles = pTris;
            padExpandingRingMesh.RecalculateNormals();
        }

        private void BeautifyFloorAndWalls()
        {
            Shader litShader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");

            // 1. FLOOR: Dark navy / deep charcoal with subtle grid lines
            Texture2D gridTex = GenerateSciFiGridTexture();
            floorMat = new Material(litShader)
            {
                name = "Mat_SciFi_Floor",
                mainTexture = gridTex,
                color = Color.white
            };
            floorMat.SetTextureScale("_MainTex", new Vector2(20f, 5f));
            floorMat.SetFloat("_Glossiness", 0.22f);
            floorMat.SetFloat("_Metallic", 0.12f);

            GameObject floor = GameObject.Find("Floor");
            if (floor != null)
            {
                var rend = floor.GetComponent<Renderer>();
                if (rend != null) rend.material = floorMat;
            }

            // 2. WALLS: Frosted dark glass
            frostedWallMat = new Material(litShader)
            {
                name = "Mat_FrostedDarkGlass_Wall",
                color = new Color(0.06f, 0.085f, 0.12f, 0.62f)
            };
            frostedWallMat.SetFloat("_Mode", 3);
            frostedWallMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            frostedWallMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            frostedWallMat.SetInt("_ZWrite", 0);
            frostedWallMat.DisableKeyword("_ALPHATEST_ON");
            frostedWallMat.EnableKeyword("_ALPHABLEND_ON");
            frostedWallMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            frostedWallMat.renderQueue = 3000;
            frostedWallMat.SetFloat("_Glossiness", 0.88f);
            frostedWallMat.SetFloat("_Metallic", 0.22f);

            string[] wallNames = new string[]
            {
                "Wall_North", "Wall_South", "Wall_West", "Wall_East",
                "Partition_-5_North", "Partition_-5_South",
                "Partition_5_North", "Partition_5_South",
                "Partition_15_North", "Partition_15_South"
            };
            foreach (var wName in wallNames)
            {
                GameObject wall = GameObject.Find(wName);
                if (wall != null)
                {
                    var rend = wall.GetComponent<Renderer>();
                    if (rend != null) rend.material = frostedWallMat;
                }
            }
        }

        private Texture2D GenerateSciFiGridTexture()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "SciFiGridTex",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color baseBg = new Color(0.045f, 0.060f, 0.085f, 1f);
            Color gridLine = new Color(0.10f, 0.14f, 0.20f, 1f);
            Color dotColor = new Color(0.16f, 0.22f, 0.32f, 1f);

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isLine = (x % 64 == 0) || (y % 64 == 0);
                    bool isDot = (x % 64 <= 1 || x % 64 >= 63) && (y % 64 <= 1 || y % 64 >= 63);

                    if (isDot) pixels[y * size + x] = dotColor;
                    else if (isLine) pixels[y * size + x] = gridLine;
                    else pixels[y * size + x] = baseBg;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D GenerateSoftGateGlowTexture()
        {
            int w = 128;
            int h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "SoftGateGlowTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] pix = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float ny = ((y / (float)(h - 1)) - 0.5f) * 2f;
                float fallY = Mathf.Clamp01(1f - Mathf.Abs(ny));
                fallY = Mathf.SmoothStep(0f, 1f, fallY);

                for (int x = 0; x < w; x++)
                {
                    float nx = ((x / (float)(w - 1)) - 0.5f) * 2f;
                    float fallX = Mathf.Clamp01(1f - Mathf.Abs(nx));
                    fallX = Mathf.SmoothStep(0f, 1f, fallX);

                    float alpha = fallX * fallY;
                    pix[y * w + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void CreateGateBoundaryGlows()
        {
            GameObject root = new GameObject("Gate_Boundary_Glows");
            root.transform.SetParent(transform);

            Shader litShader = Shader.Find("Standard");
            Texture2D glowTex = GenerateSoftGateGlowTexture();

            // Gate 1 Floor Glow (Blue at x = -5.0)
            gate1FloorMat = CreateGlowMat(litShader, glowTex, new Color(0.05f, 0.55f, 1.0f));
            CreateBoundaryQuad("Gate1_FloorGlow", new Vector3(-5.0f, 0.02f, 0f), new Vector3(1.6f, 5.2f, 1f), gate1FloorMat, root.transform);

            // Gate 2 Floor Glow (Amber at x = 5.0)
            gate2FloorMat = CreateGlowMat(litShader, glowTex, new Color(1.0f, 0.65f, 0.08f));
            CreateBoundaryQuad("Gate2_FloorGlow", new Vector3(5.0f, 0.02f, 0f), new Vector3(1.6f, 5.2f, 1f), gate2FloorMat, root.transform);

            // Gate 3 Floor Glow (Emerald at x = 15.0)
            gate3FloorMat = CreateGlowMat(litShader, glowTex, new Color(0.08f, 0.95f, 0.55f));
            CreateBoundaryQuad("Gate3_FloorGlow", new Vector3(15.0f, 0.02f, 0f), new Vector3(1.6f, 5.2f, 1f), gate3FloorMat, root.transform);
        }

        private Material CreateGlowMat(Shader shader, Texture2D tex, Color col)
        {
            var mat = new Material(shader)
            {
                name = "Mat_Gate_FloorGlow_" + col.ToString(),
                mainTexture = tex,
                color = new Color(col.r, col.g, col.b, 0.45f)
            };
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            mat.SetColor("_EmissionColor", col * 1.6f);
            mat.EnableKeyword("_EMISSION");
            return mat;
        }

        private void CreateBoundaryQuad(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent);
            quad.transform.position = pos;
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = scale;

            var col = quad.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            var rend = quad.GetComponent<Renderer>();
            if (rend != null) rend.material = mat;
        }

        private void BeautifyGates()
        {
            Shader litShader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");

            ApplyGateMaterial("Gate1", new Color(0.10f, 0.18f, 0.32f, 0.90f), new Color(0.05f, 0.40f, 0.95f) * 0.40f, litShader, out gate1Mat);
            ApplyGateMaterial("Gate2", new Color(0.32f, 0.18f, 0.08f, 0.90f), new Color(0.95f, 0.45f, 0.05f) * 0.40f, litShader, out gate2Mat);
            ApplyGateMaterial("Gate3", new Color(0.08f, 0.28f, 0.18f, 0.90f), new Color(0.05f, 0.95f, 0.55f) * 0.40f, litShader, out gate3Mat);
        }

        private void ApplyGateMaterial(string gateName, Color baseCol, Color emissiveCol, Shader shader, out Material gateMat)
        {
            gateMat = null;
            GameObject g = GameObject.Find(gateName);
            if (g != null)
            {
                var rend = g.GetComponent<Renderer>();
                if (rend != null)
                {
                    gateMat = new Material(shader)
                    {
                        name = "Mat_" + gateName + "_Glowing",
                        color = baseCol
                    };
                    gateMat.SetColor("_EmissionColor", emissiveCol);
                    gateMat.EnableKeyword("_EMISSION");
                    gateMat.SetFloat("_Glossiness", 0.75f);
                    rend.material = gateMat;
                }
            }
        }

        private void BeautifyReceiver()
        {
            GameObject rx = GameObject.Find("ReceiverTerminal");
            if (rx != null)
            {
                receiverRend = rx.GetComponent<Renderer>();
                if (receiverRend != null)
                {
                    Shader litShader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");
                    receiverMat = new Material(litShader)
                    {
                        name = "Mat_Receiver_PulsingGreen",
                        color = new Color(0.06f, 0.18f, 0.10f)
                    };
                    receiverMat.SetColor("_EmissionColor", new Color(0.0f, 0.9f, 0.4f) * 0.6f);
                    receiverMat.EnableKeyword("_EMISSION");
                    receiverMat.SetFloat("_Glossiness", 0.80f);
                    receiverRend.material = receiverMat;
                }
            }
        }

        private void SetupRelayPadsVisuals()
        {
            padVisuals.Clear();
            Shader litShader = Shader.Find("Standard");

            // Define colors per pad
            Dictionary<string, Color> padColors = new Dictionary<string, Color>
            {
                { "RelayPad_A", new Color(0.1f, 0.85f, 1.0f) },  // Room 1: Cyan
                { "RelayPad_B", new Color(1.0f, 0.75f, 0.15f) }, // Room 2: Amber
                { "RelayPad_C", new Color(1.0f, 0.75f, 0.15f) }, // Room 2: Amber
                { "RelayPad_D", new Color(0.1f, 0.95f, 0.55f) }, // Room 3: Emerald
                { "RelayPad_E", new Color(0.1f, 0.95f, 0.55f) }, // Room 3: Emerald
                { "RelayPad_F", new Color(0.1f, 0.95f, 0.55f) }   // Room 3: Emerald
            };

            foreach (var kvp in padColors)
            {
                var padGo = GameObject.Find(kvp.Key);
                if (padGo != null)
                {
                    var pad = padGo.GetComponent<RelayPad>();
                    if (pad != null)
                    {
                        Material rMat;
                        GameObject ring = CreateExpandingRingObject(pad.transform, kvp.Key + "_ExpandingRing", kvp.Value, litShader, out rMat);
                        padVisuals.Add(new PadVisualData
                        {
                            pad = pad,
                            ringGo = ring,
                            ringMat = rMat,
                            color = kvp.Value
                        });
                    }
                }
            }
        }

        private GameObject CreateExpandingRingObject(Transform padTr, string name, Color color, Shader shader, out Material mat)
        {
            Transform existing = padTr.Find(name);
            if (existing != null) DestroyImmediate(existing.gameObject);

            GameObject ring = new GameObject(name);
            ring.transform.SetParent(padTr);
            ring.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            ring.transform.localRotation = Quaternion.identity;
            ring.transform.localScale = Vector3.one;

            var mf = ring.AddComponent<MeshFilter>();
            mf.sharedMesh = padExpandingRingMesh;

            var mr = ring.AddComponent<MeshRenderer>();
            mat = new Material(shader)
            {
                name = "Mat_" + name,
                color = new Color(color.r, color.g, color.b, 0.4f)
            };
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            mat.SetColor("_EmissionColor", color * 2.2f);
            mat.EnableKeyword("_EMISSION");
            mr.material = mat;

            ring.SetActive(false);
            return ring;
        }

        private void SetupEnergyCellVisuals()
        {
            cellRef = FindAnyObjectByType<EnergyCell>();
            if (cellRef == null) return;

            // 1. Core Sphere: Brighter white-gold emissive sphere
            Shader litShader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");
            cellMat = new Material(litShader)
            {
                name = "Mat_EnergyCell_WhiteGold",
                color = new Color(1.0f, 0.96f, 0.88f)
            };
            cellMat.SetColor("_EmissionColor", new Color(1.0f, 0.92f, 0.65f) * 2.8f);
            cellMat.EnableKeyword("_EMISSION");
            cellMat.SetFloat("_Glossiness", 0.95f);
            cellMat.SetFloat("_Metallic", 0.35f);

            var mrCore = cellRef.GetComponent<Renderer>();
            if (mrCore != null) mrCore.material = cellMat;

            // 2. Saturn-like Orbital Ring
            Transform existingRing = cellRef.transform.Find("Cell_OrbitRing");
            if (existingRing != null) DestroyImmediate(existingRing.gameObject);

            GameObject ringGo = new GameObject("Cell_OrbitRing");
            ringGo.transform.SetParent(cellRef.transform);
            ringGo.transform.localPosition = Vector3.zero;
            ringGo.transform.localRotation = Quaternion.Euler(25f, 0f, 15f);
            ringGo.transform.localScale = Vector3.one * 1.35f;

            var mf = ringGo.AddComponent<MeshFilter>();
            mf.sharedMesh = ringMesh3D;

            var mr = ringGo.AddComponent<MeshRenderer>();
            Material ringMat = new Material(litShader)
            {
                name = "Mat_Cell_OrbitRing",
                color = new Color(1.0f, 0.90f, 0.60f)
            };
            ringMat.SetColor("_EmissionColor", new Color(1.0f, 0.88f, 0.50f) * 2.2f);
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetFloat("_Glossiness", 0.85f);
            mr.material = ringMat;

            cellRingTr = ringGo.transform;
        }

        private void SetupAgentsVisuals()
        {
            agentVisuals.Clear();

            Dictionary<string, Color> colorMap = new Dictionary<string, Color>()
            {
                { "Agent_0_HighE", new Color(0.0f, 0.85f, 1.0f) },       // Cyan
                { "Agent_1_LowE", new Color(0.1f, 0.90f, 0.45f) },        // Green
                { "Agent_2_HighA", new Color(1.0f, 0.58f, 0.05f) },       // Orange
                { "Agent_3_LowA", new Color(1.0f, 0.15f, 0.65f) },        // Magenta
                { "LivePolicyAgent_Slot", new Color(1.0f, 0.88f, 0.35f) } // White/Gold
            };

            var agents = FindObjectsByType<RelayAgent>(FindObjectsSortMode.None);
            Shader litShader = Shader.Find("Standard");

            foreach (var ag in agents)
            {
                Color pColor = Color.white;
                if (!colorMap.TryGetValue(ag.gameObject.name, out pColor))
                {
                    pColor = ag.isLivePolicySlot ? new Color(1.0f, 0.88f, 0.35f) : ag.AgentColor;
                }

                Transform oldHalo = ag.transform.Find("Agent_Halo");
                if (oldHalo != null) DestroyImmediate(oldHalo.gameObject);
                Transform oldBeam = ag.transform.Find("Agent_Beam");
                if (oldBeam != null) DestroyImmediate(oldBeam.gameObject);
                Transform oldAura = ag.transform.Find("Agent_CarrierAura");
                if (oldAura != null) DestroyImmediate(oldAura.gameObject);

                var agRend = ag.GetComponent<Renderer>();
                if (agRend != null)
                {
                    Material agMat = new Material(litShader)
                    {
                        name = "Mat_" + ag.name,
                        color = pColor
                    };
                    agMat.SetColor("_EmissionColor", pColor * 0.45f);
                    agMat.EnableKeyword("_EMISSION");
                    agMat.SetFloat("_Glossiness", 0.85f);
                    agRend.material = agMat;
                }

                // 1. Floating Ring Halo rotating slowly
                GameObject haloGo = new GameObject("Agent_Halo");
                haloGo.transform.SetParent(ag.transform);
                haloGo.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                haloGo.transform.localRotation = Quaternion.identity;
                haloGo.transform.localScale = Vector3.one * 1.05f;

                var hMf = haloGo.AddComponent<MeshFilter>();
                hMf.sharedMesh = ringMesh3D;

                var hMr = haloGo.AddComponent<MeshRenderer>();
                Material hMat = new Material(litShader)
                {
                    name = "Mat_Halo_" + ag.name,
                    color = pColor
                };
                hMat.SetColor("_EmissionColor", pColor * 2.5f);
                hMat.EnableKeyword("_EMISSION");
                hMat.SetFloat("_Glossiness", 0.9f);
                hMr.material = hMat;

                // 2. Thin Vertical Light Beam
                GameObject beamGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beamGo.name = "Agent_Beam";
                beamGo.transform.SetParent(ag.transform);
                beamGo.transform.localPosition = new Vector3(0f, 1.95f, 0f);
                beamGo.transform.localScale = new Vector3(0.06f, 0.60f, 0.06f);

                var beamCol = beamGo.GetComponent<Collider>();
                if (beamCol != null) DestroyImmediate(beamCol);

                var bMr = beamGo.GetComponent<MeshRenderer>();
                Material bMat = new Material(litShader)
                {
                    name = "Mat_Beam_" + ag.name,
                    color = new Color(pColor.r, pColor.g, pColor.b, 0.70f)
                };
                bMat.SetFloat("_Mode", 3);
                bMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                bMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                bMat.SetInt("_ZWrite", 0);
                bMat.EnableKeyword("_ALPHABLEND_ON");
                bMat.renderQueue = 3000;
                bMat.SetColor("_EmissionColor", pColor * 2.2f);
                bMat.EnableKeyword("_EMISSION");
                bMr.material = bMat;

                // 3. Carrier Pulsing Outline Effect
                GameObject auraGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                auraGo.name = "Agent_CarrierAura";
                auraGo.transform.SetParent(ag.transform);
                auraGo.transform.localPosition = Vector3.zero;
                auraGo.transform.localScale = new Vector3(1.22f, 1.06f, 1.22f);

                var auraCol = auraGo.GetComponent<Collider>();
                if (auraCol != null) DestroyImmediate(auraCol);

                var aMr = auraGo.GetComponent<MeshRenderer>();
                Material aMat = new Material(litShader)
                {
                    name = "Mat_CarrierAura_" + ag.name,
                    color = new Color(1.0f, 0.95f, 0.4f, 0.45f)
                };
                aMat.SetFloat("_Mode", 3);
                aMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                aMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                aMat.SetInt("_ZWrite", 0);
                aMat.EnableKeyword("_ALPHABLEND_ON");
                aMat.renderQueue = 3000;
                aMat.SetColor("_EmissionColor", new Color(1.0f, 0.95f, 0.4f) * 3.5f);
                aMat.EnableKeyword("_EMISSION");
                aMr.material = aMat;
                auraGo.SetActive(false);

                agentVisuals.Add(new AgentVisualData
                {
                    agent = ag,
                    haloTr = haloGo.transform,
                    carrierAuraGo = auraGo,
                    carrierAuraMat = aMat,
                    color = pColor
                });
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // 1. Receiver Terminal Soft Green Pulse
            if (receiverMat != null)
            {
                float rxPulse = 0.35f + 0.35f * Mathf.Sin(Time.time * 2.2f);
                receiverMat.SetColor("_EmissionColor", new Color(0.0f, 0.95f, 0.45f) * rxPulse);
            }

            // 2. Relay Pads Active / Inactive states with expanding animated rings
            for (int i = 0; i < padVisuals.Count; i++)
            {
                var pv = padVisuals[i];
                UpdatePadState(pv.pad, pv.ringGo, pv.ringMat, pv.color);
            }

            // 3. Energy Cell Dynamic Behavior
            if (cellRef != null)
            {
                bool carried = cellRef.IsCarried;
                if (cellRingTr != null)
                {
                    float spinSpeed = carried ? 220f : 35f;
                    cellRingTr.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
                }

                if (cellMat != null)
                {
                    if (carried)
                    {
                        cellMat.SetColor("_EmissionColor", new Color(1.0f, 0.94f, 0.70f) * 4.5f);
                    }
                    else
                    {
                        float pulse = 2.0f + 0.8f * Mathf.Sin(Time.time * 2.5f);
                        cellMat.SetColor("_EmissionColor", new Color(1.0f, 0.92f, 0.65f) * pulse);
                    }
                }
            }

            // 4. Agent Halo Rotation & Carrier Pulsing Outline
            for (int i = 0; i < agentVisuals.Count; i++)
            {
                var data = agentVisuals[i];
                if (data.agent == null) continue;

                if (data.haloTr != null)
                {
                    data.haloTr.Rotate(Vector3.up, 38f * Time.deltaTime, Space.Self);
                }

                bool isCarrying = cellRef != null && cellRef.IsCarried && cellRef.CarryingAgent == data.agent.transform;
                if (data.carrierAuraGo != null)
                {
                    if (isCarrying)
                    {
                        if (!data.carrierAuraGo.activeSelf) data.carrierAuraGo.SetActive(true);
                        float auraPulse = 0.35f + 0.35f * Mathf.Sin(Time.time * 7f);
                        Color c = data.carrierAuraMat.color;
                        c.a = auraPulse;
                        data.carrierAuraMat.color = c;
                    }
                    else
                    {
                        if (data.carrierAuraGo.activeSelf) data.carrierAuraGo.SetActive(false);
                    }
                }
            }
        }

        private void UpdatePadState(RelayPad pad, GameObject ringGo, Material ringMat, Color col)
        {
            if (pad == null || ringGo == null) return;

            if (pad.IsActive)
            {
                if (!ringGo.activeSelf) ringGo.SetActive(true);

                float t = (Time.time * 1.5f) % 1.0f;
                float currentScale = 1.0f + 0.95f * t;
                ringGo.transform.localScale = Vector3.one * currentScale;

                if (ringMat != null)
                {
                    Color c = col;
                    c.a = (1.0f - t) * 0.85f;
                    ringMat.color = c;
                    if (ringMat.HasProperty("_EmissionColor"))
                    {
                        ringMat.SetColor("_EmissionColor", col * ((1.0f - t) * 2.5f));
                    }
                }
            }
            else
            {
                if (ringGo.activeSelf) ringGo.SetActive(false);
            }
        }
    }
}
