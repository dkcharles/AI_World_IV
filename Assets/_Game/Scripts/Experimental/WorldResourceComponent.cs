using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Research;
using AIWorld.Experimental;
using AIWorld.Agents;
using AIWorld.BDI;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Component for individual world resources that agents can collect and compete for
    /// Handles resource regeneration, scarcity mechanics, and usage tracking
    /// </summary>
    public class WorldResourceComponent : MonoBehaviour
    {
        [Header("Resource Configuration")]
        [SerializeField] private WorldResource resourceConfig;
        [SerializeField] private int currentQuantity;
        [SerializeField] private bool isAvailable = true;
        [SerializeField] private bool hasBeenCollected = false;
        
        [Header("Collection Settings")]
        [SerializeField] private float collectionRange = 2f;
        [SerializeField] private float collectionTime = 2f;
        [SerializeField] private bool requiresTools = false;
        [SerializeField] private string[] requiredTools;
        
        [Header("Regeneration")]
        [SerializeField] private float timeSinceCollection = 0f;
        [SerializeField] private float regenerationTimer = 0f;
        [SerializeField] private bool canRegenerate = true;
        
        [Header("Visual Feedback")]
        [SerializeField] private Color availableColor = Color.green;
        [SerializeField] private Color collectingColor = Color.yellow;
        [SerializeField] private Color depletedColor = Color.red;
        [SerializeField] private bool showAvailabilityIndicator = true;
        
        // Runtime data
        private List<ResourceCollectionEvent> collectionHistory = new List<ResourceCollectionEvent>();
        private Dictionary<string, int> agentCollectionCounts = new Dictionary<string, int>();
        private Agent currentCollector = null;
        private float collectionProgress = 0f;
        private Renderer resourceRenderer;
        private Collider resourceCollider;
        
        // Events
        public static event Action<WorldResourceComponent, Agent> OnCollectionStarted;
        public static event Action<WorldResourceComponent, Agent, int> OnResourceCollected;
        public static event Action<WorldResourceComponent> OnResourceDepleted;
        public static event Action<WorldResourceComponent> OnResourceRegenerated;
        
        public WorldResource ResourceConfig => resourceConfig;
        public bool IsAvailable => isAvailable && currentQuantity > 0;
        public bool IsBeingCollected => currentCollector != null;
        public int CurrentQuantity => currentQuantity;
        public float CollectionProgress => collectionProgress;
        public Agent CurrentCollector => currentCollector;
        public float TimeSinceCollection => timeSinceCollection;
        
        private void Awake()
        {
            resourceRenderer = GetComponent<Renderer>();
            resourceCollider = GetComponent<Collider>();
            
            if (resourceCollider == null)
            {
                // Add a trigger collider if none exists
                var sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = collectionRange;
                resourceCollider = sphereCollider;
            }
        }
        
        private void Start()
        {
            UpdateVisualState();
        }
        
        private void Update()
        {
            UpdateRegenerationTimer();
            UpdateCollectionProgress();
            UpdateTimeSinceCollection();
        }
        
        #region Initialization
        
        public void Initialize(WorldResource config)
        {
            resourceConfig = config;
            currentQuantity = config.maxQuantity > 0 ? config.maxQuantity : 1;
            
            // Apply configuration-specific settings
            if (!string.IsNullOrEmpty(config.name))
            {
                gameObject.name = $"Resource_{config.name}";
            }
            
            // Configure regeneration
            canRegenerate = config.regenerationRate > 0f;
            regenerationTimer = 0f;
            
            // Configure collection requirements
            SetupCollectionRequirements(config);
            
            isAvailable = true;
            hasBeenCollected = false;
            
            Debug.Log($"[World Resource] Initialized resource '{config.name}' " +
                     $"(Type: {config.type}, Quantity: {currentQuantity}, Value: {config.value})");
        }
        
        private void SetupCollectionRequirements(WorldResource config)
        {
            // Determine if tools are required based on resource type and context
            requiresTools = config.type switch
            {
                ResourceType.Tool => false, // Tools don't require tools to collect
                ResourceType.Essential => false, // Basic resources are directly collectible
                ResourceType.Consumable => false, // Food/water can be gathered by hand
                ResourceType.Currency => true, // May require paperwork, negotiations
                ResourceType.Achievement => true, // Requires specific skills/tools
                ResourceType.Special => true, // Exclusive resources require special access
                _ => false
            };
            
            if (requiresTools)
            {
                requiredTools = GetRequiredToolsForResource(config);
            }
        }
        
        private string[] GetRequiredToolsForResource(WorldResource config)
        {
            return config.type switch
            {
                ResourceType.Currency => new[] { "Documentation", "Authorization" },
                ResourceType.Achievement => new[] { "Research Skills", "Presentation Tools" },
                ResourceType.Special => new[] { "Access Key", "Permission" },
                _ => new string[0]
            };
        }
        
        #endregion
        
        #region Collection System
        
        private void OnTriggerEnter(Collider other)
        {
            var agent = other.GetComponent<Agent>();
            if (agent != null && CanAgentCollect(agent) && currentCollector == null)
            {
                StartCollection(agent);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            var agent = other.GetComponent<Agent>();
            if (agent != null && currentCollector == agent)
            {
                InterruptCollection("Agent moved away");
            }
        }
        
        public bool CanAgentCollect(Agent agent)
        {
            if (!IsAvailable) return false;
            if (IsBeingCollected) return false;
            
            // Check tool requirements
            if (requiresTools && !HasRequiredTools(agent))
            {
                return false;
            }
            
            // Check agent capacity (if implemented)
            if (!CanAgentCarryMore(agent))
            {
                return false;
            }
            
            // Check collection cooldown for this agent
            if (HasCollectionCooldown(agent))
            {
                return false;
            }
            
            return true;
        }
        
        private bool HasRequiredTools(Agent agent)
        {
            if (requiredTools == null || requiredTools.Length == 0) return true;
            
            // Simple tool check - in a real implementation, this would check agent inventory
            // For now, assume agents have basic tools based on their archetype
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return false;
            
            return identity.Archetype switch
            {
                AgentArchetype.Analyst => true, // Analysts have research tools
                AgentArchetype.Innovator => true, // Innovators have creative tools
                AgentArchetype.Collaborator => requiredTools.Contains("Documentation"), // Good with paperwork
                _ => !requiresTools // Other archetypes can collect non-tool-requiring resources
            };
        }
        
        private bool CanAgentCarryMore(Agent agent)
        {
            // Simple capacity check - assume agents can carry limited resources
            // In a real implementation, this would check agent inventory capacity
            return true; // For now, assume unlimited capacity
        }
        
        private bool HasCollectionCooldown(Agent agent)
        {
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return false;
            
            var agentId = identity.UniqueResearchID;
            var lastCollection = collectionHistory
                .Where(e => e.agentId == agentId)
                .OrderByDescending(e => e.timestamp)
                .FirstOrDefault();
                
            if (lastCollection != null)
            {
                var timeSinceLastCollection = (DateTime.UtcNow - lastCollection.timestamp).TotalSeconds;
                var cooldownPeriod = GetCollectionCooldown();
                return timeSinceLastCollection < cooldownPeriod;
            }
            
            return false;
        }
        
        private float GetCollectionCooldown()
        {
            // Cooldown based on resource scarcity
            return resourceConfig.scarcity * 60f; // 0-60 seconds based on scarcity
        }
        
        public void StartCollection(Agent agent)
        {
            if (!CanAgentCollect(agent)) return;
            
            currentCollector = agent;
            collectionProgress = 0f;
            
            // Update visual state
            UpdateVisualState();
            
            // Fire event
            OnCollectionStarted?.Invoke(this, agent);
            
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            Debug.Log($"[World Resource] {identity?.GetCasualName() ?? agent.name} started collecting {resourceConfig.name}");
        }
        
        private void UpdateCollectionProgress()
        {
            if (currentCollector == null) return;
            
            // Increase collection progress
            collectionProgress += Time.deltaTime / collectionTime;
            
            // Check if collection is complete
            if (collectionProgress >= 1f)
            {
                CompleteCollection();
            }
            
            // Check if collector is still in range
            var distance = Vector3.Distance(transform.position, currentCollector.transform.position);
            if (distance > collectionRange * 1.2f) // Allow slight overshoot
            {
                InterruptCollection("Agent too far away");
            }
        }
        
        private void CompleteCollection()
        {
            if (currentCollector == null) return;
            
            var collector = currentCollector;
            var identity = collector.GetComponent<ResearchValidatedIdentity>();
            var agentId = identity?.UniqueResearchID ?? "Unknown";
            
            // Calculate collection amount
            var collectedAmount = CalculateCollectionAmount(collector);
            
            // Update resource quantity
            currentQuantity = Mathf.Max(0, currentQuantity - collectedAmount);
            
            // Record collection event
            var collectionEvent = new ResourceCollectionEvent
            {
                timestamp = DateTime.UtcNow,
                agentId = agentId,
                resourceName = resourceConfig.name,
                resourceType = resourceConfig.type,
                amountCollected = collectedAmount,
                resourceValueGained = collectedAmount * resourceConfig.value,
                agentNeedState = GetNeedStates(collector),
                collectionMethod = GetCollectionMethod(),
                competitionLevel = CalculateCompetitionLevel()
            };
            
            collectionHistory.Add(collectionEvent);
            if (collectionHistory.Count > 100) // Keep only recent events
            {
                collectionHistory.RemoveAt(0);
            }
            
            // Update agent collection count
            if (!agentCollectionCounts.ContainsKey(agentId))
            {
                agentCollectionCounts[agentId] = 0;
            }
            agentCollectionCounts[agentId] += collectedAmount;
            
            // Apply resource effects to agent
            ApplyResourceEffectsToAgent(collector, collectedAmount);
            
            // Update agent beliefs about resource value
            UpdateAgentResourceBeliefs(collector, collectedAmount);
            
            // Reset collection state
            currentCollector = null;
            collectionProgress = 0f;
            timeSinceCollection = 0f;
            hasBeenCollected = true;
            
            // Check if resource is depleted
            if (currentQuantity <= 0)
            {
                isAvailable = false;
                OnResourceDepleted?.Invoke(this);
            }
            
            // Start regeneration timer
            if (canRegenerate)
            {
                regenerationTimer = CalculateRegenerationTime();
            }
            
            // Update visual state
            UpdateVisualState();
            
            // Fire events
            OnResourceCollected?.Invoke(this, collector, collectedAmount);
            
            Debug.Log($"[World Resource] {identity?.GetCasualName() ?? collector.name} collected {collectedAmount} {resourceConfig.name} " +
                     $"(Remaining: {currentQuantity})");
        }
        
        public void InterruptCollection(string reason)
        {
            if (currentCollector == null) return;
            
            var identity = currentCollector.GetComponent<ResearchValidatedIdentity>();
            Debug.Log($"[World Resource] Collection interrupted for {identity?.GetCasualName() ?? currentCollector.name}: {reason}");
            
            currentCollector = null;
            collectionProgress = 0f;
            
            UpdateVisualState();
        }
        
        #endregion
        
        #region Resource Effects and Value
        
        private int CalculateCollectionAmount(Agent agent)
        {
            // Base collection amount
            var baseAmount = 1;
            
            // Efficiency based on agent archetype
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity != null)
            {
                var efficiency = identity.Archetype switch
                {
                    AgentArchetype.Analyst => resourceConfig.type == ResourceType.Achievement ? 1.5f : 1.0f,
                    AgentArchetype.Innovator => resourceConfig.type == ResourceType.Special ? 1.3f : 1.0f,
                    AgentArchetype.Competitor => resourceConfig.type == ResourceType.Currency ? 1.2f : 1.0f,
                    _ => 1.0f
                };
                
                baseAmount = Mathf.RoundToInt(baseAmount * efficiency);
            }
            
            // Don't collect more than available
            return Mathf.Min(baseAmount, currentQuantity);
        }
        
        private void ApplyResourceEffectsToAgent(Agent agent, int amount)
        {
            // Apply resource-specific effects
            switch (resourceConfig.type)
            {
                case ResourceType.Consumable:
                    // Satisfy physiological needs
                    agent.Needs.SatisfyNeedsFromAction("Physiological", amount * 0.3f);
                    agent.Needs.SatisfyNeedsFromAction("Energy", amount * 0.2f);
                    break;
                    
                case ResourceType.Essential:
                    // Satisfy safety and physiological needs
                    agent.Needs.SatisfyNeedsFromAction("Safety", amount * 0.4f);
                    agent.Needs.SatisfyNeedsFromAction("Physiological", amount * 0.3f);
                    break;
                    
                case ResourceType.Currency:
                    // Satisfy achievement and esteem needs
                    agent.Needs.SatisfyNeedsFromAction("Achievement", amount * 0.3f);
                    agent.Needs.SatisfyNeedsFromAction("Esteem", amount * 0.2f);
                    break;
                    
                case ResourceType.Achievement:
                    // Satisfy achievement and self-actualization needs
                    agent.Needs.SatisfyNeedsFromAction("Achievement", amount * 0.5f);
                    agent.Needs.SatisfyNeedsFromAction("Self-actualization", amount * 0.3f);
                    break;
                    
                case ResourceType.Social:
                    // Satisfy social and esteem needs
                    agent.Needs.SatisfyNeedsFromSocialInteraction("Social", amount * 0.4f);
                    agent.Needs.SatisfyNeedsFromSocialInteraction("Esteem", amount * 0.3f);
                    break;
                    
                case ResourceType.Tool:
                    // Improve efficiency for future actions
                    agent.Needs.SatisfyNeedsFromAction("Achievement", amount * 0.2f);
                    break;
            }
            
            // Apply mood effects
            var moodSystem = agent.GetComponent<AgentMoodSystem>();
            if (moodSystem != null)
            {
                var moodBoost = amount * resourceConfig.value * 0.001f; // Scale appropriately
                moodSystem.TriggerConversationMoodResponse(true, moodBoost);
            }
        }
        
        private void UpdateAgentResourceBeliefs(Agent agent, int amount)
        {
            var bdiEngine = agent.GetComponent<BDIEngine>();
            if (bdiEngine == null) return;
            
            // Update belief about resource value
            var valueRating = CalculateResourceValueRating(amount);
            var beliefText = $"Resource {resourceConfig.name} has value rating {valueRating:F1}";
            bdiEngine.AddBelief("resource_value", 
                new[] { resourceConfig.name, valueRating.ToString("F1") }, 
                0.8f, beliefText);
            
            // Update belief about resource location
            var locationBelief = $"Resource {resourceConfig.name} found at {transform.position}";
            bdiEngine.AddBelief("resource_location", 
                new[] { resourceConfig.name, transform.position.ToString() }, 
                0.9f, locationBelief);
        }
        
        private float CalculateResourceValueRating(int amount)
        {
            // Value rating from 1-10 based on resource properties
            var baseValue = (float)resourceConfig.value / 100f; // Normalize
            var scarcityBonus = resourceConfig.scarcity * 2f; // Scarce = more valuable
            var amountBonus = amount * 0.5f;
            
            return Mathf.Clamp(baseValue + scarcityBonus + amountBonus, 1f, 10f);
        }
        
        private string GetCollectionMethod()
        {
            if (requiresTools && requiredTools != null && requiredTools.Length > 0)
            {
                return $"Using {string.Join(", ", requiredTools)}";
            }
            
            return resourceConfig.type switch
            {
                ResourceType.Consumable => "Direct gathering",
                ResourceType.Essential => "Careful extraction",
                ResourceType.Currency => "Negotiation and paperwork",
                ResourceType.Achievement => "Skill demonstration",
                ResourceType.Social => "Social interaction",
                ResourceType.Tool => "Physical collection",
                _ => "Standard collection"
            };
        }
        
        private float CalculateCompetitionLevel()
        {
            // Competition level based on recent collection attempts
            var recentCollections = collectionHistory
                .Where(e => (DateTime.UtcNow - e.timestamp).TotalMinutes < 10)
                .Count();
                
            return Mathf.Clamp01(recentCollections / 5f); // 0-1 scale
        }
        
        #endregion
        
        #region Regeneration System
        
        private void UpdateRegenerationTimer()
        {
            if (!canRegenerate || isAvailable) return;
            
            regenerationTimer -= Time.deltaTime;
            
            if (regenerationTimer <= 0f)
            {
                RegenerateResource();
            }
        }
        
        private void UpdateTimeSinceCollection()
        {
            if (hasBeenCollected)
            {
                timeSinceCollection += Time.deltaTime;
            }
        }
        
        private float CalculateRegenerationTime()
        {
            if (resourceConfig.regenerationRate <= 0f) return float.MaxValue;
            
            // Base regeneration time
            var baseTime = 1f / resourceConfig.regenerationRate;
            
            // Modify based on scarcity (scarce resources take longer)
            var scarcityMultiplier = 1f + resourceConfig.scarcity;
            
            // Modify based on competition (high competition = slower regen)
            var competitionLevel = CalculateCompetitionLevel();
            var competitionMultiplier = 1f + (competitionLevel * 0.5f);
            
            return baseTime * scarcityMultiplier * competitionMultiplier;
        }
        
        private void RegenerateResource()
        {
            var previousQuantity = currentQuantity;
            
            // Restore to maximum quantity
            currentQuantity = resourceConfig.maxQuantity > 0 ? resourceConfig.maxQuantity : 1;
            isAvailable = true;
            regenerationTimer = 0f;
            
            // Update visual state
            UpdateVisualState();
            
            // Fire event
            OnResourceRegenerated?.Invoke(this);
            
            Debug.Log($"[World Resource] {resourceConfig.name} regenerated " +
                     $"({previousQuantity} -> {currentQuantity})");
        }
        
        #endregion
        
        #region Visual Management
        
        private void UpdateVisualState()
        {
            if (!showAvailabilityIndicator || resourceRenderer == null) return;
            
            var targetColor = GetStatusColor();
            
            if (resourceRenderer.material.color != targetColor)
            {
                resourceRenderer.material.color = targetColor;
            }
            
            // Scale based on quantity
            if (resourceConfig.maxQuantity > 1)
            {
                var scaleFactor = 0.5f + ((float)currentQuantity / resourceConfig.maxQuantity * 0.5f);
                transform.localScale = Vector3.one * scaleFactor;
            }
        }
        
        private Color GetStatusColor()
        {
            if (!isAvailable || currentQuantity <= 0) return depletedColor;
            if (IsBeingCollected) return collectingColor;
            return availableColor;
        }
        
        #endregion
        
        #region Research Data
        
        /// <summary>
        /// Get research data for this resource
        /// </summary>
        public ResourceResearchData GetResearchData()
        {
            var totalCollected = collectionHistory.Sum(e => e.amountCollected);
            var totalValue = collectionHistory.Sum(e => e.resourceValueGained);
            var uniqueCollectors = collectionHistory.Select(e => e.agentId).Distinct().Count();
            var averageCompetition = collectionHistory.Count > 0 ? 
                collectionHistory.Average(e => e.competitionLevel) : 0f;
                
            return new ResourceResearchData
            {
                resourceName = resourceConfig.name,
                resourceType = resourceConfig.type,
                baseValue = resourceConfig.value,
                scarcityLevel = resourceConfig.scarcity,
                currentQuantity = currentQuantity,
                maxQuantity = resourceConfig.maxQuantity,
                totalCollected = totalCollected,
                totalValueGenerated = totalValue,
                uniqueCollectors = uniqueCollectors,
                averageCompetitionLevel = averageCompetition,
                timeSinceLastCollection = timeSinceCollection,
                regenerationRate = resourceConfig.regenerationRate,
                recentCollections = collectionHistory.TakeLast(10).ToList()
            };
        }
        
        /// <summary>
        /// Get collection statistics for research analysis
        /// </summary>
        public ResourceCollectionStats GetCollectionStats()
        {
            var collectionsByAgent = collectionHistory
                .GroupBy(e => e.agentId)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.amountCollected));
                
            var collectionsByHour = collectionHistory
                .GroupBy(e => e.timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.amountCollected));
                
            var averageTimeBetweenCollections = 0.0;
            if (collectionHistory.Count > 1)
            {
                var orderedCollections = collectionHistory.OrderBy(e => e.timestamp).ToList();
                var intervals = new List<double>();
                
                for (int i = 1; i < orderedCollections.Count; i++)
                {
                    var interval = (orderedCollections[i].timestamp - orderedCollections[i - 1].timestamp).TotalSeconds;
                    intervals.Add(interval);
                }
                
                averageTimeBetweenCollections = intervals.Average();
            }
            
            return new ResourceCollectionStats
            {
                totalCollectionEvents = collectionHistory.Count,
                mostProductiveCollector = collectionsByAgent.Count > 0 ? 
                    collectionsByAgent.OrderByDescending(kvp => kvp.Value).First().Key : "None",
                peakCollectionHour = collectionsByHour.Count > 0 ? 
                    collectionsByHour.OrderByDescending(kvp => kvp.Value).First().Key : 0,
                averageTimeBetweenCollections = averageTimeBetweenCollections,
                collectionsByAgent = collectionsByAgent,
                hourlyCollectionPattern = collectionsByHour
            };
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Force reset resource to full quantity (for testing/admin)
        /// </summary>
        public void ForceRegenerate()
        {
            RegenerateResource();
        }
        
        /// <summary>
        /// Force depletion of resource (for testing scenarios)
        /// </summary>
        public void ForceDeplete()
        {
            currentQuantity = 0;
            isAvailable = false;
            UpdateVisualState();
            OnResourceDepleted?.Invoke(this);
        }
        
        /// <summary>
        /// Set resource availability state
        /// </summary>
        public void SetAvailable(bool available)
        {
            isAvailable = available;
            UpdateVisualState();
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw collection range
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, collectionRange);
            
            // Draw quantity visualization
            if (resourceConfig != null && currentQuantity > 0)
            {
                Gizmos.color = Color.blue;
                var height = (float)currentQuantity / (resourceConfig.maxQuantity > 0 ? resourceConfig.maxQuantity : 1) * 2f;
                Gizmos.DrawWireCube(transform.position + Vector3.up * height * 0.5f, 
                                   new Vector3(1f, height, 1f));
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private Dictionary<string, float> GetNeedStates(Agent agent)
        {
            if (agent?.Needs?.activeNeeds == null)
                return new Dictionary<string, float>();
            return agent.Needs.activeNeeds.ToDictionary(n => n.needName, n => n.currentSatisfaction);
        }
        
        #endregion
        
        #region Data Structures
        
        [System.Serializable]
        public class ResourceCollectionEvent
        {
            public DateTime timestamp;
            public string agentId;
            public string resourceName;
            public ResourceType resourceType;
            public int amountCollected;
            public int resourceValueGained;
            public Dictionary<string, float> agentNeedState;
            public string collectionMethod;
            public float competitionLevel;
        }
        
        [System.Serializable]
        public class ResourceResearchData
        {
            public string resourceName;
            public ResourceType resourceType;
            public int baseValue;
            public float scarcityLevel;
            public int currentQuantity;
            public int maxQuantity;
            public int totalCollected;
            public int totalValueGenerated;
            public int uniqueCollectors;
            public float averageCompetitionLevel;
            public float timeSinceLastCollection;
            public float regenerationRate;
            public List<ResourceCollectionEvent> recentCollections;
        }
        
        [System.Serializable]
        public class ResourceCollectionStats
        {
            public int totalCollectionEvents;
            public string mostProductiveCollector;
            public int peakCollectionHour;
            public double averageTimeBetweenCollections;
            public Dictionary<string, int> collectionsByAgent;
            public Dictionary<int, int> hourlyCollectionPattern;
        }
        
        #endregion
    }
}