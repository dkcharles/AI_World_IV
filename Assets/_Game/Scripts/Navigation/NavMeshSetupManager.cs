using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AIWorld.Environment;
using AIWorld.Agents;

namespace AIWorld.Navigation
{
    /// <summary>
    /// Comprehensive NavMesh setup manager for AI World simulation
    /// Uses built-in NavMesh system for maximum compatibility
    /// Automatically configures obstacles and agents for navigation
    /// </summary>
    public class NavMeshSetupManager : MonoBehaviour
    {
        [Header("NavMesh Configuration")]
        public bool autoSetupOnStart = true;
        public bool rebakeAfterSetup = true;
        public float navMeshBakeDelay = 1f;
        
        [Header("Surface Configuration")]
        public LayerMask groundLayers = 1; // Default layer
        public LayerMask walkableLayers = 1;
        
        [Header("Obstacle Configuration")]
        public bool autoConfigureObstacles = true;
        public LayerMask obstacleLayers = 0;
        public float defaultObstacleRadius = 1f;
        public float defaultObstacleHeight = 2f;
        
        [Header("Agent Configuration")]
        public bool configureAgentLayers = true;
        public LayerMask agentLayer = 8; // Agents layer
        public LayerMask agentIgnoreLayers = 0;
        
        [Header("Debug")]
        public bool logSetupProgress = true;
        public bool showDebugVisualization = true;
        public bool validateSetupCompletion = true;
        
        // NavMesh components tracking (using built-in components only)
        private List<NavMeshObstacle> navMeshObstacles = new List<NavMeshObstacle>();
        private List<NavMeshAgent> navMeshAgents = new List<NavMeshAgent>();
        private List<GameObject> groundObjects = new List<GameObject>();
        
        // Setup state
        private bool isSetupComplete = false;
        private bool isSetupInProgress = false;
        
        // Properties
        public bool IsSetupComplete => isSetupComplete;
        public int TotalObstacles => navMeshObstacles.Count;
        public int TotalAgents => navMeshAgents.Count;
        public int TotalGroundObjects => groundObjects.Count;
        
        private void Start()
        {
            if (autoSetupOnStart)
            {
                StartCoroutine(SetupNavMeshWithDelay(0.5f));
            }
        }
        
        /// <summary>
        /// Main setup method - configures all NavMesh components
        /// </summary>
        public void SetupNavMesh()
        {
            if (isSetupInProgress)
            {
                Debug.LogWarning("🚧 NavMesh setup already in progress");
                return;
            }
            
            StartCoroutine(SetupNavMeshCoroutine());
        }
        
        /// <summary>
        /// Setup NavMesh with a delay (useful for world generation)
        /// </summary>
        private IEnumerator SetupNavMeshWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            yield return StartCoroutine(SetupNavMeshCoroutine());
        }
        
        /// <summary>
        /// Main setup coroutine
        /// </summary>
        private IEnumerator SetupNavMeshCoroutine()
        {
            isSetupInProgress = true;
            isSetupComplete = false;
            
            if (logSetupProgress)
            {
                Debug.Log("🗺️ Starting NavMesh setup process (Built-in NavMesh)...");
            }
            
            // Clear existing tracking
            ClearTrackingLists();
            
            // Step 1: Setup collision layers
            if (configureAgentLayers)
            {
                SetupCollisionLayers();
                yield return null;
            }
            
            // Step 2: Identify ground objects (for built-in NavMesh)
            IdentifyGroundObjects();
            yield return null;
            
            // Step 3: Configure obstacles
            if (autoConfigureObstacles)
            {
                SetupNavMeshObstacles();
                yield return null;
            }
            
            // Step 4: Configure agents
            SetupNavMeshAgents();
            yield return null;
            
            // Step 5: Bake NavMesh using built-in system
            if (rebakeAfterSetup)
            {
                yield return new WaitForSeconds(navMeshBakeDelay);
                BakeBuiltInNavMesh();
                yield return new WaitForSeconds(1f); // Wait for baking
            }
            
            // Step 6: Validation
            if (validateSetupCompletion)
            {
                ValidateSetup();
            }
            
            isSetupComplete = true;
            isSetupInProgress = false;
            
            if (logSetupProgress)
            {
                LogSetupSummary();
            }
        }
        
        /// <summary>
        /// Setup collision layers for proper agent navigation
        /// </summary>
        private void SetupCollisionLayers()
        {
            if (logSetupProgress)
            {
                Debug.Log("🔧 Configuring collision layers...");
            }
            
            // Check if agent layer is properly configured
            int agentLayerIndex = GetLayerIndex(agentLayer);
            if (agentLayerIndex >= 0)
            {
                if (logSetupProgress)
                {
                    Debug.Log($"✅ Agent layer configured: {LayerMask.LayerToName(agentLayerIndex)}");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ Agent layer not properly configured. Please check Physics Layer Collision Matrix.");
            }
        }
        
        /// <summary>
        /// Identify ground objects for built-in NavMesh baking
        /// </summary>
        private void IdentifyGroundObjects()
        {
            if (logSetupProgress)
            {
                Debug.Log("🏞️ Identifying ground objects for NavMesh...");
            }
            
            // Find all potential ground objects
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            
            foreach (GameObject obj in allObjects)
            {
                if (IsGroundObject(obj))
                {
                    ConfigureGroundObject(obj);
                    groundObjects.Add(obj);
                }
            }
            
            if (logSetupProgress)
            {
                Debug.Log($"✅ Identified {groundObjects.Count} ground objects for NavMesh");
            }
        }
        
        /// <summary>
        /// Determine if object is a ground object
        /// </summary>
        private bool IsGroundObject(GameObject obj)
        {
            // Check layer
            if (!IsInLayerMask(obj.layer, groundLayers)) return false;
            
            // Check for ground-like names
            string name = obj.name.ToLower();
            if (name.Contains("ground") || name.Contains("floor") || name.Contains("plane") || 
                name.Contains("terrain") || name.Contains("surface"))
            {
                return true;
            }
            
            // Check for horizontal surfaces (potential ground)
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Vector3 size = renderer.bounds.size;
                // Large, relatively flat objects
                if (size.x > 2f && size.z > 2f && size.y < 1f)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Configure ground object for NavMesh baking
        /// </summary>
        private void ConfigureGroundObject(GameObject obj)
        {
            // Ensure object has the correct layer for NavMesh baking
            if (!IsInLayerMask(obj.layer, walkableLayers))
            {
                int walkableLayer = GetLayerIndex(walkableLayers);
                if (walkableLayer >= 0)
                {
                    obj.layer = walkableLayer;
                    if (logSetupProgress)
                    {
                        Debug.Log($"🏞️ Set {obj.name} to walkable layer {walkableLayer}");
                    }
                }
            }
            
            // Ensure object has a MeshRenderer for NavMesh baking
            if (obj.GetComponent<MeshRenderer>() == null && obj.GetComponent<Renderer>() == null)
            {
                Debug.LogWarning($"⚠️ Ground object {obj.name} has no renderer - NavMesh baking may fail");
            }
        }
        
        /// <summary>
        /// Setup NavMesh obstacles on environment objects
        /// </summary>
        private void SetupNavMeshObstacles()
        {
            if (logSetupProgress)
            {
                Debug.Log("🚧 Setting up NavMesh obstacles...");
            }
            
            // Find InteractableObjects and add obstacles
            InteractableObject[] interactableObjects = FindObjectsOfType<InteractableObject>();
            foreach (var interactable in interactableObjects)
            {
                AddNavMeshObstacle(interactable.gameObject);
            }
            
            // Find other potential obstacles
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (ShouldAddNavMeshObstacle(obj))
                {
                    AddNavMeshObstacle(obj);
                }
            }
            
            if (logSetupProgress)
            {
                Debug.Log($"✅ Created {navMeshObstacles.Count} NavMesh obstacles");
            }
        }
        
        /// <summary>
        /// Determine if object should have a NavMesh obstacle
        /// </summary>
        private bool ShouldAddNavMeshObstacle(GameObject obj)
        {
            // Skip if already has obstacle
            if (obj.GetComponent<NavMeshObstacle>() != null) return false;
            
            // Skip if it's a ground object
            if (groundObjects.Contains(obj)) return false;
            
            // Skip agents
            if (obj.GetComponent<Agent>() != null) return false;
            
            // Check layer mask
            if (obstacleLayers != 0 && !IsInLayerMask(obj.layer, obstacleLayers)) return false;
            
            // Check for obstacle-like names
            string name = obj.name.ToLower();
            if (name.Contains("wall") || name.Contains("obstacle") || name.Contains("barrier") ||
                name.Contains("station") || name.Contains("table") || name.Contains("desk") ||
                name.Contains("chair") || name.Contains("furniture"))
            {
                return true;
            }
            
            // Check if object has collider and reasonable size
            Collider col = obj.GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                Vector3 size = col.bounds.size;
                // Objects that are tall enough to be obstacles
                if (size.y > 0.5f && (size.x > 0.5f || size.z > 0.5f))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Add NavMesh obstacle to an object
        /// </summary>
        private void AddNavMeshObstacle(GameObject obj)
        {
            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = obj.AddComponent<NavMeshObstacle>();
            }
            
            // Configure obstacle based on object's collider
            Collider col = obj.GetComponent<Collider>();
            if (col != null)
            {
                Vector3 size = col.bounds.size;
                obstacle.size = size;
                obstacle.center = col.bounds.center - obj.transform.position;
            }
            else
            {
                // Default size
                obstacle.size = new Vector3(defaultObstacleRadius * 2f, defaultObstacleHeight, defaultObstacleRadius * 2f);
                obstacle.center = Vector3.zero;
            }
            
            // Configure obstacle settings
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            
            navMeshObstacles.Add(obstacle);
            
            if (logSetupProgress)
            {
                Debug.Log($"🚧 Added NavMeshObstacle to {obj.name}");
            }
        }
        
        /// <summary>
        /// Setup NavMesh agents
        /// </summary>
        private void SetupNavMeshAgents()
        {
            if (logSetupProgress)
            {
                Debug.Log("🤖 Configuring NavMesh agents...");
            }
            
            Agent[] agents = FindObjectsOfType<Agent>();
            foreach (Agent agent in agents)
            {
                SetupNavMeshAgent(agent.gameObject);
            }
            
            if (logSetupProgress)
            {
                Debug.Log($"✅ Configured {navMeshAgents.Count} NavMesh agents");
            }
        }
        
        /// <summary>
        /// Setup individual NavMesh agent
        /// </summary>
        private void SetupNavMeshAgent(GameObject agentObj)
        {
            NavMeshAgent navAgent = agentObj.GetComponent<NavMeshAgent>();
            if (navAgent == null)
            {
                navAgent = agentObj.AddComponent<NavMeshAgent>();
            }
            
            // Configure agent settings for optimal navigation
            navAgent.radius = 0.5f;
            navAgent.height = 2f;
            navAgent.speed = 3.5f;
            navAgent.acceleration = 8f;
            navAgent.angularSpeed = 120f;
            navAgent.stoppingDistance = 1f;
            navAgent.autoBraking = true;
            navAgent.autoRepath = true;
            navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            
            // Set proper layer
            if (configureAgentLayers)
            {
                int agentLayerIndex = GetLayerIndex(agentLayer);
                if (agentLayerIndex >= 0)
                {
                    agentObj.layer = agentLayerIndex;
                }
            }
            
            navMeshAgents.Add(navAgent);
            
            if (logSetupProgress)
            {
                Debug.Log($"🤖 Configured NavMeshAgent for {agentObj.name}");
            }
        }
        
        /// <summary>
        /// Bake NavMesh using Unity's built-in system
        /// </summary>
        private void BakeBuiltInNavMesh()
        {
            if (logSetupProgress)
            {
                Debug.Log("🔥 Baking NavMesh using built-in system...");
                Debug.Log("⚠️ Note: For automatic baking, please use Window > AI > Navigation and bake manually, or install AI Navigation package");
            }
            
            // Note: Built-in NavMesh requires manual baking through the Navigation window
            // We can only provide guidance here
            Debug.Log("📝 To complete setup: Open Window > AI > Navigation > Bake tab and click 'Bake'");
        }
        
        /// <summary>
        /// Validate the setup completion
        /// </summary>
        private void ValidateSetup()
        {
            bool allValid = true;
            List<string> issues = new List<string>();
            
            // Check if we have ground objects
            if (groundObjects.Count == 0)
            {
                issues.Add("No ground objects found for NavMesh");
                allValid = false;
            }
            
            // Check if agents have valid NavMeshAgent components
            Agent[] agents = FindObjectsOfType<Agent>();
            foreach (Agent agent in agents)
            {
                NavMeshAgent navAgent = agent.GetComponent<NavMeshAgent>();
                if (navAgent == null)
                {
                    issues.Add($"Agent {agent.name} missing NavMeshAgent component");
                    allValid = false;
                }
                else if (!navAgent.enabled)
                {
                    issues.Add($"Agent {agent.name} has disabled NavMeshAgent");
                    allValid = false;
                }
            }
            
            // Check if NavMesh data exists
            if (NavMesh.CalculateTriangulation().vertices.Length == 0)
            {
                issues.Add("No NavMesh data found - please bake NavMesh manually in Window > AI > Navigation");
                Debug.LogWarning("🔥 Please open Window > AI > Navigation > Bake tab and click 'Bake' to complete NavMesh setup");
            }
            
            if (allValid)
            {
                Debug.Log("✅ NavMesh setup validation passed");
            }
            else
            {
                Debug.LogWarning($"⚠️ NavMesh setup validation found {issues.Count} issues:");
                foreach (string issue in issues)
                {
                    Debug.LogWarning($"   - {issue}");
                }
            }
        }
        
        /// <summary>
        /// Clear tracking lists
        /// </summary>
        private void ClearTrackingLists()
        {
            navMeshObstacles.Clear();
            navMeshAgents.Clear();
            groundObjects.Clear();
        }
        
        /// <summary>
        /// Log setup summary
        /// </summary>
        private void LogSetupSummary()
        {
            Debug.Log("══════════════════════════════════════════════════");
            Debug.Log("🗺️ NAVMESH SETUP COMPLETED (Built-in)");
            Debug.Log("══════════════════════════════════════════════════");
            Debug.Log($"🏞️ Ground Objects: {TotalGroundObjects}");
            Debug.Log($"🚧 NavMesh Obstacles: {TotalObstacles}");
            Debug.Log($"🤖 NavMesh Agents: {TotalAgents}");
            Debug.Log($"⏱️ Setup Status: {(isSetupComplete ? "Complete" : "In Progress")}");
            Debug.Log("🔥 Remember to bake NavMesh in Window > AI > Navigation");
            Debug.Log("══════════════════════════════════════════════════");
        }
        
        /// <summary>
        /// Helper method to check if object is in layer mask
        /// </summary>
        private bool IsInLayerMask(int layer, LayerMask layerMask)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }
        
        /// <summary>
        /// Get the first active layer index from a layer mask
        /// </summary>
        private int GetLayerIndex(LayerMask layerMask)
        {
            for (int i = 0; i < 32; i++)
            {
                if ((layerMask.value & (1 << i)) != 0)
                {
                    return i;
                }
            }
            return -1;
        }
        
        /// <summary>
        /// Manually trigger setup (public method)
        /// </summary>
        [ContextMenu("Setup NavMesh")]
        public void ManualSetup()
        {
            SetupNavMesh();
        }
        
        /// <summary>
        /// Open Navigation window helper
        /// </summary>
        [ContextMenu("Open Navigation Window")]
        public void OpenNavigationWindow()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
            Debug.Log("📖 Navigation window opened. Go to Bake tab and click 'Bake' to complete setup.");
            #endif
        }
        
        /// <summary>
        /// Debug visualization
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!showDebugVisualization) return;
            
            // Draw ground objects
            Gizmos.color = Color.green;
            foreach (var groundObj in groundObjects)
            {
                if (groundObj != null)
                {
                    Gizmos.DrawWireCube(groundObj.transform.position, Vector3.one * 2f);
                }
            }
            
            // Draw NavMesh obstacles
            Gizmos.color = Color.red;
            foreach (var obstacle in navMeshObstacles)
            {
                if (obstacle != null)
                {
                    Gizmos.DrawWireCube(obstacle.transform.position + obstacle.center, obstacle.size);
                }
            }
            
            // Draw NavMesh agents
            Gizmos.color = Color.blue;
            foreach (var agent in navMeshAgents)
            {
                if (agent != null)
                {
                    Gizmos.DrawWireSphere(agent.transform.position, agent.radius);
                }
            }
        }
        
        #if UNITY_EDITOR
        [Header("Editor Debug Info")]
        [SerializeField] private string setupStatus;
        [SerializeField] private int groundObjectCount;
        [SerializeField] private int obstacleCount;
        [SerializeField] private int agentCount;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                setupStatus = isSetupComplete ? "Complete" : (isSetupInProgress ? "In Progress" : "Pending");
                groundObjectCount = TotalGroundObjects;
                obstacleCount = TotalObstacles;
                agentCount = TotalAgents;
            }
        }
        #endif
    }
}