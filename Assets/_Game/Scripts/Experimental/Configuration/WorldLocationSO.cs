using UnityEngine;

namespace AIWorld.Experimental
{
    /// <summary>
    /// World location configuration ScriptableObject
    /// Defines a specific location that can be generated in the world
    /// </summary>
    [CreateAssetMenu(fileName = "New World Location", menuName = "AI World/World Location")]
    public class WorldLocationSO : ScriptableObject
    {
        [Header("Location Identity")]
        public string locationName = "Location";
        public LocationType locationType = LocationType.SocialArea;
        [TextArea(2, 3)]
        public string locationDescription = "";
        
        [Header("Location Properties")]
        public int capacity = 4;
        public string[] needsSatisfied = new string[0];
        public float accessCost = 0f;
        public bool requiresPermission = false;
        
        [Header("Visual Representation")]
        public GameObject locationPrefab;
        public Material locationMaterial;
        public Color locationColor = Color.white;
        public Vector3 preferredScale = Vector3.one;
        
        [Header("Positioning")]
        public bool usePreferredPosition = false;
        public Vector3 preferredPosition = Vector3.zero;
        public bool allowRandomPlacement = true;
        
        [Header("Context Suitability")]
        public bool universityContext = true;
        public bool survivalContext = true;
        public bool socialContext = true;
        
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(locationName) && capacity > 0;
        }
        
        public string GetValidationMessage()
        {
            if (string.IsNullOrEmpty(locationName)) return "Location name is required";
            if (capacity <= 0) return "Capacity must be positive";
            return "Location configuration is valid";
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
            if (!string.IsNullOrEmpty(locationDescription))
                return locationDescription;
                
            return context switch
            {
                SimulationContext.University => GetUniversityDescription(),
                SimulationContext.Survival => GetSurvivalDescription(),
                SimulationContext.Social => GetSocialDescription(),
                _ => $"A {locationType} location"
            };
        }
        
        private string GetUniversityDescription()
        {
            return locationType switch
            {
                LocationType.WorkArea => "A dedicated space for research and academic work",
                LocationType.SocialArea => "Faculty lounge for networking and collaboration",
                LocationType.MeetingArea => "Conference room for academic meetings",
                LocationType.KnowledgeArea => "Library or study area for learning",
                _ => "An academic facility"
            };
        }
        
        private string GetSurvivalDescription()
        {
            return locationType switch
            {
                LocationType.ResourceSite => "A location with essential survival resources",
                LocationType.ShelterArea => "Protected area for rest and safety",
                LocationType.SafetyArea => "High ground for observation and security",
                LocationType.SocialArea => "Gathering place for group coordination",
                _ => "A survival location"
            };
        }
        
        private string GetSocialDescription()
        {
            return locationType switch
            {
                LocationType.SocialArea => "Popular gathering spot for social interaction",
                LocationType.IntimateArea => "Private space for close conversations",
                LocationType.GatheringArea => "Central area for group activities",
                LocationType.RelaxationArea => "Comfortable space for unwinding",
                _ => "A social venue"
            };
        }
    }
}