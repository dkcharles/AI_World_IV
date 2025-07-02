using UnityEngine;
using AIWorld.Agents;

namespace AIWorld.Core
{
    /// <summary>
    /// Simple script to quickly fix agent positioning issues
    /// Add this to any GameObject and run the context menu functions
    /// </summary>
    public class QuickAgentFix : MonoBehaviour
    {
        [Header("Quick Fix Settings")]
        public float groundOffset = 1f; // How high above ground to place agents
        
        /// <summary>
        /// Fix all agent positions immediately
        /// </summary>
        [ContextMenu("Quick Fix All Agent Positions")]
        public void QuickFixAllAgents()
        {
            var allAgents = FindObjectsByType<Agent>(FindObjectsSortMode.None);
            int fixedCount = 0;
            
            foreach (var agent in allAgents)
            {
                Vector3 currentPos = agent.transform.position;
                Vector3 fixedPos = new Vector3(currentPos.x, groundOffset, currentPos.z);
                
                agent.transform.position = fixedPos;
                
                // Update NavMeshAgent if present
                var navAgent = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.Warp(fixedPos);
                }
                
                fixedCount++;
            }
            
            Debug.Log($"🔧 Quick-fixed {fixedCount} agents to ground level Y={groundOffset}");
        }
        
        /// <summary>
        /// Show agent positions for debugging
        /// </summary>
        [ContextMenu("Show All Agent Positions")]
        public void ShowAllAgentPositions()
        {
            var allAgents = FindObjectsByType<Agent>(FindObjectsSortMode.None);
            
            Debug.Log($"📊 Current Agent Positions ({allAgents.Length} agents):");
            foreach (var agent in allAgents)
            {
                Vector3 pos = agent.transform.position;
                Debug.Log($"   🤖 {agent.gameObject.name}: X={pos.x:F1}, Y={pos.y:F1}, Z={pos.z:F1}");
            }
        }
    }
}
