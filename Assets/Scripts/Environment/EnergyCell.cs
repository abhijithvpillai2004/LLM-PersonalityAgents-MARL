using UnityEngine;

namespace RelayCorridor
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class EnergyCell : MonoBehaviour
    {
        [SerializeField] private float radius = 0.4f;
        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private Vector3 defaultSpawnPosition;
        private Transform originalParent;
        private bool initialized;

        public bool IsCarried { get; private set; }
        public string CarryingAgentId { get; private set; }
        public Transform CarryingAgent { get; private set; }

        public Vector3 Position => transform.position;

        private void Awake()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            if (initialized) return;
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider != null) sphereCollider.radius = radius;
            originalParent = transform.parent;
            defaultSpawnPosition = transform.position;
            initialized = true;
        }

        public void InitializeSpawn(Vector3 spawnPosition)
        {
            EnsureComponents();
            defaultSpawnPosition = spawnPosition;
            ResetCell(spawnPosition);
        }

        public bool Pickup(Transform agentTransform, string agentId)
        {
            if (IsCarried) return false;
            EnsureComponents();

            IsCarried = true;
            CarryingAgentId = agentId;
            CarryingAgent = agentTransform;

            if (rb != null) rb.isKinematic = true;
            if (sphereCollider != null) sphereCollider.isTrigger = true;

            transform.SetParent(agentTransform);
            transform.localPosition = new Vector3(0f, 0.6f, 0.6f);
            return true;
        }

        public void Drop(Vector3 dropPosition)
        {
            if (!IsCarried) return;
            EnsureComponents();

            transform.SetParent(originalParent);
            transform.position = dropPosition;

            IsCarried = false;
            CarryingAgentId = null;
            CarryingAgent = null;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = false;
            }
        }

        public void ResetCell(Vector3? position = null)
        {
            EnsureComponents();
            transform.SetParent(originalParent);
            Vector3 pos = position ?? defaultSpawnPosition;
            transform.position = pos;

            IsCarried = false;
            CarryingAgentId = null;
            CarryingAgent = null;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = false;
            }
        }
    }
}
