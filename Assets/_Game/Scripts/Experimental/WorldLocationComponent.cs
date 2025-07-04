using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Research;
using AIWorld.Experimental;
using AIWorld.Agents;
using AIWorld.Needs;
using AIWorld.BDI;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Component for individual world locations that agents can interact with
    /// Handles need satisfaction, capacity management, and usage tracking
    /// </summary>
    public class WorldLocationComponent : MonoBehaviour
    {
        [Header("Location Configuration")]
        [SerializeField] private WorldLocation locationConfig;
        [SerializeField] private bool isActive = true;
        [SerializeField] private int currentOccupancy = 0;
        
        [Header("Interaction Settings")]
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private bool requiresLineOfSight = false;
        [SerializeField] private float needSatisfactionRate = 0.1f; // per second
        
        [Header("Visual Feedback")]
        [SerializeField] private Color availableColor = Color.green;
        [SerializeField] private Color occupiedColor = Color.yellow;
        [SerializeField] private Color fullColor = Color.red;
        [SerializeField] private bool showOccupancyIndicator = true;
        
        // Runtime data
        private List<Agent> currentOccupants = new List<Agent>();
        private Dictionary<string, DateTime> lastUsage = new Dictionary<string, DateTime>();
        private List<LocationUsageEvent> usageHistory = new List<LocationUsageEvent>();
        private Renderer locationRenderer;
        private Collider locationCollider;
        
        // Events
        public static event Action<WorldLocationComponent, Agent> OnAgentEntered;
        public static event Action<WorldLocationComponent, Agent> OnAgentExited;
        public static event Action<WorldLocationComponent> OnLocationFull;
        public static event Action<WorldLocationComponent> OnLocationAvailable;
        
        public WorldLocation LocationConfig => locationConfig;
        public bool IsAvailable => isActive && currentOccupancy < locationConfig.capacity;
        public bool IsFull => currentOccupancy >= locationConfig.capacity;
        public int CurrentOccupancy => currentOccupancy;
        public List<Agent> CurrentOccupants => new List<Agent>(currentOccupants);
        public float UtilizationRate => locationConfig.capacity > 0 ? (float)currentOccupancy / locationConfig.capacity : 0f;
        
        private void Awake()
        {
            locationRenderer = GetComponent<Renderer>();
            locationCollider = GetComponent<Collider>();
            
            if (locationCollider == null)
            {
                // Add a trigger collider if none exists
                var sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = interactionRange;
                locationCollider = sphereCollider;
            }
        }
        
        private void Start()
        {
            UpdateVisualState();
        }
        
        private void Update()
        {
            if (isActive && currentOccupancy > 0)
            {
                ProcessOccupantNeeds();
            }
        }
        
        #region Initialization
        
        public void Initialize(WorldLocation config)
        {
            locationConfig = config;
            
            // Apply configuration-specific settings
            if (!string.IsNullOrEmpty(config.name))
            {
                gameObject.name = $"Location_{config.name}";
            }
            
            // Set up position if specified
            if (config.position != Vector3.zero)
            {
                transform.position = config.position;
            }
            
            // Configure capacity and access
            isActive = true;
            currentOccupancy = 0;
            
            Debug.Log($"[World Location] Initialized location '{config.name}' " +
                     $"(Type: {config.type}, Capacity: {config.capacity})");
        }
        
        #endregion
        
        #region Agent Interaction
        
        private void OnTriggerEnter(Collider other)
        {
            var agent = other.GetComponent<Agent>();
            if (agent != null && CanAgentEnter(agent))
            {
                EnterLocation(agent);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            var agent = other.GetComponent<Agent>();
            if (agent != null && currentOccupants.Contains(agent))
            {
                ExitLocation(agent);
            }
        }
        
        public bool CanAgentEnter(Agent agent)
        {
            if (!isActive) return false;
            if (IsFull) return false;
            if (currentOccupants.Contains(agent)) return false;
            
            // Check access requirements
            if (locationConfig.requiresPermission)
            {
                return HasPermission(agent);
            }
            
            // Check access cost
            if (locationConfig.accessCost > 0)
            {
                return CanPayAccessCost(agent);
            }
            
            return true;
        }
        
        private bool HasPermission(Agent agent)
        {
            // Simple permission check based on context
            // In a real implementation, this might check agent roles, relationships, etc.
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return false;
            
            // For now, all agents have basic permissions
            return true;
        }
        
        private bool CanPayAccessCost(Agent agent)
        {
            // Simple cost check - for now, always allow access
            // In a real implementation, this might check agent resources
            return true;
        }
        
        public void EnterLocation(Agent agent)
        {
            if (!CanAgentEnter(agent)) return;
            
            currentOccupants.Add(agent);
            currentOccupancy = currentOccupants.Count;
            
            // Record usage
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            var agentId = identity?.UniqueResearchID ?? "Unknown";
            lastUsage[agentId] = DateTime.UtcNow;
            
            // Create usage event
            var usageEvent = new LocationUsageEvent
            {
                timestamp = DateTime.UtcNow,
                agentId = agentId,
                action = LocationAction.Enter,
                locationName = locationConfig.name,
                occupancyAfter = currentOccupancy,
                needStates = agent.Needs != null ? agent.Needs.activeNeeds.ToDictionary(n => n.needName, n => n.currentSatisfaction) : new Dictionary<string, float>()
            };
            
            usageHistory.Add(usageEvent);
            if (usageHistory.Count > 100) // Keep only recent events
            {
                usageHistory.RemoveAt(0);
            }
            
            // Update visual state
            UpdateVisualState();
            
            // Fire events
            OnAgentEntered?.Invoke(this, agent);
            if (IsFull)
            {
                OnLocationFull?.Invoke(this);
            }
            
            Debug.Log($"[World Location] {identity?.GetCasualName() ?? agent.name} entered {locationConfig.name} " +
                     $"(Occupancy: {currentOccupancy}/{locationConfig.capacity})");
        }
        
        public void ExitLocation(Agent agent)
        {
            if (!currentOccupants.Contains(agent)) return;
            
            var wasFull = IsFull;
            
            currentOccupants.Remove(agent);
            currentOccupancy = currentOccupants.Count;
            
            // Record usage
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            var agentId = identity?.UniqueResearchID ?? "Unknown";
            
            var usageEvent = new LocationUsageEvent
            {
                timestamp = DateTime.UtcNow,
                agentId = agentId,
                action = LocationAction.Exit,
                locationName = locationConfig.name,
                occupancyAfter = currentOccupancy,
                needStates = agent.Needs != null ? agent.Needs.activeNeeds.ToDictionary(n => n.needName, n => n.currentSatisfaction) : new Dictionary<string, float>()
            };
            
            // Calculate duration if we have entry time
            if (lastUsage.TryGetValue(agentId, out var entryTime))
            {
                usageEvent.durationSeconds = (float)(DateTime.UtcNow - entryTime).TotalSeconds;
            }
            
            usageHistory.Add(usageEvent);
            
            // Update visual state
            UpdateVisualState();
            
            // Fire events
            OnAgentExited?.Invoke(this, agent);
            if (wasFull && !IsFull)
            {
                OnLocationAvailable?.Invoke(this);
            }
            
            Debug.Log($"[World Location] {identity?.GetCasualName() ?? agent.name} exited {locationConfig.name} " +
                     $"(Occupancy: {currentOccupancy}/{locationConfig.capacity})");
        }
        
        #endregion
        
        #region Need Satisfaction
        
        private void ProcessOccupantNeeds()
        {
            if (locationConfig.needsSatisfied == null || locationConfig.needsSatisfied.Length == 0) return;
            
            foreach (var agent in currentOccupants.ToList()) // ToList to avoid modification during iteration
            {
                if (agent == null || agent.Needs == null) continue;
                
                ProcessAgentNeedSatisfaction(agent);
            }
        }
        
        private void ProcessAgentNeedSatisfaction(Agent agent)
        {
            var needsManager = agent.Needs;
            var satisfactionAmount = needSatisfactionRate * Time.deltaTime;
            
            foreach (var needType in locationConfig.needsSatisfied)
            {
                // Check if agent has this need
                var need = needsManager.activeNeeds.FirstOrDefault(n => n.needName == needType);
                if (need != null && need.currentSatisfaction < 1.0f) // Only satisfy if not already full
                {
                    // needsManager.SatisfyNeed(needType, satisfactionAmount);
                    
                    // Update agent beliefs about this location
                    var bdiEngine = agent.GetComponent<BDIEngine>();
                    if (bdiEngine != null)
                    {
                        var beliefText = $"Location {locationConfig.name} satisfies {needType} need";
                        bdiEngine.AddBelief("location_utility", 
                            new[] { locationConfig.name, needType }, 
                            0.8f, beliefText);
                    }
                }
            }
        }
        
        /// <summary>
        /// Get the satisfaction potential for a specific need type
        /// </summary>
        public float GetNeedSatisfactionPotential(string needType)
        {
            if (locationConfig.needsSatisfied == null) return 0f;
            
            return locationConfig.needsSatisfied.Contains(needType) ? needSatisfactionRate : 0f;
        }
        
        /// <summary>
        /// Check if this location can satisfy a specific need
        /// </summary>
        public bool CanSatisfyNeed(string needType)
        {
            return locationConfig.needsSatisfied?.Contains(needType) ?? false;
        }
        
        #endregion
        
        #region Visual Management
        
        private void UpdateVisualState()
        {
            if (!showOccupancyIndicator || locationRenderer == null) return;
            
            var targetColor = GetStatusColor();
            
            if (locationRenderer.material.color != targetColor)
            {
                locationRenderer.material.color = targetColor;
            }
        }
        
        private Color GetStatusColor()
        {
            if (!isActive) return Color.gray;
            if (IsFull) return fullColor;
            if (currentOccupancy > 0) return occupiedColor;
            return availableColor;
        }
        
        #endregion
        
        #region Research Data
        
        /// <summary>
        /// Get research data for this location
        /// </summary>
        public LocationResearchData GetResearchData()
        {
            var totalUsageTime = usageHistory
                .Where(e => e.action == LocationAction.Exit && e.durationSeconds > 0)
                .Sum(e => (float)e.durationSeconds);
                
            var uniqueUsers = usageHistory
                .Select(e => e.agentId)
                .Distinct()
                .Count();
                
            var averageOccupancy = usageHistory.Count > 0 ? 
                (float)usageHistory.Average(e => e.occupancyAfter) : 0f;
                
            return new LocationResearchData
            {
                locationName = locationConfig.name,
                locationType = locationConfig.type,
                capacity = locationConfig.capacity,
                currentOccupancy = currentOccupancy,
                totalUsageEvents = usageHistory.Count,
                uniqueUsers = uniqueUsers,
                totalUsageTimeSeconds = totalUsageTime,
                averageOccupancy = averageOccupancy,
                utilizationRate = UtilizationRate,
                needsSatisfied = locationConfig.needsSatisfied?.ToList() ?? new List<string>(),
                recentUsageEvents = usageHistory.TakeLast(10).ToList()
            };
        }
        
        /// <summary>
        /// Get usage statistics for research analysis
        /// </summary>
        public LocationUsageStats GetUsageStats()
        {
            var entriesPerHour = usageHistory
                .Where(e => e.action == LocationAction.Enter)
                .GroupBy(e => e.timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Count());
                
            var averageSessionDuration = usageHistory
                .Where(e => e.action == LocationAction.Exit && e.durationSeconds > 0)
                .DefaultIfEmpty(new LocationUsageEvent { durationSeconds = 0 })
                .Average(e => (float)e.durationSeconds);
                
            return new LocationUsageStats
            {
                peakUsageHour = entriesPerHour.Count > 0 ? 
                    entriesPerHour.OrderByDescending(kvp => kvp.Value).First().Key : 0,
                averageSessionDuration = averageSessionDuration,
                mostFrequentUsers = usageHistory
                    .GroupBy(e => e.agentId)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .ToDictionary(g => g.Key, g => g.Count()),
                hourlyUsagePattern = entriesPerHour
            };
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Force remove an agent from the location (for cleanup)
        /// </summary>
        public void ForceRemoveAgent(Agent agent)
        {
            if (currentOccupants.Contains(agent))
            {
                ExitLocation(agent);
            }
        }
        
        /// <summary>
        /// Clear all occupants (for reset/cleanup)
        /// </summary>
        public void ClearAllOccupants()
        {
            var occupantsCopy = new List<Agent>(currentOccupants);
            foreach (var agent in occupantsCopy)
            {
                ExitLocation(agent);
            }
        }
        
        /// <summary>
        /// Set location active/inactive state
        /// </summary>
        public void SetActive(bool active)
        {
            isActive = active;
            UpdateVisualState();
            
            if (!active)
            {
                ClearAllOccupants();
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw interaction range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            
            // Draw capacity visualization
            if (locationConfig != null && locationConfig.capacity > 0)
            {
                Gizmos.color = Color.blue;
                var positions = GenerateCapacityPositions();
                for (int i = 0; i < locationConfig.capacity; i++)
                {
                    var color = i < currentOccupancy ? Color.red : Color.blue;
                    Gizmos.color = color;
                    Gizmos.DrawWireCube(positions[i], Vector3.one * 0.5f);
                }
            }
        }
        
        private Vector3[] GenerateCapacityPositions()
        {
            var positions = new Vector3[locationConfig.capacity];
            var center = transform.position;
            var radius = interactionRange * 0.7f;
            
            for (int i = 0; i < locationConfig.capacity; i++)
            {
                var angle = (float)i / locationConfig.capacity * 360f * Mathf.Deg2Rad;
                positions[i] = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.5f,
                    Mathf.Sin(angle) * radius
                );
            }
            
            return positions;
        }
        
        #endregion
    }
    
    #region Data Structures
    
    [System.Serializable]
    public class LocationUsageEvent
    {
        public DateTime timestamp;
        public string agentId;
        public LocationAction action;
        public string locationName;
        public int occupancyAfter;
        public float durationSeconds;
        public Dictionary<string, float> needStates;
    }
    
    [System.Serializable]
    public class LocationResearchData
    {
        public string locationName;
        public LocationType locationType;
        public int capacity;
        public int currentOccupancy;
        public int totalUsageEvents;
        public int uniqueUsers;
        public float totalUsageTimeSeconds;
        public float averageOccupancy;
        public float utilizationRate;
        public List<string> needsSatisfied;
        public List<LocationUsageEvent> recentUsageEvents;
    }
    
    [System.Serializable]
    public class LocationUsageStats
    {
        public int peakUsageHour;
        public float averageSessionDuration;
        public Dictionary<string, int> mostFrequentUsers;
        public Dictionary<int, int> hourlyUsagePattern;
    }
    
    public enum LocationAction
    {
        Enter,
        Exit,
        Interact
    }
    
    #endregion
}