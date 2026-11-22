using System;
using UnityEngine;

namespace RelayCorridor
{
    [RequireComponent(typeof(Collider))]
    public class ReceiverTerminal : MonoBehaviour
    {
        [SerializeField] private Color idleColor = new Color(0.2f, 0.9f, 0.4f);
        [SerializeField] private Color completedColor = new Color(1.0f, 0.85f, 0.2f);

        private Renderer terminalRenderer;
        private Material terminalMaterial;

        public bool IsDelivered { get; private set; }
        public event Action OnCellDelivered;

        private void Awake()
        {
            terminalRenderer = GetComponent<Renderer>();
            if (terminalRenderer != null && Application.isPlaying)
            {
                terminalMaterial = terminalRenderer.material;
                UpdateVisuals();
            }
        }

        public bool DeliverCell(EnergyCell cell)
        {
            if (IsDelivered) return false;

            IsDelivered = true;
            UpdateVisuals();
            OnCellDelivered?.Invoke();
            return true;
        }

        public void ResetTerminal()
        {
            IsDelivered = false;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (terminalMaterial == null) return;
            Color c = IsDelivered ? completedColor : idleColor;
            terminalMaterial.color = c;
            if (terminalMaterial.HasProperty("_EmissionColor"))
            {
                terminalMaterial.SetColor("_EmissionColor", c * (IsDelivered ? 3.0f : 1.5f));
                terminalMaterial.EnableKeyword("_EMISSION");
            }
        }
    }
}
