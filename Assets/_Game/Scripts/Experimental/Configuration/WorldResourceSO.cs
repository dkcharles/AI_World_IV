using UnityEngine;

namespace AIWorld.Experimental
{
    /// <summary>
    /// World resource configuration ScriptableObject
    /// Defines a specific resource type that can be generated in the world
    /// </summary>
    [CreateAssetMenu(fileName = "New World Resource", menuName = "AI World/World Resource")]
    public class WorldResourceSO : ScriptableObject
    {
        [Header("Resource Identity")]
        public string resourceName = "Resource";
        public ResourceType resourceType = ResourceType.Consumable;
        [TextArea(2, 3)]
        public string resourceDescription = "";
        
        [Header("Resource Properties")]
        [Range(0f, 1f)]
        public float scarcity = 0.5f; // 0 = abundant, 1 = very rare
        public int value = 50;
        public float regenerationRate = 0.1f; // per second
        public int maxQuantity = 10;
        
        [Header("Visual Representation")]
        public GameObject resourcePrefab;
        public Material resourceMaterial;
        public Color resourceColor = Color.white;
        public Vector3 preferredScale = Vector3.one;
        
        [Header("Collection Properties")]
        public float collectionTime = 1f; // seconds to collect
        public bool consumeOnUse = true;
        public bool requiresTools = false;
        public string[] requiredTools = new string[0];
        
        [Header("Context Suitability")]
        public bool universityContext = true;
        public bool survivalContext = true;
        public bool socialContext = true;
        
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(resourceName) && value > 0 && maxQuantity > 0;
        }
        
        public string GetValidationMessage()
        {
            if (string.IsNullOrEmpty(resourceName)) return "Resource name is required";
            if (value <= 0) return "Value must be positive";
            if (maxQuantity <= 0) return "Max quantity must be positive";
            return "Resource configuration is valid";
        }
        
        public bool IsSuitableForContext(SimulationContext context)
        {
            return context switch
            {
                SimulationContext.University => universityContext,
                SimulationContext.Survival => survivalContext,
                SimulationContext.Social => socialContext,
                _ => true
            };
        }
        
        public string GetContextDescription(SimulationContext context)
        {
            if (!string.IsNullOrEmpty(resourceDescription))
                return resourceDescription;
                
            return context switch
            {
                SimulationContext.University => GetUniversityDescription(),
                SimulationContext.Survival => GetSurvivalDescription(),
                SimulationContext.Social => GetSocialDescription(),
                _ => $"A {resourceType} resource"
            };
        }
        
        private string GetUniversityDescription()
        {
            return resourceType switch
            {
                ResourceType.Currency => "Research funding or grant money",
                ResourceType.Tool => "Laboratory equipment or research tools",
                ResourceType.Achievement => "Publication opportunity or academic recognition",
                ResourceType.Special => "Exclusive access or academic privilege",
                _ => "Academic resource"
            };
        }
        
        private string GetSurvivalDescription()
        {
            return resourceType switch
            {
                ResourceType.Essential => "Vital survival resource like water",
                ResourceType.Consumable => "Food or consumable survival item",
                ResourceType.Tool => "Survival equipment or crafting material",
                ResourceType.Currency => "Trade goods or valuable materials",
                _ => "Survival resource"
            };
        }
        
        private string GetSocialDescription()
        {
            return resourceType switch
            {
                ResourceType.Social => "Social token or relationship currency",
                ResourceType.Special => "Exclusive privilege or VIP access",
                ResourceType.Currency => "Social influence or status points",
                ResourceType.Achievement => "Social recognition or status symbol",
                _ => "Social resource"
            };
        }
        
        /// <summary>
        /// Calculate effective scarcity based on context multiplier
        /// </summary>
        public float GetEffectiveScarcity(float contextMultiplier = 1f)
        {
            return Mathf.Clamp01(scarcity * contextMultiplier);
        }
        
        /// <summary>
        /// Calculate number of instances to spawn based on scarcity
        /// </summary>
        public int CalculateInstanceCount(int maxInstances, float contextMultiplier = 1f)
        {
            var effectiveScarcity = GetEffectiveScarcity(contextMultiplier);
            var availability = 1f - effectiveScarcity;
            var baseCount = Mathf.RoundToInt(availability * 10f) + 1;
            return Mathf.Min(baseCount, maxInstances / 3); // Don't let one resource dominate
        }
    }
}