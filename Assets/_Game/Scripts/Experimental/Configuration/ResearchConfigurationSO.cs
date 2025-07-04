using UnityEngine;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Research data collection configuration ScriptableObject
    /// Defines what data to collect and how to collect it
    /// </summary>
    [CreateAssetMenu(fileName = "New Research Configuration", menuName = "AI World/Research Configuration")]
    public class ResearchConfigurationSO : ScriptableObject
    {
        [Header("Data Collection")]
        public bool enableDataLogging = true;
        public float dataLoggingInterval = 30f; // seconds
        public bool enableConsciousnessTracking = true;
        public bool enableSocialNetworkTracking = true;
        public bool enableDetailedLogging = false; // More verbose logging
        
        [Header("Research Metrics")]
        public bool trackMoodChanges = true;
        public bool trackConflictEvents = true;
        public bool trackResourceUsage = true;
        public bool trackLocationUsage = true;
        public bool trackConversationContent = true;
        public bool trackPersonalityChanges = false; // Usually static
        public bool trackGoalProgression = true;
        public bool trackSocialRelationships = true;
        
        [Header("Data Export")]
        public bool autoExportData = true;
        public float autoExportInterval = 300f; // 5 minutes
        public string exportPath = "ExperimentData";
        public DataExportFormat exportFormat = DataExportFormat.JSON;
        public bool includeTimestamps = true;
        public bool includeMetadata = true;
        
        [Header("Analysis Settings")]
        public bool enableRealTimeAnalysis = true;
        public bool generateInsights = true;
        public bool enableStatisticalValidation = true;
        public float analysisInterval = 60f; // 1 minute
        public int minimumDataPointsForAnalysis = 10;
        
        [Header("Privacy and Ethics")]
        public bool anonymizeAgentData = false;
        public bool enableDataEncryption = false;
        public bool requireExplicitConsent = false; // For human participants
        public string dataRetentionPolicy = "Retain for research duration only";
        
        [Header("Performance Settings")]
        public int maxDataPointsInMemory = 10000;
        public bool compressStoredData = true;
        public bool enableDataCaching = true;
        public float cacheCleanupInterval = 3600f; // 1 hour
        
        [Header("Research Quality Assurance")]
        public bool enableDataValidation = true;
        public bool detectOutliers = true;
        public float outlierThreshold = 3f; // Standard deviations
        public bool enableDuplicateDetection = true;
        public bool enableConsistencyChecks = true;
        
        public bool IsValid()
        {
            return dataLoggingInterval > 0 && 
                   autoExportInterval > 0 && 
                   analysisInterval > 0 &&
                   maxDataPointsInMemory > 0;
        }
        
        public string GetValidationMessage()
        {
            if (dataLoggingInterval <= 0) return "Data logging interval must be positive";
            if (autoExportInterval <= 0) return "Auto export interval must be positive";
            if (analysisInterval <= 0) return "Analysis interval must be positive";
            if (maxDataPointsInMemory <= 0) return "Max data points in memory must be positive";
            if (string.IsNullOrEmpty(exportPath)) return "Export path cannot be empty";
            return "Research configuration is valid";
        }
        
        /// <summary>
        /// Get data collection settings as flags
        /// </summary>
        public DataCollectionFlags GetDataCollectionFlags()
        {
            var flags = DataCollectionFlags.None;
            
            if (trackMoodChanges) flags |= DataCollectionFlags.MoodChanges;
            if (trackConflictEvents) flags |= DataCollectionFlags.ConflictEvents;
            if (trackResourceUsage) flags |= DataCollectionFlags.ResourceUsage;
            if (trackLocationUsage) flags |= DataCollectionFlags.LocationUsage;
            if (trackConversationContent) flags |= DataCollectionFlags.ConversationContent;
            if (trackPersonalityChanges) flags |= DataCollectionFlags.PersonalityChanges;
            if (trackGoalProgression) flags |= DataCollectionFlags.GoalProgression;
            if (trackSocialRelationships) flags |= DataCollectionFlags.SocialRelationships;
            if (enableConsciousnessTracking) flags |= DataCollectionFlags.ConsciousnessMetrics;
            if (enableSocialNetworkTracking) flags |= DataCollectionFlags.SocialNetworkAnalysis;
            
            return flags;
        }
        
        /// <summary>
        /// Generate file name for data export
        /// </summary>
        public string GenerateExportFileName(string experimentName, string suffix = "")
        {
            var timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{experimentName}_{suffix}_{timestamp}";
            
            var extension = exportFormat switch
            {
                DataExportFormat.JSON => ".json",
                DataExportFormat.CSV => ".csv",
                DataExportFormat.XML => ".xml",
                _ => ".json"
            };
            
            return fileName + extension;
        }
        
        /// <summary>
        /// Get full export path with filename
        /// </summary>
        public string GetFullExportPath(string experimentName, string suffix = "")
        {
            var fileName = GenerateExportFileName(experimentName, suffix);
            return System.IO.Path.Combine(exportPath, fileName);
        }
        
        /// <summary>
        /// Apply research quality defaults
        /// </summary>
        [ContextMenu("Apply Research Quality Defaults")]
        public void ApplyResearchQualityDefaults()
        {
            // High-quality research settings
            enableDataLogging = true;
            dataLoggingInterval = 30f;
            enableConsciousnessTracking = true;
            enableSocialNetworkTracking = true;
            
            // Track everything for comprehensive research
            trackMoodChanges = true;
            trackConflictEvents = true;
            trackResourceUsage = true;
            trackLocationUsage = true;
            trackConversationContent = true;
            trackGoalProgression = true;
            trackSocialRelationships = true;
            
            // Rigorous analysis
            enableRealTimeAnalysis = true;
            generateInsights = true;
            enableStatisticalValidation = true;
            enableDataValidation = true;
            detectOutliers = true;
            enableConsistencyChecks = true;
            
            // Quality assurance
            autoExportData = true;
            autoExportInterval = 300f; // 5 minutes
            includeTimestamps = true;
            includeMetadata = true;
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// Apply performance-optimized defaults
        /// </summary>
        [ContextMenu("Apply Performance Defaults")]
        public void ApplyPerformanceDefaults()
        {
            // Optimized for performance over detail
            enableDataLogging = true;
            dataLoggingInterval = 60f; // Less frequent
            enableDetailedLogging = false;
            
            // Essential tracking only
            trackMoodChanges = true;
            trackConflictEvents = true;
            trackResourceUsage = false;
            trackLocationUsage = false;
            trackConversationContent = false;
            trackGoalProgression = true;
            trackSocialRelationships = true;
            
            // Lighter analysis
            enableRealTimeAnalysis = false;
            generateInsights = false;
            enableStatisticalValidation = false;
            
            // Performance optimizations
            maxDataPointsInMemory = 5000;
            compressStoredData = true;
            enableDataCaching = false;
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
    }
    
    // DataExportFormat and DataCollectionFlags are now defined in ExperimentalDefinitions.cs
}