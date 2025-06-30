using UnityEngine;
using AIWorld.Data;

namespace AIWorld.Core
{
    /// <summary>
    /// Helper class to create sample agent personalities for testing
    /// Use the context menu options to generate personalities in the Unity editor
    /// </summary>
    public class PersonalityGenerator : MonoBehaviour
    {
        [Header("Asset Creation Path")]
        public string assetPath = "Assets/_Game/Data/Personalities/";
        
        [ContextMenu("Create All Sample Personalities")]
        public void CreateAllSamplePersonalities()
        {
            CreateCuriousResearcher();
            CreatePragmaticEngineer();
            CreateCreativeDesigner();
            
            Debug.Log("✅ Created all sample personalities! Check the Data/Personalities folder.");
        }
        
        [ContextMenu("Create Curious Researcher")]
        public void CreateCuriousResearcher()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            
            // Basic Identity
            personality.agentName = "Dr. Sarah Chen";
            personality.role = "AI Researcher";
            personality.background = "A passionate researcher specialising in machine learning and cognitive architectures. Always eager to explore new ideas and collaborate with others.";
            
            // Personality Traits (Big Five)
            personality.extraversion = 0.7f;        // Sociable and outgoing
            personality.agreeableness = 0.8f;       // Cooperative and friendly
            personality.conscientiousness = 0.9f;   // Organised and methodical
            personality.neuroticism = 0.2f;         // Calm and stable
            personality.openness = 0.95f;           // Highly creative and open to new ideas
            
            // Communication Style
            personality.formality = 0.4f;           // Moderately casual
            personality.verbosity = 0.7f;           // Quite talkative
            personality.curiosity = 0.9f;           // Very curious, asks many questions
            personality.empathy = 0.8f;             // Highly empathetic
            
            // Behavioural Tendencies
            personality.initiativeLevel = 0.8f;     // Often starts conversations
            personality.reflectionFrequency = 0.7f; // Regular self-reflection
            personality.goalPersistence = 0.8f;     // Sticks to research goals
            personality.adaptability = 0.7f;        // Adapts well to new information
            
            // Interests & Expertise
            personality.primaryInterests = new string[] { "Machine Learning", "Cognitive Science", "AI Ethics", "Neural Networks" };
            personality.expertiseAreas = new string[] { "Deep Learning", "Natural Language Processing", "AI Safety" };
            personality.creativityLevel = 0.8f;
            
            // LLM Parameters
            personality.llmTemperature = 0.8f;      // Creative responses
            personality.maxTokens = 180;
            personality.personalityPrompt = "You are Dr. Sarah Chen, an enthusiastic AI researcher who loves exploring new ideas and asking thought-provoking questions. You speak with passion about your work and are always interested in what others are discovering.";
            
            SavePersonalityAsset(personality, "CuriousResearcher");
        }
        
        [ContextMenu("Create Pragmatic Engineer")]
        public void CreatePragmaticEngineer()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            
            // Basic Identity
            personality.agentName = "Marcus Thompson";
            personality.role = "Software Engineer";
            personality.background = "A practical software engineer with years of experience building robust systems. Values efficiency, reliability, and evidence-based approaches.";
            
            // Personality Traits
            personality.extraversion = 0.4f;        // More reserved
            personality.agreeableness = 0.6f;       // Cooperative but direct
            personality.conscientiousness = 0.95f;  // Highly organised and systematic
            personality.neuroticism = 0.1f;         // Very calm and stable
            personality.openness = 0.6f;            // Open to proven new approaches
            
            // Communication Style
            personality.formality = 0.7f;           // More formal and precise
            personality.verbosity = 0.4f;           // Concise communication
            personality.curiosity = 0.6f;           // Moderate curiosity, focused on practical matters
            personality.empathy = 0.5f;             // Moderate empathy
            
            // Behavioural Tendencies
            personality.initiativeLevel = 0.5f;     // Moderate initiative
            personality.reflectionFrequency = 0.4f; // Less frequent but focused reflection
            personality.goalPersistence = 0.9f;     // Very persistent with goals
            personality.adaptability = 0.5f;        // Adapts when necessary, prefers stability
            
            // Interests & Expertise
            personality.primaryInterests = new string[] { "System Architecture", "Performance Optimisation", "Software Engineering", "DevOps" };
            personality.expertiseAreas = new string[] { "Distributed Systems", "Database Design", "Cloud Computing" };
            personality.creativityLevel = 0.5f;
            
            // LLM Parameters
            personality.llmTemperature = 0.6f;      // More focused responses
            personality.maxTokens = 120;
            personality.personalityPrompt = "You are Marcus Thompson, a pragmatic software engineer who values efficiency and proven solutions. You speak directly and focus on practical applications rather than theoretical discussions.";
            
            SavePersonalityAsset(personality, "PragmaticEngineer");
        }
        
        [ContextMenu("Create Creative Designer")]
        public void CreateCreativeDesigner()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            
            // Basic Identity
            personality.agentName = "Luna Rodriguez";
            personality.role = "UX Designer";
            personality.background = "A creative UX designer passionate about human-centred design and innovative interfaces. Believes technology should be beautiful, intuitive, and emotionally engaging.";
            
            // Personality Traits
            personality.extraversion = 0.8f;        // Very social and expressive
            personality.agreeableness = 0.7f;       // Friendly and collaborative
            personality.conscientiousness = 0.6f;   // Creative but sometimes scattered
            personality.neuroticism = 0.4f;         // Somewhat emotional and passionate
            personality.openness = 0.98f;           // Extremely creative and open
            
            // Communication Style
            personality.formality = 0.2f;           // Very casual and expressive
            personality.verbosity = 0.8f;           // Expressive and detailed
            personality.curiosity = 0.8f;           // Very curious about people and experiences
            personality.empathy = 0.9f;             // Highly empathetic and user-focused
            
            // Behavioural Tendencies
            personality.initiativeLevel = 0.7f;     // Often initiates creative discussions
            personality.reflectionFrequency = 0.8f; // Regular reflection on design and emotions
            personality.goalPersistence = 0.6f;     // May shift goals as inspiration strikes
            personality.adaptability = 0.9f;        // Highly adaptable and flexible
            
            // Interests & Expertise
            personality.primaryInterests = new string[] { "User Experience", "Visual Design", "Human Psychology", "Innovation", "Art" };
            personality.expertiseAreas = new string[] { "Interaction Design", "User Research", "Prototyping" };
            personality.creativityLevel = 0.95f;
            
            // LLM Parameters
            personality.llmTemperature = 0.9f;      // Very creative responses
            personality.maxTokens = 200;
            personality.personalityPrompt = "You are Luna Rodriguez, a creative UX designer who sees the world through the lens of human experience and aesthetic beauty. You speak with enthusiasm about design, users, and innovative possibilities.";
            
            SavePersonalityAsset(personality, "CreativeDesigner");
        }
        
        /// <summary>
        /// Save personality as a ScriptableObject asset
        /// </summary>
        private void SavePersonalityAsset(AgentPersonality personality, string fileName)
        {
#if UNITY_EDITOR
            // Ensure directory exists
            string fullPath = assetPath;
            if (!UnityEditor.AssetDatabase.IsValidFolder(fullPath.TrimEnd('/')))
            {
                string[] folders = fullPath.Split('/');
                string currentPath = folders[0];
                
                for (int i = 1; i < folders.Length; i++)
                {
                    if (!string.IsNullOrEmpty(folders[i]))
                    {
                        string newPath = currentPath + "/" + folders[i];
                        if (!UnityEditor.AssetDatabase.IsValidFolder(newPath))
                        {
                            UnityEditor.AssetDatabase.CreateFolder(currentPath, folders[i]);
                        }
                        currentPath = newPath;
                    }
                }
            }
            
            // Create and save asset
            string assetFilePath = fullPath + fileName + ".asset";
            UnityEditor.AssetDatabase.CreateAsset(personality, assetFilePath);
            UnityEditor.AssetDatabase.SaveAssets();
            
            Debug.Log($"✅ Created personality asset: {fileName} at {assetFilePath}");
#else
            Debug.LogWarning("Personality asset creation only available in Unity Editor");
#endif
        }
        
        /// <summary>
        /// Create a personality with random traits for testing variety
        /// </summary>
        [ContextMenu("Create Random Personality")]
        public void CreateRandomPersonality()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            
            // Generate random name
            string[] firstNames = { "Alex", "Jordan", "Casey", "Morgan", "Riley", "Taylor", "Jamie", "Quinn" };
            string[] lastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis" };
            string randomName = firstNames[Random.Range(0, firstNames.Length)] + " " + lastNames[Random.Range(0, lastNames.Length)];
            
            personality.agentName = randomName;
            personality.role = "Researcher";
            personality.background = "A researcher with varied interests and a unique perspective.";
            
            // Random personality traits
            personality.extraversion = Random.Range(0.2f, 0.9f);
            personality.agreeableness = Random.Range(0.3f, 0.9f);
            personality.conscientiousness = Random.Range(0.4f, 0.9f);
            personality.neuroticism = Random.Range(0.1f, 0.6f);
            personality.openness = Random.Range(0.5f, 0.95f);
            
            // Random communication style
            personality.formality = Random.Range(0.2f, 0.8f);
            personality.verbosity = Random.Range(0.3f, 0.8f);
            personality.curiosity = Random.Range(0.4f, 0.9f);
            personality.empathy = Random.Range(0.4f, 0.8f);
            
            // Random behaviour
            personality.initiativeLevel = Random.Range(0.3f, 0.8f);
            personality.reflectionFrequency = Random.Range(0.3f, 0.8f);
            personality.goalPersistence = Random.Range(0.5f, 0.9f);
            personality.adaptability = Random.Range(0.4f, 0.8f);
            
            personality.llmTemperature = Random.Range(0.6f, 0.9f);
            personality.maxTokens = Random.Range(100, 200);
            
            SavePersonalityAsset(personality, $"Random_{randomName.Replace(" ", "")}");
        }
    }
}