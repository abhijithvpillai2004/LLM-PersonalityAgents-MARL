using System.Collections;
using UnityEngine;

namespace RelayCorridor
{
    public class LiveSlotVisuals : MonoBehaviour
    {
        public enum SlotVisualState
        {
            Idle,
            Designing,
            Spawning,
            Active
        }

        [Header("Components")]
        [SerializeField] private Renderer platformRenderer;
        [SerializeField] private Renderer ringRenderer;
        [SerializeField] private TextMesh labelText;
        [SerializeField] private TextMesh subLabelText;
        [SerializeField] private ParticleSystem particles;

        [Header("State Colors")]
        [SerializeField] private Color idleColor = new Color(0.2f, 0.8f, 1.0f);
        [SerializeField] private Color designingColor = new Color(1.0f, 0.6f, 0.1f);
        [SerializeField] private Color activeColor = new Color(1.0f, 0.85f, 0.2f);

        private SlotVisualState currentState = SlotVisualState.Idle;
        private Material ringMaterial;
        private float pulseTimer;

        public SlotVisualState CurrentState => currentState;

        private void Awake()
        {
            if (ringRenderer != null)
            {
                ringMaterial = ringRenderer.material;
            }
            SetIdle();
        }

        private void Update()
        {
            pulseTimer += Time.deltaTime;

            if (currentState == SlotVisualState.Idle)
            {
                // Slow sine wave, white/cyan
                float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTimer * 2.0f);
                UpdateRingGlow(idleColor, 1.0f + pulse * 2.0f);
            }
            else if (currentState == SlotVisualState.Designing)
            {
                // Fast amber pulse
                float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTimer * 6.0f);
                UpdateRingGlow(designingColor, 1.5f + pulse * 3.5f);
            }
            else if (currentState == SlotVisualState.Active)
            {
                // Steady glow
                UpdateRingGlow(activeColor, 2.5f);
            }
        }

        private void LateUpdate()
        {
            // Face Main Camera
            if (Camera.main != null)
            {
                if (labelText != null)
                {
                    Vector3 lookDir = (labelText.transform.position - Camera.main.transform.position).normalized;
                    if (lookDir != Vector3.zero) labelText.transform.rotation = Quaternion.LookRotation(lookDir);
                }
                if (subLabelText != null)
                {
                    Vector3 lookDir = (subLabelText.transform.position - Camera.main.transform.position).normalized;
                    if (lookDir != Vector3.zero) subLabelText.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }

        public void SetIdle()
        {
            currentState = SlotVisualState.Idle;
            if (labelText != null)
            {
                labelText.text = "AWAITING TRAINED POLICY";
                labelText.color = idleColor;
            }
            if (subLabelText != null)
            {
                subLabelText.text = "LLM REWARD LOOP INACTIVE";
                subLabelText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            }
            if (particles != null)
            {
                var main = particles.main;
                main.startColor = idleColor;
                if (!particles.isPlaying) particles.Play();
            }
            UpdateRingGlow(idleColor, 1.5f);
        }

        public void SetDesigning(int iterationN, float score, string status)
        {
            currentState = SlotVisualState.Designing;
            if (labelText != null)
            {
                labelText.text = $"DESIGNING POLICY — ITER {iterationN}/10 — SCORE {score:0.#}/10";
                labelText.color = designingColor;
            }
            if (subLabelText != null)
            {
                subLabelText.text = $"STATUS: {status.ToUpper()}";
                subLabelText.color = Color.white;
            }
            if (particles != null)
            {
                var main = particles.main;
                main.startColor = designingColor;
            }
        }

        public void PlayActivationSequence(string personaName)
        {
            StartCoroutine(ActivationSequenceCoroutine(personaName));
        }

        private IEnumerator ActivationSequenceCoroutine(string personaName)
        {
            currentState = SlotVisualState.Spawning;
            if (labelText != null)
            {
                labelText.text = $"SPAWNING: {personaName.ToUpper()}";
                labelText.color = Color.white;
            }

            // Flare ring to full white
            UpdateRingGlow(Color.white, 8.0f);

            if (particles != null)
            {
                particles.Emit(100);
            }

            yield return new WaitForSeconds(0.6f);
            SetActive(personaName, 8.5f, 1, 1);
        }

        public void SetActive(string personaName, float score, int iter, int totalIter)
        {
            currentState = SlotVisualState.Active;
            if (labelText != null)
            {
                labelText.text = personaName.ToUpper();
                labelText.color = activeColor;
            }
            if (subLabelText != null)
            {
                subLabelText.text = $"LLM-DESIGNED REWARD — PPO POLICY — Score {score:0.#}/10 — Iter {iter}";
                subLabelText.color = Color.white;
            }
            if (particles != null)
            {
                var main = particles.main;
                main.startColor = activeColor;
            }
            UpdateRingGlow(activeColor, 2.5f);
        }

        public void ShowError(string errorMessage)
        {
            if (labelText != null)
            {
                labelText.text = "POLICY LOAD ERROR";
                labelText.color = Color.red;
            }
            if (subLabelText != null)
            {
                subLabelText.text = errorMessage;
                subLabelText.color = Color.red;
            }
            UpdateRingGlow(Color.red, 3.0f);
        }

        private void UpdateRingGlow(Color color, float intensity)
        {
            if (ringMaterial == null) return;
            ringMaterial.color = color;
            if (ringMaterial.HasProperty("_EmissionColor"))
            {
                ringMaterial.SetColor("_EmissionColor", color * intensity);
                ringMaterial.EnableKeyword("_EMISSION");
            }
        }
    }
}
