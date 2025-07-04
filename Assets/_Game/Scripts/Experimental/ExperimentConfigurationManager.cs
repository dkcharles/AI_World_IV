using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Simplified configuration manager that uses ScriptableObject configurations
    /// Single source of truth for all experimental settings
    /// Updated to use ScriptableObject-based configuration system
    /// </summary>
    public class ExperimentConfigurationManager : MonoBehaviour
    {
        [Header("Master Configuration")]
        [SerializeField] private ExperimentConfigurationSO currentExperimentConfig;
        
        [Header("Quick Configuration Overrides")]
        [SerializeField] private bool allowRuntimeOverrides = false;
        [SerializeField] private int runtimeAgentCountOverride = -1;
        [SerializeField] private float runtimeDurationOverride = -1f;
        
        [Header("UI References")]
        [SerializeField] private Button startExperimentButton;
        [SerializeField] private Button saveConfigurationButton;
        [SerializeField] private Button loadConfigurationButton;
        [SerializeField] private Dropdown contextDropdown;
        [SerializeField] private Slider agentCountSlider;
        [SerializeField] private Text configurationStatusText;
        [SerializeField] private Text validationStatusText;
        
        // Runtime references
        private ExperimentManager experimentManager;
        
        // Events
        public static event Action<ExperimentConfiguration> OnExperimentConfigured;
        public static event Action OnExperimentStarted;
        public static event Action OnExperimentCompleted;
        
        public ExperimentConfigurationSO CurrentConfiguration => currentExperimentConfig;
        public bool HasValidConfiguration => currentExperimentConfig != null && currentExperimentConfig.IsValid();
        
        private void Awake()
        {
            InitializeComponents();
            SetupUI();
        }
        
        private void Start()
        {
            ValidateConfiguration();
            UpdateConfigurationDisplay();
            
            if (currentExperimentConfig != null)
            {
                Debug.Log($"[Experiment Config] Loaded configuration: {currentExperimentConfig.experimentName}");
            }
            else
            {
                Debug.LogWarning("[Experiment Config] No experiment configuration assigned! " +
                               "Create an ExperimentConfigurationSO asset and assign it in the inspector.");
            }
        }
        
        #region Initialization
        
        private void InitializeComponents()
        {
            experimentManager = FindFirstObjectByType<ExperimentManager>();
            if (experimentManager == null)
            {
                Debug.LogWarning("[Experiment Config] ExperimentManager not found. Creating one...");
                var managerGO = new GameObject("ExperimentManager");
                experimentManager = managerGO.AddComponent<ExperimentManager>();
            }
        }
        
        private void SetupUI()
        {
            if (startExperimentButton != null)
                startExperimentButton.onClick.AddListener(StartExperiment);
                
            if (saveConfigurationButton != null)
                saveConfigurationButton.onClick.AddListener(SaveConfiguration);
                
            if (loadConfigurationButton != null)
                loadConfigurationButton.onClick.AddListener(LoadConfiguration);
                
            if (contextDropdown != null)
            {
                SetupContextDropdown();
            }
            
            if (agentCountSlider != null)
            {
                SetupAgentCountSlider();
            }
        }
        
        private void SetupContextDropdown()
        {
            contextDropdown.ClearOptions();
            var contextNames = System.Enum.GetNames(typeof(SimulationContext));
            contextDropdown.AddOptions(new System.Collections.Generic.List<string>(contextNames));
            
            if (currentExperimentConfig?.worldConfiguration != null)
            {
                contextDropdown.value = (int)currentExperimentConfig.worldConfiguration.simulationContext;
            }
            
            contextDropdown.onValueChanged.AddListener(OnContextChanged);
        }
        
        private void SetupAgentCountSlider()
        {
            if (currentExperimentConfig != null)
            {
                agentCountSlider.value = currentExperimentConfig.TotalAgentCount;
                agentCountSlider.maxValue = Mathf.Max(20, currentExperimentConfig.TotalAgentCount * 2);
            }
            else
            {
                agentCountSlider.value = 5;
                agentCountSlider.maxValue = 20;
            }
            
            agentCountSlider.onValueChanged.AddListener(OnAgentCountChanged);
        }
        
        #endregion
        
        #region UI Event Handlers
        
        private void OnContextChanged(int contextIndex)
        {
            if (!allowRuntimeOverrides || currentExperimentConfig?.worldConfiguration == null) return;
            
            var newContext = (SimulationContext)contextIndex;
            
            // Update the ScriptableObject
            currentExperimentConfig.worldConfiguration.simulationContext = newContext;
            
            // Apply context defaults
            currentExperimentConfig.worldConfiguration.ApplyContextDefaults();
            
            // Update conflict configuration if present
            if (currentExperimentConfig.conflictConfiguration != null)
            {
                currentExperimentConfig.conflictConfiguration.ApplyContextDefaults(newContext);
            }
            
            // Update mood configuration if present
            if (currentExperimentConfig.moodConfiguration != null)
            {
                currentExperimentConfig.moodConfiguration.ApplyContextDefaults(newContext);
            }
            
            UpdateConfigurationDisplay();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(currentExperimentConfig);
            if (currentExperimentConfig.worldConfiguration != null)
                UnityEditor.EditorUtility.SetDirty(currentExperimentConfig.worldConfiguration);
            if (currentExperimentConfig.conflictConfiguration != null)
                UnityEditor.EditorUtility.SetDirty(currentExperimentConfig.conflictConfiguration);
            if (currentExperimentConfig.moodConfiguration != null)
                UnityEditor.EditorUtility.SetDirty(currentExperimentConfig.moodConfiguration);
            #endif
            
            Debug.Log($"[Experiment Config] Context changed to: {newContext}");
        }
        
        private void OnAgentCountChanged(float count)
        {
            if (!allowRuntimeOverrides) return;
            
            runtimeAgentCountOverride = Mathf.RoundToInt(count);
            UpdateConfigurationDisplay();
        }
        
        #endregion
        
        #region Experiment Management
        
        public void StartExperiment()
        {
            if (!ValidateConfiguration())
            {
                Debug.LogError("[Experiment Config] Configuration validation failed. Cannot start experiment.");
                ShowValidationError();
                return;
            }
            
            // Convert ScriptableObject to legacy ExperimentConfiguration for compatibility
            var legacyConfig = currentExperimentConfig.ToExperimentConfiguration();
            
            // Apply runtime overrides if enabled
            if (allowRuntimeOverrides)
            {
                if (runtimeAgentCountOverride > 0)
                {
                    legacyConfig.totalAgentCount = runtimeAgentCountOverride;
                    AdjustAgentGroupsForNewTotal(legacyConfig, runtimeAgentCountOverride);
                }
                
                if (runtimeDurationOverride > 0)
                {
                    legacyConfig.simulationDuration = runtimeDurationOverride;
                }
            }
            
            Debug.Log($"[Experiment Config] Starting experiment: {currentExperimentConfig.experimentName}");
            Debug.Log($"[Experiment Config] Master Context: {currentExperimentConfig.SimulationContext}");
            Debug.Log($"[Experiment Config] Master Agent Groups: {currentExperimentConfig.agentGroups?.Length ?? 0}");
            
            // Start the experiment with the legacy configuration
            experimentManager.StartExperiment(legacyConfig);
            
            // Fire events with the legacy configuration
            OnExperimentConfigured?.Invoke(legacyConfig);
            OnExperimentStarted?.Invoke();
            
            Debug.Log($"[Experiment Config] Experiment started successfully!");
        }
        
        private void AdjustAgentGroupsForNewTotal(ExperimentConfiguration config, int newTotal)
        {
            if (config.agentGroups == null || config.agentGroups.Count == 0) return;
            
            // Redistribute agents proportionally
            var currentTotal = config.agentGroups.Sum(g => g.agentCount);
            
            foreach (var group in config.agentGroups)
            {
                var proportion = (float)group.agentCount / currentTotal;
                group.agentCount = Mathf.Max(1, Mathf.RoundToInt(newTotal * proportion));
            }
            
            // Adjust for rounding differences
            var actualTotal = config.agentGroups.Sum(g => g.agentCount);
            var difference = newTotal - actualTotal;
            
            if (difference != 0 && config.agentGroups.Count > 0)
            {
                config.agentGroups[0].agentCount += difference;
            }
        }
        
        public void StopExperiment()
        {
            if (experimentManager != null)
            {
                experimentManager.StopExperiment();
            }
        }
        
        #endregion
        
        #region Configuration Management
        
        private bool ValidateConfiguration()
        {
            if (currentExperimentConfig == null)
            {
                Debug.LogError("[Experiment Config] No experiment configuration assigned!");
                return false;
            }
            
            if (!currentExperimentConfig.IsValid())
            {
                var validationMessage = currentExperimentConfig.GetValidationMessage();
                Debug.LogError($"[Experiment Config] Configuration invalid: {validationMessage}");
                return false;
            }
            
            Debug.Log("[Experiment Config] Configuration validation passed");
            return true;
        }
        
        private void ShowValidationError()
        {
            if (validationStatusText != null)
            {
                if (currentExperimentConfig == null)
                {
                    validationStatusText.text = "ERROR: No configuration assigned";
                }
                else
                {
                    validationStatusText.text = $"ERROR: {currentExperimentConfig.GetValidationMessage()}";
                }
                validationStatusText.color = Color.red;
            }
        }
        
        public void SaveConfiguration()
        {
            if (currentExperimentConfig != null)
            {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(currentExperimentConfig);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"[Experiment Config] Configuration saved: {currentExperimentConfig.name}");
                #else
                Debug.Log("[Experiment Config] Save configuration only available in editor");
                #endif
            }
            else
            {
                Debug.LogWarning("[Experiment Config] No configuration to save");
            }
        }
        
        public void LoadConfiguration()
        {
            #if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel("Load Experiment Configuration", 
                                                              "Assets", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                // Convert absolute path to relative asset path
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
                
                var loadedConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<ExperimentConfigurationSO>(path);
                if (loadedConfig != null)
                {
                    SetExperimentConfiguration(loadedConfig);
                    Debug.Log($"[Experiment Config] Configuration loaded: {loadedConfig.experimentName}");
                }
                else
                {
                    Debug.LogError("[Experiment Config] Failed to load configuration from: " + path);
                }
            }
            #else
            Debug.Log("[Experiment Config] Load configuration only available in editor");
            #endif
        }
        
        /// <summary>
        /// Change the current experiment configuration
        /// </summary>
        public void SetExperimentConfiguration(ExperimentConfigurationSO newConfig)
        {
            currentExperimentConfig = newConfig;
            
            // Update UI to reflect new configuration
            if (contextDropdown != null && newConfig?.worldConfiguration != null)
            {
                contextDropdown.value = (int)newConfig.worldConfiguration.simulationContext;
            }
            
            if (agentCountSlider != null && newConfig != null)
            {
                agentCountSlider.value = newConfig.TotalAgentCount;
            }
            
            ValidateConfiguration();
            UpdateConfigurationDisplay();
            
            // Fire event with legacy configuration for compatibility
            if (newConfig != null)
            {
                var legacyConfig = newConfig.ToExperimentConfiguration();
                OnExperimentConfigured?.Invoke(legacyConfig);
            }
            
            Debug.Log($"[Experiment Config] Configuration changed to: {newConfig?.experimentName ?? "None"}");
        }
        
        #endregion
        
        #region Display Updates
        
        private void UpdateConfigurationDisplay()
        {
            UpdateStatusDisplay();
            UpdateValidationDisplay();
        }
        
        private void UpdateStatusDisplay()
        {
            if (configurationStatusText == null) return;
            
            if (currentExperimentConfig == null)
            {
                configurationStatusText.text = "No Configuration Assigned";
                return;
            }
            
            var totalAgents = allowRuntimeOverrides && runtimeAgentCountOverride > 0 ? 
                              runtimeAgentCountOverride : currentExperimentConfig.TotalAgentCount;
            
            var duration = allowRuntimeOverrides && runtimeDurationOverride > 0 ? 
                          runtimeDurationOverride : currentExperimentConfig.simulationDuration;
            
            var status = $"Experiment: {currentExperimentConfig.experimentName}\\n" +
                        $"Researcher: {currentExperimentConfig.researcherName}\\n" +
                        $"Context: {currentExperimentConfig.SimulationContext}\\n" +
                        $"Total Agents: {totalAgents}\\n" +
                        $"Agent Groups: {currentExperimentConfig.agentGroups?.Length ?? 0}\\n" +
                        $"Duration: {duration:F0} seconds\\n" +
                        $"World Size: {currentExperimentConfig.worldConfiguration?.worldSize ?? Vector2.zero}\\n" +
                        $"Locations: {currentExperimentConfig.worldConfiguration?.predefinedLocations?.Length ?? 0}\\n" +
                        $"Resources: {currentExperimentConfig.worldConfiguration?.predefinedResources?.Length ?? 0}\\n" +
                        $"Conflict Enabled: {currentExperimentConfig.conflictConfiguration?.enableConflictSystem ?? false}\\n" +
                        $"Data Logging: {currentExperimentConfig.researchConfiguration?.enableDataLogging ?? false}";
                        
            configurationStatusText.text = status;
        }
        
        private void UpdateValidationDisplay()
        {
            if (validationStatusText == null) return;
            
            if (currentExperimentConfig == null)
            {
                validationStatusText.text = "No configuration assigned";
                validationStatusText.color = Color.red;
            }
            else if (currentExperimentConfig.IsValid())
            {
                validationStatusText.text = "Configuration Valid";
                validationStatusText.color = Color.green;
            }
            else
            {
                validationStatusText.text = currentExperimentConfig.GetValidationMessage();
                validationStatusText.color = Color.red;
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Create a new experiment configuration asset
        /// </summary>
        [ContextMenu("Create New Configuration")]
        public void CreateNewConfiguration()
        {
            #if UNITY_EDITOR
            var newConfig = ScriptableObject.CreateInstance<ExperimentConfigurationSO>();
            
            var path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Create New Experiment Configuration",
                "NewExperimentConfig",
                "asset",
                "Create a new experiment configuration");
                
            if (!string.IsNullOrEmpty(path))
            {
                UnityEditor.AssetDatabase.CreateAsset(newConfig, path);
                UnityEditor.AssetDatabase.SaveAssets();
                
                SetExperimentConfiguration(newConfig);
                
                Debug.Log($"[Experiment Config] Created new configuration: {path}");
            }
            #endif
        }
        
        /// <summary>
        /// Duplicate current configuration
        /// </summary>
        [ContextMenu("Duplicate Configuration")]
        public void DuplicateConfiguration()
        {
            #if UNITY_EDITOR
            if (currentExperimentConfig == null)
            {
                Debug.LogWarning("[Experiment Config] No configuration to duplicate");
                return;
            }
            
            var duplicate = UnityEngine.Object.Instantiate(currentExperimentConfig);
            duplicate.experimentName += " (Copy)";
            
            var path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Duplicate Experiment Configuration",
                currentExperimentConfig.name + "_Copy",
                "asset",
                "Save duplicated configuration");
                
            if (!string.IsNullOrEmpty(path))
            {
                UnityEditor.AssetDatabase.CreateAsset(duplicate, path);
                UnityEditor.AssetDatabase.SaveAssets();
                
                Debug.Log($"[Experiment Config] Duplicated configuration: {path}");
            }
            #endif
        }
        
        /// <summary>
        /// Get current effective agent count (including overrides)
        /// </summary>
        public int GetEffectiveAgentCount()
        {
            if (allowRuntimeOverrides && runtimeAgentCountOverride > 0)
                return runtimeAgentCountOverride;
                
            return currentExperimentConfig?.TotalAgentCount ?? 0;
        }
        
        /// <summary>
        /// Get current effective duration (including overrides)
        /// </summary>
        public float GetEffectiveDuration()
        {
            if (allowRuntimeOverrides && runtimeDurationOverride > 0)
                return runtimeDurationOverride;
                
            return currentExperimentConfig?.simulationDuration ?? 0f;
        }
        
        private void OnValidate()
        {
            // Update display when values change in inspector
            if (Application.isPlaying)
            {
                UpdateConfigurationDisplay();
            }
        }
        
        #endregion
    }
}