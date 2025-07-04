using UnityEngine;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Conflict system configuration ScriptableObject
    /// Defines conflict generation and management settings
    /// </summary>
    [CreateAssetMenu(fileName = "New Conflict Configuration", menuName = "AI World/Conflict Configuration")]
    public class ConflictConfigurationSO : ScriptableObject
    {
        [Header("Conflict System")]
        public bool enableConflictSystem = true;
        [Range(0f, 1f)]
        public float baseConflictProbability = 0.2f;
        public float conflictCheckInterval = 30f;
        public float maxConflictIntensity = 1.0f;
        
        [Header("Conflict Types")]
        public bool enableResourceConflicts = true;
        public bool enablePersonalityConflicts = true;
        public bool enableTerritorialConflicts = true;
        public bool enableGoalConflicts = true;
        
        [Header("Escalation Settings")]
        [Range(0f, 1f)]
        public float escalationProbability = 0.3f;
        [Range(0f, 1f)]
        public float deEscalationRate = 0.1f;
        public float conflictMemoryDuration = 300f; // 5 minutes
        
        [Header("Context-Specific Weights")]
        [Range(0f, 2f)]
        public float resourceCompetitionWeight = 1.0f;
        [Range(0f, 2f)]
        public float personalityClashWeight = 1.0f;
        [Range(0f, 2f)]
        public float territorialWeight = 1.0f;
        [Range(0f, 2f)]
        public float goalConflictWeight = 1.0f;
        
        [Header("Stress and Mood Impact")]
        [Range(0f, 1f)]
        public float stressEscalationRate = 0.4f;
        [Range(0f, 1f)]
        public float cooperationPenalty = 0.5f;
        [Range(0f, 1f)]
        public float conflictMoodImpact = 0.6f;
        
        [Header("Context-Specific Conflict Types")]
        public string[] universityConflictTypes = {
            "Research Competition",
            "Publication Credit", 
            "Lab Access",
            "Grant Competition",
            "Academic Disagreement"
        };
        
        public string[] survivalConflictTypes = {
            "Food Competition",
            "Shelter Dispute", 
            "Water Access",
            "Territory Claim",
            "Resource Hoarding"
        };
        
        public string[] socialConflictTypes = {
            "Romantic Rivalry",
            "Social Status", 
            "Attention Seeking",
            "Personality Clash",
            "Influence Competition"
        };
        
        public bool IsValid()
        {
            return conflictCheckInterval > 0 && maxConflictIntensity > 0 && conflictMemoryDuration > 0;
        }
        
        public string GetValidationMessage()
        {
            if (conflictCheckInterval <= 0) return "Conflict check interval must be positive";
            if (maxConflictIntensity <= 0) return "Max conflict intensity must be positive";
            if (conflictMemoryDuration <= 0) return "Conflict memory duration must be positive";
            return "Conflict configuration is valid";
        }
        
        /// <summary>
        /// Get conflict types appropriate for the simulation context
        /// </summary>
        public string[] GetConflictTypesForContext(SimulationContext context)
        {
            return context switch
            {
                SimulationContext.University => universityConflictTypes,
                SimulationContext.Survival => survivalConflictTypes,
                SimulationContext.Social => socialConflictTypes,
                _ => new string[] { "Resource Dispute", "Goal Conflict", "Personality Clash" }
            };
        }
        
        /// <summary>
        /// Apply context-specific defaults
        /// </summary>
        [ContextMenu("Apply Context Defaults")]
        public void ApplyContextDefaults(SimulationContext context)
        {
            switch (context)
            {
                case SimulationContext.University:
                    resourceCompetitionWeight = 0.8f;
                    personalityClashWeight = 0.6f;
                    territorialWeight = 0.4f;
                    goalConflictWeight = 0.9f;
                    stressEscalationRate = 0.3f;
                    cooperationPenalty = 0.5f;
                    break;
                    
                case SimulationContext.Survival:
                    resourceCompetitionWeight = 1.0f;
                    personalityClashWeight = 0.4f;
                    territorialWeight = 0.9f;
                    goalConflictWeight = 0.7f;
                    stressEscalationRate = 0.6f;
                    cooperationPenalty = 0.2f;
                    break;
                    
                case SimulationContext.Social:
                    resourceCompetitionWeight = 0.3f;
                    personalityClashWeight = 0.9f;
                    territorialWeight = 0.5f;
                    goalConflictWeight = 0.8f;
                    stressEscalationRate = 0.4f;
                    cooperationPenalty = 0.7f;
                    break;
            }
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// Create context configuration data structure for legacy compatibility
        /// </summary>
        public ConflictContextConfig CreateContextConfig(SimulationContext context)
        {
            return new ConflictContextConfig
            {
                resourceCompetitionWeight = this.resourceCompetitionWeight,
                personalityClashWeight = this.personalityClashWeight,
                territorialWeight = this.territorialWeight,
                goalConflictWeight = this.goalConflictWeight,
                stressEscalationRate = this.stressEscalationRate,
                cooperationPenalty = this.cooperationPenalty,
                commonConflictTypes = GetConflictTypesForContext(context)
            };
        }
    }
}