using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;
using AIWorld.Experimental;

namespace AIWorld.Environment
{
    /// <summary>
    /// Detects when agents interact with resource trigger areas
    /// Manages resource collection and provides resource-based benefits
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ResourceTriggerDetector : MonoBehaviour
    {
        [Header("Resource Configuration")]
        public string resourceName;
        public float collectionTime = 2f;
        public bool autoCollectOnEnter = false;
        public bool requiresInteraction = true;
        
        [Header("Collection Settings")]
        public float collectionRange = 2f;
        public int maxSimultaneousCollectors = 1;
        public float regenerationDelay = 10f;
        
        [Header("Debug")]
        public bool logCollectionEvents = true;
        public bool showCollectionIndicators = true;
        
        // State tracking
        private WorldResourceComponent resourceComponent;
        private System.Collections.Generic.List<Agent> nearbyAgents;
        private System.Collections.Generic.List<Agent> collectingAgents;
        private System.Collections.Generic.Dictionary<Agent, float> collectionStartTimes;
        private bool isBeingCollected;
        private bool isAvailable = true;
        private float lastCollectionTime;
        
        // Properties
        public bool IsAvailable => isAvailable && resourceComponent != null;
        public bool IsBeingCollected => isBeingCollected;
        public int CurrentCollectors => collectingAgents?.Count ?? 0;
        public bool HasCapacityForCollection => CurrentCollectors < maxSimultaneousCollectors;
        
        private void Awake()
        {
            nearbyAgents = new System.Collections.Generic.List<Agent>();
            collectingAgents = new System.Collections.Generic.List<Agent>();
            collectionStartTimes = new System.Collections.Generic.Dictionary<Agent, float>();
            
            resourceComponent = GetComponent<WorldResourceComponent>();
            
            // Ensure collider is set as trigger
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            
            // Get resource name from component if not manually set
            if (string.IsNullOrEmpty(resourceName) && resourceComponent != null)
            {
                resourceName = resourceComponent.ResourceConfig?.name ?? gameObject.name;
            }
        }
        
        private void Update()
        {
            ProcessOngoingCollections();
            ProcessAutoRegeneration();
        }
        
        private void OnTriggerEnter(Collider other)
        {
            var agent = other.GetComponent<Agent>();
            if (agent != null && !nearbyAgents.Contains(agent))
            {
                OnAgentNearby(agent);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            var agent = other.GetComponent<Agent>();
            if (agent != null && nearbyAgents.Contains(agent))
            {
                OnAgentLeft(agent);
            }
        }
        
        /// <summary>
        /// Handle agent entering resource area
        /// </summary>
        private void OnAgentNearby(Agent agent)
        {
            nearbyAgents.Add(agent);
            
            if (logCollectionEvents)
            {
                Debug.Log($"📦 Agent {agent.AgentName} approached resource '{resourceName}' " +
                         $"(Available: {IsAvailable})");
            }
            
            // Update agent's beliefs about discovered resource
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("resource_nearby", 
                    new[] { resourceName, "discovered" }, 
                    1f, $"I found {resourceName} nearby");
                    
                if (IsAvailable)
                {
                    agent.BDI.AddBelief("resource_available", 
                        new[] { resourceName, "collectible" }, 
                        0.9f, $"{resourceName} is available for collection");
                }
            }
            
            // Auto-collect if enabled
            if (autoCollectOnEnter && IsAvailable && HasCapacityForCollection)
            {
                StartCollection(agent);
            }
        }
        
        /// <summary>
        /// Handle agent leaving resource area
        /// </summary>
        private void OnAgentLeft(Agent agent)
        {
            nearbyAgents.Remove(agent);
            
            // Cancel collection if agent was collecting
            if (collectingAgents.Contains(agent))
            {
                CancelCollection(agent, "Agent left area");
            }
            
            if (logCollectionEvents)
            {
                Debug.Log($"📦 Agent {agent.AgentName} left resource '{resourceName}' area");
            }
            
            // Update agent's beliefs
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("resource_nearby", 
                    new[] { resourceName, "out_of_range" }, 
                    0.3f, $"{resourceName} is no longer nearby");
            }
        }
        
        /// <summary>
        /// Start resource collection for an agent
        /// </summary>
        public bool StartCollection(Agent agent)
        {
            if (!CanStartCollection(agent))
            {
                return false;
            }
            
            collectingAgents.Add(agent);
            collectionStartTimes[agent] = Time.time;
            isBeingCollected = true;
            
            if (logCollectionEvents)
            {
                Debug.Log($"⛏️ Agent {agent.AgentName} started collecting '{resourceName}' " +
                         $"(ETA: {collectionTime:F1}s)");
            }
            
            // Update agent's beliefs about collection
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("collecting_resource", 
                    new[] { agent.AgentName, resourceName }, 
                    1f, $"I am collecting {resourceName}");
            }
            
            return true;
        }
        
        /// <summary>
        /// Cancel resource collection for an agent
        /// </summary>
        public void CancelCollection(Agent agent, string reason = "Collection cancelled")
        {
            if (!collectingAgents.Contains(agent)) return;
            
            collectingAgents.Remove(agent);
            collectionStartTimes.Remove(agent);
            
            if (collectingAgents.Count == 0)
            {
                isBeingCollected = false;
            }
            
            if (logCollectionEvents)
            {
                Debug.Log($"❌ Agent {agent.AgentName} cancelled collecting '{resourceName}': {reason}");
            }
            
            // Update agent's beliefs
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("collection_cancelled", 
                    new[] { resourceName, reason }, 
                    0.7f, $"Collection of {resourceName} was cancelled");
            }
        }
        
        /// <summary>
        /// Process ongoing collections
        /// </summary>
        private void ProcessOngoingCollections()
        {
            if (collectingAgents.Count == 0) return;
            
            var completedCollections = new System.Collections.Generic.List<Agent>();
            
            foreach (var agent in collectingAgents)
            {
                if (collectionStartTimes.TryGetValue(agent, out var startTime))
                {
                    var elapsedTime = Time.time - startTime;
                    
                    if (elapsedTime >= collectionTime)
                    {
                        completedCollections.Add(agent);
                    }
                }
            }
            
            // Complete collections
            foreach (var agent in completedCollections)
            {
                CompleteCollection(agent);
            }
        }
        
        /// <summary>
        /// Complete resource collection for an agent
        /// </summary>
        private void CompleteCollection(Agent agent)
        {
            if (!collectingAgents.Contains(agent)) return;
            
            // Remove from collecting list
            collectingAgents.Remove(agent);
            collectionStartTimes.Remove(agent);
            
            if (collectingAgents.Count == 0)
            {
                isBeingCollected = false;
            }
            
            // Provide resource benefits to agent
            ProvideResourceBenefits(agent);
            
            // Mark resource as consumed (temporarily unavailable)
            ConsumeResource();
            
            if (logCollectionEvents)
            {
                Debug.Log($"✅ Agent {agent.AgentName} successfully collected '{resourceName}'");
            }
            
            // Update agent's beliefs about successful collection
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("resource_collected", 
                    new[] { agent.AgentName, resourceName }, 
                    1f, $"I successfully collected {resourceName}");
                    
                agent.BDI.AddBelief("collecting_resource", 
                    new[] { agent.AgentName, resourceName }, 
                    0f, "Collection completed"); // Remove active collection belief
            }
        }
        
        /// <summary>
        /// Provide resource benefits to collecting agent
        /// </summary>
        private void ProvideResourceBenefits(Agent agent)
        {
            if (agent.Needs == null || resourceComponent?.ResourceConfig == null) return;
            
            var resourceConfig = resourceComponent.ResourceConfig;
            
            // Provide need satisfaction based on resource type
            switch (resourceConfig.type)
            {
                case ResourceType.Essential:
                    agent.Needs.SatisfyNeedsFromAction("EssentialResource", 0.4f);
                    agent.Needs.SatisfyNeedsFromAction("Physiological", 0.3f);
                    break;
                    
                case ResourceType.Consumable:
                    agent.Needs.SatisfyNeedsFromAction("Consumable", 0.3f);
                    agent.Needs.SatisfyNeedsFromAction("Energy", 0.2f);
                    break;
                    
                case ResourceType.Tool:
                    agent.Needs.SatisfyNeedsFromAction("ToolAcquisition", 0.2f);
                    agent.Needs.SatisfyNeedsFromAction("Achievement", 0.1f);
                    break;
                    
                case ResourceType.Currency:
                    agent.Needs.SatisfyNeedsFromAction("WealthGain", 0.3f);
                    agent.Needs.SatisfyNeedsFromAction("Security", 0.2f);
                    break;
                    
                case ResourceType.Social:
                    agent.Needs.SatisfyNeedsFromSocialInteraction("resource_sharing", 0.3f);
                    break;
                    
                case ResourceType.Achievement:
                    agent.Needs.SatisfyNeedsFromAction("Achievement", 0.4f);
                    agent.Needs.SatisfyNeedsFromAction("Self-actualization", 0.2f);
                    break;
                    
                case ResourceType.Special:
                    agent.Needs.SatisfyNeedsFromAction("SpecialResource", 0.5f);
                    agent.Needs.SatisfyNeedsFromAction("Self-actualization", 0.3f);
                    break;
            }
            
            // Additional satisfaction based on resource value
            var valueBenefit = Mathf.Clamp01(resourceConfig.value / 100f) * 0.2f;
            agent.Needs.SatisfyNeedsFromAction($"ResourceValue_{resourceName}", valueBenefit);
        }
        
        /// <summary>
        /// Mark resource as consumed and unavailable
        /// </summary>
        private void ConsumeResource()
        {
            isAvailable = false;
            lastCollectionTime = Time.time;
            
            // Hide visual representation
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
            
            // Notify resource component
            if (resourceComponent != null)
            {
                // The WorldResourceComponent handles its own collection logic
                // We can subscribe to its events or provide additional functionality
                // For now, just log that we detected the consumption
                Debug.Log($"📦 ResourceTriggerDetector: Detected consumption of {resourceName}");
            }
            
            if (logCollectionEvents)
            {
                Debug.Log($"📦 Resource '{resourceName}' consumed, regenerating in {regenerationDelay:F1}s");
            }
        }
        
        /// <summary>
        /// Process automatic regeneration
        /// </summary>
        private void ProcessAutoRegeneration()
        {
            if (isAvailable || regenerationDelay <= 0f) return;
            
            var timeSinceCollection = Time.time - lastCollectionTime;
            if (timeSinceCollection >= regenerationDelay)
            {
                RegenerateResource();
            }
        }
        
        /// <summary>
        /// Regenerate the resource
        /// </summary>
        public void RegenerateResource()
        {
            isAvailable = true;
            
            // Show visual representation
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }
            
            // Notify resource component
            if (resourceComponent != null)
            {
                // The WorldResourceComponent handles its own regeneration logic
                // We can subscribe to its events or provide additional functionality
                // For now, just log that we detected the regeneration
                Debug.Log($"🔄 ResourceTriggerDetector: Detected regeneration of {resourceName}");
            }
            
            if (logCollectionEvents)
            {
                Debug.Log($"🔄 Resource '{resourceName}' regenerated and available");
            }
            
            // Update beliefs of nearby agents
            foreach (var agent in nearbyAgents)
            {
                if (agent?.BDI != null)
                {
                    agent.BDI.AddBelief("resource_available", 
                        new[] { resourceName, "regenerated" }, 
                        0.9f, $"{resourceName} has regenerated and is available");
                }
            }
        }
        
        /// <summary>
        /// Check if agent can start collection
        /// </summary>
        private bool CanStartCollection(Agent agent)
        {
            if (!IsAvailable)
            {
                if (logCollectionEvents)
                {
                    Debug.Log($"❌ Agent {agent.AgentName} cannot collect '{resourceName}': Not available");
                }
                return false;
            }
            
            if (!HasCapacityForCollection)
            {
                if (logCollectionEvents)
                {
                    Debug.Log($"❌ Agent {agent.AgentName} cannot collect '{resourceName}': Too many collectors");
                }
                return false;
            }
            
            if (collectingAgents.Contains(agent))
            {
                if (logCollectionEvents)
                {
                    Debug.Log($"❌ Agent {agent.AgentName} is already collecting '{resourceName}'");
                }
                return false;
            }
            
            if (!nearbyAgents.Contains(agent))
            {
                if (logCollectionEvents)
                {
                    Debug.Log($"❌ Agent {agent.AgentName} cannot collect '{resourceName}': Not in range");
                }
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Get resource status information for debugging
        /// </summary>
        public string GetResourceStatus()
        {
            var status = $"Resource '{resourceName}': ";
            
            if (!IsAvailable)
            {
                var timeUntilRegen = regenerationDelay - (Time.time - lastCollectionTime);
                status += $"Regenerating ({timeUntilRegen:F1}s remaining)";
            }
            else if (IsBeingCollected)
            {
                status += $"Being collected by {CurrentCollectors} agent(s)";
            }
            else
            {
                status += $"Available ({nearbyAgents.Count} nearby agents)";
            }
            
            return status;
        }
        
        /// <summary>
        /// Force regenerate resource (for testing/management)
        /// </summary>
        [ContextMenu("Force Regenerate")]
        public void ForceRegenerate()
        {
            RegenerateResource();
        }
        
        /// <summary>
        /// Force consume resource (for testing/management)
        /// </summary>
        [ContextMenu("Force Consume")]
        public void ForceConsume()
        {
            // Cancel all ongoing collections
            var collectorsToCancel = new System.Collections.Generic.List<Agent>(collectingAgents);
            foreach (var agent in collectorsToCancel)
            {
                CancelCollection(agent, "Forced consumption");
            }
            
            ConsumeResource();
        }
        
        private void OnDrawGizmos()
        {
            if (!showCollectionIndicators || !Application.isPlaying) return;
            
            // Draw availability indicator
            Gizmos.color = IsAvailable ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.3f);
            
            // Draw collection range
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, collectionRange);
            
            // Draw lines to collecting agents
            if (IsBeingCollected)
            {
                Gizmos.color = Color.yellow;
                foreach (var agent in collectingAgents)
                {
                    if (agent != null)
                    {
                        Gizmos.DrawLine(transform.position + Vector3.up, agent.transform.position + Vector3.up);
                    }
                }
            }
        }
        
        #if UNITY_EDITOR
        [Header("Editor Debug Info")]
        [SerializeField] private string currentResourceStatus;
        [SerializeField] private int nearbyAgentCount;
        [SerializeField] private int collectingAgentCount;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                currentResourceStatus = GetResourceStatus();
                nearbyAgentCount = nearbyAgents?.Count ?? 0;
                collectingAgentCount = collectingAgents?.Count ?? 0;
            }
        }
        #endif
    }
}
