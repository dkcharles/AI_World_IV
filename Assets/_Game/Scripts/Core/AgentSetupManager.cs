using UnityEngine;
using AIWorld.Agents;
using System.Collections.Generic;

namespace AIWorld.Core
{
    /// <summary>
    /// Manager to fix positioning and setup issues for all agents in the scene
    /// Provides batch operations for common agent setup problems
    /// </summary>
    public class AgentSetupManager : MonoBehaviour
    {
        [Header("Batch Fix Settings")]
        public bool autoFixOnStart = true;
        public bool addPositionFixComponents = true;
        public bool fixAgentPositions = true;
        public bool ensureNavMeshAgents = true;
        
        [Header("Position Settings")]
        public float groundLevel = 0f;
        public float capsuleHeightOffset = 1f; // Default capsule height
        
        [Header("Debug")]
        public bool logAllOperations = true;
        public bool showAgentCount = true;
        
        [Header("Status")]
        [SerializeField] private int totalAgents;
        [SerializeField] private int fixedAgents;
        [SerializeField] private int agentsNeedingFix;
        
        private void Start()
        {
            if (autoFixOnStart)
            {
                PerformBatchAgentFix();
            }
        }
        
        /// <summary>
        /// Fix all agents in the scene
        /// </summary>
        [ContextMenu("Fix All Agents")]
        public void PerformBatchAgentFix()
        {
            var allAgents = FindObjectsByType<Agent>(FindObjectsSortMode.None);
            totalAgents = allAgents.Length;
            fixedAgents = 0;
            agentsNeedingFix = 0;
            
            if (logAllOperations)
            {
                Debug.Log($"🔧 AgentSetupManager: Starting batch fix for {totalAgents} agents");
            }
            
            foreach (var agent in allAgents)
            {
                FixSingleAgent(agent);
            }
            
            if (logAllOperations)
            {
                Debug.Log($"✅ AgentSetupManager: Completed batch fix. Fixed {fixedAgents}/{totalAgents} agents");
            }
        }
        
        /// <summary>
        /// Fix a single agent's setup issues
        /// </summary>
        private void FixSingleAgent(Agent agent)
        {
            bool agentWasFixed = false;
            string agentName = agent.gameObject.name;
            
            // 1. Add AgentPositionFix component if needed
            if (addPositionFixComponents)
            {
                var positionFix = agent.GetComponent<AgentPositionFix>();
                if (positionFix == null)
                {
                    positionFix = agent.gameObject.AddComponent<AgentPositionFix>();
                    agentWasFixed = true;
                    
                    if (logAllOperations)
                    {
                        Debug.Log($"➕ {agentName}: Added AgentPositionFix component");
                    }
                }
            }
            
            // 2. Fix agent position
            if (fixAgentPositions)
            {
                Vector3 originalPosition = agent.transform.position;
                Vector3 fixedPosition = FixAgentPosition(agent);
                
                if (Vector3.Distance(originalPosition, fixedPosition) > 0.01f)
                {
                    agent.transform.position = fixedPosition;
                    agentWasFixed = true;
                    agentsNeedingFix++;
                    
                    // Update NavMeshAgent if present
                    var navAgent = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (navAgent != null && navAgent.enabled)
                    {
                        navAgent.Warp(fixedPosition);
                    }
                    
                    if (logAllOperations)
                    {
                        Debug.Log($"📍 {agentName}: Fixed position from Y={originalPosition.y:F2} to Y={fixedPosition.y:F2}");
                    }
                }
            }
            
            // 3. Ensure NavMeshAgent is properly configured
            if (ensureNavMeshAgents)
            {
                var navAgent = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent != null)
                {
                    // Ensure NavMeshAgent is enabled and properly configured
                    if (!navAgent.enabled)
                    {
                        navAgent.enabled = true;
                        agentWasFixed = true;
                        
                        if (logAllOperations)
                        {
                            Debug.Log($"🧭 {agentName}: Enabled NavMeshAgent");
                        }
                    }
                    
                    // Set reasonable defaults if needed
                    if (navAgent.speed <= 0) navAgent.speed = 3.5f;
                    if (navAgent.stoppingDistance <= 0) navAgent.stoppingDistance = 1.5f;
                }
            }
            
            if (agentWasFixed)
            {
                fixedAgents++;
            }
        }
        
        /// <summary>
        /// Calculate correct position for an agent
        /// </summary>
        private Vector3 FixAgentPosition(Agent agent)
        {
            Vector3 currentPosition = agent.transform.position;
            
            // Try to get capsule collider for accurate positioning
            var capsuleCollider = agent.GetComponent<CapsuleCollider>();
            float heightOffset = capsuleHeightOffset;
            
            if (capsuleCollider != null)
            {
                heightOffset = capsuleCollider.height * 0.5f + capsuleCollider.center.y;
            }
            
            // Use raycast to find ground if possible
            Vector3 rayStart = currentPosition + Vector3.up * 10f;
            Ray ray = new Ray(rayStart, Vector3.down);
            
            if (Physics.Raycast(ray, out RaycastHit hit, 20f))
            {
                return hit.point + Vector3.up * heightOffset;
            }
            else
            {
                // Fallback to ground level + height offset
                return new Vector3(currentPosition.x, groundLevel + heightOffset, currentPosition.z);
            }
        }
        
        /// <summary>
        /// Check all agents and report issues
        /// </summary>
        [ContextMenu("Check Agent Status")]
        public void CheckAllAgentStatus()
        {
            var allAgents = FindObjectsByType<Agent>(FindObjectsSortMode.None);
            int agentsWithIssues = 0;
            int agentsBelowGround = 0;
            int agentsWithoutNavMesh = 0;
            
            foreach (var agent in allAgents)
            {
                bool hasIssues = false;
                string agentName = agent.gameObject.name;
                
                // Check if agent is below ground
                var capsule = agent.GetComponent<CapsuleCollider>();
                float agentBottom = agent.transform.position.y;
                if (capsule != null)
                {
                    agentBottom -= (capsule.height * 0.5f);
                }
                
                if (agentBottom < groundLevel + 0.1f)
                {
                    agentsBelowGround++;
                    hasIssues = true;
                    Debug.LogWarning($"⚠️ {agentName}: Agent appears to be below ground (bottom at Y={agentBottom:F2})");
                }
                
                // Check NavMeshAgent
                var navAgent = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent == null || !navAgent.enabled)
                {
                    agentsWithoutNavMesh++;
                    hasIssues = true;
                    Debug.LogWarning($"⚠️ {agentName}: NavMeshAgent is missing or disabled");
                }
                
                if (hasIssues)
                {
                    agentsWithIssues++;
                }
            }
            
            Debug.Log($"📊 Agent Status Report:");
            Debug.Log($"   Total Agents: {allAgents.Length}");
            Debug.Log($"   Agents with Issues: {agentsWithIssues}");
            Debug.Log($"   Agents Below Ground: {agentsBelowGround}");
            Debug.Log($"   Agents without NavMesh: {agentsWithoutNavMesh}");
        }
        
        /// <summary>
        /// Reset all agents to their original positions
        /// </summary>
        [ContextMenu("Reset All Agent Positions")]
        public void ResetAllAgentPositions()
        {
            var allAgents = FindObjectsByType<Agent>(FindObjectsSortMode.None);
            
            foreach (var agent in allAgents)
            {
                var positionFix = agent.GetComponent<AgentPositionFix>();
                if (positionFix != null)
                {
                    positionFix.ResetToOriginalPosition();
                }
            }
            
            if (logAllOperations)
            {
                Debug.Log($"🔄 Reset positions for {allAgents.Length} agents");
            }
        }
        
        /// <summary>
        /// Force fix all agent positions right now
        /// </summary>
        [ContextMenu("Force Fix All Positions")]
        public void ForceFixAllPositions()
        {
            var allAgents = FindObjectsByType<Agent>(FindObjectsSortMode.None);
            int fixedCount = 0;
            
            foreach (var agent in allAgents)
            {
                Vector3 originalPos = agent.transform.position;
                Vector3 fixedPos = FixAgentPosition(agent);
                
                if (Vector3.Distance(originalPos, fixedPos) > 0.01f)
                {
                    agent.transform.position = fixedPos;
                    
                    // Update NavMeshAgent
                    var navAgent = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (navAgent != null && navAgent.enabled)
                    {
                        navAgent.Warp(fixedPos);
                    }
                    
                    fixedCount++;
                }
            }
            
            Debug.Log($"🔧 Force-fixed positions for {fixedCount}/{allAgents.Length} agents");
        }
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && showAgentCount)
            {
                var allAgents = FindObjectsByType<Agent>(FindObjectsSortMode.None);
                totalAgents = allAgents.Length;
            }
        }
        #endif
    }
}
