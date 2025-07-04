using UnityEngine;

namespace AIWorld.Experimental
{
    /// <summary>
    /// World configuration ScriptableObject
    /// Defines world generation settings for specific simulation contexts
    /// </summary>
    [CreateAssetMenu(fileName = "New World Configuration", menuName = "AI World/World Configuration")]
    public class WorldConfigurationSO : ScriptableObject
    {
        [Header("World Settings")]
        public SimulationContext simulationContext = SimulationContext.University;
        public Vector2 worldSize = new Vector2(50f, 50f);
        public bool generateLocationsAutomatically = true;
        public bool generateResourcesAutomatically = true;
        
        [Header("Context-Specific Assets")]
        public GameObject[] locationPrefabs;
        public GameObject[] resourcePrefabs;
        public Material[] environmentMaterials;
        
        [Header("World Generation")]
        public WorldLocationSO[] predefinedLocations;
        public WorldResourceSO[] predefinedResources;
        public int maxResourceInstances = 20;
        public float minLocationSpacing = 5f;
        public float minResourceSpacing = 3f;
        
        [Header("Environmental Features")]
        public GameObject[] decorativePrefabs;
        public int decorationDensity = 15;
        public Color ambientLightColor = Color.white;
        
        [Header("Context-Specific Settings")]
        [Range(0f, 2f)]
        public float competitionLevel = 1.0f;
        [Range(0f, 2f)]
        public float cooperationBonus = 1.0f;
        [Range(0f, 2f)]
        public float resourceScarcityMultiplier = 1.0f;
        [Range(0f, 1f)]
        public float socialImportance = 0.5f;
        
        public bool IsValid()
        {
            return worldSize.x > 0 && worldSize.y > 0 && maxResourceInstances > 0;
        }
        
        public string GetValidationMessage()
        {
            if (worldSize.x <= 0 || worldSize.y <= 0) return "World size must be positive";
            if (maxResourceInstances <= 0) return "Max resource instances must be positive";
            return "World configuration is valid";
        }
        
        /// <summary>
        /// Get context-appropriate goal types
        /// </summary>
        public string[] GetContextGoalTypes()
        {
            return simulationContext switch
            {
                SimulationContext.University => new string[] 
                { 
                    "Research", "Collaborate", "Publish", "Network", "Grant Application"
                },
                SimulationContext.Survival => new string[] 
                { 
                    "Gather", "Shelter", "Protect", "Explore", "Form Alliance"
                },
                SimulationContext.Social => new string[] 
                { 
                    "Connect", "Impress", "Romance", "Socialize", "Influence"
                },
                _ => new string[] 
                { 
                    "Achieve", "Interact", "Explore", "Learn", "Cooperate"
                }
            };
        }
        
        /// <summary>
        /// Get context-appropriate conflict types
        /// </summary>
        public string[] GetContextConflictTypes()
        {
            return simulationContext switch
            {
                SimulationContext.University => new string[] 
                { 
                    "Research Competition", "Publication Credit", "Lab Access", "Grant Competition"
                },
                SimulationContext.Survival => new string[] 
                { 
                    "Food Competition", "Shelter Dispute", "Water Access", "Territory Claim"
                },
                SimulationContext.Social => new string[] 
                { 
                    "Romantic Rivalry", "Social Status", "Attention Seeking", "Personality Clash"
                },
                _ => new string[] 
                { 
                    "Resource Dispute", "Goal Conflict", "Personality Clash", "Territorial Dispute"
                }
            };
        }
        
        /// <summary>
        /// Apply context-specific defaults
        /// </summary>
        [ContextMenu("Apply Context Defaults")]
        public void ApplyContextDefaults()
        {
            switch (simulationContext)
            {
                case SimulationContext.University:
                    competitionLevel = 1.2f;
                    cooperationBonus = 1.5f;
                    resourceScarcityMultiplier = 0.7f;
                    socialImportance = 0.6f;
                    ambientLightColor = new Color(0.9f, 0.9f, 1f); // Cool professional
                    break;
                    
                case SimulationContext.Survival:
                    competitionLevel = 1.8f;
                    cooperationBonus = 1.2f;
                    resourceScarcityMultiplier = 1.5f;
                    socialImportance = 0.4f;
                    ambientLightColor = new Color(0.8f, 0.7f, 0.6f); // Harsh natural
                    break;
                    
                case SimulationContext.Social:
                    competitionLevel = 1.0f;
                    cooperationBonus = 1.3f;
                    resourceScarcityMultiplier = 0.8f;
                    socialImportance = 0.9f;
                    ambientLightColor = new Color(1f, 0.9f, 0.8f); // Warm social
                    break;
            }
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
    }
}