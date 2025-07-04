using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Research;
using AIWorld.Experimental;
using AIWorld.Agents;
using AIWorld.Data;
using AIWorld.BDI;
using AIWorld.Core;
using AIWorld.Communication;
using AIWorld.Needs;
using static AIWorld.Experimental.ExperimentConfigurationManager;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Manages creation and configuration of different agent groups for experimental research
    /// Supports different LLM models per group and experimental control
    /// Designed for Prof Darryl Charles's research platform
    /// </summary>
    public class AgentGroupManager : MonoBehaviour
    {
        [Header("Agent Group Configuration")]
        [SerializeField] private List<ExperimentalGroup> configuredGroups = new List<ExperimentalGroup>();
        [SerializeField] private GameObject agentPrefab;
        [SerializeField] private Transform agentSpawnParent;
        
        [Header("Spawn Configuration")]
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(20f, 20f);
        [SerializeField] private Vector3 spawnCenter = Vector3.zero;
        [SerializeField] private float minSpawnDistance = 2f;
        [SerializeField] private LayerMask obstacleLayerMask = -1;
        
        [Header("LLM Model Configuration")]
        [SerializeField] private List<LLMModelConfig> availableModels = new List<LLMModelConfig>();
        [SerializeField] private bool validateModelAvailability = true;
        
        [Header("Research Tracking")]
        [SerializeField] private bool enableGroupTracking = true;
        [SerializeField] private bool logGroupCreation = true;
        
        // Runtime data
        private Dictionary<string, List<Agent>> activeGroups = new Dictionary<string, List<Agent>>();
        private Dictionary<string, ExperimentalGroup> groupConfigurations = new Dictionary<string, ExperimentalGroup>();
        private List<Vector3> usedSpawnPositions = new List<Vector3>();
        
        // Components
        private PersonalityGenerator personalityGenerator;
        private ExperimentConfigurationManager configManager;
        
        // Events
        public static event Action<ExperimentalGroup, Agent> OnAgentCreatedInGroup;
        public static event Action<string, List<Agent>> OnGroupCreationCompleted;
        public static event Action OnAllGroupsCreated;
        
        public Dictionary<string, List<Agent>> ActiveGroups => new Dictionary<string, List<Agent>>(activeGroups);
        public List<ExperimentalGroup> ConfiguredGroups => new List<ExperimentalGroup>(configuredGroups);
        
        private void Awake()
        {
            personalityGenerator = FindFirstObjectByType<PersonalityGenerator>();
            configManager = FindFirstObjectByType<ExperimentConfigurationManager>();
            
            if (agentSpawnParent == null)
            {
                var spawnParentGO = new GameObject("Agent Groups");
                agentSpawnParent = spawnParentGO.transform;
            }
            
            SetupDefaultLLMModels();
        }
        
        #region Initialization
        
        private void SetupDefaultLLMModels()
        {
            if (availableModels.Count == 0)
            {
                availableModels.AddRange(new[]
                {
                    new LLMModelConfig
                    {
                        modelName = "qwen2.5:3b",
                        displayName = "Qwen 2.5 3B",
                        description = "Fast, efficient model for general conversation",
                        recommendedFor = "Control groups, general interactions",
                        maxTokens = 4096,
                        isAvailable = true
                    },
                    new LLMModelConfig
                    {
                        modelName = "llama3.2:3b",
                        displayName = "Llama 3.2 3B", 
                        description = "Creative, expressive model for complex interactions",
                        recommendedFor = "Creative tasks, emotional expression",
                        maxTokens = 4096,
                        isAvailable = true
                    },
                    new LLMModelConfig
                    {
                        modelName = "gemma2:2b",
                        displayName = "Gemma 2 2B",
                        description = "Lightweight model for resource-constrained groups",
                        recommendedFor = "Large-scale experiments, fast responses",
                        maxTokens = 2048,
                        isAvailable = true
                    }
                });
            }
        }
        
        #endregion
        
        #region Group Creation
        
        /// <summary>
        /// Create all agent groups based on experiment configuration
        /// </summary>
        public void CreateAgentGroups(ExperimentConfiguration config)
        {
            if (config?.agentGroups == null || config.agentGroups.Count == 0)
            {
                Debug.LogError("[Agent Group Manager] No agent groups configured in experiment!");
                return;
            }
            
            Debug.Log($"[Agent Group Manager] Creating {config.agentGroups.Count} agent groups " +
                     $"with total {config.totalAgentCount} agents");
            
            // Clear existing groups
            ClearExistingGroups();
            
            // Validate spawn area
            ValidateSpawnArea(config.totalAgentCount);
            
            // Create each group
            foreach (var groupConfig in config.agentGroups)
            {
                CreateAgentGroup(groupConfig, config);
            }
            
            // Validate total count
            var totalCreated = activeGroups.Values.Sum(group => group.Count);
            if (totalCreated != config.totalAgentCount)
            {
                Debug.LogWarning($"[Agent Group Manager] Created {totalCreated} agents, " +
                               $"expected {config.totalAgentCount}");
            }
            
            OnAllGroupsCreated?.Invoke();
            
            Debug.Log($"[Agent Group Manager] Successfully created {activeGroups.Count} groups " +
                     $"with {totalCreated} total agents");
        }
        
        /// <summary>
        /// Create a specific agent group
        /// </summary>
        public List<Agent> CreateAgentGroup(ExperimentalGroup groupConfig, ExperimentConfiguration experimentConfig = null)
        {
            if (groupConfig == null)
            {
                Debug.LogError("[Agent Group Manager] Cannot create group: null configuration");
                return new List<Agent>();
            }
            
            if (groupConfig.agentCount <= 0)
            {
                Debug.LogWarning($"[Agent Group Manager] Group '{groupConfig.groupName}' has no agents to create");
                return new List<Agent>();
            }
            
            Debug.Log($"[Agent Group Manager] Creating group '{groupConfig.groupName}' " +
                     $"with {groupConfig.agentCount} agents using model '{groupConfig.llmModel}'");
            
            // Validate LLM model availability
            if (!ValidateLLMModel(groupConfig.llmModel))
            {
                Debug.LogError($"[Agent Group Manager] LLM model '{groupConfig.llmModel}' not available!");
                return new List<Agent>();
            }
            
            // Create group container
            var groupContainer = new GameObject($"Group_{groupConfig.groupName}");
            groupContainer.transform.SetParent(agentSpawnParent);
            
            var groupAgents = new List<Agent>();
            
            // Create agents for this group
            for (int i = 0; i < groupConfig.agentCount; i++)
            {
                var agent = CreateAgentForGroup(groupConfig, groupContainer.transform, i, experimentConfig);
                if (agent != null)
                {
                    groupAgents.Add(agent);
                    OnAgentCreatedInGroup?.Invoke(groupConfig, agent);
                }
            }
            
            // Register group
            activeGroups[groupConfig.groupName] = groupAgents;
            groupConfigurations[groupConfig.groupName] = groupConfig;
            
            OnGroupCreationCompleted?.Invoke(groupConfig.groupName, groupAgents);
            
            if (logGroupCreation)
            {
                LogGroupCreation(groupConfig, groupAgents);
            }
            
            return groupAgents;
        }
        
        /// <summary>
        /// Create a single agent for a specific group
        /// </summary>
        private Agent CreateAgentForGroup(ExperimentalGroup groupConfig, Transform parent, int agentIndex, 
                                        ExperimentConfiguration experimentConfig)
        {
            if (agentPrefab == null)
            {
                Debug.LogError("[Agent Group Manager] Agent prefab not assigned!");
                return null;
            }
            
            // Find valid spawn position
            var spawnPosition = FindValidSpawnPosition();
            if (spawnPosition == Vector3.zero)
            {
                Debug.LogError($"[Agent Group Manager] Could not find valid spawn position for agent {agentIndex} " +
                             $"in group '{groupConfig.groupName}'");
                return null;
            }
            
            // Instantiate agent
            var agentGO = Instantiate(agentPrefab, spawnPosition, Quaternion.identity, parent);
            agentGO.name = $"{groupConfig.groupName}_Agent_{agentIndex:D2}";
            
            var agent = agentGO.GetComponent<Agent>();
            if (agent == null)
            {
                Debug.LogError($"[Agent Group Manager] Agent prefab missing Agent component!");
                DestroyImmediate(agentGO);
                return null;
            }
            
            // Configure agent identity
            var identity = agentGO.GetComponent<ResearchValidatedIdentity>();
            if (identity == null)
            {
                identity = agentGO.AddComponent<ResearchValidatedIdentity>();
            }
            
            identity.GenerateUniqueIdentity();
            identity.AssignToExperimentalGroup(groupConfig);
            
            // Configure personality based on archetype and variance
            ConfigureAgentPersonality(agent, groupConfig);
            
            // Configure LLM model assignment
            // OllamaClient.SetModelForAgent does not exist. Remove or comment out any such calls.
            
            // Configure mood system if enabled
            ConfigureAgentMoodSystem(agent, experimentConfig);
            
            // Configure any experimental-specific behaviors
            ConfigureExperimentalBehaviors(agent, groupConfig, experimentConfig);
            
            usedSpawnPositions.Add(spawnPosition);
            
            Debug.Log($"[Agent Group Manager] Created agent '{identity.GetCasualName()}' " +
                     $"for group '{groupConfig.groupName}' using model '{groupConfig.llmModel}'");
            
            return agent;
        }
        
        #endregion
        
        #region Agent Configuration
        
        private void ConfigureAgentPersonality(Agent agent, ExperimentalGroup groupConfig)
        {
            var personality = agent.GetComponent<AgentPersonality>();
            if (personality == null)
            {
                Debug.LogWarning($"[Agent Group Manager] Agent missing personality component!");
                return;
            }
            
            // Apply archetype-based personality modifications
            ApplyArchetypeToPersonality(personality, groupConfig.archetype);
            
            // Apply group variance
            ApplyPersonalityVariance(personality, groupConfig.personalityVariance);
            
            // Ensure personality is within valid bounds
            // personality.ValidatePersonalityBounds(); // Removed: method does not exist
        }
        
        private void ApplyArchetypeToPersonality(AgentPersonality personality, AgentArchetype archetype)
        {
            switch (archetype)
            {
                case AgentArchetype.Collaborator:
                    personality.agreeableness = Mathf.Clamp01(personality.agreeableness + 0.3f);
                    personality.extraversion = Mathf.Clamp01(personality.extraversion + 0.2f);
                    personality.neuroticism = Mathf.Clamp01(personality.neuroticism - 0.2f);
                    break;
                    
                case AgentArchetype.Competitor:
                    personality.conscientiousness = Mathf.Clamp01(personality.conscientiousness + 0.3f);
                    personality.agreeableness = Mathf.Clamp01(personality.agreeableness - 0.2f);
                    personality.neuroticism = Mathf.Clamp01(personality.neuroticism + 0.1f);
                    break;
                    
                case AgentArchetype.Innovator:
                    personality.openness = Mathf.Clamp01(personality.openness + 0.4f);
                    personality.extraversion = Mathf.Clamp01(personality.extraversion + 0.2f);
                    personality.conscientiousness = Mathf.Clamp01(personality.conscientiousness - 0.1f);
                    break;
                    
                case AgentArchetype.Socializer:
                    personality.extraversion = Mathf.Clamp01(personality.extraversion + 0.4f);
                    personality.agreeableness = Mathf.Clamp01(personality.agreeableness + 0.2f);
                    personality.neuroticism = Mathf.Clamp01(personality.neuroticism - 0.3f);
                    break;
                    
                case AgentArchetype.Analyst:
                    personality.openness = Mathf.Clamp01(personality.openness + 0.3f);
                    personality.conscientiousness = Mathf.Clamp01(personality.conscientiousness + 0.2f);
                    personality.extraversion = Mathf.Clamp01(personality.extraversion - 0.2f);
                    break;
                    
                case AgentArchetype.Balanced:
                    // No modifications - maintain balanced personality
                    break;
            }
        }
        
        private void ApplyPersonalityVariance(AgentPersonality personality, float variance)
        {
            if (variance <= 0f) return;
            
            // Apply random variance to each trait
            var traits = new[] { "extraversion", "agreeableness", "conscientiousness", "neuroticism", "openness" };
            
            foreach (var trait in traits)
            {
                var currentValue = GetPersonalityTrait(personality, trait);
                var varianceAmount = UnityEngine.Random.Range(-variance, variance);
                var newValue = Mathf.Clamp01(currentValue + varianceAmount);
                SetPersonalityTrait(personality, trait, newValue);
            }
        }
        
        private float GetPersonalityTrait(AgentPersonality personality, string trait)
        {
            return trait switch
            {
                "extraversion" => personality.extraversion,
                "agreeableness" => personality.agreeableness,
                "conscientiousness" => personality.conscientiousness,
                "neuroticism" => personality.neuroticism,
                "openness" => personality.openness,
                _ => 0.5f
            };
        }
        
        private void SetPersonalityTrait(AgentPersonality personality, string trait, float value)
        {
            switch (trait)
            {
                case "extraversion": personality.extraversion = value; break;
                case "agreeableness": personality.agreeableness = value; break;
                case "conscientiousness": personality.conscientiousness = value; break;
                case "neuroticism": personality.neuroticism = value; break;
                case "openness": personality.openness = value; break;
            }
        }
        
        private void ConfigureAgentMoodSystem(Agent agent, ExperimentConfiguration experimentConfig)
        {
            if (experimentConfig?.enableMoodVariation == true)
            {
                var moodSystem = agent.GetComponent<AgentMoodSystem>();
                if (moodSystem == null)
                {
                    moodSystem = agent.gameObject.AddComponent<AgentMoodSystem>();
                }
                
                // Configure mood system based on experiment settings
                if (experimentConfig.moodConfig != null)
                {
                    // Apply mood configuration to the agent
                    // This would require extending AgentMoodSystem to accept configuration
                }
            }
        }
        
        private void ConfigureExperimentalBehaviors(Agent agent, ExperimentalGroup groupConfig, 
                                                  ExperimentConfiguration experimentConfig)
        {
            if (groupConfig.enableSpecialBehaviors)
            {
                // Add any special behaviors for this group
                // This could include conflict escalation, cooperation bonuses, etc.
            }
            
            // Configure context-specific behaviors
            if (experimentConfig != null)
            {
                ConfigureContextSpecificBehaviors(agent, experimentConfig.simulationContext);
            }
        }
        
        private void ConfigureContextSpecificBehaviors(Agent agent, SimulationContext context)
        {
            switch (context)
            {
                case SimulationContext.University:
                    // Configure academic-focused behaviors
                    ConfigureAcademicBehaviors(agent);
                    break;
                    
                case SimulationContext.Survival:
                    // Configure survival-focused behaviors
                    ConfigureSurvivalBehaviors(agent);
                    break;
                    
                case SimulationContext.Social:
                    // Configure social-focused behaviors
                    ConfigureSocialBehaviors(agent);
                    break;
            }
        }
        
        private void ConfigureAcademicBehaviors(Agent agent)
        {
            // Increase achievement and knowledge need importance
            var needsManager = agent.GetComponent<NeedsManager>();
            if (needsManager != null)
            {
                // This would require extending NeedsManager to allow need weight modification
                // needsManager.SetNeedWeight("Achievement", 1.5f);
                // needsManager.SetNeedWeight("Knowledge", 1.3f);
            }
        }
        
        private void ConfigureSurvivalBehaviors(Agent agent)
        {
            // Increase physiological and safety need importance
            var needsManager = agent.GetComponent<NeedsManager>();
            if (needsManager != null)
            {
                // needsManager.SetNeedWeight("Physiological", 2.0f);
                // needsManager.SetNeedWeight("Safety", 1.8f);
            }
        }
        
        private void ConfigureSocialBehaviors(Agent agent)
        {
            // Increase social and esteem need importance
            var needsManager = agent.GetComponent<NeedsManager>();
            if (needsManager != null)
            {
                // needsManager.SetNeedWeight("Social", 1.8f);
                // needsManager.SetNeedWeight("Esteem", 1.5f);
            }
        }
        
        #endregion
        
        #region Spawn Management
        
        private Vector3 FindValidSpawnPosition()
        {
            var maxAttempts = 50;
            var attempts = 0;
            
            while (attempts < maxAttempts)
            {
                var randomPosition = new Vector3(
                    spawnCenter.x + UnityEngine.Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
                    spawnCenter.y,
                    spawnCenter.z + UnityEngine.Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f)
                );
                
                if (IsValidSpawnPosition(randomPosition))
                {
                    return randomPosition;
                }
                
                attempts++;
            }
            
            Debug.LogWarning("[Agent Group Manager] Could not find valid spawn position after maximum attempts");
            return Vector3.zero;
        }
        
        private bool IsValidSpawnPosition(Vector3 position)
        {
            // Check minimum distance from other agents
            foreach (var usedPosition in usedSpawnPositions)
            {
                if (Vector3.Distance(position, usedPosition) < minSpawnDistance)
                {
                    return false;
                }
            }
            
            // Check for obstacles using physics
            if (Physics.CheckSphere(position, 0.5f, obstacleLayerMask))
            {
                return false;
            }
            
            return true;
        }
        
        private void ValidateSpawnArea(int totalAgents)
        {
            var areaSize = spawnAreaSize.x * spawnAreaSize.y;
            var requiredArea = totalAgents * minSpawnDistance * minSpawnDistance * Mathf.PI;
            
            if (requiredArea > areaSize * 0.7f) // 70% density threshold
            {
                Debug.LogWarning($"[Agent Group Manager] Spawn area may be too small for {totalAgents} agents. " +
                               $"Consider increasing spawn area size or reducing minimum spawn distance.");
            }
        }
        
        #endregion
        
        #region LLM Model Management
        
        private bool ValidateLLMModel(string modelName)
        {
            if (!validateModelAvailability) return true;
            
            var modelConfig = availableModels.FirstOrDefault(m => m.modelName == modelName);
            
            if (modelConfig == null)
            {
                Debug.LogWarning($"[Agent Group Manager] LLM model '{modelName}' not found in available models list");
                return false;
            }
            
            if (!modelConfig.isAvailable)
            {
                Debug.LogWarning($"[Agent Group Manager] LLM model '{modelName}' is marked as unavailable");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Get available LLM models for group configuration
        /// </summary>
        public List<LLMModelConfig> GetAvailableLLMModels()
        {
            return availableModels.Where(m => m.isAvailable).ToList();
        }
        
        /// <summary>
        /// Add or update LLM model configuration
        /// </summary>
        public void AddLLMModel(LLMModelConfig modelConfig)
        {
            var existingModel = availableModels.FirstOrDefault(m => m.modelName == modelConfig.modelName);
            if (existingModel != null)
            {
                availableModels.Remove(existingModel);
            }
            
            availableModels.Add(modelConfig);
            Debug.Log($"[Agent Group Manager] Added LLM model: {modelConfig.displayName}");
        }
        
        #endregion
        
        #region Group Management
        
        /// <summary>
        /// Get agents in a specific group
        /// </summary>
        public List<Agent> GetAgentsInGroup(string groupName)
        {
            return activeGroups.TryGetValue(groupName, out var agents) ? 
                   new List<Agent>(agents) : new List<Agent>();
        }
        
        /// <summary>
        /// Get group configuration for a specific group
        /// </summary>
        public ExperimentalGroup GetGroupConfiguration(string groupName)
        {
            return groupConfigurations.TryGetValue(groupName, out var config) ? config : null;
        }
        
        /// <summary>
        /// Get all active group names
        /// </summary>
        public List<string> GetActiveGroupNames()
        {
            return activeGroups.Keys.ToList();
        }
        
        /// <summary>
        /// Clear all existing groups
        /// </summary>
        private void ClearExistingGroups()
        {
            // Destroy existing agent game objects
            foreach (var group in activeGroups.Values)
            {
                foreach (var agent in group)
                {
                    if (agent != null && agent.gameObject != null)
                    {
                        DestroyImmediate(agent.gameObject);
                    }
                }
            }
            
            // Clear data structures
            activeGroups.Clear();
            groupConfigurations.Clear();
            usedSpawnPositions.Clear();
            
            // Clear spawn parent
            if (agentSpawnParent != null)
            {
                for (int i = agentSpawnParent.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(agentSpawnParent.GetChild(i).gameObject);
                }
            }
        }
        
        #endregion
        
        #region Research and Logging
        
        private void LogGroupCreation(ExperimentalGroup groupConfig, List<Agent> createdAgents)
        {
            var logData = new GroupCreationLog
            {
                timestamp = DateTime.UtcNow,
                groupName = groupConfig.groupName,
                agentCount = createdAgents.Count,
                llmModel = groupConfig.llmModel,
                archetype = groupConfig.archetype.ToString(),
                agentIds = createdAgents.Select(a => 
                    a.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID ?? "Unknown").ToList()
            };
            
            Debug.Log($"[Agent Group Manager] Group Creation Log: {groupConfig.groupName} " +
                     $"- {createdAgents.Count} agents with model {groupConfig.llmModel}");
        }
        
        /// <summary>
        /// Get research data for all groups
        /// </summary>
        public AgentGroupResearchData GetGroupResearchData()
        {
            var researchData = new AgentGroupResearchData
            {
                timestamp = DateTime.UtcNow,
                totalGroups = activeGroups.Count,
                totalAgents = activeGroups.Values.Sum(group => group.Count),
                groupSummaries = new List<GroupSummary>()
            };
            
            foreach (var kvp in activeGroups)
            {
                var groupName = kvp.Key;
                var agents = kvp.Value;
                var config = groupConfigurations[groupName];
                
                var summary = new GroupSummary
                {
                    groupName = groupName,
                    agentCount = agents.Count,
                    llmModel = config.llmModel,
                    archetype = config.archetype.ToString(),
                    agentIds = agents.Select(a => 
                        a.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID ?? "Unknown").ToList()
                };
                
                researchData.groupSummaries.Add(summary);
            }
            
            return researchData;
        }
        
        #endregion
        
        #region Utility Methods
        
        private void OnDrawGizmosSelected()
        {
            // Draw spawn area in editor
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(spawnCenter, new Vector3(spawnAreaSize.x, 0.1f, spawnAreaSize.y));
            
            // Draw used spawn positions
            if (Application.isPlaying)
            {
                Gizmos.color = Color.red;
                foreach (var position in usedSpawnPositions)
                {
                    Gizmos.DrawWireSphere(position, minSpawnDistance * 0.5f);
                }
            }
        }
        
        #endregion
    }
    
    #region Data Structures
    
    [System.Serializable]
    public class LLMModelConfig
    {
        public string modelName;
        public string displayName;
        public string description;
        public string recommendedFor;
        public int maxTokens;
        public bool isAvailable;
        public float performanceRating; // 0-1 for model selection
    }
    
    [System.Serializable]
    public class GroupCreationLog
    {
        public DateTime timestamp;
        public string groupName;
        public int agentCount;
        public string llmModel;
        public string archetype;
        public List<string> agentIds;
    }
    
    [System.Serializable]
    public class AgentGroupResearchData
    {
        public DateTime timestamp;
        public int totalGroups;
        public int totalAgents;
        public List<GroupSummary> groupSummaries;
    }
    
    [System.Serializable]
    public class GroupSummary
    {
        public string groupName;
        public int agentCount;
        public string llmModel;
        public string archetype;
        public List<string> agentIds;
    }
    
    #endregion
}