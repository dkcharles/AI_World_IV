using UnityEngine;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Mood system configuration ScriptableObject
    /// Defines mood behavior and visualization settings
    /// </summary>
    [CreateAssetMenu(fileName = "New Mood Configuration", menuName = "AI World/Mood Configuration")]
    public class MoodConfigurationSO : ScriptableObject
    {
        [Header("Mood System")]
        public bool enableMoodSystem = true;
        public float moodChangeFrequency = 300f; // 5 minutes
        [Range(0f, 1f)]
        public float moodVolatility = 0.4f;
        
        [Header("Mood Influences")]
        [Range(0f, 1f)]
        public float conflictMoodImpact = 0.6f;
        [Range(0f, 1f)]
        public float successMoodBoost = 0.3f;
        [Range(0f, 1f)]
        public float failureMoodPenalty = 0.4f;
        [Range(0f, 1f)]
        public float socialMoodInfluence = 0.2f;
        
        [Header("Mood Persistence")]
        public float moodDecayRate = 0.1f; // per minute
        public float moodMemoryDuration = 600f; // 10 minutes
        public bool enableMoodContagion = true;
        [Range(0f, 1f)]
        public float moodContagionStrength = 0.3f;
        
        [Header("Context-Specific Modifiers")]
        [Range(0f, 2f)]
        public float universityMoodMultiplier = 1.0f;
        [Range(0f, 2f)]
        public float survivalMoodMultiplier = 1.2f;
        [Range(0f, 2f)]
        public float socialMoodMultiplier = 1.5f;
        
        [Header("Mood Visualization")]
        public bool enableMoodVisualization = true;
        public bool enableMoodUIDisplay = true;
        public Material moodVisualizationMaterial;
        
        [Header("Mood Colors")]
        public Color ecstaticColor = Color.magenta;
        public Color joyfulColor = Color.yellow;
        public Color contentColor = Color.green;
        public Color calmColor = Color.cyan;
        public Color neutralColor = Color.white;
        public Color pensiveColor = Color.blue;
        public Color melancholicColor = Color.gray;
        public Color frustratedColor = new Color(1f, 0.5f, 0f); // Orange
        public Color angryColor = Color.red;
        public Color fearfulColor = new Color(0.5f, 0f, 0.5f); // Dark purple
        public Color depressedColor = Color.black;
        
        [Header("Personality Mood Modifiers")]
        [Range(0f, 2f)]
        public float extraversionMoodMultiplier = 1.2f;
        [Range(0f, 2f)]
        public float neuroticismMoodMultiplier = 1.5f;
        [Range(0f, 2f)]
        public float agreeablenessMoodStabilizer = 0.8f;
        
        public bool IsValid()
        {
            return moodChangeFrequency > 0 && moodMemoryDuration > 0;
        }
        
        public string GetValidationMessage()
        {
            if (moodChangeFrequency <= 0) return "Mood change frequency must be positive";
            if (moodMemoryDuration <= 0) return "Mood memory duration must be positive";
            return "Mood configuration is valid";
        }
        
        /// <summary>
        /// Get mood color for a specific mood state
        /// </summary>
        public Color GetMoodColor(AgentMoodState moodState)
        {
            return moodState switch
            {
                AgentMoodState.Ecstatic => ecstaticColor,
                AgentMoodState.Joyful => joyfulColor,
                AgentMoodState.Content => contentColor,
                AgentMoodState.Calm => calmColor,
                AgentMoodState.Neutral => neutralColor,
                AgentMoodState.Pensive => pensiveColor,
                AgentMoodState.Melancholic => melancholicColor,
                AgentMoodState.Frustrated => frustratedColor,
                AgentMoodState.Angry => angryColor,
                AgentMoodState.Fearful => fearfulColor,
                AgentMoodState.Depressed => depressedColor,
                _ => neutralColor
            };
        }
        
        /// <summary>
        /// Get mood colors as an array for easy iteration
        /// </summary>
        public Color[] GetMoodColorsArray()
        {
            return new Color[]
            {
                ecstaticColor, joyfulColor, contentColor, calmColor, neutralColor,
                pensiveColor, melancholicColor, frustratedColor, angryColor, fearfulColor, depressedColor
            };
        }
        
        /// <summary>
        /// Get context-specific mood multiplier
        /// </summary>
        public float GetContextMoodMultiplier(SimulationContext context)
        {
            return context switch
            {
                SimulationContext.University => universityMoodMultiplier,
                SimulationContext.Survival => survivalMoodMultiplier,
                SimulationContext.Social => socialMoodMultiplier,
                _ => 1.0f
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
                    moodVolatility = 0.3f;
                    conflictMoodImpact = 0.5f;
                    successMoodBoost = 0.4f;
                    socialMoodInfluence = 0.3f;
                    universityMoodMultiplier = 1.0f;
                    break;
                    
                case SimulationContext.Survival:
                    moodVolatility = 0.6f;
                    conflictMoodImpact = 0.8f;
                    successMoodBoost = 0.5f;
                    socialMoodInfluence = 0.4f;
                    survivalMoodMultiplier = 1.2f;
                    break;
                    
                case SimulationContext.Social:
                    moodVolatility = 0.5f;
                    conflictMoodImpact = 0.6f;
                    successMoodBoost = 0.6f;
                    socialMoodInfluence = 0.7f;
                    socialMoodMultiplier = 1.5f;
                    break;
            }
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// Calculate personality-modified mood impact
        /// </summary>
        public float CalculatePersonalityMoodImpact(float baseImpact, float extraversion, float neuroticism, float agreeableness)
        {
            var impact = baseImpact;
            impact *= 1f + (extraversion - 0.5f) * (extraversionMoodMultiplier - 1f);
            impact *= 1f + (neuroticism - 0.5f) * (neuroticismMoodMultiplier - 1f);
            impact *= agreeablenessMoodStabilizer + (1f - agreeablenessMoodStabilizer) * agreeableness;
            return Mathf.Clamp01(impact);
        }
    }
    
    /// <summary>
    /// Mood states that agents can experience
    /// </summary>
    public enum AgentMoodState
    {
        Ecstatic,       // Extremely positive
        Joyful,         // Very positive
        Content,        // Positive
        Calm,           // Slightly positive
        Neutral,        // Baseline
        Pensive,        // Slightly negative
        Melancholic,    // Negative
        Frustrated,     // Very negative
        Angry,          // Extremely negative
        Fearful,        // Anxious/worried
        Depressed       // Lowest mood state
    }
}