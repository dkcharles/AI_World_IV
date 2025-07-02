using UnityEngine;

namespace AIWorld.Data
{
    /// <summary>
    /// ScriptableObject defining agent personality traits and behaviour parameters
    /// This allows designers to create different personality profiles in the Unity editor
    /// </summary>
    [CreateAssetMenu(fileName = "New Agent Personality", menuName = "AI World/Agent Personality")]
    public class AgentPersonality : ScriptableObject
    {
        [Header("Basic Identity")]
        [SerializeField] private string agentId; // Unique identifier - auto-generated
        public string agentName = "Agent";
        public string role = "Researcher";
        public string background = "A curious researcher interested in AI and innovation.";
        
        // Property to access agentId (read-only from outside)
        public string AgentId 
        {
            get 
            {
                if (string.IsNullOrEmpty(agentId))
                {
                    agentId = System.Guid.NewGuid().ToString("N")[..8]; // Short 8-character ID
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(this); // Mark for saving
                    #endif
                }
                return agentId;
            }
        }
        
        [Header("Personality Traits (0-1)")]
        [Range(0f, 1f)] public float extraversion = 0.5f;
        [Range(0f, 1f)] public float agreeableness = 0.5f;
        [Range(0f, 1f)] public float conscientiousness = 0.5f;
        [Range(0f, 1f)] public float neuroticism = 0.3f;
        [Range(0f, 1f)] public float openness = 0.7f;
        
        [Header("Communication Style")]
        [Range(0f, 1f)] public float formality = 0.5f; // How formal vs casual
        [Range(0f, 1f)] public float verbosity = 0.5f; // How much they talk
        [Range(0f, 1f)] public float curiosity = 0.7f; // How often they ask questions
        [Range(0f, 1f)] public float empathy = 0.6f;   // How much they consider others' feelings
        
        [Header("Behavioural Tendencies")]
        [Range(0f, 1f)] public float initiativeLevel = 0.6f; // How likely to start conversations
        [Range(0f, 1f)] public float reflectionFrequency = 0.5f; // How often they have inner dialogue
        [Range(0f, 1f)] public float goalPersistence = 0.7f; // How much they stick to goals
        [Range(0f, 1f)] public float adaptability = 0.6f; // How quickly they change plans
        
        [Header("Interests & Expertise")]
        public string[] primaryInterests = {"AI", "Research", "Innovation"};
        public string[] expertiseAreas = {"Machine Learning", "Cognitive Science"};
        public float creativityLevel = 0.7f;
        
        [Header("LLM Interaction Parameters")]
        [Range(0.1f, 2f)] public float llmTemperature = 0.7f; // Creativity in responses
        [Range(1, 200)] public int maxTokens = 150; // Response length
        public string personalityPrompt = "You are a curious and thoughtful researcher who enjoys collaborative discussions about AI and innovation.";
        
        /// <summary>
        /// Generate a context prompt for LLM that includes personality traits
        /// </summary>
        public string GeneratePersonalityContext()
        {
            string traits = "";
            
            if (extraversion > 0.7f) traits += "outgoing and sociable, ";
            else if (extraversion < 0.3f) traits += "reserved and introspective, ";
            
            if (agreeableness > 0.7f) traits += "cooperative and trusting, ";
            else if (agreeableness < 0.3f) traits += "competitive and sceptical, ";
            
            if (conscientiousness > 0.7f) traits += "organised and disciplined, ";
            else if (conscientiousness < 0.3f) traits += "flexible and spontaneous, ";
            
            if (openness > 0.7f) traits += "creative and open to new ideas, ";
            else if (openness < 0.3f) traits += "practical and traditional, ";
            
            if (neuroticism > 0.7f) traits += "sensitive and emotionally reactive, ";
            else if (neuroticism < 0.3f) traits += "calm and emotionally stable, ";
            
            traits = traits.TrimEnd(' ', ',');
            
            return $"You are {agentName}, a {role}. {background} " +
                   $"Your personality is {traits}. " +
                   $"Your main interests are: {string.Join(", ", primaryInterests)}. " +
                   $"{personalityPrompt}";
        }
        
        /// <summary>
        /// Get conversation starter probability based on personality
        /// </summary>
        public float GetConversationStarterProbability()
        {
            return (extraversion * 0.4f + initiativeLevel * 0.4f + curiosity * 0.2f);
        }
        
        /// <summary>
        /// Get inner dialogue frequency based on personality
        /// </summary>
        public float GetInnerDialogueFrequency()
        {
            return (reflectionFrequency * 0.5f + conscientiousness * 0.3f + neuroticism * 0.2f);
        }
    }
}