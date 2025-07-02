using UnityEngine;
using UnityEngine.AI;

namespace AIWorld.Core
{
    /// <summary>
    /// Fixes agent positioning to ensure they spawn properly on the ground
    /// Addresses the issue of agents appearing half-buried in the ground
    /// </summary>
    public class AgentPositionFix : MonoBehaviour
    {
        [Header("Position Fix Settings")]
        public bool autoFixOnStart = true;
        public bool useRaycastForGroundDetection = true;
        public float raycastDistance = 10f;
        public LayerMask groundLayerMask = -1; // All layers by default
        
        [Header("Debug")]
        public bool logPositionFixes = true;
        public bool showDebugGizmos = false;
        
        private CapsuleCollider agentCapsule;
        private NavMeshAgent navAgent;
        private Vector3 originalPosition;
        
        private void Awake()
        {
            agentCapsule = GetComponent<CapsuleCollider>();
            navAgent = GetComponent<NavMeshAgent>();
            originalPosition = transform.position;
        }
        
        private void Start()
        {
            if (autoFixOnStart)
            {
                FixAgentPosition();
            }
        }
        
        /// <summary>
        /// Fix the agent's position to properly place them on the ground
        /// </summary>
        [ContextMenu("Fix Agent Position")]
        public void FixAgentPosition()
        {
            Vector3 currentPosition = transform.position;
            Vector3 fixedPosition = currentPosition;
            
            if (useRaycastForGroundDetection)
            {
                fixedPosition = FixPositionWithRaycast(currentPosition);
            }
            else
            {
                fixedPosition = FixPositionWithCapsule(currentPosition);
            }
            
            // Apply the fixed position
            if (Vector3.Distance(currentPosition, fixedPosition) > 0.01f)
            {
                transform.position = fixedPosition;
                
                // Update NavMeshAgent if present
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.Warp(fixedPosition);
                }
                
                if (logPositionFixes)
                {
                    Debug.Log($"🔧 {gameObject.name}: Fixed position from {currentPosition.y:F2} to {fixedPosition.y:F2}");
                }
            }
        }
        
        /// <summary>
        /// Fix position using raycast to find ground
        /// </summary>
        private Vector3 FixPositionWithRaycast(Vector3 currentPosition)
        {
            Vector3 rayStart = currentPosition + Vector3.up * raycastDistance;
            Ray ray = new Ray(rayStart, Vector3.down);
            
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance * 2f, groundLayerMask))
            {
                // Position agent so the bottom of the capsule touches the ground
                float capsuleBottom = agentCapsule != null ? agentCapsule.height * 0.5f : 1f;
                Vector3 fixedPosition = hit.point + Vector3.up * capsuleBottom;
                
                if (logPositionFixes)
                {
                    Debug.Log($"📍 {gameObject.name}: Ground found at Y={hit.point.y:F2}, positioned at Y={fixedPosition.y:F2}");
                }
                
                return fixedPosition;
            }
            else
            {
                // Fallback: use capsule-based fix
                return FixPositionWithCapsule(currentPosition);
            }
        }
        
        /// <summary>
        /// Fix position based on capsule collider dimensions
        /// </summary>
        private Vector3 FixPositionWithCapsule(Vector3 currentPosition)
        {
            if (agentCapsule == null)
            {
                // No capsule collider, assume standard capsule height
                return new Vector3(currentPosition.x, currentPosition.y + 1f, currentPosition.z);
            }
            
            // Calculate offset needed to place bottom of capsule on ground
            float capsuleBottom = agentCapsule.height * 0.5f + agentCapsule.center.y;
            Vector3 fixedPosition = new Vector3(currentPosition.x, currentPosition.y + capsuleBottom, currentPosition.z);
            
            if (logPositionFixes)
            {
                Debug.Log($"📏 {gameObject.name}: Using capsule height {agentCapsule.height:F2}, offset by {capsuleBottom:F2}");
            }
            
            return fixedPosition;
        }
        
        /// <summary>
        /// Reset to original position
        /// </summary>
        [ContextMenu("Reset Position")]
        public void ResetToOriginalPosition()
        {
            transform.position = originalPosition;
            
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.Warp(originalPosition);
            }
            
            if (logPositionFixes)
            {
                Debug.Log($"🔄 {gameObject.name}: Reset to original position {originalPosition}");
            }
        }
        
        /// <summary>
        /// Check if agent needs position fixing
        /// </summary>
        public bool NeedsPositionFix()
        {
            if (agentCapsule == null) return true;
            
            // Check if bottom of capsule is below ground (Y=0)
            float capsuleBottom = transform.position.y - (agentCapsule.height * 0.5f);
            return capsuleBottom < 0.1f; // Small tolerance
        }
        
        /// <summary>
        /// Get the ground Y position for this agent
        /// </summary>
        public float GetGroundY()
        {
            Vector3 rayStart = transform.position + Vector3.up * raycastDistance;
            Ray ray = new Ray(rayStart, Vector3.down);
            
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance * 2f, groundLayerMask))
            {
                return hit.point.y;
            }
            
            return 0f; // Default ground level
        }
        
        /// <summary>
        /// Debug visualization
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;
            
            // Draw original position
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(originalPosition, 0.2f);
            
            // Draw current position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            
            // Draw raycast line
            if (useRaycastForGroundDetection)
            {
                Gizmos.color = Color.yellow;
                Vector3 rayStart = transform.position + Vector3.up * raycastDistance;
                Gizmos.DrawLine(rayStart, rayStart + Vector3.down * raycastDistance * 2f);
            }
            
            // Draw capsule bounds
            if (agentCapsule != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 capsuleCenter = transform.position + agentCapsule.center;
                float halfHeight = agentCapsule.height * 0.5f;
                
                // Draw capsule outline
                Gizmos.DrawWireSphere(capsuleCenter + Vector3.up * halfHeight, agentCapsule.radius);
                Gizmos.DrawWireSphere(capsuleCenter - Vector3.up * halfHeight, agentCapsule.radius);
                Gizmos.DrawLine(capsuleCenter + Vector3.up * halfHeight, capsuleCenter - Vector3.up * halfHeight);
            }
        }
        
        #if UNITY_EDITOR
        [Header("Editor Tools")]
        [SerializeField] private bool needsFixDisplay;
        [SerializeField] private float groundYDisplay;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                needsFixDisplay = NeedsPositionFix();
                groundYDisplay = GetGroundY();
            }
        }
        #endif
    }
}
