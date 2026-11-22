using System.Collections;
using UnityEngine;

namespace RelayCorridor
{
    public class SlidingGate : MonoBehaviour
    {
        [SerializeField] private string gateId = "Gate1";
        [SerializeField] private float openHeight = 4.0f;
        [SerializeField] private float animationDuration = 0.5f;

        private Vector3 closedPosition;
        private Vector3 openPosition;
        private Collider gateCollider;
        private Coroutine moveCoroutine;
        private bool initialized;

        public string GateId => gateId;
        public bool IsOpen { get; private set; }
        public bool IsMoving => moveCoroutine != null;
        public Vector3 ClosedPosition => closedPosition;
        public Vector3 OpenPosition => openPosition;

        private void Awake()
        {
            EnsureInitialized();
            // Start closed
            transform.position = closedPosition;
            if (gateCollider != null) gateCollider.enabled = true;
            IsOpen = false;
        }

        public void EnsureInitialized()
        {
            if (initialized) return;
            gateCollider = GetComponent<Collider>();
            // If the gate was saved while in the open state (e.g. y ~ 6.0), the true closed position is at y = 2.0
            float closedY = (transform.position.y > 3.0f) ? 2.0f : transform.position.y;
            closedPosition = new Vector3(transform.position.x, closedY, transform.position.z);
            openPosition = closedPosition + Vector3.up * openHeight;
            initialized = true;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            EnsureInitialized();
            Vector3 target = IsOpen ? openPosition : closedPosition;
            // Failsafe: if gate is stopped but physically out of sync with desired position, animate to target
            if (moveCoroutine == null && Vector3.Distance(transform.position, target) > 0.05f)
            {
                moveCoroutine = StartCoroutine(AnimateGate(IsOpen));
            }
        }

        public void SetOpen(bool open, bool immediate = false)
        {
            EnsureInitialized();
            IsOpen = open;

            if (moveCoroutine != null && Application.isPlaying)
            {
                StopCoroutine(moveCoroutine);
                moveCoroutine = null;
            }

            if (immediate || !Application.isPlaying)
            {
                transform.position = open ? openPosition : closedPosition;
                if (gateCollider != null) gateCollider.enabled = !open;
            }
            else
            {
                moveCoroutine = StartCoroutine(AnimateGate(open));
            }
        }

        private IEnumerator AnimateGate(bool open)
        {
            Vector3 start = transform.position;
            Vector3 target = open ? openPosition : closedPosition;
            float elapsed = 0f;

            if (open && gateCollider != null)
            {
                gateCollider.enabled = false;
            }

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                t = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            transform.position = target;
            if (!open && gateCollider != null)
            {
                gateCollider.enabled = true;
            }
            moveCoroutine = null;
        }

        public void ResetGate()
        {
            EnsureInitialized();
            SetOpen(false, immediate: true);
            transform.position = closedPosition;
            if (gateCollider != null) gateCollider.enabled = true;
        }
    }
}
