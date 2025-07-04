using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using AIWorld.Research;
using AIWorld.Experimental;
using AIWorld.Environment;

#if UNITY_AI_NAVIGATION
using Unity.AI.Navigation;
#endif

namespace AIWorld.Experimental
{
    /// <summary>
    /// Generates context-specific worlds with appropriate locations, resources, and environmental features
    /// Supports University, Survival, and Social contexts with configurable parameters
    /// Designed for Prof Darryl Charles's experimental research platform
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        [Header("World Generation Configuration")]
        [SerializeField] private Vector2 worldBounds = new Vector2(50f, 50f);
        [SerializeField] private bool generateOnStart = false;
        [SerializeField] private bool enableWorldVisualization = true;
        [SerializeField] private Transform worldParent;
        
        [Header("NavMesh Configuration")]
        [SerializeField] private bool setupNavMeshOnGeneration = true;
        [SerializeField] private bool makeLocationsWalkable = true;
        [SerializeField] private bool makeResourcesAsTriggers = true;
        [SerializeField] private bool makeBoundariesAsObstacles = true;
        
        [Header("URP Material Configuration")]
        [SerializeField] private bool usePresetURPMaterials = true;
        [SerializeField] private Material[] urpLocationMaterials;
        [SerializeField] private Material[] urpResourceMaterials;
        [SerializeField] private Material urpGroundMaterial;
        
        [Header("Location Generation")]
        [SerializeField] private GameObject locationPrefab;
        [SerializeField] private Material[] locationMaterials;
        [SerializeField] private float minLocationSpacing = 5f;
        [SerializeField] private int maxLocationAttempts = 100;
        
        [Header("Resource Generation")]
        [SerializeField] private GameObject resourcePrefab;
        [SerializeField] private Material[] resourceMaterials;
        [SerializeField] private float minResourceSpacing = 3f;
        [SerializeField] private int maxResourceAttempts = 50;
        
        [Header("Environmental Features")]
        [SerializeField] private GameObject[] environmentalDecorations;
        [SerializeField] private int decorationDensity = 20;
        [SerializeField] private LayerMask obstacleLayerMask = -1;
        
        [Header("Context-Specific Assets")]
        [SerializeField] private UniversityWorldAssets universityAssets;
        [SerializeField] private SurvivalWorldAssets survivalAssets;
        [SerializeField] private SocialWorldAssets socialAssets;
        
        // Runtime data
        private List<GameObject> generatedLocations = new List<GameObject>();
        private List<GameObject> generatedResources = new List<GameObject>();
        private List<GameObject> generatedDecorations = new List<GameObject>();
        private Dictionary<string, Vector3> locationPositions = new Dictionary<string, Vector3>();
        private Dictionary<string, List<GameObject>> resourcesByType = new Dictionary<string, List<GameObject>>();
        
        // Current world configuration
        private ExperimentConfiguration currentConfig;
        private SimulationContext currentContext;
        private WorldKnowledgeSystem worldKnowledge;
        
        // Events
        public static event Action<SimulationContext> OnWorldGenerated;
        public static event Action<WorldLocation, Vector3> OnLocationCreated;
        public static event Action<WorldResource, Vector3> OnResourceCreated;
        
        public Vector2 WorldBounds => worldBounds;
        public List<GameObject> GeneratedLocations => new List<GameObject>(generatedLocations);
        public List<GameObject> GeneratedResources => new List<GameObject>(generatedResources);
        public Dictionary<string, Vector3> LocationPositions => new Dictionary<string, Vector3>(locationPositions);
        
        private void Awake()
        {
            if (worldParent == null)
            {
                var worldGO = new GameObject("Generated World");
                worldParent = worldGO.transform;
            }
            
            worldKnowledge = GetComponent<WorldKnowledgeSystem>();
            if (worldKnowledge == null)
            {
                worldKnowledge = gameObject.AddComponent<WorldKnowledgeSystem>();
            }
            
            SetupDefaultAssets();
        }
        
        private void Start()
        {
            if (generateOnStart)
            {
                GenerateDefaultWorld();
            }
        }
        
        #region Initialization
        
        private void SetupDefaultAssets()
        {
            // Setup URP materials if available, otherwise create them
            if (usePresetURPMaterials && urpLocationMaterials != null && urpLocationMaterials.Length > 0)
            {
                locationMaterials = urpLocationMaterials;
                Debug.Log("[World Generator] Using preset URP location materials");
            }
            else
            {
                // Create default URP materials if none assigned
                locationMaterials = new Material[]
                {
                    CreateURPMaterial(Color.blue),    // Work areas
                    CreateURPMaterial(Color.green),   // Social areas
                    CreateURPMaterial(Color.yellow),  // Resource sites
                    CreateURPMaterial(Color.red),     // Special areas
                    CreateURPMaterial(Color.cyan),    // Knowledge areas
                    CreateURPMaterial(Color.magenta)  // Meeting areas
                };
                Debug.Log("[World Generator] Created runtime URP location materials");
            }
            
            if (usePresetURPMaterials && urpResourceMaterials != null && urpResourceMaterials.Length > 0)
            {
                resourceMaterials = urpResourceMaterials;
                Debug.Log("[World Generator] Using preset URP resource materials");
            }
            else
            {
                resourceMaterials = new Material[]
                {
                    CreateURPMaterial(Color.white),
                    CreateURPMaterial(new Color(1f, 0.8f, 0f)), // Gold
                    CreateURPMaterial(new Color(0.5f, 0.3f, 0.1f)), // Brown
                    CreateURPMaterial(new Color(0.8f, 0.4f, 0.2f))  // Orange
                };
                Debug.Log("[World Generator] Created runtime URP resource materials");
            }
        }
        
        /// <summary>
        /// Create URP-compatible material with fallback to built-in shaders
        /// </summary>
        private Material CreateURPMaterial(Color color)
        {
            Material material;
            
            // Try URP Lit shader first
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null)
            {
                material = new Material(urpShader);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0.1f);
                material.SetFloat("_Metallic", 0.0f);
            }
            else
            {
                // Fallback to URP Simple Lit
                var urpSimpleShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (urpSimpleShader != null)
                {
                    material = new Material(urpSimpleShader);
                    material.SetColor("_BaseColor", color);
                }
                else
                {
                    // Final fallback to built-in Standard (will be magenta in URP but won't crash)
                    Debug.LogWarning("[World Generator] URP shaders not found, falling back to Standard shader");
                    material = new Material(Shader.Find("Standard"));
                    material.color = color;
                }
            }
            
            return material;
        }
        
        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        private Material CreateDefaultMaterial(Color color)
        {
            return CreateURPMaterial(color);
        }
        
        #endregion
        
        #region NavMesh Setup Methods
        
        /// <summary>
        /// Setup NavMesh Surface on the ground for agent navigation
        /// </summary>
        private void SetupNavMeshSurface(GameObject groundObject)
        {
#if UNITY_AI_NAVIGATION
            var navMeshSurface = groundObject.GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                navMeshSurface = groundObject.AddComponent<NavMeshSurface>();
            }
            
            // Configure NavMesh Surface
            navMeshSurface.collectObjects = CollectObjects.Children;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navMeshSurface.layerMask = -1; // All layers
            navMeshSurface.overrideVoxelSize = false;
            navMeshSurface.buildHeightMesh = false;
            
            // Build the NavMesh after a short delay (let other objects spawn first)
            Invoke(nameof(BuildNavMesh), 2f);
            
            Debug.Log("[World Generator] NavMesh Surface configured on ground");
#else
            Debug.LogWarning("[World Generator] AI Navigation package not installed. NavMesh Surface setup skipped.");
            Debug.LogWarning("[World Generator] Please install AI Navigation package from Package Manager for full navigation support.");
#endif
        }
        
        /// <summary>
        /// Build the NavMesh (called with delay)
        /// </summary>
        private void BuildNavMesh()
        {
#if UNITY_AI_NAVIGATION
            var navMeshSurface = FindObjectOfType<NavMeshSurface>();
            if (navMeshSurface != null)
            {
                navMeshSurface.BuildNavMesh();
                Debug.Log("[World Generator] NavMesh built successfully");
            }
            else
            {
                Debug.LogWarning("[World Generator] No NavMeshSurface found for building NavMesh");
            }
#else
            Debug.LogWarning("[World Generator] AI Navigation package not available. NavMesh building skipped.");
#endif
        }
        
        /// <summary>
        /// Setup NavMesh obstacle for boundaries and blocking objects
        /// </summary>
        private void SetupNavMeshObstacle(GameObject obstacleObject)
        {
            var navMeshObstacle = obstacleObject.GetComponent<NavMeshObstacle>();
            if (navMeshObstacle == null)
            {
                navMeshObstacle = obstacleObject.AddComponent<NavMeshObstacle>();
            }
            
            navMeshObstacle.carving = true;
            navMeshObstacle.shape = NavMeshObstacleShape.Box;
            navMeshObstacle.center = Vector3.zero;
            navMeshObstacle.size = obstacleObject.transform.localScale;
            
            Debug.Log($"[World Generator] NavMesh Obstacle configured on {obstacleObject.name}");
        }
        
        /// <summary>
        /// Setup navigation properties for location objects
        /// </summary>
        private void SetupLocationNavigation(GameObject locationObject, WorldLocation locationConfig)
        {
            var collider = locationObject.GetComponent<Collider>();
            
            if (makeLocationsWalkable)
            {
                // Make locations walkable areas (agents can enter them)
                if (collider != null)
                {
                    collider.isTrigger = true;
                }
                
                // Add a trigger detector for agents
                var triggerDetector = locationObject.GetComponent<LocationTriggerDetector>();
                if (triggerDetector == null)
                {
                    triggerDetector = locationObject.AddComponent<LocationTriggerDetector>();
                }
                
                Debug.Log($"[World Generator] Location '{locationConfig.name}' configured as walkable trigger area");
            }
            else
            {
                // Make locations as obstacles
                SetupNavMeshObstacle(locationObject);
            }
        }
        
        /// <summary>
        /// Setup navigation properties for resource objects
        /// </summary>
        private void SetupResourceNavigation(GameObject resourceObject, WorldResource resourceConfig)
        {
            var collider = resourceObject.GetComponent<Collider>();
            
            if (makeResourcesAsTriggers)
            {
                // Make resources as collectible triggers
                if (collider != null)
                {
                    collider.isTrigger = true;
                }
                
                // Add a trigger detector for resource collection
                var triggerDetector = resourceObject.GetComponent<ResourceTriggerDetector>();
                if (triggerDetector == null)
                {
                    triggerDetector = resourceObject.AddComponent<ResourceTriggerDetector>();
                }
                
                Debug.Log($"[World Generator] Resource '{resourceConfig.name}' configured as collectible trigger");
            }
            else
            {
                // Make resources as small obstacles
                SetupNavMeshObstacle(resourceObject);
            }
        }
        
        #endregion
        
        #region World Generation
        
        /// <summary>
        /// Generate world based on experiment configuration
        /// </summary>
        public void GenerateWorld(ExperimentConfiguration config)
        {
            if (config == null)
            {
                Debug.LogError("[World Generator] Cannot generate world: null configuration");
                return;
            }
            
            currentConfig = config;
            currentContext = config.simulationContext;
            worldBounds = config.worldSize;
            
            Debug.Log($"[World Generator] Generating {currentContext} world " +
                     $"({worldBounds.x}x{worldBounds.y})");
            
            // Clear existing world
            ClearGeneratedWorld();
            
            // Generate world components
            GenerateEnvironmentalFoundation();
            GenerateLocations(config.worldLocations);
            GenerateResources(config.worldResources, config.maxResourceInstances);
            GenerateDecorations();
            
            // Generate world knowledge
            GenerateWorldKnowledge(config);
            
            // Apply context-specific world features
            ApplyContextSpecificFeatures(config);
            
            OnWorldGenerated?.Invoke(currentContext);
            
            Debug.Log($"[World Generator] Generated world with {generatedLocations.Count} locations, " +
                     $"{generatedResources.Count} resources, {generatedDecorations.Count} decorations");
        }
        
        /// <summary>
        /// Generate a default world for testing
        /// </summary>
        public void GenerateDefaultWorld()
        {
            var defaultConfig = new ExperimentConfiguration
            {
                simulationContext = SimulationContext.University,
                worldSize = worldBounds,
                worldLocations = GetDefaultLocations(SimulationContext.University),
                worldResources = GetDefaultResources(SimulationContext.University),
                maxResourceInstances = 15
            };
            
            GenerateWorld(defaultConfig);
        }
        
        private void GenerateEnvironmentalFoundation()
        {
            // Create ground plane
            var groundGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGO.name = "Ground";
            groundGO.transform.SetParent(worldParent);
            groundGO.transform.localScale = new Vector3(worldBounds.x * 0.1f, 1f, worldBounds.y * 0.1f);
            groundGO.transform.position = Vector3.zero;
            groundGO.layer = 0; // Default layer
            
            // Apply ground material based on context
            var groundRenderer = groundGO.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                groundRenderer.material = GetContextGroundMaterial();
            }
            
            // Setup NavMesh surface on ground
            if (setupNavMeshOnGeneration)
            {
                SetupNavMeshSurface(groundGO);
            }
            
            // Create world boundaries
            CreateWorldBoundaries();
            
            Debug.Log("[World Generator] Environmental foundation created with proper layer assignments and NavMesh setup");
        }
        
        private void CreateWorldBoundaries()
        {
            var boundaryHeight = 2f;
            var boundaryThickness = 0.5f;
            
            // Create invisible walls at world boundaries
            var boundaries = new[]
            {
                new { pos = new Vector3(0, boundaryHeight * 0.5f, worldBounds.y * 0.5f), 
                      scale = new Vector3(worldBounds.x + boundaryThickness, boundaryHeight, boundaryThickness) },
                new { pos = new Vector3(0, boundaryHeight * 0.5f, -worldBounds.y * 0.5f), 
                      scale = new Vector3(worldBounds.x + boundaryThickness, boundaryHeight, boundaryThickness) },
                new { pos = new Vector3(worldBounds.x * 0.5f, boundaryHeight * 0.5f, 0), 
                      scale = new Vector3(boundaryThickness, boundaryHeight, worldBounds.y) },
                new { pos = new Vector3(-worldBounds.x * 0.5f, boundaryHeight * 0.5f, 0), 
                      scale = new Vector3(boundaryThickness, boundaryHeight, worldBounds.y) }
            };
            
            for (int i = 0; i < boundaries.Length; i++)
            {
                var boundaryGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boundaryGO.name = $"Boundary_{i}";
                boundaryGO.transform.SetParent(worldParent);
                boundaryGO.transform.position = boundaries[i].pos;
                boundaryGO.transform.localScale = boundaries[i].scale;
                
                // Make invisible but keep collider
                var renderer = boundaryGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
                
                // Set to Default layer (0) - boundaries don't need special layers
                boundaryGO.layer = 0; // Default layer
                
                // Setup NavMesh obstacle for boundaries
                if (setupNavMeshOnGeneration && makeBoundariesAsObstacles)
                {
                    SetupNavMeshObstacle(boundaryGO);
                }
                
                Debug.Log($"[World Generator] Created boundary {i} on layer {boundaryGO.layer}");
            }
        }
        
        private Material GetContextGroundMaterial()
        {
            // Use preset URP ground material if available
            if (usePresetURPMaterials && urpGroundMaterial != null)
            {
                return urpGroundMaterial;
            }
            
            // Create context-appropriate URP ground material
            return currentContext switch
            {
                SimulationContext.University => CreateURPMaterial(new Color(0.9f, 0.9f, 0.9f)), // Light gray
                SimulationContext.Survival => CreateURPMaterial(new Color(0.4f, 0.3f, 0.2f)),  // Brown earth
                SimulationContext.Social => CreateURPMaterial(new Color(0.9f, 0.8f, 0.6f)),    // Sandy beach
                _ => CreateURPMaterial(Color.gray)
            };
        }
        
        #endregion
        
        #region Location Generation
        
        private void GenerateLocations(List<WorldLocation> locationConfigs)
        {
            if (locationConfigs == null || locationConfigs.Count == 0)
            {
                Debug.LogWarning("[World Generator] No locations configured for generation");
                return;
            }
            
            foreach (var locationConfig in locationConfigs)
            {
                var position = FindValidLocationPosition(locationConfig);
                if (position != Vector3.zero)
                {
                    CreateLocation(locationConfig, position);
                }
            }
        }
        
        private Vector3 FindValidLocationPosition(WorldLocation locationConfig)
        {
            var attempts = 0;
            
            while (attempts < maxLocationAttempts)
            {
                var position = GenerateRandomWorldPosition();
                
                if (IsValidLocationPosition(position, locationConfig))
                {
                    return position;
                }
                
                attempts++;
            }
            
            Debug.LogWarning($"[World Generator] Could not find valid position for location '{locationConfig.name}' " +
                           $"after {maxLocationAttempts} attempts");
            return Vector3.zero;
        }
        
        private Vector3 GenerateRandomWorldPosition()
        {
            return new Vector3(
                UnityEngine.Random.Range(-worldBounds.x * 0.4f, worldBounds.x * 0.4f), // Keep some margin
                0f,
                UnityEngine.Random.Range(-worldBounds.y * 0.4f, worldBounds.y * 0.4f)
            );
        }
        
        private bool IsValidLocationPosition(Vector3 position, WorldLocation locationConfig)
        {
            // Check minimum spacing from other locations
            foreach (var existingPosition in locationPositions.Values)
            {
                if (Vector3.Distance(position, existingPosition) < minLocationSpacing)
                {
                    return false;
                }
            }
            
            // Check for obstacles
            if (Physics.CheckSphere(position, 2f, obstacleLayerMask))
            {
                return false;
            }
            
            // Context-specific position validation
            return ValidateContextSpecificPosition(position, locationConfig);
        }
        
        private bool ValidateContextSpecificPosition(Vector3 position, WorldLocation locationConfig)
        {
            switch (currentContext)
            {
                case SimulationContext.University:
                    return ValidateUniversityLocationPosition(position, locationConfig);
                case SimulationContext.Survival:
                    return ValidateSurvivalLocationPosition(position, locationConfig);
                case SimulationContext.Social:
                    return ValidateSocialLocationPosition(position, locationConfig);
                default:
                    return true;
            }
        }
        
        private bool ValidateUniversityLocationPosition(Vector3 position, WorldLocation locationConfig)
        {
            // Research areas should be somewhat separated
            if (locationConfig.type == LocationType.WorkArea)
            {
                foreach (var kvp in locationPositions)
                {
                    if (kvp.Key.Contains("Research") && Vector3.Distance(position, kvp.Value) < 8f)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        private bool ValidateSurvivalLocationPosition(Vector3 position, WorldLocation locationConfig)
        {
            // Water sources should be accessible but not too close together
            if (locationConfig.name.Contains("Water") && locationPositions.Any(kvp => 
                kvp.Key.Contains("Water") && Vector3.Distance(position, kvp.Value) < 15f))
            {
                return false;
            }
            
            return true;
        }
        
        private bool ValidateSocialLocationPosition(Vector3 position, WorldLocation locationConfig)
        {
            // Intimate areas should be more secluded (edges of map)
            if (locationConfig.type == LocationType.IntimateArea)
            {
                var distanceFromCenter = Vector3.Distance(position, Vector3.zero);
                var maxDistance = Mathf.Min(worldBounds.x, worldBounds.y) * 0.3f;
                return distanceFromCenter > maxDistance;
            }
            
            return true;
        }
        
        private void CreateLocation(WorldLocation locationConfig, Vector3 position)
        {
            GameObject locationGO;
            
            // Use prefab if available, otherwise create primitive
            if (locationPrefab != null)
            {
                locationGO = Instantiate(locationPrefab, position, Quaternion.identity, worldParent);
            }
            else
            {
                locationGO = CreateDefaultLocationObject(locationConfig, position);
            }
            
            locationGO.name = $"Location_{locationConfig.name}";
            locationGO.layer = 0; // Ensure all locations are on Default layer
            
            // Configure location component
            var locationComponent = locationGO.GetComponent<WorldLocationComponent>();
            if (locationComponent == null)
            {
                locationComponent = locationGO.AddComponent<WorldLocationComponent>();
            }
            
            locationComponent.Initialize(locationConfig);
            
            // Apply visual styling based on location type
            ApplyLocationStyling(locationGO, locationConfig);
            
            // Setup navigation properties for locations
            if (setupNavMeshOnGeneration)
            {
                SetupLocationNavigation(locationGO, locationConfig);
            }
            
            // Add location to tracking
            generatedLocations.Add(locationGO);
            locationPositions[locationConfig.name] = position;
            
            OnLocationCreated?.Invoke(locationConfig, position);
            
            Debug.Log($"[World Generator] Created location '{locationConfig.name}' at {position} on layer {locationGO.layer}");
        }
        
        private GameObject CreateDefaultLocationObject(WorldLocation locationConfig, Vector3 position)
        {
            var locationGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            locationGO.transform.position = position;
            locationGO.transform.localScale = GetLocationScale(locationConfig);
            locationGO.layer = 0; // Default layer for locations
            
            return locationGO;
        }
        
        private Vector3 GetLocationScale(WorldLocation locationConfig)
        {
            return locationConfig.type switch
            {
                LocationType.WorkArea => new Vector3(4f, 1f, 4f),
                LocationType.SocialArea => new Vector3(6f, 1f, 6f),
                LocationType.ResourceSite => new Vector3(3f, 1f, 3f),
                LocationType.MeetingArea => new Vector3(5f, 1f, 5f),
                LocationType.IntimateArea => new Vector3(2f, 1f, 2f),
                _ => new Vector3(3f, 1f, 3f)
            };
        }
        
        private void ApplyLocationStyling(GameObject locationGO, WorldLocation locationConfig)
        {
            var renderer = locationGO.GetComponent<Renderer>();
            if (renderer == null) return;
            
            var materialIndex = (int)locationConfig.type % locationMaterials.Length;
            renderer.material = locationMaterials[materialIndex];
            
            // Apply context-specific styling
            ApplyContextSpecificLocationStyling(locationGO, locationConfig);
        }
        
        private void ApplyContextSpecificLocationStyling(GameObject locationGO, WorldLocation locationConfig)
        {
            switch (currentContext)
            {
                case SimulationContext.University:
                    ApplyUniversityLocationStyling(locationGO, locationConfig);
                    break;
                case SimulationContext.Survival:
                    ApplySurvivalLocationStyling(locationGO, locationConfig);
                    break;
                case SimulationContext.Social:
                    ApplySocialLocationStyling(locationGO, locationConfig);
                    break;
            }
        }
        
        private void ApplyUniversityLocationStyling(GameObject locationGO, WorldLocation locationConfig)
        {
            // Add university-specific visual elements
            if (universityAssets?.locationPrefabs != null)
            {
                // Use context-specific prefabs if available
            }
        }
        
        private void ApplySurvivalLocationStyling(GameObject locationGO, WorldLocation locationConfig)
        {
            // Add survival-specific visual elements
            if (survivalAssets?.locationPrefabs != null)
            {
                // Use context-specific prefabs if available
            }
        }
        
        private void ApplySocialLocationStyling(GameObject locationGO, WorldLocation locationConfig)
        {
            // Add social-specific visual elements
            if (socialAssets?.locationPrefabs != null)
            {
                // Use context-specific prefabs if available
            }
        }
        
        #endregion
        
        #region Resource Generation
        
        private void GenerateResources(List<WorldResource> resourceConfigs, int maxInstances)
        {
            if (resourceConfigs == null || resourceConfigs.Count == 0)
            {
                Debug.LogWarning("[World Generator] No resources configured for generation");
                return;
            }
            
            var totalResourcesToCreate = Mathf.Min(maxInstances, CalculateTotalResourceInstances(resourceConfigs));
            var createdCount = 0;
            
            foreach (var resourceConfig in resourceConfigs)
            {
                var instanceCount = CalculateResourceInstanceCount(resourceConfig, totalResourcesToCreate);
                
                for (int i = 0; i < instanceCount && createdCount < totalResourcesToCreate; i++)
                {
                    var position = FindValidResourcePosition(resourceConfig);
                    if (position != Vector3.zero)
                    {
                        CreateResource(resourceConfig, position);
                        createdCount++;
                    }
                }
            }
        }
        
        private int CalculateTotalResourceInstances(List<WorldResource> resourceConfigs)
        {
            // Calculate total instances based on scarcity
            var totalInstances = 0;
            
            foreach (var resource in resourceConfigs)
            {
                var instances = Mathf.RoundToInt((1.0f - resource.scarcity) * 10f) + 1;
                totalInstances += instances;
            }
            
            return totalInstances;
        }
        
        private int CalculateResourceInstanceCount(WorldResource resourceConfig, int totalBudget)
        {
            // More instances for less scarce resources
            var scarcityFactor = 1.0f - resourceConfig.scarcity;
            var baseCount = Mathf.RoundToInt(scarcityFactor * 8f) + 1;
            
            return Mathf.Min(baseCount, totalBudget / 3); // Don't let one resource dominate
        }
        
        private Vector3 FindValidResourcePosition(WorldResource resourceConfig)
        {
            var attempts = 0;
            
            while (attempts < maxResourceAttempts)
            {
                var position = GenerateRandomWorldPosition();
                
                if (IsValidResourcePosition(position, resourceConfig))
                {
                    return position;
                }
                
                attempts++;
            }
            
            return Vector3.zero;
        }
        
        private bool IsValidResourcePosition(Vector3 position, WorldResource resourceConfig)
        {
            // Check minimum spacing from other resources
            foreach (var resource in generatedResources)
            {
                if (Vector3.Distance(position, resource.transform.position) < minResourceSpacing)
                {
                    return false;
                }
            }
            
            // Check for obstacles
            if (Physics.CheckSphere(position, 1f, obstacleLayerMask))
            {
                return false;
            }
            
            // Some resources should be near locations
            if (ShouldResourceBeNearLocation(resourceConfig))
            {
                var nearLocation = locationPositions.Values.Any(loc => 
                    Vector3.Distance(position, loc) < 8f);
                
                if (!nearLocation)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        private bool ShouldResourceBeNearLocation(WorldResource resourceConfig)
        {
            return resourceConfig.type switch
            {
                ResourceType.Tool => true,
                ResourceType.Achievement => true,
                ResourceType.Special => true,
                _ => false
            };
        }
        
        private void CreateResource(WorldResource resourceConfig, Vector3 position)
        {
            GameObject resourceGO;
            
            if (resourcePrefab != null)
            {
                resourceGO = Instantiate(resourcePrefab, position, Quaternion.identity, worldParent);
            }
            else
            {
                resourceGO = CreateDefaultResourceObject(resourceConfig, position);
            }
            
            resourceGO.name = $"Resource_{resourceConfig.name}_{generatedResources.Count:D2}";
            resourceGO.layer = 0; // Ensure all resources are on Default layer
            
            // Configure resource component
            var resourceComponent = resourceGO.GetComponent<WorldResourceComponent>();
            if (resourceComponent == null)
            {
                resourceComponent = resourceGO.AddComponent<WorldResourceComponent>();
            }
            
            resourceComponent.Initialize(resourceConfig);
            
            // Apply visual styling
            ApplyResourceStyling(resourceGO, resourceConfig);
            
            // Setup navigation properties for resources
            if (setupNavMeshOnGeneration)
            {
                SetupResourceNavigation(resourceGO, resourceConfig);
            }
            
            // Add to tracking
            generatedResources.Add(resourceGO);
            
            if (!resourcesByType.ContainsKey(resourceConfig.name))
            {
                resourcesByType[resourceConfig.name] = new List<GameObject>();
            }
            resourcesByType[resourceConfig.name].Add(resourceGO);
            
            OnResourceCreated?.Invoke(resourceConfig, position);
            
            Debug.Log($"[World Generator] Created resource '{resourceConfig.name}' at {position} on layer {resourceGO.layer}");
        }
        
        private GameObject CreateDefaultResourceObject(WorldResource resourceConfig, Vector3 position)
        {
            var resourceGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            resourceGO.transform.position = position + Vector3.up * 0.5f; // Slightly above ground
            resourceGO.transform.localScale = GetResourceScale(resourceConfig);
            resourceGO.layer = 0; // Default layer for resources
            
            return resourceGO;
        }
        
        private Vector3 GetResourceScale(WorldResource resourceConfig)
        {
            return resourceConfig.type switch
            {
                ResourceType.Essential => Vector3.one * 1.2f,
                ResourceType.Special => Vector3.one * 1.5f,
                ResourceType.Achievement => Vector3.one * 0.8f,
                _ => Vector3.one
            };
        }
        
        private void ApplyResourceStyling(GameObject resourceGO, WorldResource resourceConfig)
        {
            var renderer = resourceGO.GetComponent<Renderer>();
            if (renderer == null) return;
            
            var materialIndex = (int)resourceConfig.type % resourceMaterials.Length;
            renderer.material = resourceMaterials[materialIndex];
        }
        
        #endregion
        
        #region Decoration Generation
        
        private void GenerateDecorations()
        {
            if (environmentalDecorations == null || environmentalDecorations.Length == 0) return;
            
            for (int i = 0; i < decorationDensity; i++)
            {
                var position = GenerateRandomWorldPosition();
                
                if (IsValidDecorationPosition(position))
                {
                    CreateDecoration(position);
                }
            }
        }
        
        private bool IsValidDecorationPosition(Vector3 position)
        {
            // Keep decorations away from locations and resources
            var minDistance = 3f;
            
            foreach (var location in generatedLocations)
            {
                if (Vector3.Distance(position, location.transform.position) < minDistance)
                {
                    return false;
                }
            }
            
            foreach (var resource in generatedResources)
            {
                if (Vector3.Distance(position, resource.transform.position) < minDistance)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        private void CreateDecoration(Vector3 position)
        {
            var decorationPrefab = environmentalDecorations[UnityEngine.Random.Range(0, environmentalDecorations.Length)];
            var decorationGO = Instantiate(decorationPrefab, position, Quaternion.identity, worldParent);
            decorationGO.name = $"Decoration_{generatedDecorations.Count:D2}";
            decorationGO.layer = 0; // Default layer for decorations
            
            // Random rotation and slight scale variation
            decorationGO.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
            var scaleVariation = UnityEngine.Random.Range(0.8f, 1.2f);
            decorationGO.transform.localScale *= scaleVariation;
            
            generatedDecorations.Add(decorationGO);
        }
        
        #endregion
        
        #region Context-Specific Features
        
        private void ApplyContextSpecificFeatures(ExperimentConfiguration config)
        {
            switch (config.simulationContext)
            {
                case SimulationContext.University:
                    ApplyUniversityFeatures(config);
                    break;
                case SimulationContext.Survival:
                    ApplySurvivalFeatures(config);
                    break;
                case SimulationContext.Social:
                    ApplySocialFeatures(config);
                    break;
            }
        }
        
        private void ApplyUniversityFeatures(ExperimentConfiguration config)
        {
            // Add university-specific features
            CreateUniversityAmbientElements();
            SetupUniversityLighting();
        }
        
        private void ApplySurvivalFeatures(ExperimentConfiguration config)
        {
            // Add survival-specific features
            CreateSurvivalEnvironmentalHazards();
            SetupSurvivalLighting();
        }
        
        private void ApplySocialFeatures(ExperimentConfiguration config)
        {
            // Add social-specific features
            CreateSocialAmbientElements();
            SetupSocialLighting();
        }
        
        private void CreateUniversityAmbientElements()
        {
            // Add subtle university atmosphere elements
            Debug.Log("[World Generator] Applied university-specific features");
        }
        
        private void CreateSurvivalEnvironmentalHazards()
        {
            // Add environmental challenges
            Debug.Log("[World Generator] Applied survival-specific features");
        }
        
        private void CreateSocialAmbientElements()
        {
            // Add social atmosphere elements
            Debug.Log("[World Generator] Applied social-specific features");
        }
        
        private void SetupUniversityLighting()
        {
            // Bright, professional lighting
            RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.9f);
        }
        
        private void SetupSurvivalLighting()
        {
            // Harsher, more dramatic lighting
            RenderSettings.ambientLight = new Color(0.6f, 0.5f, 0.4f);
        }
        
        private void SetupSocialLighting()
        {
            // Warm, inviting lighting
            RenderSettings.ambientLight = new Color(0.9f, 0.7f, 0.6f);
        }
        
        #endregion
        
        #region World Knowledge Generation
        
        private void GenerateWorldKnowledge(ExperimentConfiguration config)
        {
            if (worldKnowledge == null) return;
            
            var knowledgeData = new WorldKnowledgeData
            {
                worldContext = config.simulationContext,
                worldSize = config.worldSize,
                locations = GenerateLocationKnowledge(config.worldLocations),
                resources = GenerateResourceKnowledge(config.worldResources),
                contextualRules = GenerateContextualRules(config.simulationContext),
                socialDynamics = GenerateSocialDynamicsKnowledge(config),
                objectives = GenerateContextObjectives(config.simulationContext)
            };
            
            worldKnowledge.Initialize(knowledgeData);
            
            Debug.Log($"[World Generator] Generated world knowledge for {config.simulationContext} context");
        }
        
        private List<LocationKnowledge> GenerateLocationKnowledge(List<WorldLocation> locations)
        {
            var knowledge = new List<LocationKnowledge>();
            
            foreach (var location in locations)
            {
                if (locationPositions.TryGetValue(location.name, out var position))
                {
                    knowledge.Add(new LocationKnowledge
                    {
                        name = location.name,
                        type = location.type,
                        position = position,
                        capacity = location.capacity,
                        accessRequirements = location.requiresPermission ? "Permission required" : "Open access",
                        primaryPurpose = GetLocationPurpose(location),
                        needsSatisfied = location.needsSatisfied?.ToList() ?? new List<string>()
                    });
                }
            }
            
            return knowledge;
        }
        
        private string GetLocationPurpose(WorldLocation location)
        {
            return currentContext switch
            {
                SimulationContext.University => GetUniversityLocationPurpose(location),
                SimulationContext.Survival => GetSurvivalLocationPurpose(location),
                SimulationContext.Social => GetSocialLocationPurpose(location),
                _ => "General purpose location"
            };
        }
        
        private string GetUniversityLocationPurpose(WorldLocation location)
        {
            return location.type switch
            {
                LocationType.WorkArea => "Research and academic work",
                LocationType.SocialArea => "Faculty networking and collaboration",
                LocationType.MeetingArea => "Academic meetings and presentations",
                LocationType.KnowledgeArea => "Study and knowledge acquisition",
                _ => "Academic facility"
            };
        }
        
        private string GetSurvivalLocationPurpose(WorldLocation location)
        {
            return location.type switch
            {
                LocationType.ResourceSite => "Essential resource gathering",
                LocationType.ShelterArea => "Protection and rest",
                LocationType.SafetyArea => "Security and observation",
                LocationType.SocialArea => "Group coordination",
                _ => "Survival facility"
            };
        }
        
        private string GetSocialLocationPurpose(WorldLocation location)
        {
            return location.type switch
            {
                LocationType.SocialArea => "Social interaction and networking",
                LocationType.IntimateArea => "Private conversations and bonding",
                LocationType.GatheringArea => "Group activities and entertainment",
                LocationType.RelaxationArea => "Rest and casual interaction",
                _ => "Social facility"
            };
        }
        
        private List<ResourceKnowledge> GenerateResourceKnowledge(List<WorldResource> resources)
        {
            var knowledge = new List<ResourceKnowledge>();
            
            foreach (var resource in resources)
            {
                if (resourcesByType.TryGetValue(resource.name, out var instances))
                {
                    knowledge.Add(new ResourceKnowledge
                    {
                        name = resource.name,
                        type = resource.type,
                        value = resource.value,
                        scarcity = resource.scarcity,
                        regenerationRate = resource.regenerationRate,
                        availableQuantity = instances.Count,
                        acquisitionMethod = GetResourceAcquisitionMethod(resource),
                        competitionLevel = CalculateResourceCompetition(resource)
                    });
                }
            }
            
            return knowledge;
        }
        
        private string GetResourceAcquisitionMethod(WorldResource resource)
        {
            return currentContext switch
            {
                SimulationContext.University => GetUniversityResourceMethod(resource),
                SimulationContext.Survival => GetSurvivalResourceMethod(resource),
                SimulationContext.Social => GetSocialResourceMethod(resource),
                _ => "Standard acquisition"
            };
        }
        
        private string GetUniversityResourceMethod(WorldResource resource)
        {
            return resource.type switch
            {
                ResourceType.Currency => "Grant application and approval",
                ResourceType.Achievement => "Research publication and presentation",
                ResourceType.Tool => "Equipment reservation and usage",
                _ => "Academic process"
            };
        }
        
        private string GetSurvivalResourceMethod(WorldResource resource)
        {
            return resource.type switch
            {
                ResourceType.Essential => "Direct gathering from environment",
                ResourceType.Consumable => "Foraging and hunting",
                ResourceType.Tool => "Crafting from raw materials",
                _ => "Survival activity"
            };
        }
        
        private string GetSocialResourceMethod(WorldResource resource)
        {
            return resource.type switch
            {
                ResourceType.Social => "Social interaction and relationship building",
                ResourceType.Special => "Exclusive activities and privileges",
                ResourceType.Currency => "Social influence and networking",
                _ => "Social activity"
            };
        }
        
        private float CalculateResourceCompetition(WorldResource resource)
        {
            // Higher scarcity = higher competition
            return resource.scarcity * (currentConfig?.enableResourceCompetition == true ? 1.5f : 0.5f);
        }
        
        private List<string> GenerateContextualRules(SimulationContext context)
        {
            return context switch
            {
                SimulationContext.University => new List<string>
                {
                    "Research collaboration increases knowledge gain",
                    "Publication requires sustained research effort",
                    "Grant competition determines resource access",
                    "Academic reputation affects collaboration opportunities"
                },
                SimulationContext.Survival => new List<string>
                {
                    "Resource sharing improves group survival",
                    "Environmental hazards require collective response",
                    "Territory control provides resource advantages",
                    "Cooperation essential for complex tasks"
                },
                SimulationContext.Social => new List<string>
                {
                    "Social connections determine influence",
                    "Relationship quality affects collaboration",
                    "Reputation spreads through social networks",
                    "Exclusive activities require social capital"
                },
                _ => new List<string> { "Standard social interaction rules apply" }
            };
        }
        
        private List<string> GenerateSocialDynamicsKnowledge(ExperimentConfiguration config)
        {
            var dynamics = new List<string>
            {
                $"Total agents: {config.totalAgentCount}",
                $"Groups: {config.agentGroups?.Count ?? 0}",
                $"Conflict system: {(config.enableConflictSystem ? "Enabled" : "Disabled")}",
                $"Resource competition: {(config.enableResourceCompetition ? "Enabled" : "Disabled")}"
            };
            
            if (config.agentGroups != null)
            {
                foreach (var group in config.agentGroups)
                {
                    dynamics.Add($"Group '{group.groupName}': {group.agentCount} {group.archetype} agents");
                }
            }
            
            return dynamics;
        }
        
        private List<string> GenerateContextObjectives(SimulationContext context)
        {
            return context switch
            {
                SimulationContext.University => new List<string>
                {
                    "Conduct research and publish findings",
                    "Secure grants and resources",
                    "Build academic reputation",
                    "Collaborate with colleagues",
                    "Present at conferences"
                },
                SimulationContext.Survival => new List<string>
                {
                    "Secure food and water sources",
                    "Build and maintain shelter",
                    "Establish territorial control",
                    "Form survival alliances",
                    "Prepare for environmental challenges"
                },
                SimulationContext.Social => new List<string>
                {
                    "Build romantic relationships",
                    "Increase social status",
                    "Form strategic alliances",
                    "Gain exclusive access",
                    "Influence group dynamics"
                },
                _ => new List<string> { "Achieve personal goals", "Maintain social relationships" }
            };
        }
        
        #endregion
        
        #region Utility Methods
        
        private List<WorldLocation> GetDefaultLocations(SimulationContext context)
        {
            return context switch
            {
                SimulationContext.University => new List<WorldLocation>
                {
                    new WorldLocation { name = "Research Lab Alpha", type = LocationType.WorkArea, capacity = 4, 
                                      needsSatisfied = new[] { "Achievement", "Knowledge" } },
                    new WorldLocation { name = "Faculty Lounge", type = LocationType.SocialArea, capacity = 8,
                                      needsSatisfied = new[] { "Social", "Energy" } },
                    new WorldLocation { name = "Library", type = LocationType.KnowledgeArea, capacity = 6,
                                      needsSatisfied = new[] { "Knowledge", "Creativity" } }
                },
                SimulationContext.Survival => new List<WorldLocation>
                {
                    new WorldLocation { name = "Water Source", type = LocationType.ResourceSite, capacity = 3,
                                      needsSatisfied = new[] { "Physiological" } },
                    new WorldLocation { name = "Cave Shelter", type = LocationType.ShelterArea, capacity = 5,
                                      needsSatisfied = new[] { "Safety", "Energy" } }
                },
                SimulationContext.Social => new List<WorldLocation>
                {
                    new WorldLocation { name = "Beach Bar", type = LocationType.SocialArea, capacity = 8,
                                      needsSatisfied = new[] { "Social", "Energy" } },
                    new WorldLocation { name = "Pool Area", type = LocationType.RelaxationArea, capacity = 6,
                                      needsSatisfied = new[] { "Social", "Self-actualization" } }
                },
                _ => new List<WorldLocation>()
            };
        }
        
        private List<WorldResource> GetDefaultResources(SimulationContext context)
        {
            return context switch
            {
                SimulationContext.University => new List<WorldResource>
                {
                    new WorldResource { name = "Research Grant", type = ResourceType.Currency, 
                                      scarcity = 0.3f, value = 100, regenerationRate = 0.1f },
                    new WorldResource { name = "Lab Equipment", type = ResourceType.Tool,
                                      scarcity = 0.5f, value = 50, regenerationRate = 0.0f }
                },
                SimulationContext.Survival => new List<WorldResource>
                {
                    new WorldResource { name = "Food", type = ResourceType.Consumable,
                                      scarcity = 0.4f, value = 30, regenerationRate = 0.2f },
                    new WorldResource { name = "Fresh Water", type = ResourceType.Essential,
                                      scarcity = 0.6f, value = 50, regenerationRate = 0.3f }
                },
                SimulationContext.Social => new List<WorldResource>
                {
                    new WorldResource { name = "Romantic Token", type = ResourceType.Social,
                                      scarcity = 0.4f, value = 75, regenerationRate = 0.15f },
                    new WorldResource { name = "Social Currency", type = ResourceType.Currency,
                                      scarcity = 0.5f, value = 40, regenerationRate = 0.2f }
                },
                _ => new List<WorldResource>()
            };
        }
        
        /// <summary>
        /// Clear all generated world objects
        /// </summary>
        public void ClearGeneratedWorld()
        {
            // Destroy generated objects
            foreach (var location in generatedLocations)
            {
                if (location != null) DestroyImmediate(location);
            }
            
            foreach (var resource in generatedResources)
            {
                if (resource != null) DestroyImmediate(resource);
            }
            
            foreach (var decoration in generatedDecorations)
            {
                if (decoration != null) DestroyImmediate(decoration);
            }
            
            // Clear world parent
            if (worldParent != null)
            {
                for (int i = worldParent.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(worldParent.GetChild(i).gameObject);
                }
            }
            
            // Clear tracking data
            generatedLocations.Clear();
            generatedResources.Clear();
            generatedDecorations.Clear();
            locationPositions.Clear();
            resourcesByType.Clear();
            
            Debug.Log("[World Generator] Cleared generated world");
        }
        
        /// <summary>
        /// Get world generation statistics for research
        /// </summary>
        public WorldGenerationStats GetWorldStats()
        {
            return new WorldGenerationStats
            {
                worldContext = currentContext,
                worldSize = worldBounds,
                locationCount = generatedLocations.Count,
                resourceCount = generatedResources.Count,
                decorationCount = generatedDecorations.Count,
                locationTypes = generatedLocations
                    .Select(loc => loc.GetComponent<WorldLocationComponent>()?.LocationConfig.type.ToString() ?? "Unknown")
                    .GroupBy(type => type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                resourceTypes = resourcesByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count)
            };
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw world bounds
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(worldBounds.x, 0.1f, worldBounds.y));
            
            // Draw location positions
            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                foreach (var position in locationPositions.Values)
                {
                    Gizmos.DrawWireSphere(position, 2f);
                }
            }
        }
        
        #endregion
    }
    
    #region Data Structures and Components
    
    [System.Serializable]
    public class UniversityWorldAssets
    {
        public GameObject[] locationPrefabs;
        public GameObject[] decorationPrefabs;
        public Material[] universityMaterials;
    }
    
    [System.Serializable]
    public class SurvivalWorldAssets
    {
        public GameObject[] locationPrefabs;
        public GameObject[] decorationPrefabs;
        public Material[] survivalMaterials;
    }
    
    [System.Serializable]
    public class SocialWorldAssets
    {
        public GameObject[] locationPrefabs;
        public GameObject[] decorationPrefabs;
        public Material[] socialMaterials;
    }
    
    // WorldGenerationStats is now defined in ExperimentalDefinitions.cs
    
    #endregion
}