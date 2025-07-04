using UnityEngine;
using AIWorld.Research;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Agent group configuration ScriptableObject
    /// Defines settings for a specific group of agents in an experiment
    /// </summary>
    [CreateAssetMenu(fileName = "New Agent Group", menuName = "AI World/Agent Group Configuration")]
    public class AgentGroupConfigurationSO : ScriptableObject
    {
        [Header("Group Identity")]
        public string groupName = "Default Group";
        [TextArea(2, 3)]
        public string groupDescription = "";
        public Color groupColor = Color.white;
        
        [Header("Group Composition")]
        public int agentCount = 3;
        public AgentArchetype archetype = AgentArchetype.Balanced;
        [Range(0f, 1f)]
        public float personalityVariance = 0.3f; // 0-1
        
        [Header("LLM Configuration")]
        public string llmModel = "qwen2.5:3b";
        public string alternativeLLMModel = ""; // Fallback if primary unavailable
        public int maxTokens = 4096;
        [Range(0f, 2f)]
        public float temperature = 0.7f;
        
        [Header("Special Behaviors")]
        public bool enableSpecialBehaviors = false;
        public string[] specialBehaviorTags = new string[0];
        
        [Header("Research Settings")]
        public bool trackIndividualMetrics = true;
        public bool enableGroupComparison = true;
        
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(groupName) && 
                   agentCount > 0 && 
                   !string.IsNullOrEmpty(llmModel);
        }
        
        public string GetValidationMessage()
        {
            if (string.IsNullOrEmpty(groupName)) return "Group name is required";
            if (agentCount <= 0) return "Agent count must be positive";
            if (string.IsNullOrEmpty(llmModel)) return "LLM model is required";
            return "Group configuration is valid";
        }
        
        /// <summary>
        /// Get display name for UI
        /// </summary>
        public string GetDisplayName()
        {
            return $"{groupName} ({agentCount} {archetype} agents, {llmModel})";
        }
        
        /// <summary>
        /// Create a copy of this configuration with modified settings
        /// </summary>
        public AgentGroupConfigurationSO CreateVariant(string variantName, int newAgentCount = -1)
        {
            var variant = CreateInstance<AgentGroupConfigurationSO>();
            
            // Copy all settings
            variant.groupName = variantName;
            variant.groupDescription = this.groupDescription;
            variant.groupColor = this.groupColor;
            variant.agentCount = newAgentCount > 0 ? newAgentCount : this.agentCount;
            variant.archetype = this.archetype;
            variant.personalityVariance = this.personalityVariance;
            variant.llmModel = this.llmModel;
            variant.alternativeLLMModel = this.alternativeLLMModel;
            variant.maxTokens = this.maxTokens;
            variant.temperature = this.temperature;
            variant.enableSpecialBehaviors = this.enableSpecialBehaviors;
            variant.specialBehaviorTags = (string[])this.specialBehaviorTags.Clone();
            variant.trackIndividualMetrics = this.trackIndividualMetrics;
            variant.enableGroupComparison = this.enableGroupComparison;
            
            return variant;
        }
    }
}