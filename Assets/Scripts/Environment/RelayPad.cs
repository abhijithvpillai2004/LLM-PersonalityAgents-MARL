using System;
using System.Collections.Generic;
using UnityEngine;

namespace RelayCorridor
{
    [RequireComponent(typeof(Collider))]
    public class RelayPad : MonoBehaviour
    {
        [SerializeField] private string padId = "PadA";
        [SerializeField] private Color baseColor = new Color(0.1f, 0.4f, 0.9f);
        [SerializeField] private Color activeColor = new Color(0.2f, 0.8f, 1.0f);
        [SerializeField] private float baseEmission = 1.0f;
        [SerializeField] private float activeEmission = 3.5f;

        private Renderer padRenderer;
        private Material padMaterial;
        private readonly HashSet<Collider> occupants = new HashSet<Collider>();

        public string PadId => padId;
        public bool IsActive => occupants.Count > 0;
        public int OccupantCount => occupants.Count;

        public event Action<RelayPad, bool> OnPadStateChanged;

        private void Awake()
        {
            padRenderer = GetComponent<Renderer>();
            if (padRenderer != null && Application.isPlaying)
            {
                padMaterial = padRenderer.material;
                UpdateVisuals();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.isTrigger) return;
            // Only agents (or test objects) activate relay pads
            if (other.GetComponentInParent<RelayAgent>() == null && other.gameObject != gameObject) return;
            bool wasActive = IsActive;
            occupants.Add(other);
            if (!wasActive && IsActive)
            {
                UpdateVisuals();
                OnPadStateChanged?.Invoke(this, true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.isTrigger) return;
            if (other.GetComponentInParent<RelayAgent>() == null && other.gameObject != gameObject) return;
            bool wasActive = IsActive;
            occupants.Remove(other);
            if (wasActive && !IsActive)
            {
                UpdateVisuals();
                OnPadStateChanged?.Invoke(this, false);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.isTrigger) return;
            if (other.GetComponentInParent<RelayAgent>() == null && other.gameObject != gameObject) return;
            if (!occupants.Contains(other))
            {
                bool wasActive = IsActive;
                occupants.Add(other);
                if (!wasActive && IsActive)
                {
                    UpdateVisuals();
                    OnPadStateChanged?.Invoke(this, true);
                }
            }
        }

        private void FixedUpdate()
        {
            if (occupants.Count > 0)
            {
                bool wasActive = IsActive;
                occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy || 
                    (c.gameObject != gameObject && c.GetComponentInParent<RelayAgent>() == null) ||
                    Vector2.Distance(new Vector2(c.bounds.center.x, c.bounds.center.z), new Vector2(transform.position.x, transform.position.z)) > 2.2f);
                if (wasActive && !IsActive)
                {
                    UpdateVisuals();
                    OnPadStateChanged?.Invoke(this, false);
                }
            }
        }

        public void ForceSetOccupied(bool occupied)
        {
            bool wasActive = IsActive;
            if (occupied)
            {
                var col = GetComponent<Collider>();
                if (col == null) col = gameObject.AddComponent<BoxCollider>();
                occupants.Add(col);
            }
            else
            {
                occupants.Clear();
            }
            if (wasActive != IsActive)
            {
                UpdateVisuals();
                OnPadStateChanged?.Invoke(this, IsActive);
            }
        }

        public void ResetPad()
        {
            occupants.Clear();
            UpdateVisuals();
            OnPadStateChanged?.Invoke(this, false);
        }

        private void UpdateVisuals()
        {
            if (padMaterial == null) return;
            Color current = IsActive ? activeColor * activeEmission : baseColor * baseEmission;
            padMaterial.color = IsActive ? activeColor : baseColor;
            if (padMaterial.HasProperty("_EmissionColor"))
            {
                padMaterial.SetColor("_EmissionColor", current);
                padMaterial.EnableKeyword("_EMISSION");
            }
        }
    }
}
