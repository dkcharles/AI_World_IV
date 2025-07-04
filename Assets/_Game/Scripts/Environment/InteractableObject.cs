using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AIWorld.Agents;
using AIWorld.Needs;

namespace AIWorld.Environment
{
    /// <summary>
    /// Base class for interactive objects that agents can discover and use
    /// Provides need satisfaction and goal-directed interaction
    /// </summary>
    public abstract class InteractableObject : MonoBehaviour
    {
        [Header("Object Configuration")]
        public string objectName = "Interactive Object";
        public string description = "An object that agents can interact with";
        public InteractionType primaryInteractionType = InteractionType.Use;
        public float interactionRange = 2f;
        public float interactionDuration = 3f;
        
        [Header("Need Satisfaction")]
        public NeedSatisfaction[] needSatisfactions;
        public float cooldownTime = 5f;
        public int maxSimultaneousUsers = 1;
        
        [Header("Discovery")]
        public float discoveryRange = 8f;
        public bool requiresDiscovery = true;
        public bool announceDiscovery = true;
        
        [Header("Visual Feedback")]
        public GameObject interactionIndicator;
        public Color availableColor = Color.green;
        public Color inUseColor = Color.yellow;
        public Color cooldownColor = Color.red;
        
        [Header("Debug")]
        public bool logInteractions = true;
        public bool showDebugGizmos = true;
        
        [Header("NavMesh Configuration")]
        public bool autoSetupNavMeshObstacle = true;
        public bool isWalkableObject = false;
        public bool customObstacleSize = false;
        public Vector3 obstacleSize = Vector3.one;
        
        // State tracking
        private HashSet<Agent> discoveredByAgents = new HashSet<Agent>();
        protected List<Agent> currentUsers = new List<Agent>();
        private Dictionary<Agent, float> agentCooldowns = new Dictionary<Agent, float>();
        private float lastInteractionTime;
        
        // Properties
        public bool IsAvailable => currentUsers.Count < maxSimultaneousUsers;
        public bool IsInUse => currentUsers.Count > 0;
        public int CurrentUserCount => currentUsers.Count;
        public abstract string InteractionPrompt { get; }
        
        protected virtual void Start()
        {
            UpdateVisualState();
            
            // Setup NavMesh components
            if (autoSetupNavMeshObstacle)
            {
                SetupNavMeshComponents();
            }
            
            if (logInteractions)
            {
                Debug.Log($"🏗️ {objectName} initialized at {transform.position}");
            }
        }
        
        protected virtual void Update()
        {
            HandleDiscovery();
            UpdateCooldowns();
            UpdateVisualState();
        }
        
        /// <summary>
        /// Handle agent discovery of this object
        /// </summary>
        private void HandleDiscovery()
        {
            if (!requiresDiscovery) return;
            
            var gameManager = FindFirstObjectByType<AIWorld.Managers.GameManager>();
            if (gameManager == null) return;
            
            var nearbyAgents = gameManager.GetAgentsInRange(transform.position, discoveryRange);
            foreach (var agent in nearbyAgents)
            {
                if (!discoveredByAgents.Contains(agent))
                {
                    OnDiscoveredBy(agent);
                    discoveredByAgents.Add(agent);
                }
            }
        }
        
        /// <summary>
        /// Called when an agent discovers this object
        /// </summary>
        protected virtual void OnDiscoveredBy(Agent agent)
        {
            if (announceDiscovery && logInteractions)
            {
                Debug.Log($"🔍 {agent.AgentName} discovered {objectName}");
            }
            
            // Add belief about this object to agent's BDI engine
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("object_location", 
                    new[] { objectName, $"{transform.position.x:F1},{transform.position.y:F1},{transform.position.z:F1}" },
                    1f, $"I found {objectName} at this location");
                    
                agent.BDI.AddBelief("object_type", 
                    new[] { objectName, primaryInteractionType.ToString() },
                    0.9f, $"{objectName} can be used for {primaryInteractionType}");
            }
        }
        
        /// <summary>
        /// Check if agent can interact with this object
        /// </summary>
        public virtual bool CanInteract(Agent agent)
        {
            if (agent == null) return false;
            
            // Check if agent has discovered this object
            if (requiresDiscovery && !discoveredByAgents.Contains(agent))
                return false;
            
            // Check distance
            float distance = Vector3.Distance(transform.position, agent.transform.position);
            if (distance > interactionRange)
                return false;
            
            // Check availability
            if (!IsAvailable)
                return false;
            
            // Check cooldown
            if (agentCooldowns.ContainsKey(agent) && agentCooldowns[agent] > 0f)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Start interaction with agent
        /// </summary>
        public virtual bool StartInteraction(Agent agent)
        {
            if (!CanInteract(agent))
                return false;
            
            currentUsers.Add(agent);
            lastInteractionTime = Time.time;
            
            if (logInteractions)
            {
                Debug.Log($"🤝 {agent.AgentName} started interacting with {objectName}");
            }
            
            // Start interaction coroutine
            StartCoroutine(HandleInteractionCoroutine(agent));
            
            OnInteractionStarted(agent);
            return true;
        }
        
        /// <summary>
        /// Handle interaction over time
        /// </summary>
        private System.Collections.IEnumerator HandleInteractionCoroutine(Agent agent)
        {
            yield return new WaitForSeconds(interactionDuration);
            
            if (currentUsers.Contains(agent))
            {
                CompleteInteraction(agent);
            }
        }
        
        /// <summary>
        /// Complete interaction with agent
        /// </summary>
        protected virtual void CompleteInteraction(Agent agent)
        {
            if (!currentUsers.Contains(agent)) return;
            
            currentUsers.Remove(agent);
            agentCooldowns[agent] = cooldownTime;
            
            // Apply need satisfactions
            ApplyNeedSatisfactions(agent);
            
            if (logInteractions)
            {
                Debug.Log($"✅ {agent.AgentName} completed interaction with {objectName}");
            }
            
            OnInteractionCompleted(agent);
        }
        
        /// <summary>
        /// Apply need satisfactions to agent
        /// </summary>
        private void ApplyNeedSatisfactions(Agent agent)
        {
            if (agent.Needs == null || needSatisfactions == null) return;
            
            foreach (var satisfaction in needSatisfactions)
            {
                var matchingNeeds = agent.Needs.activeNeeds.FindAll(n => 
                    satisfaction.needName == "Any" || n.needName == satisfaction.needName);
                
                foreach (var need in matchingNeeds)
                {
                    need.SatisfyNeed(satisfaction.satisfactionAmount, objectName);
                }
            }
        }
        
        /// <summary>
        /// Update agent cooldowns
        /// </summary>
        private void UpdateCooldowns()
        {
            var agentsToUpdate = new List<Agent>(agentCooldowns.Keys);
            foreach (var agent in agentsToUpdate)
            {
                if (agentCooldowns[agent] > 0f)
                {
                    agentCooldowns[agent] -= Time.deltaTime;
                    if (agentCooldowns[agent] <= 0f)
                    {
                        agentCooldowns[agent] = 0f;
                    }
                }
            }
        }
        
        /// <summary>
        /// Update visual state based on current status
        /// </summary>
        protected virtual void UpdateVisualState()
        {
            if (interactionIndicator == null) return;
            
            Renderer indicatorRenderer = interactionIndicator.GetComponent<Renderer>();
            if (indicatorRenderer == null) return;
            
            Color targetColor;
            if (IsInUse)
            {
                targetColor = inUseColor;
            }
            else if (IsAvailable)
            {
                targetColor = availableColor;
            }
            else
            {
                targetColor = cooldownColor;
            }
            
            indicatorRenderer.material.color = targetColor;
        }
        
        /// <summary>
        /// Get interaction attractiveness for agent (0-1)
        /// </summary>
        public virtual float GetAttractiveness(Agent agent)
        {
            if (!CanInteract(agent)) return 0f;
            
            float attractiveness = 0f;
            
            // Base attractiveness from need satisfactions
            if (agent.Needs != null && needSatisfactions != null)
            {
                foreach (var satisfaction in needSatisfactions)
                {
                    var matchingNeeds = agent.Needs.activeNeeds.FindAll(n => 
                        satisfaction.needName == "Any" || n.needName == satisfaction.needName);
                    
                    foreach (var need in matchingNeeds)
                    {
                        float needUrgency = need.GetUrgency();
                        attractiveness += needUrgency * satisfaction.satisfactionAmount;
                    }
                }
            }
            
            // Distance factor (closer is more attractive)
            float distance = Vector3.Distance(transform.position, agent.transform.position);
            float distanceFactor = 1f - (distance / discoveryRange);
            attractiveness *= distanceFactor;
            
            return Mathf.Clamp01(attractiveness);
        }
        
        /// <summary>
        /// Force stop interaction for agent
        /// </summary>
        public virtual void StopInteraction(Agent agent)
        {
            if (currentUsers.Contains(agent))
            {
                currentUsers.Remove(agent);
                OnInteractionStopped(agent);
                
                if (logInteractions)
                {
                    Debug.Log($"🛑 {agent.AgentName} stopped interacting with {objectName}");
                }
            }
        }
        
        // Virtual methods for subclasses to override
        protected virtual void OnInteractionStarted(Agent agent) { }
        protected virtual void OnInteractionCompleted(Agent agent) { }
        protected virtual void OnInteractionStopped(Agent agent) { }
        
        /// <summary>
        /// Setup NavMesh components for proper agent navigation
        /// </summary>
        protected virtual void SetupNavMeshComponents()
        {
            if (isWalkableObject)
            {
                SetupNavMeshSurface();
            }
            else
            {
                SetupNavMeshObstacle();
            }
        }
        
        /// <summary>
        /// Setup NavMesh surface for walkable objects (using built-in system)
        /// </summary>
        private void SetupNavMeshSurface()
        {
            // For built-in NavMesh, we ensure object is properly configured for baking
            // Set appropriate layer for NavMesh baking
            if (gameObject.layer == 0) // Default layer
            {
                // Objects should be on a layer included in NavMesh baking
                if (logInteractions)
                {
                    Debug.Log($"🗺️ {objectName}: Configured as walkable surface for built-in NavMesh");
                }
            }
            
            // Ensure object has a renderer for NavMesh baking
            if (GetComponent<Renderer>() == null)
            {
                Debug.LogWarning($"⚠️ {objectName}: Walkable object has no renderer - NavMesh baking may not include this object");
            }
        }
        
        /// <summary>
        /// Setup NavMesh obstacle for non-walkable objects
        /// </summary>
        private void SetupNavMeshObstacle()
        {
            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>();
                
                if (logInteractions)
                {
                    Debug.Log($"🚧 {objectName}: Added NavMeshObstacle");
                }
            }
            
            // Configure obstacle based on object properties
            ConfigureNavMeshObstacle(obstacle);
        }
        
        /// <summary>
        /// Configure NavMesh obstacle with appropriate settings
        /// </summary>
        private void ConfigureNavMeshObstacle(NavMeshObstacle obstacle)
        {
            if (customObstacleSize)
            {
                obstacle.size = obstacleSize;
                obstacle.center = Vector3.zero;
            }
            else
            {
                // Auto-configure based on collider or renderer
                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    obstacle.size = col.bounds.size;
                    obstacle.center = col.bounds.center - transform.position;
                }
                else
                {
                    Renderer renderer = GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        obstacle.size = renderer.bounds.size;
                        obstacle.center = renderer.bounds.center - transform.position;
                    }
                    else
                    {
                        // Default size for interactive objects
                        obstacle.size = new Vector3(2f, 2f, 2f);
                        obstacle.center = Vector3.zero;
                    }
                }
            }
            
            // Configure obstacle behaviour
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            
            // Adjust based on interaction type
            switch (primaryInteractionType)
            {
                case InteractionType.Rest:
                    obstacle.carving = false; // Rest areas might be walkable around
                    break;
                case InteractionType.Socialize:
                    obstacle.carving = false; // Social hubs should allow gathering
                    break;
                default:
                    obstacle.carving = true; // Most objects should carve the NavMesh
                    break;
            }
            
            if (logInteractions)
            {
                Debug.Log($"🔧 {objectName}: NavMeshObstacle configured - Size: {obstacle.size}, Carving: {obstacle.carving}");
            }
        }
        
        /// <summary>
        /// Manually trigger NavMesh component setup
        /// </summary>
        [ContextMenu("Setup NavMesh Components")]
        public void ManualNavMeshSetup()
        {
            SetupNavMeshComponents();
            Debug.Log($"🔄 {objectName}: Manual NavMesh setup completed");
        }
        
        /// <summary>
        /// Toggle between walkable and obstacle modes
        /// </summary>
        [ContextMenu("Toggle Walkable/Obstacle")]
        public void ToggleNavMeshMode()
        {
            isWalkableObject = !isWalkableObject;
            
            // Remove existing obstacle components (surfaces are handled differently in built-in system)
            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null) DestroyImmediate(obstacle);
            
            // Setup new component
            SetupNavMeshComponents();
            
            Debug.Log($"🔄 {objectName}: Switched to {(isWalkableObject ? "Walkable" : "Obstacle")} mode");
        }
        
        /// <summary>
        /// Debug visualization
        /// </summary>
        protected virtual void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;
            
            // Draw interaction range
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            
            // Draw discovery range
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, discoveryRange);
            
            // Draw connections to current users
            Gizmos.color = Color.yellow;
            foreach (var user in currentUsers)
            {
                if (user != null)
                {
                    Gizmos.DrawLine(transform.position, user.transform.position);
                }
            }
        }
        
        #if UNITY_EDITOR
        [Header("Editor Debug Info")]
        [SerializeField] private string currentStatus;
        [SerializeField] private int discoveredByCount;
        
        protected virtual void OnValidate()
        {
            if (Application.isPlaying)
            {
                currentStatus = IsInUse ? $"In Use ({CurrentUserCount})" : "Available";
                discoveredByCount = discoveredByAgents?.Count ?? 0;
            }
        }
        #endif
    }
    
    /// <summary>
    /// Types of interactions available
    /// </summary>
    public enum InteractionType
    {
        Use,            // General usage
        Study,          // Learning/research
        Create,         // Creative work
        Socialize,      // Social interaction
        Rest,           // Recovery/relaxation
        Observe,        // Passive observation
        Collaborate     // Group work
    }
    
    /// <summary>
    /// Need satisfaction configuration
    /// </summary>
    [System.Serializable]
    public class NeedSatisfaction
    {
        public string needName = "Any";  // "Any" means all needs of this type
        [Range(0f, 1f)] public float satisfactionAmount = 0.3f;
        public string description = "Satisfies this need";
    }
}