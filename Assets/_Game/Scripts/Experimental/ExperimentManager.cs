using System;
using System.Collections;
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
    /// Main controller for managing experimental runs and coordinating all experimental systems
    /// Designed for Prof Darryl Charles's multi-context research platform
    /// </summary>
    public class ExperimentManager : MonoBehaviour
    {
        [Header("Experiment Status")]
        [SerializeField] private bool experimentRunning = false;
        [SerializeField] private float experimentStartTime = 0f;
        [SerializeField] private float currentExperimentDuration = 0f;
        [SerializeField] private ExperimentPhase currentPhase = ExperimentPhase.Inactive;
        
        [Header("Experiment Configuration")]
        [SerializeField] private ExperimentConfiguration currentExperiment;
        [SerializeField] private bool autoSaveResults = true;
        [SerializeField] private float autoSaveInterval = 300f; // 5 minutes
        [SerializeField] private string experimentResultsPath = "ExperimentResults";
        
        [Header("System Coordination")]
        [SerializeField] private bool validateSystemsOnStart = true;
        [SerializeField] private bool enableRealTimeMonitoring = true;
        [SerializeField] private float monitoringInterval = 30f; // 30 seconds
        
        [Header("Experiment Control")]
        [SerializeField] private bool allowEarlyTermination = true;
        [SerializeField] private bool pauseOnError = true;
        [SerializeField] private int maxErrorsBeforeTermination = 5;
        
        // System references
        private ExperimentConfigurationManager configManager;
        private AgentGroupManager agentGroupManager;
        private WorldGenerator worldGenerator;
        private ConflictManager conflictManager;
        private WorldKnowledgeSystem knowledgeSystem;
        private ResearchDataManager dataManager;
        
        // Runtime data
        private List<ExperimentEvent> experimentEvents = new List<ExperimentEvent>();
        private Dictionary<string, object> experimentMetrics = new Dictionary<string, object>();
        private List<string> systemErrors = new List<string>();
        private Coroutine experimentCoroutine;
        private DateTime experimentStartTimestamp;
        
        // Events
        public static event Action<ExperimentConfiguration> OnExperimentStarted;
        public static event Action<ExperimentPhase> OnExperimentPhaseChanged;
        public static event Action<ExperimentResults> OnExperimentCompleted;
        public static event Action<string> OnExperimentError;
        public static event Action OnExperimentPaused;
        public static event Action OnExperimentResumed;
        
        public bool IsExperimentRunning => experimentRunning;
        public ExperimentPhase CurrentPhase => currentPhase;
        public float ExperimentDuration => currentExperimentDuration;
        public ExperimentConfiguration CurrentExperiment => currentExperiment;
        public int ErrorCount => systemErrors.Count;
        
        private void Awake()
        {
            InitializeSystemReferences();
        }
        
        private void Start()
        {
            if (validateSystemsOnStart)
            {
                ValidateExperimentSystems();
            }
        }
        
        private void Update()
        {
            if (experimentRunning)
            {
                UpdateExperimentTimer();
                CheckExperimentTerminationConditions();
            }
        }
        
        #region Initialization
        
        private void InitializeSystemReferences()
        {
            configManager = FindFirstObjectByType<ExperimentConfigurationManager>();
            agentGroupManager = FindFirstObjectByType<AgentGroupManager>();
            worldGenerator = FindFirstObjectByType<WorldGenerator>();
            conflictManager = FindFirstObjectByType<ConflictManager>();
            knowledgeSystem = FindFirstObjectByType<WorldKnowledgeSystem>();
            dataManager = FindFirstObjectByType<ResearchDataManager>();
            
            // Subscribe to system events
            if (configManager != null)
            {
                ExperimentConfigurationManager.OnExperimentConfigured += OnExperimentConfigured;
            }
            
            // Create missing components if needed
            EnsureRequiredComponents();
        }
        
        private void EnsureRequiredComponents()
        {
            if (dataManager == null)
            {
                var dataManagerGO = new GameObject("Research Data Manager");
                dataManager = dataManagerGO.AddComponent<ResearchDataManager>();
            }
            
            if (knowledgeSystem == null)
            {
                knowledgeSystem = gameObject.AddComponent<WorldKnowledgeSystem>();
            }
        }
        
        private void ValidateExperimentSystems()
        {
            var validationErrors = new List<string>();
            
            // Validate core systems
            if (configManager == null)
                validationErrors.Add("ExperimentConfigurationManager not found");
            if (agentGroupManager == null)
                validationErrors.Add("AgentGroupManager not found");
            if (worldGenerator == null)
                validationErrors.Add("WorldGenerator not found");
            if (dataManager == null)
                validationErrors.Add("ResearchDataManager not found");
            
            // Validate optional systems
            if (conflictManager == null)
                Debug.LogWarning("[Experiment Manager] ConflictManager not found - conflict system disabled");
            if (knowledgeSystem == null)
                Debug.LogWarning("[Experiment Manager] WorldKnowledgeSystem not found - knowledge system disabled");
            
            if (validationErrors.Count > 0)
            {
                var errorMessage = "System validation failed:\\n" + string.Join("\\n", validationErrors);
                Debug.LogError($"[Experiment Manager] {errorMessage}");
                OnExperimentError?.Invoke(errorMessage);
            }
            else
            {
                Debug.Log("[Experiment Manager] All required systems validated successfully");
            }
        }
        
        #endregion
        
        #region Experiment Control
        
        public void StartExperiment(ExperimentConfiguration config)
        {
            if (experimentRunning)
            {
                Debug.LogWarning("[Experiment Manager] Experiment already running. Stop current experiment first.");
                return;
            }
            
            if (config == null)
            {
                LogExperimentError("Cannot start experiment: configuration is null");
                return;
            }
            
            currentExperiment = config;
            
            Debug.Log($"[Experiment Manager] Starting experiment: {config.experimentName}");
            
            // Start experiment coroutine
            experimentCoroutine = StartCoroutine(RunExperimentSequence(config));
        }
        
        private IEnumerator RunExperimentSequence(ExperimentConfiguration config)
        {
            experimentRunning = true;
            experimentStartTime = Time.time;
            experimentStartTimestamp = DateTime.UtcNow;
            currentExperimentDuration = 0f;
            systemErrors.Clear();
            experimentEvents.Clear();

            // Phase 1: Preparation
            yield return StartCoroutine(ExperimentPreparationPhase(config));
            // Phase 2: World Generation
            yield return StartCoroutine(WorldGenerationPhase(config));
            // Phase 3: Agent Creation
            yield return StartCoroutine(AgentCreationPhase(config));
            // Phase 4: System Initialization
            yield return StartCoroutine(SystemInitializationPhase(config));
            // Phase 5: Main Experiment
            yield return StartCoroutine(MainExperimentPhase(config));
            // Phase 6: Data Collection and Cleanup
            yield return StartCoroutine(ExperimentCleanupPhase(config));
            // Complete experiment
            CompleteExperiment();
        }
        
        #endregion
        
        #region Experiment Phases
        
        private IEnumerator ExperimentPreparationPhase(ExperimentConfiguration config)
        {
            SetExperimentPhase(ExperimentPhase.Preparation);
            
            LogExperimentEvent("Experiment preparation started", ExperimentEventType.PhaseStarted);
            
            // Clear any existing data
            if (dataManager != null)
            {
                dataManager.ClearExperimentData();
            }
            
            // Initialize metrics tracking
            InitializeExperimentMetrics(config);
            
            // Validate configuration one more time
            var validationResult = ValidateExperimentConfiguration(config);
            if (!validationResult.isValid)
            {
                LogExperimentError($"Configuration validation failed: {validationResult.errorMessage}");
                yield break;
            }
            
            LogExperimentEvent("Experiment preparation completed", ExperimentEventType.PhaseCompleted);
            
            yield return new WaitForSeconds(1f); // Brief pause between phases
        }
        
        private IEnumerator WorldGenerationPhase(ExperimentConfiguration config)
        {
            SetExperimentPhase(ExperimentPhase.WorldGeneration);
            
            LogExperimentEvent("World generation started", ExperimentEventType.PhaseStarted);
            
            if (worldGenerator != null)
            {
                worldGenerator.GenerateWorld(config);
                
                // Wait for world generation to complete
                yield return new WaitForSeconds(2f);
                
                // Validate world generation
                var worldStats = worldGenerator.GetWorldStats();
                if (worldStats.locationCount == 0)
                {
                    LogExperimentError("World generation failed: no locations created");
                    yield break;
                }
                
                UpdateExperimentMetric("WorldLocations", worldStats.locationCount);
                UpdateExperimentMetric("WorldResources", worldStats.resourceCount);
                
                LogExperimentEvent($"World generated: {worldStats.locationCount} locations, {worldStats.resourceCount} resources", 
                                 ExperimentEventType.SystemEvent);
            }
            else
            {
                LogExperimentError("WorldGenerator not available");
                yield break;
            }
            
            LogExperimentEvent("World generation completed", ExperimentEventType.PhaseCompleted);
            
            yield return new WaitForSeconds(1f);
        }
        
        private IEnumerator AgentCreationPhase(ExperimentConfiguration config)
        {
            SetExperimentPhase(ExperimentPhase.AgentCreation);
            
            LogExperimentEvent("Agent creation started", ExperimentEventType.PhaseStarted);
            
            if (agentGroupManager != null)
            {
                agentGroupManager.CreateAgentGroups(config);
                
                // Wait for agent creation to complete
                yield return new WaitForSeconds(3f);
                
                // Validate agent creation
                var createdAgents = FindObjectsByType<Agent>(UnityEngine.FindObjectsSortMode.None).Length;
                if (createdAgents == 0)
                {
                    LogExperimentError("Agent creation failed: no agents created");
                    yield break;
                }
                
                if (createdAgents != config.totalAgentCount)
                {
                    LogExperimentError($"Agent count mismatch: expected {config.totalAgentCount}, created {createdAgents}");
                }
                
                UpdateExperimentMetric("TotalAgents", createdAgents);
                UpdateExperimentMetric("AgentGroups", config.agentGroups.Count);
                
                LogExperimentEvent($"Created {createdAgents} agents in {config.agentGroups.Count} groups", 
                                 ExperimentEventType.SystemEvent);
            }
            else
            {
                LogExperimentError("AgentGroupManager not available");
                yield break;
            }
            
            LogExperimentEvent("Agent creation completed", ExperimentEventType.PhaseCompleted);
            
            yield return new WaitForSeconds(1f);
        }
        
        private IEnumerator SystemInitializationPhase(ExperimentConfiguration config)
        {
            SetExperimentPhase(ExperimentPhase.SystemInitialization);
            
            LogExperimentEvent("System initialization started", ExperimentEventType.PhaseStarted);
            
            // Initialize conflict manager
            if (conflictManager != null && config.enableConflictSystem)
            {
                conflictManager.Initialize(config);
                LogExperimentEvent("Conflict system initialized", ExperimentEventType.SystemEvent);
            }
            
            // Initialize knowledge system
            if (knowledgeSystem != null)
            {
                // Register all agents with knowledge system
                var agents = FindObjectsByType<Agent>(UnityEngine.FindObjectsSortMode.None);
                foreach (var agent in agents)
                {
                    knowledgeSystem.RegisterAgent(agent);
                }
                
                LogExperimentEvent($"Knowledge system initialized for {agents.Length} agents", 
                                 ExperimentEventType.SystemEvent);
            }
            
            // Initialize data collection
            if (dataManager != null && config.enableDataLogging)
            {
                dataManager.StartExperimentDataCollection(config);
                LogExperimentEvent("Research data collection started", ExperimentEventType.SystemEvent);
            }
            
            // Start monitoring if enabled
            if (enableRealTimeMonitoring)
            {
                InvokeRepeating(nameof(MonitorExperimentSystems), monitoringInterval, monitoringInterval);
                LogExperimentEvent("Real-time monitoring started", ExperimentEventType.SystemEvent);
            }
            
            // Start auto-save if enabled
            if (autoSaveResults)
            {
                InvokeRepeating(nameof(AutoSaveExperimentData), autoSaveInterval, autoSaveInterval);
                LogExperimentEvent("Auto-save enabled", ExperimentEventType.SystemEvent);
            }
            
            LogExperimentEvent("System initialization completed", ExperimentEventType.PhaseCompleted);
            
            yield return new WaitForSeconds(2f);
        }
        
        private IEnumerator MainExperimentPhase(ExperimentConfiguration config)
        {
            SetExperimentPhase(ExperimentPhase.Running);
            
            LogExperimentEvent("Main experiment phase started", ExperimentEventType.PhaseStarted);
            
            // Fire experiment started event
            OnExperimentStarted?.Invoke(config);
            
            // Run for the configured duration
            var targetDuration = config.simulationDuration;
            var phaseStartTime = Time.time;
            
            LogExperimentEvent($"Experiment will run for {targetDuration} seconds", ExperimentEventType.SystemEvent);
            
            // Main experiment loop
            while (Time.time - phaseStartTime < targetDuration && experimentRunning)
            {
                // Check for termination conditions
                if (ShouldTerminateExperiment())
                {
                    LogExperimentEvent("Early termination conditions met", ExperimentEventType.SystemEvent);
                    break;
                }
                
                // Yield control for one frame
                yield return null;
            }
            
            LogExperimentEvent("Main experiment phase completed", ExperimentEventType.PhaseCompleted);
        }
        
        private IEnumerator ExperimentCleanupPhase(ExperimentConfiguration config)
        {
            SetExperimentPhase(ExperimentPhase.DataCollection);
            
            LogExperimentEvent("Data collection and cleanup started", ExperimentEventType.PhaseStarted);
            
            // Stop monitoring and auto-save
            CancelInvoke(nameof(MonitorExperimentSystems));
            CancelInvoke(nameof(AutoSaveExperimentData));
            
            // Final data collection
            if (dataManager != null)
            {
                dataManager.FinalizeExperimentData();
                LogExperimentEvent("Final data collection completed", ExperimentEventType.SystemEvent);
            }
            
            // Generate experiment results
            var results = GenerateExperimentResults(config);
            
            // Save results
            if (autoSaveResults)
            {
                SaveExperimentResults(results);
            }
            
            LogExperimentEvent("Cleanup and data collection completed", ExperimentEventType.PhaseCompleted);
            
            yield return new WaitForSeconds(1f);
        }
        
        #endregion
        
        #region Experiment Management
        
        public void PauseExperiment()
        {
            if (!experimentRunning) return;
            
            Time.timeScale = 0f;
            LogExperimentEvent("Experiment paused", ExperimentEventType.SystemEvent);
            OnExperimentPaused?.Invoke();
        }
        
        public void ResumeExperiment()
        {
            if (!experimentRunning) return;
            
            Time.timeScale = 1f;
            LogExperimentEvent("Experiment resumed", ExperimentEventType.SystemEvent);
            OnExperimentResumed?.Invoke();
        }
        
        public void StopExperiment()
        {
            if (!experimentRunning) return;
            
            LogExperimentEvent("Experiment stopped by user", ExperimentEventType.ExperimentStopped);
            TerminateExperiment(ExperimentTerminationReason.UserStopped);
        }
        
        private void CompleteExperiment()
        {
            if (!experimentRunning) return;
            
            LogExperimentEvent("Experiment completed successfully", ExperimentEventType.ExperimentCompleted);
            
            var results = GenerateExperimentResults(currentExperiment);
            
            // Cleanup
            CleanupExperiment();
            
            // Fire completion event
            OnExperimentCompleted?.Invoke(results);
            
            Debug.Log($"[Experiment Manager] Experiment '{currentExperiment.experimentName}' completed successfully");
        }
        
        private void TerminateExperiment(ExperimentTerminationReason reason)
        {
            LogExperimentEvent($"Experiment terminated: {reason}", ExperimentEventType.ExperimentTerminated);
            
            var results = GenerateExperimentResults(currentExperiment);
            results.terminationReason = reason;
            
            // Cleanup
            CleanupExperiment();
            
            // Fire completion event (even for terminated experiments)
            OnExperimentCompleted?.Invoke(results);
            
            Debug.LogWarning($"[Experiment Manager] Experiment terminated: {reason}");
        }
        
        private void CleanupExperiment()
        {
            experimentRunning = false;
            currentPhase = ExperimentPhase.Inactive;
            
            // Stop all coroutines
            if (experimentCoroutine != null)
            {
                StopCoroutine(experimentCoroutine);
                experimentCoroutine = null;
            }
            
            // Cancel invokes
            CancelInvoke();
            
            // Reset time scale
            Time.timeScale = 1f;
            
            // Stop data collection
            if (dataManager != null)
            {
                dataManager.StopDataCollection();
            }
        }
        
        #endregion
        
        #region Monitoring and Validation
        
        private void MonitorExperimentSystems()
        {
            if (!experimentRunning) return;
            
            // Monitor agent count
            var activeAgents = FindObjectsByType<Agent>(UnityEngine.FindObjectsSortMode.None).Length;
            UpdateExperimentMetric("ActiveAgents", activeAgents);
            
            if (activeAgents == 0)
            {
                LogExperimentError("No active agents detected");
            }
            
            // Monitor system errors
            if (systemErrors.Count >= maxErrorsBeforeTermination)
            {
                LogExperimentError($"Maximum error count reached ({maxErrorsBeforeTermination})");
                if (pauseOnError)
                {
                    TerminateExperiment(ExperimentTerminationReason.Error);
                }
            }
            
            // Monitor consciousness levels if available
            MonitorConsciousnessLevels();
            
            // Monitor conflict system
            MonitorConflictSystem();
            
            // Log periodic status
            LogExperimentEvent($"Systems monitored - {activeAgents} active agents, {systemErrors.Count} errors", 
                             ExperimentEventType.SystemEvent);
        }
        
        private void MonitorConsciousnessLevels()
        {
            var consciousnessComponents = FindObjectsByType<ConsciousnessResearchMetrics>(UnityEngine.FindObjectsSortMode.None);
            if (consciousnessComponents.Length > 0)
            {
                var averageConsciousness = consciousnessComponents.Average(c => c.GetCurrentLevel());
                UpdateExperimentMetric("AverageConsciousness", averageConsciousness);
            }
        }
        
        private void MonitorConflictSystem()
        {
            if (conflictManager != null)
            {
                var activeConflicts = conflictManager.ActiveConflicts.Count;
                UpdateExperimentMetric("ActiveConflicts", activeConflicts);
                
                if (activeConflicts > 10) // Threshold for excessive conflict
                {
                    LogExperimentEvent($"High conflict level detected: {activeConflicts} active conflicts", 
                                     ExperimentEventType.SystemEvent);
                }
            }
        }
        
        private bool ShouldTerminateExperiment()
        {
            // Check error count
            if (systemErrors.Count >= maxErrorsBeforeTermination)
            {
                return true;
            }
            
            // Check agent count
            var activeAgents = FindObjectsByType<Agent>(UnityEngine.FindObjectsSortMode.None).Length;
            if (activeAgents == 0)
            {
                LogExperimentError("All agents have been destroyed");
                return true;
            }
            
            // Check for system failures
            if (dataManager == null && currentExperiment.enableDataLogging)
            {
                LogExperimentError("Data manager lost during experiment");
                return true;
            }
            
            return false;
        }
        
        private void UpdateExperimentTimer()
        {
            currentExperimentDuration = Time.time - experimentStartTime;
        }
        
        private void CheckExperimentTerminationConditions()
        {
            if (currentExperiment != null && 
                currentExperimentDuration >= currentExperiment.simulationDuration && 
                currentPhase == ExperimentPhase.Running)
            {
                LogExperimentEvent("Experiment duration reached", ExperimentEventType.SystemEvent);
                // Natural completion will be handled by the experiment coroutine
            }
        }
        
        #endregion
        
        #region Data Management
        
        private void AutoSaveExperimentData()
        {
            if (!experimentRunning || dataManager == null) return;
            
            try
            {
                var partialResults = GenerateExperimentResults(currentExperiment);
                SaveExperimentResults(partialResults, true);
                
                LogExperimentEvent("Auto-save completed", ExperimentEventType.SystemEvent);
            }
            catch (System.Exception e)
            {
                LogExperimentError($"Auto-save failed: {e.Message}");
            }
        }
        
        private void SaveExperimentResults(ExperimentResults results, bool isPartialSave = false)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = isPartialSave ? 
                    $"{results.experimentName}_AutoSave_{timestamp}.json" :
                    $"{results.experimentName}_Final_{timestamp}.json";
                    
                var json = JsonUtility.ToJson(results, true);
                
                // Ensure directory exists
                if (!System.IO.Directory.Exists(experimentResultsPath))
                {
                    System.IO.Directory.CreateDirectory(experimentResultsPath);
                }
                
                var fullPath = System.IO.Path.Combine(experimentResultsPath, filename);
                System.IO.File.WriteAllText(fullPath, json);
                
                Debug.Log($"[Experiment Manager] Results saved to: {fullPath}");
            }
            catch (System.Exception e)
            {
                LogExperimentError($"Failed to save results: {e.Message}");
            }
        }
        
        private ExperimentResults GenerateExperimentResults(ExperimentConfiguration config)
        {
            var results = new ExperimentResults
            {
                experimentName = config.experimentName,
                researcherName = config.researcherName,
                startTime = experimentStartTimestamp,
                endTime = DateTime.UtcNow,
                duration = currentExperimentDuration,
                simulationContext = config.simulationContext,
                totalAgents = config.totalAgentCount,
                experimentPhase = currentPhase,
                terminationReason = ExperimentTerminationReason.Completed,
                experimentEvents = new List<ExperimentEvent>(experimentEvents),
                experimentMetrics = new Dictionary<string, object>(experimentMetrics),
                systemErrors = new List<string>(systemErrors)
            };
            
            // Collect data from various systems
            if (dataManager != null)
            {
                results.researchData = dataManager.GetExperimentResearchData();
            }
            
            if (agentGroupManager != null)
            {
                results.agentGroupData = agentGroupManager.GetGroupResearchData();
            }
            
            if (conflictManager != null && conflictManager.ConflictSystemEnabled)
            {
                results.conflictData = conflictManager.GetConflictResearchData();
            }
            
            if (worldGenerator != null)
            {
                results.worldStats = worldGenerator.GetWorldStats();
            }
            
            if (knowledgeSystem != null && knowledgeSystem.KnowledgeSystemEnabled)
            {
                results.knowledgeData = knowledgeSystem.GetResearchData();
            }
            
            return results;
        }
        
        #endregion
        
        #region Utility Methods
        
        private void SetExperimentPhase(ExperimentPhase phase)
        {
            if (currentPhase != phase)
            {
                currentPhase = phase;
                OnExperimentPhaseChanged?.Invoke(phase);
                
                Debug.Log($"[Experiment Manager] Phase changed to: {phase}");
            }
        }
        
        private void LogExperimentEvent(string message, ExperimentEventType eventType)
        {
            var experimentEvent = new ExperimentEvent
            {
                timestamp = DateTime.UtcNow,
                eventType = eventType,
                message = message,
                experimentPhase = currentPhase,
                experimentTime = currentExperimentDuration
            };
            
            experimentEvents.Add(experimentEvent);
            
            // Keep only recent events to manage memory
            if (experimentEvents.Count > 1000)
            {
                experimentEvents.RemoveAt(0);
            }
            
            Debug.Log($"[Experiment Manager] {eventType}: {message}");
        }
        
        private void LogExperimentError(string errorMessage)
        {
            systemErrors.Add($"{DateTime.UtcNow:HH:mm:ss}: {errorMessage}");
            LogExperimentEvent(errorMessage, ExperimentEventType.Error);
            OnExperimentError?.Invoke(errorMessage);
        }
        
        private void UpdateExperimentMetric(string metricName, object value)
        {
            experimentMetrics[metricName] = value;
        }
        
        private void InitializeExperimentMetrics(ExperimentConfiguration config)
        {
            experimentMetrics.Clear();
            experimentMetrics["ExperimentName"] = config.experimentName;
            experimentMetrics["SimulationContext"] = config.simulationContext.ToString();
            experimentMetrics["ConfiguredAgents"] = config.totalAgentCount;
            experimentMetrics["ConfiguredGroups"] = config.agentGroups.Count;
            experimentMetrics["ConflictSystemEnabled"] = config.enableConflictSystem;
            experimentMetrics["DataLoggingEnabled"] = config.enableDataLogging;
            experimentMetrics["StartTime"] = experimentStartTimestamp;
        }
        
        private (bool isValid, string errorMessage) ValidateExperimentConfiguration(ExperimentConfiguration config)
        {
            if (config.totalAgentCount <= 0)
                return (false, "Total agent count must be greater than 0");
                
            if (config.agentGroups == null || config.agentGroups.Count == 0)
                return (false, "At least one agent group must be configured");
                
            if (config.simulationDuration <= 0)
                return (false, "Simulation duration must be greater than 0");
                
            if (string.IsNullOrEmpty(config.experimentName))
                return (false, "Experiment name cannot be empty");
                
            return (true, "");
        }
        
        private void OnExperimentConfigured(ExperimentConfiguration config)
        {
            LogExperimentEvent($"Experiment configured: {config.experimentName}", ExperimentEventType.SystemEvent);
        }
        
        private void OnDestroy()
        {
            // Cleanup subscriptions
            if (configManager != null)
            {
                ExperimentConfigurationManager.OnExperimentConfigured -= OnExperimentConfigured;
            }
            
            // Stop experiment if running
            if (experimentRunning)
            {
                CleanupExperiment();
            }
        }
        
        #endregion
    }
    
    #region Data Structures
    
    // ExperimentPhase, ExperimentEventType, ExperimentTerminationReason are now defined in ExperimentalDefinitions.cs
    
    [System.Serializable]
    public class ExperimentResults
    {
        public string experimentName;
        public string researcherName;
        public DateTime startTime;
        public DateTime endTime;
        public float duration;
        public SimulationContext simulationContext;
        public int totalAgents;
        public ExperimentPhase experimentPhase;
        public ExperimentTerminationReason terminationReason;
        public List<ExperimentEvent> experimentEvents;
        public Dictionary<string, object> experimentMetrics;
        public List<string> systemErrors;
        
        // Research data from various systems
        public object researchData;
        public AgentGroupResearchData agentGroupData;
        public ConflictResearchData conflictData;
        public WorldGenerationStats worldStats;
        public KnowledgeSystemResearchData knowledgeData;
    }
    
    [System.Serializable]
    public class ExperimentEvent
    {
        public DateTime timestamp;
        public ExperimentEventType eventType;
        public string message;
        public ExperimentPhase experimentPhase;
        public float experimentTime;
    }
    
    // ExperimentEventType and ExperimentTerminationReason are also defined in ExperimentalDefinitions.cs
    
    #endregion
}