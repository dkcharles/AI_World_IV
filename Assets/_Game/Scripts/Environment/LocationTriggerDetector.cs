using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;
using AIWorld.Experimental;

namespace AIWorld.Environment
{
    /// <summary>
    /// Detects when agents enter and exit location trigger areas
    /// Manages location occupancy and provides location-based need satisfaction
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LocationTriggerDetector : MonoBehaviour
    {
        [Header("Location Configuration")]
        public string locationName;
        public float needSatisfactionRate = 0.1f;
        public float satisfactionInterval = 2f;
        
        [Header("Debug")]
        public bool logTriggerEvents = true;
        public bool showOccupancy = true;
        
        // State tracking
        private WorldLocationComponent locationComponent;
        private System.Collections.Generic.List<Agent> occupyingAgents;
        private float lastSatisfactionTime;
        
        // Properties
        public int CurrentOccupancy => occupyingAgents?.Count ?? 0;
        public bool HasOccupants => CurrentOccupancy > 0;
        
        private void Awake()
        {
            occupyingAgents = new System.Collections.Generic.List<Agent>();
            locationComponent = GetComponent<WorldLocationComponent>();
            
            // Ensure collider is set as trigger
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            
            // Get location name from component if not manually set
            if (string.IsNullOrEmpty(locationName) && locationComponent != null)
            {
                locationName = locationComponent.LocationConfig?.name ?? gameObject.name;
            }
        }
        
        private void Update()
        {
            // Provide periodic need satisfaction to occupying agents
            if (HasOccupants && Time.time - lastSatisfactionTime >= satisfactionInterval)
            {
                ProvidePeriodicsNeedSatisfaction();
                lastSatisfactionTime = Time.time;
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            var agent = other.GetComponent<Agent>();
            if (agent != null && !occupyingAgents.Contains(agent))
            {
                OnAgentEntered(agent);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            var agent = other.GetComponent<Agent>();
            if (agent != null && occupyingAgents.Contains(agent))
            {
                OnAgentExited(agent);
            }
        }
        
        /// <summary>
        /// Handle agent entering the location
        /// </summary>
        private void OnAgentEntered(Agent agent)
        {
            occupyingAgents.Add(agent);
            
            if (logTriggerEvents)
            {
                Debug.Log($"🚪 Agent {agent.AgentName} entered location '{locationName}' " +
                         $"(Occupancy: {CurrentOccupancy})");
            }
            
            // Notify location component of new occupant
            if (locationComponent != null)
            {
                // The WorldLocationComponent handles its own triggers
                // We can subscribe to its events or provide additional functionality
                // For now, just log that we detected the entry
                Debug.Log($"🚪 LocationTriggerDetector: Detected {agent.AgentName} entering {locationName}");
            }
            
            // Provide immediate partial need satisfaction
            ProvideiImmediateNeedSatisfaction(agent);
            
            // Update agent's beliefs about current location
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("current_location", 
                    new[] { agent.AgentName, locationName }, 
                    1f, $"I am currently at {locationName}");
                    
                agent.BDI.AddBelief("location_visited", 
                    new[] { locationName, System.DateTime.Now.ToString("HH:mm") }, 
                    0.9f, $"I visited {locationName}");
            }
        }
        
        /// <summary>
        /// Handle agent exiting the location
        /// </summary>
        private void OnAgentExited(Agent agent)
        {
            occupyingAgents.Remove(agent);
            
            if (logTriggerEvents)
            {
                Debug.Log($"🚪 Agent {agent.AgentName} exited location '{locationName}' " +
                         $"(Occupancy: {CurrentOccupancy})");
            }
            
            // Notify location component of departing occupant
            if (locationComponent != null)
            {
                // The WorldLocationComponent handles its own triggers
                // We can subscribe to its events or provide additional functionality
                // For now, just log that we detected the exit
                Debug.Log($"🚪 LocationTriggerDetector: Detected {agent.AgentName} exiting {locationName}");
            }
            
            // Update agent's beliefs about leaving location
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("previous_location", 
                    new[] { agent.AgentName, locationName }, 
                    0.8f, $"I was recently at {locationName}");
            }
        }
        
        /// <summary>
        /// Provide immediate need satisfaction when agent enters
        /// </summary>
        private void ProvideiImmediateNeedSatisfaction(Agent agent)
        {
            if (agent.Needs == null || locationComponent?.LocationConfig == null) return;
            
            var locationConfig = locationComponent.LocationConfig;
            
            // Satisfy needs based on location type and configuration
            if (locationConfig.needsSatisfied != null)
            {
                foreach (var needName in locationConfig.needsSatisfied)
                {
                    agent.Needs.SatisfyNeedsFromAction($"Enter_{locationName}", needSatisfactionRate * 0.5f);
                }
            }
            
            // Location-specific immediate satisfaction
            switch (locationConfig.type)
            {
                case LocationType.SocialArea:
                    agent.Needs.SatisfyNeedsFromSocialInteraction("location_entry", needSatisfactionRate);
                    break;
                case LocationType.RelaxationArea:
                    agent.Needs.SatisfyNeedsFromAction("Rest", needSatisfactionRate);
                    break;
                case LocationType.WorkArea:
                case LocationType.KnowledgeArea:
                    agent.Needs.SatisfyNeedsFromAction("Work", needSatisfactionRate * 0.3f);
                    break;
            }
        }
        
        /// <summary>
        /// Provide periodic need satisfaction to all occupying agents
        /// </summary>
        private void ProvidePeriodicsNeedSatisfaction()
        {
            if (locationComponent?.LocationConfig == null) return;
            
            var locationConfig = locationComponent.LocationConfig;
            
            foreach (var agent in occupyingAgents)
            {
                if (agent?.Needs == null) continue;
                
                // Base satisfaction for being in location
                if (locationConfig.needsSatisfied != null)
                {
                    foreach (var needName in locationConfig.needsSatisfied)
                    {
                        agent.Needs.SatisfyNeedsFromAction($"Stay_{locationName}", needSatisfactionRate);
                    }
                }
                
                // Enhanced satisfaction for group activities in social areas
                if (locationConfig.type == LocationType.SocialArea && CurrentOccupancy > 1)
                {
                    var groupBonus = Mathf.Min(CurrentOccupancy * 0.1f, 0.5f);
                    agent.Needs.SatisfyNeedsFromSocialInteraction("group_activity", needSatisfactionRate + groupBonus);
                }
                
                // Efficiency bonus in work areas when not overcrowded
                if (locationConfig.type == LocationType.WorkArea)
                {
                    var efficiency = CurrentOccupancy <= locationConfig.capacity ? 1f : 0.5f;
                    agent.Needs.SatisfyNeedsFromAction("Work", needSatisfactionRate * efficiency);
                }
            }
        }
        
        /// <summary>
        /// Get occupancy information for debugging
        /// </summary>
        public string GetOccupancyInfo()
        {
            if (!HasOccupants) return $"Location '{locationName}': Empty";
            
            var agentNames = occupyingAgents.ConvertAll(a => a?.AgentName ?? "Unknown");
            return $"Location '{locationName}': {CurrentOccupancy} occupants - {string.Join(", ", agentNames)}";
        }
        
        /// <summary>
        /// Check if location has capacity for more agents
        /// </summary>
        public bool HasCapacity()
        {
            if (locationComponent?.LocationConfig == null) return true;
            return CurrentOccupancy < locationComponent.LocationConfig.capacity;
        }
        
        /// <summary>
        /// Get list of agents currently in this location
        /// </summary>
        public System.Collections.Generic.List<Agent> GetOccupyingAgents()
        {
            return new System.Collections.Generic.List<Agent>(occupyingAgents);
        }
        
        /// <summary>
        /// Force remove an agent from occupancy list (e.g., if agent was destroyed)
        /// </summary>
        public void ForceRemoveAgent(Agent agent)
        {
            if (occupyingAgents.Contains(agent))
            {
                occupyingAgents.Remove(agent);
                if (logTriggerEvents)
                {
                    Debug.Log($"🔧 Force removed agent {agent?.AgentName} from location '{locationName}'");
                }
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showOccupancy || !Application.isPlaying) return;
            
            // Draw occupancy visualization
            if (HasOccupants)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f + CurrentOccupancy * 0.2f);
                
                // Draw lines to occupying agents
                Gizmos.color = Color.yellow;
                foreach (var agent in occupyingAgents)
                {
                    if (agent != null)
                    {
                        Gizmos.DrawLine(transform.position + Vector3.up, agent.transform.position + Vector3.up);
                    }
                }
            }
        }
        
        #if UNITY_EDITOR
        [Header("Editor Debug Info")]
        [SerializeField] private string currentOccupancyInfo;
        [SerializeField] private int occupantCount;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                currentOccupancyInfo = GetOccupancyInfo();
                occupantCount = CurrentOccupancy;
            }
        }
        #endif
    }
}
