using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AIWorld.Research;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Master experiment configuration ScriptableObject
    /// Single source of truth for all experimental settings
    /// </summary>
    [CreateAssetMenu(fileName = "New Experiment Configuration", menuName = "AI World/Experiment Configuration")]
    public class ExperimentConfigurationSO : ScriptableObject
    {
        [Header("Experiment Metadata")]
        public string experimentName = "AI Consciousness Study 2025";
        public string researcherName = "Prof Darryl Charles";
        public string institutionName = "Ulster University";
        [TextArea(3, 5)]
        public string studyDescription = "";
        public string ethicsApproval = "IRB-2025-AI-001";
        
        [Header("Core Configuration")]
        public WorldConfigurationSO worldConfiguration;
        public AgentGroupConfigurationSO[] agentGroups;
        public ConflictConfigurationSO conflictConfiguration;
        public MoodConfigurationSO moodConfiguration;
        public ResearchConfigurationSO researchConfiguration;
        
        [Header("Experiment Parameters")]
        public float simulationDuration = 3600f; // 1 hour
        public bool enablePersonalityVariation = true;
        
        // Computed properties
        public int TotalAgentCount => (agentGroups != null && agentGroups.Length > 0) ? 
            agentGroups.Where(g => g != null).Sum(g => g.agentCount) : 0;
        
        public SimulationContext SimulationContext => worldConfiguration != null ? 
            worldConfiguration.simulationContext : SimulationContext.University;
        
        // Validation
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(experimentName)) return false;
            if (worldConfiguration == null) return false;
            if (agentGroups == null || agentGroups.Length == 0) return false;
            if (simulationDuration <= 0) return false;
            return true;
        }
        
        public string GetValidationMessage()
        {
            if (string.IsNullOrEmpty(experimentName)) return "Experiment name is required";
            if (worldConfiguration == null) return "World configuration is required";
            if (agentGroups == null || agentGroups.Length == 0) return "At least one agent group is required";
            if (simulationDuration <= 0) return "Simulation duration must be positive";
            
            // Check agent groups
            for (int i = 0; i < agentGroups.Length; i++)
            {
                if (agentGroups[i] == null) return $"Agent group {i} is null";
                if (!agentGroups[i].IsValid()) return $"Agent group {i}: {agentGroups[i].GetValidationMessage()}";
            }
            
            return "Configuration is valid";
        }
        
        /// <summary>
        /// Convert to legacy ExperimentConfiguration for compatibility
        /// </summary>
        public ExperimentConfiguration ToExperimentConfiguration()
        {
            var config = new ExperimentConfiguration
            {
                // Metadata
                experimentName = this.experimentName,
                researcherName = this.researcherName,
                institutionName = this.institutionName,
                studyDescription = this.studyDescription,
                ethicsApproval = this.ethicsApproval,
                createdAt = System.DateTime.UtcNow,
                
                // Basic parameters
                totalAgentCount = this.TotalAgentCount,
                simulationDuration = this.simulationDuration,
                enablePersonalityVariation = this.enablePersonalityVariation,
                
                // World configuration
                simulationContext = this.SimulationContext,
                worldSize = worldConfiguration?.worldSize ?? Vector2.one * 50f,
                worldLocations = ConvertWorldLocations(),
                worldResources = ConvertWorldResources(),
                maxResourceInstances = worldConfiguration?.maxResourceInstances ?? 20,
                
                // Agent groups
                agentGroups = ConvertAgentGroups(),
                
                // System configurations
                enableConflictSystem = conflictConfiguration?.enableConflictSystem ?? false,
                conflictProbability = conflictConfiguration?.baseConflictProbability ?? 0.2f,
                enableResourceCompetition = conflictConfiguration?.enableResourceConflicts ?? false,
                enableMoodVariation = moodConfiguration?.enableMoodSystem ?? false,
                moodConfig = ConvertMoodConfig(),
                
                // Research configuration
                enableDataLogging = researchConfiguration?.enableDataLogging ?? false,
                dataLoggingInterval = researchConfiguration?.dataLoggingInterval ?? 30f,
                enableConsciousnessTracking = researchConfiguration?.enableConsciousnessTracking ?? false
            };
            
            return config;
        }
        
        private List<WorldLocation> ConvertWorldLocations()
        {
            if (worldConfiguration?.predefinedLocations == null) return new List<WorldLocation>();
            
            return worldConfiguration.predefinedLocations
                .Where(loc => loc != null)
                .Select(loc => new WorldLocation
                {
                    name = loc.locationName,
                    type = loc.locationType,
                    position = loc.preferredPosition,
                    capacity = loc.capacity,
                    needsSatisfied = loc.needsSatisfied ?? new string[0],
                    accessCost = loc.accessCost,
                    requiresPermission = loc.requiresPermission
                }).ToList();
        }
        
        private List<WorldResource> ConvertWorldResources()
        {
            if (worldConfiguration?.predefinedResources == null) return new List<WorldResource>();
            
            return worldConfiguration.predefinedResources
                .Where(res => res != null)
                .Select(res => new WorldResource
                {
                    name = res.resourceName,
                    type = res.resourceType,
                    scarcity = res.scarcity,
                    value = res.value,
                    regenerationRate = res.regenerationRate,
                    maxQuantity = res.maxQuantity,
                    description = res.resourceName
                }).ToList();
        }
        
        private List<ExperimentalGroup> ConvertAgentGroups()
        {
            if (agentGroups == null) return new List<ExperimentalGroup>();
            
            return agentGroups
                .Where(group => group != null)
                .Select(group => new ExperimentalGroup
                {
                    groupName = group.groupName,
                    agentCount = group.agentCount,
                    llmModel = group.llmModel,
                    archetype = group.archetype,
                    personalityVariance = group.personalityVariance,
                    enableSpecialBehaviors = group.enableSpecialBehaviors,
                    groupDescription = group.groupDescription
                }).ToList();
        }
        
        private MoodConfiguration ConvertMoodConfig()
        {
            if (moodConfiguration == null) return null;
            
            return new MoodConfiguration
            {
                enableMoodSystem = moodConfiguration.enableMoodSystem,
                moodChangeFrequency = moodConfiguration.moodChangeFrequency,
                moodVolatility = moodConfiguration.moodVolatility,
                conflictMoodImpact = moodConfiguration.conflictMoodImpact,
                successMoodBoost = moodConfiguration.successMoodBoost,
                failureMoodPenalty = moodConfiguration.failureMoodPenalty,
                socialMoodInfluence = moodConfiguration.socialMoodInfluence
            };
        }
    }
}