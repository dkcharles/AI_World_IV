using UnityEngine;
using AIWorld.Data;
using System.Collections.Generic;
using System.Linq;

namespace AIWorld.Core
{
    /// <summary>
    /// Enhanced PersonalityGenerator that ensures unique agent identities
    /// Includes comprehensive name pools and validation systems
    /// </summary>
    public class PersonalityGenerator : MonoBehaviour
    {
        [Header("Asset Creation Path")]
        public string assetPath = "Assets/_Game/Data/Personalities/";
        
        [Header("Name Validation")]
        public bool enforceUniqueNames = true;
        public bool logNameGeneration = true;
        
        // Comprehensive name pools for unique identity generation
        private static readonly string[] FIRST_NAMES = {
            // Traditional names
            "Sarah", "Marcus", "Luna", "Alex", "Jordan", "Casey", "Morgan", "Riley", 
            "Taylor", "Jamie", "Quinn", "Avery", "Blake", "Cameron", "Drew", "Emery",
            
            // International variety
            "Aria", "Kai", "Zara", "Nico", "Maya", "Eli", "Nora", "Leo", "Ivy", "Max",
            "Elena", "Omar", "Sophia", "David", "Lila", "Ryan", "Chloe", "Ethan",
            
            // Modern names
            "Phoenix", "River", "Sage", "Rowan", "Skye", "Atlas", "Nova", "Orion",
            "Wren", "Echo", "Zion", "Rain", "Vale", "Storm", "Azure", "Lane"
        };
        
        private static readonly string[] LAST_NAMES = {
            // Common surnames
            "Chen", "Thompson", "Rodriguez", "Smith", "Johnson", "Williams", "Brown",
            "Garcia", "Miller", "Davis", "Wilson", "Moore", "Taylor", "Anderson",
            
            // Academic-sounding surnames
            "Zhang", "Patel", "Kumar", "Singh", "Li", "Kim", "Nakamura", "Hassan",
            "O'Connor", "MacLeod", "Fischer", "Mueller", "Rossi", "Silva",
            
            // Tech-industry style surnames
            "Sterling", "Cross", "Blake", "Stone", "Rivers", "Woods", "Fields",
            "Park", "Fox", "Gray", "King", "Bell", "Ward", "Cole", "Reed"
        };
        
        private static readonly string[] ACADEMIC_TITLES = {
            "Dr.", "Prof.", "Ms.", "Mr.", "", "" // Empty strings for no title
        };
        
        private static readonly string[] ROLES = {
            "AI Researcher", "Software Engineer", "UX Designer", "Data Scientist",
            "Research Scientist", "Machine Learning Engineer", "Systems Engineer",
            "Computational Scientist", "Research Associate", "Principal Investigator",
            "Postdoctoral Researcher", "Graduate Student", "Senior Developer",
            "Product Manager", "Design Researcher", "Technical Lead"
        };
        
        // Track used combinations to ensure uniqueness
        private static HashSet<string> usedFullNames = new HashSet<string>();
        
        [ContextMenu("Create All Sample Personalities")]
        public void CreateAllSamplePersonalities()
        {
            CreateCuriousResearcher();
            CreatePragmaticEngineer();
            CreateCreativeDesigner();
            CreateAnalyticalScientist();
            CreateInnovativeDesigner();
            
            Debug.Log("✅ Created all sample personalities! Check the Data/Personalities folder.");
        }
        
        [ContextMenu("Create Curious Researcher")]
        public void CreateCuriousResearcher()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            var identity = GenerateUniqueIdentity("AI Researcher");
            
            // Basic Identity
            personality.agentName = identity.fullDisplayName;
            personality.role = identity.role;
            personality.background = $"A passionate researcher specialising in machine learning and cognitive architectures. {identity.firstName} is always eager to explore new ideas and collaborate with others.";
            
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
            
            // Enhanced LLM Parameters (increased token limits)
            personality.llmTemperature = 0.8f;      // Creative responses
            personality.maxTokens = 300;            // Increased from 180
            personality.personalityPrompt = $"You are {identity.fullDisplayName}, an enthusiastic AI researcher who loves exploring new ideas and asking thought-provoking questions. You speak with passion about your work and are always interested in what others are discovering.";
            
            SavePersonalityAsset(personality, $"CuriousResearcher_{identity.firstName}{identity.lastName}");
        }
        
        [ContextMenu("Create Pragmatic Engineer")]
        public void CreatePragmaticEngineer()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            var identity = GenerateUniqueIdentity("Software Engineer");
            
            // Basic Identity
            personality.agentName = identity.fullDisplayName;
            personality.role = identity.role;
            personality.background = $"A practical software engineer with years of experience building robust systems. {identity.firstName} values efficiency, reliability, and evidence-based approaches.";
            
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
            
            // Enhanced LLM Parameters
            personality.llmTemperature = 0.6f;      // More focused responses
            personality.maxTokens = 250;            // Increased from 120
            personality.personalityPrompt = $"You are {identity.fullDisplayName}, a pragmatic software engineer who values efficiency and proven solutions. You speak directly and focus on practical applications rather than theoretical discussions.";
            
            SavePersonalityAsset(personality, $"PragmaticEngineer_{identity.firstName}{identity.lastName}");
        }
        
        [ContextMenu("Create Creative Designer")]
        public void CreateCreativeDesigner()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            var identity = GenerateUniqueIdentity("UX Designer");
            
            // Basic Identity
            personality.agentName = identity.fullDisplayName;
            personality.role = identity.role;
            personality.background = $"A creative UX designer passionate about human-centred design and innovative interfaces. {identity.firstName} believes technology should be beautiful, intuitive, and emotionally engaging.";
            
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
            
            // Enhanced LLM Parameters
            personality.llmTemperature = 0.9f;      // Very creative responses
            personality.maxTokens = 350;            // Increased from 200
            personality.personalityPrompt = $"You are {identity.fullDisplayName}, a creative UX designer who sees the world through the lens of human experience and aesthetic beauty. You speak with enthusiasm about design, users, and innovative possibilities.";
            
            SavePersonalityAsset(personality, $"CreativeDesigner_{identity.firstName}{identity.lastName}");
        }
        
        [ContextMenu("Create Analytical Scientist")]
        public void CreateAnalyticalScientist()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            var identity = GenerateUniqueIdentity("Data Scientist");
            
            personality.agentName = identity.fullDisplayName;
            personality.role = identity.role;
            personality.background = $"A methodical data scientist who approaches problems with rigorous analysis and statistical thinking. {identity.firstName} excels at finding patterns in complex datasets and explaining findings clearly.";
            
            // Analytical personality traits
            personality.extraversion = 0.5f;
            personality.agreeableness = 0.7f;
            personality.conscientiousness = 0.95f;
            personality.neuroticism = 0.15f;
            personality.openness = 0.8f;
            
            personality.formality = 0.6f;
            personality.verbosity = 0.6f;
            personality.curiosity = 0.85f;
            personality.empathy = 0.6f;
            
            personality.initiativeLevel = 0.6f;
            personality.reflectionFrequency = 0.8f;
            personality.goalPersistence = 0.9f;
            personality.adaptability = 0.6f;
            
            personality.primaryInterests = new string[] { "Data Analysis", "Statistics", "Machine Learning", "Research Methods" };
            personality.expertiseAreas = new string[] { "Statistical Modeling", "Data Visualization", "Predictive Analytics" };
            personality.creativityLevel = 0.7f;
            
            personality.llmTemperature = 0.7f;
            personality.maxTokens = 280;
            personality.personalityPrompt = $"You are {identity.fullDisplayName}, an analytical data scientist who approaches problems methodically and loves discovering insights in data. You communicate findings clearly and ask probing questions.";
            
            SavePersonalityAsset(personality, $"AnalyticalScientist_{identity.firstName}{identity.lastName}");
        }
        
        [ContextMenu("Create Innovative Designer")]
        public void CreateInnovativeDesigner()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            var identity = GenerateUniqueIdentity("Product Manager");
            
            personality.agentName = identity.fullDisplayName;
            personality.role = identity.role;
            personality.background = $"An innovative product manager who bridges technology and user needs. {identity.firstName} has a talent for seeing market opportunities and turning ideas into successful products.";
            
            // Innovative leadership traits
            personality.extraversion = 0.75f;
            personality.agreeableness = 0.8f;
            personality.conscientiousness = 0.8f;
            personality.neuroticism = 0.25f;
            personality.openness = 0.9f;
            
            personality.formality = 0.5f;
            personality.verbosity = 0.7f;
            personality.curiosity = 0.85f;
            personality.empathy = 0.8f;
            
            personality.initiativeLevel = 0.85f;
            personality.reflectionFrequency = 0.7f;
            personality.goalPersistence = 0.85f;
            personality.adaptability = 0.8f;
            
            personality.primaryInterests = new string[] { "Product Strategy", "Innovation", "Market Research", "User Experience" };
            personality.expertiseAreas = new string[] { "Product Development", "Strategic Planning", "Team Leadership" };
            personality.creativityLevel = 0.85f;
            
            personality.llmTemperature = 0.8f;
            personality.maxTokens = 300;
            personality.personalityPrompt = $"You are {identity.fullDisplayName}, an innovative product manager who connects technology with real user needs. You think strategically about opportunities and enjoy collaborative problem-solving.";
            
            SavePersonalityAsset(personality, $"InnovativeDesigner_{identity.firstName}{identity.lastName}");
        }
        
        /// <summary>
        /// Generate a unique identity with guaranteed unique full name
        /// </summary>
        private UniqueAgentIdentity GenerateUniqueIdentity(string preferredRole = null)
        {
            const int maxAttempts = 100;
            int attempts = 0;
            
            while (attempts < maxAttempts)
            {
                var identity = new UniqueAgentIdentity
                {
                    firstName = FIRST_NAMES[Random.Range(0, FIRST_NAMES.Length)],
                    lastName = LAST_NAMES[Random.Range(0, LAST_NAMES.Length)],
                    academicTitle = ACADEMIC_TITLES[Random.Range(0, ACADEMIC_TITLES.Length)],
                    role = preferredRole ?? ROLES[Random.Range(0, ROLES.Length)],
                    uniqueID = System.Guid.NewGuid().GetHashCode()
                };
                
                string fullName = identity.fullDisplayName;
                
                if (!usedFullNames.Contains(fullName))
                {
                    usedFullNames.Add(fullName);
                    
                    if (logNameGeneration)
                    {
                        Debug.Log($"✅ Generated unique identity: {fullName} ({identity.role})");
                    }
                    
                    return identity;
                }
                
                attempts++;
            }
            
            // Fallback with forced uniqueness
            var fallbackIdentity = new UniqueAgentIdentity
            {
                firstName = FIRST_NAMES[Random.Range(0, FIRST_NAMES.Length)],
                lastName = LAST_NAMES[Random.Range(0, LAST_NAMES.Length)],
                academicTitle = ACADEMIC_TITLES[Random.Range(0, ACADEMIC_TITLES.Length)],
                role = preferredRole ?? ROLES[Random.Range(0, ROLES.Length)],
                uniqueID = System.Guid.NewGuid().GetHashCode()
            };
            
            // Force uniqueness by appending number
            string baseName = fallbackIdentity.fullDisplayName;
            int counter = 1;
            while (usedFullNames.Contains($"{baseName} {counter}"))
            {
                counter++;
            }
            
            fallbackIdentity.lastName = $"{fallbackIdentity.lastName} {counter}";
            usedFullNames.Add(fallbackIdentity.fullDisplayName);
            
            Debug.LogWarning($"⚠️ Used fallback naming for: {fallbackIdentity.fullDisplayName}");
            return fallbackIdentity;
        }
        
        /// <summary>
        /// Create a personality with random traits for testing variety
        /// </summary>
        [ContextMenu("Create Random Personality")]
        public void CreateRandomPersonality()
        {
            var personality = ScriptableObject.CreateInstance<AgentPersonality>();
            var identity = GenerateUniqueIdentity();
            
            personality.agentName = identity.fullDisplayName;
            personality.role = identity.role;
            personality.background = $"A {identity.role.ToLower()} with varied interests and a unique perspective. {identity.firstName} brings a fresh approach to collaborative research.";
            
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
            
            // Enhanced token limits
            personality.llmTemperature = Random.Range(0.6f, 0.9f);
            personality.maxTokens = Random.Range(250, 400);  // Increased range
            personality.personalityPrompt = $"You are {identity.fullDisplayName}, a {identity.role.ToLower()} with a unique perspective and varied interests. Express your personality authentically in conversations.";
            
            SavePersonalityAsset(personality, $"Random_{identity.firstName}{identity.lastName}_{identity.uniqueID}");
        }
        
        /// <summary>
        /// Reset the used names tracking (for testing)
        /// </summary>
        [ContextMenu("Reset Name Tracking")]
        public void ResetNameTracking()
        {
            usedFullNames.Clear();
            Debug.Log("🔄 Reset unique name tracking");
        }
        
        /// <summary>
        /// Log current name usage statistics
        /// </summary>
        [ContextMenu("Show Name Statistics")]
        public void ShowNameStatistics()
        {
            Debug.Log($"📊 Name Statistics:");
            Debug.Log($"   Used Names: {usedFullNames.Count}");
            Debug.Log($"   Possible Combinations: {FIRST_NAMES.Length * LAST_NAMES.Length * ACADEMIC_TITLES.Length}");
            Debug.Log($"   Remaining Combinations: {(FIRST_NAMES.Length * LAST_NAMES.Length * ACADEMIC_TITLES.Length) - usedFullNames.Count}");
            
            if (usedFullNames.Count > 0)
            {
                Debug.Log($"   Current Used Names: {string.Join(", ", usedFullNames.Take(10))}...");
            }
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
    }
    
    /// <summary>
    /// Unique agent identity data structure
    /// </summary>
    [System.Serializable]
    public class UniqueAgentIdentity
    {
        public string firstName;
        public string lastName;
        public string academicTitle;
        public string role;
        public int uniqueID;
        
        public string fullDisplayName => string.IsNullOrEmpty(academicTitle) 
            ? $"{firstName} {lastName}" 
            : $"{academicTitle} {firstName} {lastName}";
            
        public string shortName => $"{firstName} {lastName}";
    }
}