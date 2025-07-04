using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Agents;
using AIWorld.Environment;
using AIWorld.Needs;
using AIWorld.BDI;

namespace AIWorld.Environment
{
    /// <summary>
    /// Advanced interaction manager that coordinates agent-environment interactions
    /// Based on agent needs, spatial awareness, and intelligent discovery
    /// </summary>
    public class SmartInteractionManager : MonoBehaviour
    {
        [Header("Interaction Configuration")]
        public bool enableSmartInteractions = true;
        public float interactionScanInterval = 10f;
        public float needSatisfactionThreshold = 0.6f;
        public bool prioritizeUrgentNeeds = true;
        
        [Header("Discovery Settings")]
        public float maxDiscoveryRange = 15f;
        public int maxInteractionsPerCycle = 3;
        public bool enableGroupInteractions = true;
        
        [Header("Debug")]
        public bool logInteractionDecisions = true;
        public bool showInteractionPaths = true;
        
        // Tracking
        private Dictionary<Agent, List<InteractableObject>> agentDiscoveredObjects;
        private Dictionary<Agent, float> lastInteractionTime;
        private List<InteractableObject> allInteractableObjects;
        
        // Properties
        public int TotalInteractableObjects => allInteractableObjects?.Count ?? 0;
        public int ActiveInteractions { get; private set; }
        
        private void Start()
        {
            InitializeInteractionSystem();
            
            if (enableSmartInteractions)
            {
                InvokeRepeating(nameof(ProcessSmartInteractions), 2f, interactionScanInterval);
            }
            
            Debug.Log($"🎯 SmartInteractionManager initialized with {TotalInteractableObjects} objects");
        }
        
        /// <summary>
        /// Initialize the interaction system
        /// </summary>
        private void InitializeInteractionSystem()
        {
            agentDiscoveredObjects = new Dictionary<Agent, List<InteractableObject>>();
            lastInteractionTime = new Dictionary<Agent, float>();
            allInteractableObjects = new List<InteractableObject>();
            
            // Find all interactable objects in the scene
            RefreshInteractableObjects();
        }
        
        /// <summary>
        /// Refresh the list of available interactable objects
        /// </summary>
        public void RefreshInteractableObjects()
        {
            allInteractableObjects.Clear();
            allInteractableObjects.AddRange(FindObjectsOfType<InteractableObject>());
            
            if (logInteractionDecisions)
            {
                Debug.Log($"🔄 Refreshed interactable objects: {allInteractableObjects.Count} found");
            }
        }
        
        /// <summary>
        /// Main smart interaction processing loop
        /// </summary>
        private void ProcessSmartInteractions()
        {
            if (!enableSmartInteractions) return;
            
            var gameManager = FindFirstObjectByType<AIWorld.Managers.GameManager>();
            if (gameManager == null) return;
            
            var allAgents = gameManager.GetAllAgents();
            ActiveInteractions = 0;
            
            foreach (var agent in allAgents)
            {
                if (agent != null && ShouldProcessAgent(agent))
                {
                    ProcessAgentInteractions(agent);
                }
            }
        }
        
        /// <summary>
        /// Check if agent should be processed for interactions
        /// </summary>
        private bool ShouldProcessAgent(Agent agent)
        {
            if (agent.Needs == null) return false;
            
            // Don't interrupt agents who are already actively interacting
            if (IsAgentActivelyInteracting(agent)) return false;
            
            // Check if enough time has passed since last interaction
            if (lastInteractionTime.ContainsKey(agent))
            {
                float timeSinceLastInteraction = Time.time - lastInteractionTime[agent];
                if (timeSinceLastInteraction < interactionScanInterval * 0.5f) return false;
            }
            
            // Check if agent has needs that can be satisfied
            var satisfiableNeeds = GetSatisfiableNeeds(agent);
            return satisfiableNeeds.Count > 0;
        }
        
        /// <summary>
        /// Process interactions for a specific agent
        /// </summary>
        private void ProcessAgentInteractions(Agent agent)
        {
            // Update agent's discovered objects
            UpdateAgentDiscovery(agent);
            
            // Get agent's most urgent satisfiable needs
            var urgentNeeds = GetUrgentSatisfiableNeeds(agent);
            if (urgentNeeds.Count == 0) return;
            
            // Find best interaction opportunities
            var bestInteractions = FindBestInteractions(agent, urgentNeeds);
            
            if (bestInteractions.Count > 0)
            {
                ExecuteSmartInteraction(agent, bestInteractions[0]);
            }
        }
        
        /// <summary>
        /// Update what objects an agent has discovered
        /// </summary>
        private void UpdateAgentDiscovery(Agent agent)
        {
            if (!agentDiscoveredObjects.ContainsKey(agent))
            {
                agentDiscoveredObjects[agent] = new List<InteractableObject>();
            }
            
            var discoveredObjects = agentDiscoveredObjects[agent];
            
            // Find objects within discovery range
            foreach (var obj in allInteractableObjects)
            {
                if (obj == null || discoveredObjects.Contains(obj)) continue;
                
                float distance = Vector3.Distance(agent.transform.position, obj.transform.position);
                if (distance <= maxDiscoveryRange)
                {
                    discoveredObjects.Add(obj);
                    
                    if (logInteractionDecisions)
                    {
                        Debug.Log($"🔍 {agent.AgentName} discovered {obj.objectName} at distance {distance:F1}m");
                    }
                    
                    // Add discovery belief to agent's BDI system
                    if (agent.BDI != null)
                    {
                        agent.BDI.AddBelief("object_discovered", 
                            new[] { obj.objectName, obj.primaryInteractionType.ToString() },
                            1f, $"I discovered {obj.objectName} nearby");
                    }
                }
            }
        }
        
        /// <summary>
        /// Get needs that can be satisfied by available objects
        /// </summary>
        private List<Need> GetSatisfiableNeeds(Agent agent)
        {
            if (agent.Needs == null) return new List<Need>();
            
            var satisfiableNeeds = new List<Need>();
            var discoveredObjects = agentDiscoveredObjects.ContainsKey(agent) 
                ? agentDiscoveredObjects[agent] 
                : new List<InteractableObject>();
            
            foreach (var need in agent.Needs.activeNeeds)
            {
                if (need.GetSatisfactionLevel() >= needSatisfactionThreshold) continue;
                
                // Check if any discovered object can satisfy this need
                bool canSatisfy = discoveredObjects.Any(obj => CanObjectSatisfyNeed(obj, need));
                if (canSatisfy)
                {
                    satisfiableNeeds.Add(need);
                }
            }
            
            return satisfiableNeeds;
        }
        
        /// <summary>
        /// Get agent's most urgent satisfiable needs
        /// </summary>
        private List<Need> GetUrgentSatisfiableNeeds(Agent agent)
        {
            var satisfiableNeeds = GetSatisfiableNeeds(agent);
            
            if (prioritizeUrgentNeeds)
            {
                // Sort by urgency (lowest satisfaction level first)
                satisfiableNeeds = satisfiableNeeds
                    .OrderBy(n => n.GetSatisfactionLevel())
                    .ThenByDescending(n => n.GetUrgency())
                    .Take(maxInteractionsPerCycle)
                    .ToList();
            }
            
            return satisfiableNeeds;
        }
        
        /// <summary>
        /// Find best interaction opportunities for agent and needs
        /// </summary>
        private List<InteractionOpportunity> FindBestInteractions(Agent agent, List<Need> needs)
        {
            var opportunities = new List<InteractionOpportunity>();
            var discoveredObjects = agentDiscoveredObjects.ContainsKey(agent) 
                ? agentDiscoveredObjects[agent] 
                : new List<InteractableObject>();
            
            foreach (var need in needs)
            {
                foreach (var obj in discoveredObjects)
                {
                    if (obj == null || !obj.CanInteract(agent)) continue;
                    
                    if (CanObjectSatisfyNeed(obj, need))
                    {
                        var opportunity = new InteractionOpportunity
                        {
                            agent = agent,
                            interactableObject = obj,
                            targetNeed = need,
                            priority = CalculateInteractionPriority(agent, obj, need),
                            distance = Vector3.Distance(agent.transform.position, obj.transform.position)
                        };
                        
                        opportunities.Add(opportunity);
                    }
                }
            }
            
            // Sort by priority (highest first)
            return opportunities.OrderByDescending(o => o.priority).ToList();
        }
        
        /// <summary>
        /// Calculate interaction priority based on multiple factors
        /// </summary>
        private float CalculateInteractionPriority(Agent agent, InteractableObject obj, Need need)
        {
            float priority = 0f;
            
            // Need urgency (0-1)
            priority += need.GetUrgency() * 0.4f;
            
            // Object attractiveness for this agent (0-1)
            priority += obj.GetAttractiveness(agent) * 0.3f;
            
            // Distance factor (closer is better)
            float distance = Vector3.Distance(agent.transform.position, obj.transform.position);
            float distanceFactor = 1f - (distance / maxDiscoveryRange);
            priority += distanceFactor * 0.2f;
            
            // Availability bonus
            if (obj.IsAvailable && !obj.IsInUse)
            {
                priority += 0.1f;
            }
            
            return Mathf.Clamp01(priority);
        }
        
        /// <summary>
        /// Execute a smart interaction
        /// </summary>
        private void ExecuteSmartInteraction(Agent agent, InteractionOpportunity opportunity)
        {
            var obj = opportunity.interactableObject;
            var need = opportunity.targetNeed;
            
            if (logInteractionDecisions)
            {
                Debug.Log($"🎯 {agent.AgentName} executing smart interaction with {obj.objectName} for {need.needName} (priority: {opportunity.priority:F2})");
            }
            
            // Add intention to agent's BDI system
            if (agent.BDI != null)
            {
                var intention = new Intention("InteractWithObject", $"Interact with {obj.objectName}", 1f)
                {
                    intentionType = "interaction",
                    description = $"Use {obj.objectName} to satisfy {need.needName}",
                    priority = opportunity.priority
                };
                
                intention.SetActionPlan(new[] { $"MoveTo:{obj.objectName}", "Interact", "SatisfyNeed" });
                
                agent.BDI.AddIntention(intention);
            }
            
            // Update tracking
            lastInteractionTime[agent] = Time.time;
            ActiveInteractions++;
            
            // Start interaction if agent is close enough
            if (opportunity.distance <= obj.interactionRange)
            {
                obj.StartInteraction(agent);
            }
            else
            {
                // Agent needs to move closer - this integrates with AgentMovement system
                TriggerMovementToObject(agent, obj);
            }
        }
        
        /// <summary>
        /// Trigger agent movement toward an object
        /// </summary>
        private void TriggerMovementToObject(Agent agent, InteractableObject obj)
        {
            var agentMovement = agent.GetComponent<AIWorld.Movement.AgentMovement>();
            if (agentMovement != null)
            {
                var movementGoal = new AIWorld.Movement.MovementGoal
                {
                    targetPosition = obj.transform.position,
                    goalType = AIWorld.Movement.MovementGoalType.Task,
                    purpose = $"interact with {obj.objectName}",
                    targetAgent = null,
                    priority = 0.8f,
                    timeLimit = 30f
                };
                
                agentMovement.SetMovementGoal(movementGoal);
                
                if (logInteractionDecisions)
                {
                    Debug.Log($"🚶 {agent.AgentName} moving to {obj.objectName} for interaction");
                }
            }
        }
        
        /// <summary>
        /// Check if an object can satisfy a specific need
        /// </summary>
        private bool CanObjectSatisfyNeed(InteractableObject obj, Need need)
        {
            if (obj.needSatisfactions == null) return false;
            
            foreach (var satisfaction in obj.needSatisfactions)
            {
                if (satisfaction.needName == "Any" || satisfaction.needName == need.needName)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if agent is currently actively interacting
        /// </summary>
        private bool IsAgentActivelyInteracting(Agent agent)
        {
            // Check if agent has an active interaction intention
            if (agent.BDI != null && agent.BDI.CurrentIntention != null)
            {
                return agent.BDI.CurrentIntention.intentionType == "interaction";
            }
            
            return false;
        }
        
        /// <summary>
        /// Force refresh and reprocess all agent interactions
        /// </summary>
        [ContextMenu("Force Refresh Interactions")]
        public void ForceRefreshInteractions()
        {
            RefreshInteractableObjects();
            ProcessSmartInteractions();
            Debug.Log($"🔄 Force refreshed interactions - {ActiveInteractions} active");
        }
        
        /// <summary>
        /// Get interaction statistics for debugging
        /// </summary>
        public string GetInteractionStats()
        {
            string stats = $"Smart Interactions Active: {ActiveInteractions}\n";
            stats += $"Total Objects: {TotalInteractableObjects}\n";
            stats += $"Agents with Discoveries: {agentDiscoveredObjects.Count}\n";
            
            int totalDiscoveries = agentDiscoveredObjects.Values.Sum(list => list.Count);
            stats += $"Total Discoveries: {totalDiscoveries}";
            
            return stats;
        }
        
        /// <summary>
        /// Debug visualization
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!showInteractionPaths || !Application.isPlaying) return;
            
            // Draw discovery ranges for agents
            var gameManager = FindFirstObjectByType<AIWorld.Managers.GameManager>();
            if (gameManager != null)
            {
                foreach (var agent in gameManager.GetAllAgents())
                {
                    if (agent != null)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawWireSphere(agent.transform.position, maxDiscoveryRange);
                        
                        // Draw lines to discovered objects
                        if (agentDiscoveredObjects.ContainsKey(agent))
                        {
                            Gizmos.color = Color.green;
                            foreach (var obj in agentDiscoveredObjects[agent])
                            {
                                if (obj != null)
                                {
                                    Gizmos.DrawLine(agent.transform.position, obj.transform.position);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        #if UNITY_EDITOR
        [Header("Editor Debug Info")]
        [SerializeField] private string interactionStats;
        [SerializeField] private int activeInteractionCount;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                interactionStats = GetInteractionStats();
                activeInteractionCount = ActiveInteractions;
            }
        }
        #endif
    }
    
    /// <summary>
    /// Data structure for interaction opportunities
    /// </summary>
    [System.Serializable]
    public class InteractionOpportunity
    {
        public Agent agent;
        public InteractableObject interactableObject;
        public Need targetNeed;
        public float priority;
        public float distance;
        public string reasoning;
        
        public override string ToString()
        {
            return $"{agent?.AgentName} -> {interactableObject?.objectName} for {targetNeed?.needName} (Priority: {priority:F2})";
        }
    }
}