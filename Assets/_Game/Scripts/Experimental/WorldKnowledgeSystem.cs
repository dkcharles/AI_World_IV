using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Research;
using AIWorld.Experimental;
using AIWorld.Agents;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Manages world knowledge that agents can access and learn about their environment
    /// Provides context-specific information for agent decision making and LLM prompts
    /// </summary>
    public class WorldKnowledgeSystem : MonoBehaviour
    {
        [Header("Knowledge Configuration")]
        [SerializeField] private bool enableKnowledgeSystem = true;
        [SerializeField] private bool allowKnowledgeSharing = true;
        [SerializeField] private float knowledgeDecayRate = 0f; // Knowledge degradation over time
        [SerializeField] private int maxKnowledgeEntries = 1000;
        
        [Header("Context Sensitivity")]
        [SerializeField] private SimulationContext currentContext;
        [SerializeField] private bool adaptToContext = true;
        [SerializeField] private float contextRelevanceThreshold = 0.3f;
        
        [Header("Agent Knowledge Tracking")]
        [SerializeField] private bool trackAgentKnowledge = true;
        [SerializeField] private float knowledgeAcquisitionRate = 0.1f;
        [SerializeField] private bool enableKnowledgeExchange = true;
        
        // Core knowledge data
        private WorldKnowledgeData worldKnowledge;
        private Dictionary<string, AgentKnowledgeProfile> agentKnowledge = new Dictionary<string, AgentKnowledgeProfile>();
        private List<KnowledgeEvent> knowledgeEvents = new List<KnowledgeEvent>();
        private Dictionary<string, float> knowledgeImportance = new Dictionary<string, float>();
        
        // Knowledge categories
        private Dictionary<string, List<KnowledgeEntry>> categorizedKnowledge = new Dictionary<string, List<KnowledgeEntry>>();
        
        // Events
        public static event Action<string, KnowledgeEntry> OnKnowledgeAcquired;
        public static event Action<string, string> OnKnowledgeShared;
        public static event Action<WorldKnowledgeData> OnWorldKnowledgeUpdated;
        
        public WorldKnowledgeData WorldKnowledge => worldKnowledge;
        public bool KnowledgeSystemEnabled => enableKnowledgeSystem;
        public SimulationContext CurrentContext => currentContext;
        
        private void Awake()
        {
            InitializeKnowledgeSystem();
        }
        
        private void Start()
        {
            if (enableKnowledgeSystem)
            {
                StartKnowledgeProcessing();
            }
        }
        
        #region Initialization
        
        private void InitializeKnowledgeSystem()
        {
            // Initialize knowledge categories
            categorizedKnowledge["Locations"] = new List<KnowledgeEntry>();
            categorizedKnowledge["Resources"] = new List<KnowledgeEntry>();
            categorizedKnowledge["SocialDynamics"] = new List<KnowledgeEntry>();
            categorizedKnowledge["ContextualRules"] = new List<KnowledgeEntry>();
            categorizedKnowledge["Objectives"] = new List<KnowledgeEntry>();
            categorizedKnowledge["Strategies"] = new List<KnowledgeEntry>();
            categorizedKnowledge["Relationships"] = new List<KnowledgeEntry>();
            
            Debug.Log("[World Knowledge] Knowledge system initialized");
        }
        
        public void Initialize(WorldKnowledgeData knowledgeData)
        {
            worldKnowledge = knowledgeData;
            currentContext = knowledgeData.worldContext;
            
            // Process and categorize initial knowledge
            ProcessInitialKnowledge();
            
            // Set up context-specific knowledge importance
            SetupKnowledgeImportance();
            
            OnWorldKnowledgeUpdated?.Invoke(worldKnowledge);
            
            Debug.Log($"[World Knowledge] Initialized with {GetTotalKnowledgeEntries()} knowledge entries " +
                     $"for {currentContext} context");
        }
        
        private void ProcessInitialKnowledge()
        {
            if (worldKnowledge == null) return;
            
            // Process location knowledge
            if (worldKnowledge.locations != null)
            {
                foreach (var location in worldKnowledge.locations)
                {
                    var entry = new KnowledgeEntry
                    {
                        category = "Locations",
                        title = $"Location: {location.name}",
                        content = GenerateLocationKnowledgeContent(location),
                        importance = CalculateLocationImportance(location),
                        contextRelevance = 1.0f,
                        timestamp = DateTime.UtcNow,
                        source = "World Generation"
                    };
                    
                    AddKnowledgeEntry(entry);
                }
            }
            
            // Process resource knowledge
            if (worldKnowledge.resources != null)
            {
                foreach (var resource in worldKnowledge.resources)
                {
                    var entry = new KnowledgeEntry
                    {
                        category = "Resources",
                        title = $"Resource: {resource.name}",
                        content = GenerateResourceKnowledgeContent(resource),
                        importance = CalculateResourceImportance(resource),
                        contextRelevance = 1.0f,
                        timestamp = DateTime.UtcNow,
                        source = "World Generation"
                    };
                    
                    AddKnowledgeEntry(entry);
                }
            }
            
            // Process contextual rules
            if (worldKnowledge.contextualRules != null)
            {
                foreach (var rule in worldKnowledge.contextualRules)
                {
                    var entry = new KnowledgeEntry
                    {
                        category = "ContextualRules",
                        title = "World Rule",
                        content = rule,
                        importance = 0.8f,
                        contextRelevance = 1.0f,
                        timestamp = DateTime.UtcNow,
                        source = "Context Rules"
                    };
                    
                    AddKnowledgeEntry(entry);
                }
            }
            
            // Process objectives
            if (worldKnowledge.objectives != null)
            {
                foreach (var objective in worldKnowledge.objectives)
                {
                    var entry = new KnowledgeEntry
                    {
                        category = "Objectives",
                        title = "Possible Objective",
                        content = objective,
                        importance = 0.7f,
                        contextRelevance = 1.0f,
                        timestamp = DateTime.UtcNow,
                        source = "Context Objectives"
                    };
                    
                    AddKnowledgeEntry(entry);
                }
            }
            
            // Process social dynamics knowledge
            if (worldKnowledge.socialDynamics != null)
            {
                foreach (var dynamic in worldKnowledge.socialDynamics)
                {
                    var entry = new KnowledgeEntry
                    {
                        category = "SocialDynamics",
                        title = "Social Information",
                        content = dynamic,
                        importance = 0.6f,
                        contextRelevance = 1.0f,
                        timestamp = DateTime.UtcNow,
                        source = "Social Dynamics"
                    };
                    
                    AddKnowledgeEntry(entry);
                }
            }
        }
        
        private void SetupKnowledgeImportance()
        {
            // Set importance weights based on simulation context
            switch (currentContext)
            {
                case SimulationContext.University:
                    knowledgeImportance["Resources"] = 0.9f; // Research resources are crucial
                    knowledgeImportance["Objectives"] = 0.8f; // Academic goals
                    knowledgeImportance["Locations"] = 0.7f; // Research spaces
                    knowledgeImportance["SocialDynamics"] = 0.6f; // Collaboration
                    knowledgeImportance["Strategies"] = 0.7f; // Research strategies
                    break;
                    
                case SimulationContext.Survival:
                    knowledgeImportance["Resources"] = 1.0f; // Essential for survival
                    knowledgeImportance["Locations"] = 0.9f; // Safe spaces crucial
                    knowledgeImportance["SocialDynamics"] = 0.8f; // Group cooperation
                    knowledgeImportance["Strategies"] = 0.8f; // Survival strategies
                    knowledgeImportance["Objectives"] = 0.6f; // Long-term goals
                    break;
                    
                case SimulationContext.Social:
                    knowledgeImportance["SocialDynamics"] = 1.0f; // Core focus
                    knowledgeImportance["Relationships"] = 0.9f; // Key to success
                    knowledgeImportance["Locations"] = 0.7f; // Social spaces
                    knowledgeImportance["Strategies"] = 0.7f; // Social strategies
                    knowledgeImportance["Resources"] = 0.5f; // Less critical
                    break;
                    
                default:
                    // Balanced importance for generic context
                    foreach (var category in categorizedKnowledge.Keys)
                    {
                        knowledgeImportance[category] = 0.7f;
                    }
                    break;
            }
        }
        
        private void StartKnowledgeProcessing()
        {
            // Start periodic knowledge updates
            InvokeRepeating(nameof(ProcessKnowledgeDecay), 60f, 60f); // Every minute
            InvokeRepeating(nameof(UpdateAgentKnowledge), 30f, 30f); // Every 30 seconds
        }
        
        #endregion
        
        #region Knowledge Management
        
        public void AddKnowledgeEntry(KnowledgeEntry entry)
        {
            if (!enableKnowledgeSystem) return;
            
            // Ensure category exists
            if (!categorizedKnowledge.ContainsKey(entry.category))
            {
                categorizedKnowledge[entry.category] = new List<KnowledgeEntry>();
            }
            
            // Add to appropriate category
            categorizedKnowledge[entry.category].Add(entry);
            
            // Maintain size limits
            if (GetTotalKnowledgeEntries() > maxKnowledgeEntries)
            {
                RemoveOldestKnowledge();
            }
            
            // Log knowledge event
            var knowledgeEvent = new KnowledgeEvent
            {
                timestamp = DateTime.UtcNow,
                eventType = KnowledgeEventType.Created,
                category = entry.category,
                title = entry.title,
                importance = entry.importance,
                source = entry.source
            };
            
            knowledgeEvents.Add(knowledgeEvent);
            if (knowledgeEvents.Count > 500) // Keep recent events
            {
                knowledgeEvents.RemoveAt(0);
            }
        }
        
        public void UpdateKnowledgeEntry(string category, string title, string newContent, float newImportance)
        {
            if (!categorizedKnowledge.ContainsKey(category)) return;
            
            var entry = categorizedKnowledge[category].FirstOrDefault(e => e.title == title);
            if (entry != null)
            {
                entry.content = newContent;
                entry.importance = newImportance;
                entry.timestamp = DateTime.UtcNow;
                entry.source = "Updated";
                
                var knowledgeEvent = new KnowledgeEvent
                {
                    timestamp = DateTime.UtcNow,
                    eventType = KnowledgeEventType.Updated,
                    category = category,
                    title = title,
                    importance = newImportance,
                    source = "System Update"
                };
                
                knowledgeEvents.Add(knowledgeEvent);
            }
        }
        
        public void RemoveKnowledgeEntry(string category, string title)
        {
            if (!categorizedKnowledge.ContainsKey(category)) return;
            
            var entry = categorizedKnowledge[category].FirstOrDefault(e => e.title == title);
            if (entry != null)
            {
                categorizedKnowledge[category].Remove(entry);
                
                var knowledgeEvent = new KnowledgeEvent
                {
                    timestamp = DateTime.UtcNow,
                    eventType = KnowledgeEventType.Removed,
                    category = category,
                    title = title,
                    importance = entry.importance,
                    source = "System Cleanup"
                };
                
                knowledgeEvents.Add(knowledgeEvent);
            }
        }
        
        private void RemoveOldestKnowledge()
        {
            // Find oldest, least important knowledge to remove
            KnowledgeEntry oldestEntry = null;
            string oldestCategory = null;
            DateTime oldestTime = DateTime.MaxValue;
            float lowestImportance = float.MaxValue;
            
            foreach (var categoryKvp in categorizedKnowledge)
            {
                foreach (var entry in categoryKvp.Value)
                {
                    var score = entry.importance + (float)(DateTime.UtcNow - entry.timestamp).TotalDays * 0.1f;
                    if (score < lowestImportance || (score == lowestImportance && entry.timestamp < oldestTime))
                    {
                        oldestEntry = entry;
                        oldestCategory = categoryKvp.Key;
                        oldestTime = entry.timestamp;
                        lowestImportance = score;
                    }
                }
            }
            
            if (oldestEntry != null && oldestCategory != null)
            {
                RemoveKnowledgeEntry(oldestCategory, oldestEntry.title);
            }
        }
        
        private void ProcessKnowledgeDecay()
        {
            if (knowledgeDecayRate <= 0f) return;
            
            foreach (var categoryKvp in categorizedKnowledge.ToList())
            {
                foreach (var entry in categoryKvp.Value.ToList())
                {
                    // Decay importance over time
                    var decayAmount = knowledgeDecayRate * Time.deltaTime;
                    entry.importance = Mathf.Max(0f, entry.importance - decayAmount);
                    
                    // Remove entries that become too unimportant
                    if (entry.importance < 0.1f)
                    {
                        RemoveKnowledgeEntry(categoryKvp.Key, entry.title);
                    }
                }
            }
        }
        
        #endregion
        
        #region Agent Knowledge System
        
        public void RegisterAgent(Agent agent)
        {
            if (!trackAgentKnowledge) return;
            
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return;
            
            var agentId = identity.UniqueResearchID;
            
            if (!agentKnowledge.ContainsKey(agentId))
            {
                agentKnowledge[agentId] = new AgentKnowledgeProfile
                {
                    agentId = agentId,
                    knowledgeAcquisitionRate = CalculateAgentLearningRate(agent),
                    knownFacts = new List<KnowledgeEntry>(),
                    knowledgeInterests = DetermineAgentInterests(agent),
                    lastKnowledgeUpdate = DateTime.UtcNow
                };
                
                // Give agent initial world knowledge based on their archetype
                ProvideInitialKnowledge(agentId, agent);
            }
        }
        
        private float CalculateAgentLearningRate(Agent agent)
        {
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return knowledgeAcquisitionRate;
            
            return identity.Archetype switch
            {
                AgentArchetype.Analyst => knowledgeAcquisitionRate * 1.5f,
                AgentArchetype.Innovator => knowledgeAcquisitionRate * 1.3f,
                AgentArchetype.Collaborator => knowledgeAcquisitionRate * 1.2f,
                _ => knowledgeAcquisitionRate
            };
        }
        
        private List<string> DetermineAgentInterests(Agent agent)
        {
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return new List<string> { "Locations", "Resources" };
            
            return identity.Archetype switch
            {
                AgentArchetype.Analyst => new List<string> { "Resources", "Strategies", "ContextualRules" },
                AgentArchetype.Collaborator => new List<string> { "SocialDynamics", "Relationships", "Locations" },
                AgentArchetype.Competitor => new List<string> { "Resources", "Objectives", "Strategies" },
                AgentArchetype.Innovator => new List<string> { "Strategies", "Objectives", "Resources" },
                AgentArchetype.Socializer => new List<string> { "SocialDynamics", "Relationships", "Locations" },
                _ => new List<string> { "Locations", "Resources", "SocialDynamics" }
            };
        }
        
        private void ProvideInitialKnowledge(string agentId, Agent agent)
        {
            var profile = agentKnowledge[agentId];
            var maxInitialKnowledge = 5; // Limit initial knowledge
            var knowledgeCount = 0;
            
            // Provide knowledge based on agent interests
            foreach (var interest in profile.knowledgeInterests)
            {
                if (knowledgeCount >= maxInitialKnowledge) break;
                
                if (categorizedKnowledge.ContainsKey(interest))
                {
                    var relevantKnowledge = categorizedKnowledge[interest]
                        .OrderByDescending(k => k.importance)
                        .Take(2)
                        .ToList();
                        
                    foreach (var knowledge in relevantKnowledge)
                    {
                        if (knowledgeCount >= maxInitialKnowledge) break;
                        
                        profile.knownFacts.Add(knowledge);
                        knowledgeCount++;
                        
                        OnKnowledgeAcquired?.Invoke(agentId, knowledge);
                    }
                }
            }
        }
        
        private void UpdateAgentKnowledge()
        {
            if (!trackAgentKnowledge) return;
            
            foreach (var agentProfile in agentKnowledge.Values.ToList())
            {
                // Update agent knowledge acquisition
                UpdateAgentKnowledgeAcquisition(agentProfile);
                
                // Process knowledge sharing if enabled
                if (enableKnowledgeExchange)
                {
                    ProcessKnowledgeSharing(agentProfile);
                }
            }
        }
        
        private void UpdateAgentKnowledgeAcquisition(AgentKnowledgeProfile profile)
        {
            var timeSinceUpdate = (DateTime.UtcNow - profile.lastKnowledgeUpdate).TotalSeconds;
            if (timeSinceUpdate < 30f) return; // Update every 30 seconds max
            
            // Chance to acquire new knowledge
            if (UnityEngine.Random.value < profile.knowledgeAcquisitionRate)
            {
                var newKnowledge = FindRelevantUnknownKnowledge(profile);
                if (newKnowledge != null)
                {
                    profile.knownFacts.Add(newKnowledge);
                    profile.lastKnowledgeUpdate = DateTime.UtcNow;
                    
                    OnKnowledgeAcquired?.Invoke(profile.agentId, newKnowledge);
                }
            }
        }
        
        private KnowledgeEntry FindRelevantUnknownKnowledge(AgentKnowledgeProfile profile)
        {
            var unknownKnowledge = new List<KnowledgeEntry>();
            
            // Find knowledge the agent doesn't have
            foreach (var interest in profile.knowledgeInterests)
            {
                if (categorizedKnowledge.ContainsKey(interest))
                {
                    var categoryKnowledge = categorizedKnowledge[interest];
                    var unknown = categoryKnowledge.Where(k => !profile.knownFacts.Contains(k)).ToList();
                    unknownKnowledge.AddRange(unknown);
                }
            }
            
            if (unknownKnowledge.Count == 0) return null;
            
            // Select based on importance and relevance
            var weightedSelection = unknownKnowledge
                .Where(k => k.importance > 0.3f)
                .OrderByDescending(k => k.importance * k.contextRelevance)
                .FirstOrDefault();
                
            return weightedSelection ?? unknownKnowledge[UnityEngine.Random.Range(0, unknownKnowledge.Count)];
        }
        
        private void ProcessKnowledgeSharing(AgentKnowledgeProfile profile)
        {
            // Simple knowledge sharing - agents can share knowledge they've learned
            if (UnityEngine.Random.value < 0.1f) // 10% chance per update
            {
                var otherAgent = agentKnowledge.Values
                    .Where(p => p.agentId != profile.agentId)
                    .OrderBy(p => UnityEngine.Random.value)
                    .FirstOrDefault();
                    
                if (otherAgent != null)
                {
                    ShareKnowledgeBetweenAgents(profile, otherAgent);
                }
            }
        }
        
        private void ShareKnowledgeBetweenAgents(AgentKnowledgeProfile giver, AgentKnowledgeProfile receiver)
        {
            // Find knowledge that giver has but receiver doesn't
            var shareableKnowledge = giver.knownFacts
                .Where(k => !receiver.knownFacts.Contains(k))
                .Where(k => receiver.knowledgeInterests.Contains(k.category))
                .OrderByDescending(k => k.importance)
                .FirstOrDefault();
                
            if (shareableKnowledge != null)
            {
                receiver.knownFacts.Add(shareableKnowledge);
                OnKnowledgeShared?.Invoke(giver.agentId, receiver.agentId);
            }
        }
        
        #endregion
        
        #region Knowledge Queries
        
        /// <summary>
        /// Get relevant knowledge for an agent to use in LLM prompts
        /// </summary>
        public string GetAgentKnowledgeContext(Agent agent, int maxEntries = 5)
        {
            if (!enableKnowledgeSystem) return "";
            
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return "";
            
            var agentId = identity.UniqueResearchID;
            if (!agentKnowledge.ContainsKey(agentId)) return "";
            
            var profile = agentKnowledge[agentId];
            var relevantKnowledge = profile.knownFacts
                .OrderByDescending(k => k.importance * k.contextRelevance)
                .Take(maxEntries)
                .ToList();
                
            var contextBuilder = new System.Text.StringBuilder();
            contextBuilder.AppendLine("**World Knowledge:**");
            
            foreach (var knowledge in relevantKnowledge)
            {
                contextBuilder.AppendLine($"• {knowledge.content}");
            }
            
            return contextBuilder.ToString();
        }
        
        /// <summary>
        /// Get knowledge by category
        /// </summary>
        public List<KnowledgeEntry> GetKnowledgeByCategory(string category)
        {
            return categorizedKnowledge.TryGetValue(category, out var knowledge) ? 
                   new List<KnowledgeEntry>(knowledge) : new List<KnowledgeEntry>();
        }
        
        /// <summary>
        /// Search knowledge by content
        /// </summary>
        public List<KnowledgeEntry> SearchKnowledge(string searchTerm, float minImportance = 0.1f)
        {
            var results = new List<KnowledgeEntry>();
            
            foreach (var categoryKnowledge in categorizedKnowledge.Values)
            {
                var matches = categoryKnowledge
                    .Where(k => k.importance >= minImportance)
                    .Where(k => k.title.ToLower().Contains(searchTerm.ToLower()) || 
                               k.content.ToLower().Contains(searchTerm.ToLower()))
                    .ToList();
                    
                results.AddRange(matches);
            }
            
            return results.OrderByDescending(k => k.importance).ToList();
        }
        
        /// <summary>
        /// Get most important knowledge across all categories
        /// </summary>
        public List<KnowledgeEntry> GetMostImportantKnowledge(int count = 10)
        {
            var allKnowledge = new List<KnowledgeEntry>();
            
            foreach (var categoryKnowledge in categorizedKnowledge.Values)
            {
                allKnowledge.AddRange(categoryKnowledge);
            }
            
            return allKnowledge
                .OrderByDescending(k => k.importance * k.contextRelevance)
                .Take(count)
                .ToList();
        }
        
        #endregion
        
        #region Knowledge Content Generation
        
        private string GenerateLocationKnowledgeContent(LocationKnowledge location)
        {
            var content = $"{location.name} is a {location.type} that can accommodate {location.capacity} agents. ";
            content += $"It serves as a {location.primaryPurpose}. ";
            
            if (location.needsSatisfied.Count > 0)
            {
                content += $"This location helps satisfy {string.Join(", ", location.needsSatisfied)} needs. ";
            }
            
            content += $"Access: {location.accessRequirements}.";
            
            return content;
        }
        
        private string GenerateResourceKnowledgeContent(ResourceKnowledge resource)
        {
            var content = $"{resource.name} is a {resource.type} resource with value {resource.value}. ";
            
            var scarcityDesc = resource.scarcity switch
            {
                < 0.3f => "abundant",
                < 0.6f => "moderately scarce",
                < 0.8f => "scarce",
                _ => "very rare"
            };
            
            content += $"It is {scarcityDesc} with {resource.availableQuantity} currently available. ";
            content += $"Collection method: {resource.acquisitionMethod}. ";
            
            if (resource.regenerationRate > 0)
            {
                content += $"This resource regenerates over time. ";
            }
            
            if (resource.competitionLevel > 0.5f)
            {
                content += "High competition expected for this resource.";
            }
            
            return content;
        }
        
        private float CalculateLocationImportance(LocationKnowledge location)
        {
            var baseImportance = 0.5f;
            
            // Higher capacity locations are more important
            baseImportance += (location.capacity / 10f) * 0.2f;
            
            // Locations that satisfy multiple needs are more important
            baseImportance += location.needsSatisfied.Count * 0.1f;
            
            // Context-specific importance
            baseImportance *= GetContextImportanceMultiplier("Locations");
            
            return Mathf.Clamp01(baseImportance);
        }
        
        private float CalculateResourceImportance(ResourceKnowledge resource)
        {
            var baseImportance = 0.5f;
            
            // Higher value resources are more important
            baseImportance += (resource.value / 100f) * 0.3f;
            
            // Scarcer resources are more important
            baseImportance += resource.scarcity * 0.3f;
            
            // High competition increases importance
            baseImportance += resource.competitionLevel * 0.2f;
            
            // Context-specific importance
            baseImportance *= GetContextImportanceMultiplier("Resources");
            
            return Mathf.Clamp01(baseImportance);
        }
        
        private float GetContextImportanceMultiplier(string category)
        {
            return knowledgeImportance.TryGetValue(category, out var multiplier) ? multiplier : 1.0f;
        }
        
        #endregion
        
        #region Utility Methods
        
        private int GetTotalKnowledgeEntries()
        {
            return categorizedKnowledge.Values.Sum(list => list.Count);
        }
        
        /// <summary>
        /// Get research data about the knowledge system
        /// </summary>
        public KnowledgeSystemResearchData GetResearchData()
        {
            var totalEntries = GetTotalKnowledgeEntries();
            var categoryDistribution = categorizedKnowledge.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value.Count
            );
            
            var averageImportance = 0f;
            if (totalEntries > 0)
            {
                var allEntries = categorizedKnowledge.Values.SelectMany(list => list);
                averageImportance = allEntries.Average(e => e.importance);
            }
            
            return new KnowledgeSystemResearchData
            {
                totalKnowledgeEntries = totalEntries,
                trackedAgents = agentKnowledge.Count,
                knowledgeEvents = knowledgeEvents.Count,
                categoryDistribution = categoryDistribution,
                averageKnowledgeImportance = averageImportance,
                recentKnowledgeEvents = knowledgeEvents.TakeLast(10).ToList(),
                agentKnowledgeStats = agentKnowledge.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.knownFacts.Count
                )
            };
        }
        
        /// <summary>
        /// Export world knowledge for agent prompts
        /// </summary>
        public string ExportWorldKnowledgeForPrompts()
        {
            var builder = new System.Text.StringBuilder();
            
            builder.AppendLine($"**{currentContext} World Knowledge:**");
            builder.AppendLine();
            
            // Export by category with importance weighting
            var sortedCategories = categorizedKnowledge
                .Where(kvp => kvp.Value.Count > 0)
                .OrderByDescending(kvp => GetContextImportanceMultiplier(kvp.Key))
                .ToList();
                
            foreach (var categoryKvp in sortedCategories)
            {
                var category = categoryKvp.Key;
                var entries = categoryKvp.Value
                    .OrderByDescending(e => e.importance)
                    .Take(3) // Top 3 per category
                    .ToList();
                    
                if (entries.Count > 0)
                {
                    builder.AppendLine($"**{category}:**");
                    foreach (var entry in entries)
                    {
                        builder.AppendLine($"• {entry.content}");
                    }
                    builder.AppendLine();
                }
            }
            
            return builder.ToString();
        }
        
        private void OnDestroy()
        {
            CancelInvoke();
        }
        
        #endregion
    }
    
    #region Data Structures
    
    // WorldKnowledgeData, LocationKnowledge, and ResourceKnowledge are now defined in ExperimentalDefinitions.cs
    
    [System.Serializable]
    public class KnowledgeEntry
    {
        public string category;
        public string title;
        public string content;
        public float importance;
        public float contextRelevance;
        public DateTime timestamp;
        public string source;
    }
    
    [System.Serializable]
    public class AgentKnowledgeProfile
    {
        public string agentId;
        public float knowledgeAcquisitionRate;
        public List<KnowledgeEntry> knownFacts;
        public List<string> knowledgeInterests;
        public DateTime lastKnowledgeUpdate;
    }
    
    [System.Serializable]
    public class KnowledgeEvent
    {
        public DateTime timestamp;
        public KnowledgeEventType eventType;
        public string category;
        public string title;
        public float importance;
        public string source;
    }
    
    [System.Serializable]
    public class KnowledgeSystemResearchData
    {
        public int totalKnowledgeEntries;
        public int trackedAgents;
        public int knowledgeEvents;
        public Dictionary<string, int> categoryDistribution;
        public float averageKnowledgeImportance;
        public List<KnowledgeEvent> recentKnowledgeEvents;
        public Dictionary<string, int> agentKnowledgeStats;
    }
    
    public enum KnowledgeEventType
    {
        Created,
        Updated,
        Shared,
        Acquired,
        Removed
    }
    
    #endregion
}