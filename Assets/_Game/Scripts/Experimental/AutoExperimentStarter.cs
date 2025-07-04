using UnityEngine;
using AIWorld.Experimental;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Automatically starts the experiment after a short delay
    /// Useful for testing and debugging
    /// </summary>
    public class AutoExperimentStarter : MonoBehaviour
    {
        [Header("Auto Start Configuration")]
        [SerializeField] private bool autoStartExperiment = true;
        [SerializeField] private float startDelay = 3f;
        [SerializeField] private bool showStartupLogs = true;
        
        [Header("Debugging")]
        [SerializeField] private bool debugConfigurationStatus = true;
        
        private ExperimentConfigurationManager configManager;
        private ExperimentManager experimentManager;
        
        private void Start()
        {
            // Get required components
            configManager = GetComponent<ExperimentConfigurationManager>();
            experimentManager = GetComponent<ExperimentManager>();
            
            if (showStartupLogs)
            {
                Debug.Log("🚀 [Auto Experiment Starter] Initializing...");
            }
            
            if (autoStartExperiment)
            {
                // Wait for systems to initialize before starting
                Invoke(nameof(TryStartExperiment), startDelay);
            }
            
            if (debugConfigurationStatus)
            {
                Invoke(nameof(DebugConfigurationStatus), 1f);
            }
        }
        
        private void DebugConfigurationStatus()
        {
            if (configManager == null)
            {
                Debug.LogError("❌ [Auto Starter] ExperimentConfigurationManager not found!");
                return;
            }
            
            Debug.Log($"🔍 [Auto Starter] Configuration Status:");
            Debug.Log($"   Has Valid Config: {configManager.HasValidConfiguration}");
            Debug.Log($"   Config Name: {configManager.CurrentConfiguration?.experimentName ?? "None"}");
            Debug.Log($"   Total Agent Count: {configManager.CurrentConfiguration?.TotalAgentCount ?? 0}");
            Debug.Log($"   Agent Groups: {configManager.CurrentConfiguration?.agentGroups?.Length ?? 0}");
            Debug.Log($"   Context: {configManager.CurrentConfiguration?.SimulationContext ?? SimulationContext.Generic}");
            
            if (!configManager.HasValidConfiguration)
            {
                Debug.LogWarning("⚠️ [Auto Starter] Configuration invalid! Please assign a valid ExperimentConfigurationSO in the inspector.");
            }
        }
        
        private void TryStartExperiment()
        {
            if (configManager == null)
            {
                Debug.LogError("❌ [Auto Starter] ExperimentConfigurationManager not found on this GameObject!");
                return;
            }
            
            if (experimentManager == null)
            {
                Debug.LogError("❌ [Auto Starter] ExperimentManager not found on this GameObject!");
                return;
            }
            
            if (experimentManager.IsExperimentRunning)
            {
                Debug.LogWarning("⚠️ [Auto Starter] Experiment is already running!");
                return;
            }
            
            if (!configManager.HasValidConfiguration)
            {
                Debug.LogError("❌ [Auto Starter] No valid configuration found! Cannot start experiment.");
                Debug.LogError("   Please assign an ExperimentConfigurationSO to the Experiment_Controller's ExperimentConfigurationManager component.");
                return;
            }
            
            try
            {
                Debug.Log("🚀 [Auto Starter] Starting experiment automatically...");
                configManager.StartExperiment();
                Debug.Log("✅ [Auto Starter] Experiment started successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ [Auto Starter] Failed to start experiment: {e.Message}");
            }
        }
        
        /// <summary>
        /// Manually trigger experiment start (can be called from UI button)
        /// </summary>
        [ContextMenu("Start Experiment Now")]
        public void StartExperimentNow()
        {
            CancelInvoke(nameof(TryStartExperiment));
            TryStartExperiment();
        }
        
        /// <summary>
        /// Stop the current experiment
        /// </summary>
        [ContextMenu("Stop Experiment")]
        public void StopExperiment()
        {
            if (experimentManager != null && experimentManager.IsExperimentRunning)
            {
                experimentManager.StopExperiment();
                Debug.Log("🛑 [Auto Starter] Experiment stopped by user.");
            }
            else
            {
                Debug.LogWarning("⚠️ [Auto Starter] No experiment running to stop.");
            }
        }
        
        private void OnValidate()
        {
            // Ensure delay is reasonable
            if (startDelay < 0.5f)
                startDelay = 0.5f;
            if (startDelay > 10f)
                startDelay = 10f;
        }
    }
}
