using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Research;
using AIWorld.Experimental;
using AIWorld.Agents;
using AIWorld.Data;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Centralized research data collection and management system
    /// Coordinates data gathering from all experimental systems for Prof Darryl Charles's research platform
    /// Provides publication-quality data analysis and export capabilities
    /// </summary>
    public class ResearchDataManager : MonoBehaviour
    {
        [Header("Data Collection Configuration")]
        [SerializeField] private bool enableDataCollection = true;
        [SerializeField] private float dataCollectionInterval = 30f; // 30 seconds
        [SerializeField] private bool enableRealTimeAnalysis = true;
        [SerializeField] private int maxDataPoints = 10000;
        
        [Header("Research Quality Assurance")]
        [SerializeField] private bool validateDataQuality = true;
        [SerializeField] private float minimumDataQualityThreshold = 0.7f;
        [SerializeField] private bool enableOutlierDetection = true;
        [SerializeField] private float outlierThreshold = 2.5f; // Standard deviations
        
        [Header("Export Configuration")]
        [SerializeField] private string dataExportPath = "ResearchData";
        [SerializeField] private bool autoExportEnabled = false;
        [SerializeField] private float autoExportInterval = 300f; // 5 minutes
        [SerializeField] private DataExportFormat exportFormat = DataExportFormat.JSON;
        
        [Header("Research Analytics")]
        [SerializeField] private bool enableStatisticalAnalysis = true;
        [SerializeField] private bool enableCorrelationAnalysis = true;
        [SerializeField] private bool enableTrendAnalysis = true;
        [SerializeField] private int analysisWindowSize = 100; // Data points for analysis
        
        // Data collection state
        private bool isCollecting = false;
        private DateTime collectionStartTime;
        private ExperimentConfiguration currentExperiment;
        
        // Data storage
        private List<ConsciousnessDataPoint> consciousnessData = new List<ConsciousnessDataPoint>();
        private List<SocialInteractionDataPoint> socialInteractionData = new List<SocialInteractionDataPoint>();
        private List<ConflictDataPoint> conflictData = new List<ConflictDataPoint>();
        private List<LocationUsageDataPoint> locationUsageData = new List<LocationUsageDataPoint>();
        private List<ResourceCollectionDataPoint> resourceCollectionData = new List<ResourceCollectionDataPoint>();
        private List<MoodDataPoint> moodData = new List<MoodDataPoint>();
        private List<KnowledgeDataPoint> knowledgeData = new List<KnowledgeDataPoint>();
        
        // Research analytics
        private ResearchAnalytics analyticsEngine;
        private Dictionary<string, object> currentAnalysisResults = new Dictionary<string, object>();
        private List<ResearchInsight> generatedInsights = new List<ResearchInsight>();
        
        // System references
        private ExperimentManager experimentManager;
        private List<Agent> trackedAgents = new List<Agent>();
        
        // Events
        public static event Action<ResearchDataSnapshot> OnDataCollected;
        public static event Action<ResearchAnalysisResults> OnAnalysisCompleted;
        public static event Action<string> OnDataExported;
        public static event Action<ResearchInsight> OnInsightGenerated;
        
        public bool IsCollectingData => isCollecting;
        public int TotalDataPoints => GetTotalDataPointCount();
        public DateTime CollectionStartTime => collectionStartTime;
        public ResearchAnalytics Analytics => analyticsEngine;
        
        private void Awake()
        {
            InitializeResearchDataManager();
        }
        
        private void Start()
        {
            if (analyticsEngine == null)
            {
                analyticsEngine = gameObject.AddComponent<ResearchAnalytics>();
                analyticsEngine.Initialize(this);
            }
        }
        
        #region Initialization
        
        private void InitializeResearchDataManager()
        {
            experimentManager = FindFirstObjectByType<ExperimentManager>();
            
            // Subscribe to system events
            if (experimentManager != null)
            {
                ExperimentManager.OnExperimentStarted += OnExperimentStarted;
                ExperimentManager.OnExperimentCompleted += OnExperimentCompleted;
            }
            
            // Subscribe to various data source events
            SubscribeToDataSources();
            
            // Ensure export directory exists
            if (!System.IO.Directory.Exists(dataExportPath))
            {
                System.IO.Directory.CreateDirectory(dataExportPath);
            }
            
            Debug.Log("[Research Data Manager] Initialized with data collection ready");
        }
        
        private void SubscribeToDataSources()
        {
            // Subscribe to consciousness data
            ConsciousnessResearchMetrics.OnConsciousnessDataGenerated += OnConsciousnessDataReceived;
            
            // Subscribe to mood data
            AgentMoodSystem.OnMoodChanged += OnMoodDataReceived;
            AgentMoodSystem.OnMoodEvent += OnMoodEventReceived;
            
            // Subscribe to conflict data
            ConflictManager.OnConflictStarted += OnConflictEventReceived;
            ConflictManager.OnConflictEscalated += OnConflictEventReceived;
            ConflictManager.OnConflictResolved += OnConflictEventReceived;
            
            // Subscribe to location usage data
            WorldLocationComponent.OnAgentEntered += OnLocationUsageReceived;
            WorldLocationComponent.OnAgentExited += OnLocationUsageReceived;
            
            // Subscribe to resource collection data
            WorldResourceComponent.OnResourceCollected += OnResourceCollectionReceived;
            
            // Subscribe to knowledge data
            WorldKnowledgeSystem.OnKnowledgeAcquired += OnKnowledgeDataReceived;
            WorldKnowledgeSystem.OnKnowledgeShared += OnKnowledgeSharedReceived;
        }
        
        #endregion
        
        #region Data Collection Control
        
        public void StartExperimentDataCollection(ExperimentConfiguration config)
        {
            if (isCollecting)
            {
                Debug.LogWarning("[Research Data Manager] Data collection already running");
                return;
            }
            
            currentExperiment = config;
            isCollecting = true;
            collectionStartTime = DateTime.UtcNow;
            
            // Clear previous data
            ClearExperimentData();
            
            // Refresh tracked agents
            RefreshTrackedAgents();
            
            // Start periodic data collection
            if (enableDataCollection)
            {
                InvokeRepeating(nameof(CollectPeriodicData), dataCollectionInterval, dataCollectionInterval);
            }
            
            // Start auto-export if enabled
            if (autoExportEnabled)
            {
                InvokeRepeating(nameof(AutoExportData), autoExportInterval, autoExportInterval);
            }
            
            // Start real-time analysis if enabled
            if (enableRealTimeAnalysis && analyticsEngine != null)
            {
                analyticsEngine.StartRealTimeAnalysis();
            }
            
            Debug.Log($"[Research Data Manager] Started data collection for experiment: {config.experimentName}");
        }
        
        public void StopDataCollection()
        {
            if (!isCollecting) return;
            
            isCollecting = false;
            
            // Stop periodic collection
            CancelInvoke(nameof(CollectPeriodicData));
            CancelInvoke(nameof(AutoExportData));
            
            // Stop real-time analysis
            if (analyticsEngine != null)
            {
                analyticsEngine.StopRealTimeAnalysis();
            }
            
            Debug.Log("[Research Data Manager] Data collection stopped");
        }
        
        public void FinalizeExperimentData()
        {
            if (!isCollecting) return;
            
            // Collect final data snapshot
            CollectPeriodicData();
            
            // Run final analysis
            if (analyticsEngine != null && enableStatisticalAnalysis)
            {
                var finalResults = analyticsEngine.RunFinalAnalysis();
                currentAnalysisResults["FinalAnalysis"] = finalResults;
                OnAnalysisCompleted?.Invoke(finalResults);
            }
            
            // Generate research insights
            GenerateResearchInsights();
            
            Debug.Log($"[Research Data Manager] Finalized data collection with {TotalDataPoints} total data points");
        }
        
        #endregion
        
        #region Data Collection Methods
        
        private void CollectPeriodicData()
        {
            if (!isCollecting) return;
            
            var snapshot = new ResearchDataSnapshot
            {
                timestamp = DateTime.UtcNow,
                experimentTime = (DateTime.UtcNow - collectionStartTime).TotalSeconds,
                totalDataPoints = TotalDataPoints
            };
            
            // Collect consciousness data
            CollectConsciousnessSnapshot(snapshot);
            
            // Collect social interaction data
            CollectSocialInteractionSnapshot(snapshot);
            
            // Collect location usage data
            CollectLocationUsageSnapshot(snapshot);
            
            // Collect resource usage data
            CollectResourceUsageSnapshot(snapshot);
            
            // Collect mood state data
            CollectMoodStateSnapshot(snapshot);
            
            // Collect knowledge state data
            CollectKnowledgeStateSnapshot(snapshot);
            
            // Validate data quality
            if (validateDataQuality)
            {
                ValidateDataSnapshot(snapshot);
            }
            
            // Fire data collected event
            OnDataCollected?.Invoke(snapshot);
            
            // Trigger analysis if enough data collected
            if (enableRealTimeAnalysis && TotalDataPoints % analysisWindowSize == 0)
            {
                TriggerPeriodicAnalysis();
            }
        }
        
        private void CollectConsciousnessSnapshot(ResearchDataSnapshot snapshot)
        {
            var consciousnessComponents = FindObjectsByType<ConsciousnessResearchMetrics>(UnityEngine.FindObjectsSortMode.None);
            snapshot.consciousnessSnapshot = new List<AgentConsciousnessState>();
            
            foreach (var component in consciousnessComponents)
            {
                var agent = component.GetComponent<Agent>();
                var identity = agent?.GetComponent<ResearchValidatedIdentity>();
                
                if (identity != null)
                {
                    var state = new AgentConsciousnessState
                    {
                        agentId = identity.UniqueResearchID,
                        consciousnessLevel = component.GetCurrentLevel(),
                        selfAwareness = component.selfAwareness.normalizedScore,
                        metaCognition = component.metaCognition.normalizedScore,
                        theoryOfMind = component.theoryOfMind.normalizedScore,
                        creativeExpression = component.creativeExpression.normalizedScore,
                        temporalIntegration = component.temporalIntegration.normalizedScore,
                        timestamp = DateTime.UtcNow
                    };
                    
                    snapshot.consciousnessSnapshot.Add(state);
                }
            }
        }
        
        private void CollectSocialInteractionSnapshot(ResearchDataSnapshot snapshot)
        {
            // Collect current social interaction states
            snapshot.socialSnapshot = new List<AgentSocialState>();
            
            foreach (var agent in trackedAgents)
            {
                var identity = agent.GetComponent<ResearchValidatedIdentity>();
                if (identity == null) continue;
                
                var socialState = new AgentSocialState
                {
                    agentId = identity.UniqueResearchID,
                    activeConversations = GetAgentActiveConversations(agent),
                    socialConnections = GetAgentSocialConnections(agent),
                    socialMoodInfluence = GetAgentSocialMoodInfluence(agent),
                    recentInteractionCount = GetRecentInteractionCount(agent),
                    timestamp = DateTime.UtcNow
                };
                
                snapshot.socialSnapshot.Add(socialState);
            }
        }
        
        private void CollectLocationUsageSnapshot(ResearchDataSnapshot snapshot)
        {
            var locationComponents = FindObjectsByType<WorldLocationComponent>(UnityEngine.FindObjectsSortMode.None);
            snapshot.locationSnapshot = new List<LocationState>();
            
            foreach (var location in locationComponents)
            {
                var state = new LocationState
                {
                    locationName = location.LocationConfig.name,
                    currentOccupancy = location.CurrentOccupancy,
                    capacity = location.LocationConfig.capacity,
                    utilizationRate = location.UtilizationRate,
                    currentOccupants = location.CurrentOccupants
                        .Select(a => a.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID ?? "Unknown")
                        .ToList(),
                    timestamp = DateTime.UtcNow
                };
                
                snapshot.locationSnapshot.Add(state);
            }
        }
        
        private void CollectResourceUsageSnapshot(ResearchDataSnapshot snapshot)
        {
            var resourceComponents = FindObjectsByType<WorldResourceComponent>(UnityEngine.FindObjectsSortMode.None);
            snapshot.resourceSnapshot = new List<ResourceState>();
            
            foreach (var resource in resourceComponents)
            {
                var state = new ResourceState
                {
                    resourceName = resource.ResourceConfig.name,
                    currentQuantity = resource.CurrentQuantity,
                    isAvailable = resource.IsAvailable,
                    isBeingCollected = resource.IsBeingCollected,
                    timeSinceCollection = resource.TimeSinceCollection,
                    currentCollector = resource.CurrentCollector?.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID,
                    timestamp = DateTime.UtcNow
                };
                
                snapshot.resourceSnapshot.Add(state);
            }
        }
        
        private void CollectMoodStateSnapshot(ResearchDataSnapshot snapshot)
        {
            var moodComponents = FindObjectsByType<AgentMoodSystem>(UnityEngine.FindObjectsSortMode.None);
            snapshot.moodSnapshot = new List<AgentMoodStateClass>();
            
            foreach (var moodSystem in moodComponents)
            {
                var agent = moodSystem.GetComponent<Agent>();
                var identity = agent?.GetComponent<ResearchValidatedIdentity>();
                
                if (identity != null)
                {
                    var state = new AgentMoodStateClass
                    {
                        agentId = identity.UniqueResearchID,
                        currentMood = moodSystem.CurrentMood,
                        moodIntensity = moodSystem.MoodIntensity,
                        moodStability = moodSystem.MoodStability,
                        isInConflictMood = moodSystem.IsInConflictMood,
                        isInPositiveMood = moodSystem.IsInPositiveMood,
                        timestamp = DateTime.UtcNow
                    };
                    
                    snapshot.moodSnapshot.Add(state);
                }
            }
        }
        
        private void CollectKnowledgeStateSnapshot(ResearchDataSnapshot snapshot)
        {
            var knowledgeSystem = FindFirstObjectByType<WorldKnowledgeSystem>();
            if (knowledgeSystem != null)
            {
                snapshot.knowledgeSnapshot = knowledgeSystem.GetResearchData();
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnExperimentStarted(ExperimentConfiguration config)
        {
            StartExperimentDataCollection(config);
        }
        
        private void OnExperimentCompleted(ExperimentResults results)
        {
            FinalizeExperimentData();
            StopDataCollection();
        }
        
        private void OnConsciousnessDataReceived(Agent agent, ConsciousnessProfile profile)
        {
            if (!isCollecting) return;
            
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return;
            
            var dataPoint = new ConsciousnessDataPoint
            {
                timestamp = DateTime.UtcNow,
                agentId = identity.UniqueResearchID,
                consciousnessProfile = profile,
                experimentTime = (DateTime.UtcNow - collectionStartTime).TotalSeconds
            };
            
            consciousnessData.Add(dataPoint);
            MaintainDataLimits(consciousnessData);
        }
        
        private void OnMoodDataReceived(AgentMoodSystem moodSystem, MoodState oldMood, MoodState newMood)
        {
            if (!isCollecting) return;
            
            var agent = moodSystem.GetComponent<Agent>();
            var identity = agent?.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return;
            
            var dataPoint = new MoodDataPoint
            {
                timestamp = DateTime.UtcNow,
                agentId = identity.UniqueResearchID,
                oldMood = oldMood,
                newMood = newMood,
                moodIntensity = moodSystem.MoodIntensity,
                experimentTime = (DateTime.UtcNow - collectionStartTime).TotalSeconds
            };
            
            moodData.Add(dataPoint);
            MaintainDataLimits(moodData);
        }
        
        private void OnMoodEventReceived(AgentMoodSystem moodSystem, string eventType, float intensity)
        {
            if (!isCollecting) return;
            
            var agent = moodSystem.GetComponent<Agent>();
            var identity = agent?.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return;
            
            // Create mood event data point
            var eventDataPoint = new MoodEventDataPoint
            {
                timestamp = DateTime.UtcNow,
                agentId = identity.UniqueResearchID,
                eventType = eventType,
                intensity = intensity,
                currentMood = moodSystem.CurrentMood,
                experimentTime = (DateTime.UtcNow - collectionStartTime).TotalSeconds
            };
            
            // Add to mood data collection (extend the MoodDataPoint structure as needed)
        }
        
        private void OnConflictEventReceived(ConflictEvent conflictEvent)
        {
            if (!isCollecting) return;
            
            var dataPoint = new ConflictDataPoint
            {
                timestamp = conflictEvent.timestamp,
                agent1Id = conflictEvent.agent1Id,
                agent2Id = conflictEvent.agent2Id,
                conflictType = conflictEvent.conflictType,
                intensity = conflictEvent.intensity,
                escalationLevel = conflictEvent.escalationLevel,
                outcome = conflictEvent.outcome,
                experimentTime = (DateTime.UtcNow - collectionStartTime).TotalSeconds
            };
            
            conflictData.Add(dataPoint);
            MaintainDataLimits(conflictData);
        }
        
        private void OnLocationUsageReceived(WorldLocationComponent location, Agent agent)
        {
            if (!isCollecting) return;
            
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return;
            
            var dataPoint = new LocationUsageDataPoint
            {
                timestamp = DateTime.UtcNow,
                agentId = identity.UniqueResearchID,
                locationName = location.LocationConfig.name,
                locationType = location.LocationConfig.type,
                action = location.CurrentOccupants.Contains(agent) ? "Enter" : "Exit",
                occupancyAfter = location.CurrentOccupancy,
                utilizationRate = location.UtilizationRate,
                experimentTime = (DateTime.UtcNow - collectionStartTime).TotalSeconds
            };
            
            locationUsageData.Add(dataPoint);
            MaintainDataLimits(locationUsageData);
        }
        
        private void OnResourceCollectionReceived(WorldResourceComponent resource, Agent agent, int amount)
        {
            if (!isCollecting) return;
            
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return;
            
            var dataPoint = new ResourceCollectionDataPoint
            {
                timestamp = DateTime.UtcNow,
                agentId = identity.UniqueResearchID,
                resourceName = resource.ResourceConfig.name,
                resourceType = resource.ResourceConfig.type,
                amountCollected = amount,
                resourceValue = resource.ResourceConfig.value,
                scarcityLevel = resource.ResourceConfig.scarcity,
                experimentTime = (DateTime.UtcNow - collectionStartTime).TotalSeconds
            };
            
            resourceCollectionData.Add(dataPoint);
            MaintainDataLimits(resourceCollectionData);
        }
        
        private void OnKnowledgeDataReceived(string agentId, KnowledgeEntry knowledge)
        {
            if (!isCollecting) return;
            
            var dataPoint = new KnowledgeDataPoint
            {
                timestamp = DateTime.UtcNow,
                agentId = agentId,
                knowledgeCategory = knowledge.category,
                knowledgeTitle = knowledge.title,
                importance = knowledge.importance,
                contextRelevance = knowledge.contextRelevance,
                acquisitionMethod = "Learning",
                experimentTime = (DateTime.UtcNow - collectionStartTime).TotalSeconds
            };
            
            knowledgeData.Add(dataPoint);
            MaintainDataLimits(knowledgeData);
        }
        
        private void OnKnowledgeSharedReceived(string giverId, string receiverId)
        {
            if (!isCollecting) return;
            
            var dataPoint = new KnowledgeDataPoint
            {
                timestamp = DateTime.UtcNow,
                agentId = receiverId,
                knowledgeCategory = "Shared Knowledge",
                knowledgeTitle = $"Knowledge from {giverId}",
                importance = 0.6f,
                contextRelevance = 0.7f,
                acquisitionMethod = "Social Sharing",
                sourceAgentId = giverId,
                experimentTime = (DateTime.UtcNow - collectionStartTime).TotalSeconds
            };
            
            knowledgeData.Add(dataPoint);
            MaintainDataLimits(knowledgeData);
        }
        
        #endregion
        
        #region Data Management
        
        public void ClearExperimentData()
        {
            consciousnessData.Clear();
            socialInteractionData.Clear();
            conflictData.Clear();
            locationUsageData.Clear();
            resourceCollectionData.Clear();
            moodData.Clear();
            knowledgeData.Clear();
            currentAnalysisResults.Clear();
            generatedInsights.Clear();
            
            Debug.Log("[Research Data Manager] Cleared all experiment data");
        }
        
        private void RefreshTrackedAgents()
        {
            trackedAgents.Clear();
            trackedAgents.AddRange(FindObjectsByType<Agent>(UnityEngine.FindObjectsSortMode.None));
            
            Debug.Log($"[Research Data Manager] Now tracking {trackedAgents.Count} agents");
        }
        
        private void MaintainDataLimits<T>(List<T> dataList)
        {
            while (dataList.Count > maxDataPoints)
            {
                dataList.RemoveAt(0);
            }
        }
        
        private int GetTotalDataPointCount()
        {
            return consciousnessData.Count + socialInteractionData.Count + conflictData.Count +
                   locationUsageData.Count + resourceCollectionData.Count + moodData.Count + knowledgeData.Count;
        }
        
        #endregion
        
        #region Data Quality and Validation
        
        private void ValidateDataSnapshot(ResearchDataSnapshot snapshot)
        {
            var qualityScore = CalculateDataQualityScore(snapshot);
            
            if (qualityScore < minimumDataQualityThreshold)
            {
                Debug.LogWarning($"[Research Data Manager] Data quality below threshold: {qualityScore:F2}");
            }
            
            // Detect outliers if enabled
            if (enableOutlierDetection)
            {
                DetectDataOutliers(snapshot);
            }
        }
        
        private float CalculateDataQualityScore(ResearchDataSnapshot snapshot)
        {
            float qualityScore = 1.0f;
            
            // Check data completeness
            var expectedDataTypes = 6; // consciousness, social, location, resource, mood, knowledge
            var actualDataTypes = 0;
            
            if (snapshot.consciousnessSnapshot?.Count > 0) actualDataTypes++;
            if (snapshot.socialSnapshot?.Count > 0) actualDataTypes++;
            if (snapshot.locationSnapshot?.Count > 0) actualDataTypes++;
            if (snapshot.resourceSnapshot?.Count > 0) actualDataTypes++;
            if (snapshot.moodSnapshot?.Count > 0) actualDataTypes++;
            if (snapshot.knowledgeSnapshot != null) actualDataTypes++;
            
            var completenessScore = (float)actualDataTypes / expectedDataTypes;
            qualityScore *= completenessScore;
            
            // Check for missing agent data
            var expectedAgents = trackedAgents.Count;
            var consciousnessAgents = snapshot.consciousnessSnapshot?.Count ?? 0;
            var agentDataCompleteness = expectedAgents > 0 ? (float)consciousnessAgents / expectedAgents : 1.0f;
            qualityScore *= agentDataCompleteness;
            
            return qualityScore;
        }
        
        private void DetectDataOutliers(ResearchDataSnapshot snapshot)
        {
            // Simple outlier detection for consciousness data
            if (snapshot.consciousnessSnapshot != null && snapshot.consciousnessSnapshot.Count > 1)
            {
                var consciousnessLevels = snapshot.consciousnessSnapshot.Select(c => c.consciousnessLevel).ToList();
                var mean = consciousnessLevels.Average();
                var stdDev = CalculateStandardDeviation(consciousnessLevels, mean);
                
                foreach (var state in snapshot.consciousnessSnapshot)
                {
                    var zScore = Math.Abs(state.consciousnessLevel - mean) / stdDev;
                    if (zScore > outlierThreshold)
                    {
                        Debug.LogWarning($"[Research Data Manager] Consciousness outlier detected for agent {state.agentId}: " +
                                       $"level {state.consciousnessLevel:F2} (z-score: {zScore:F2})");
                    }
                }
            }
        }
        
        private float CalculateStandardDeviation(List<float> values, float mean)
        {
            var sumOfSquares = values.Sum(v => (v - mean) * (v - mean));
            return Mathf.Sqrt(sumOfSquares / values.Count);
        }
        
        #endregion
        
        #region Analysis and Insights
        
        private void TriggerPeriodicAnalysis()
        {
            if (analyticsEngine != null && enableRealTimeAnalysis)
            {
                StartCoroutine(RunPeriodicAnalysisAsync());
            }
        }
        
        private IEnumerator RunPeriodicAnalysisAsync()
        {
            yield return new WaitForEndOfFrame();
            
            if (analyticsEngine != null)
            {
                var results = analyticsEngine.RunPeriodicAnalysis();
                currentAnalysisResults["PeriodicAnalysis"] = results;
                OnAnalysisCompleted?.Invoke(results);
            }
        }
        
        private void GenerateResearchInsights()
        {
            var insights = new List<ResearchInsight>();
            
            // Generate consciousness insights
            insights.AddRange(GenerateConsciousnessInsights());
            
            // Generate social dynamics insights
            insights.AddRange(GenerateSocialInsights());
            
            // Generate conflict insights
            insights.AddRange(GenerateConflictInsights());
            
            // Generate mood insights
            insights.AddRange(GenerateMoodInsights());
            
            // Add to generated insights
            generatedInsights.AddRange(insights);
            
            // Fire events for each insight
            foreach (var insight in insights)
            {
                OnInsightGenerated?.Invoke(insight);
            }
            
            Debug.Log($"[Research Data Manager] Generated {insights.Count} research insights");
        }
        
        private List<ResearchInsight> GenerateConsciousnessInsights()
        {
            var insights = new List<ResearchInsight>();
            
            if (consciousnessData.Count > 10)
            {
                // Consciousness evolution insight
                var recentData = consciousnessData.TakeLast(50).ToList();
                var avgConsciousness = recentData.Average(d => d.consciousnessProfile.overallScore);
                
                if (avgConsciousness > 0.7f)
                {
                    insights.Add(new ResearchInsight
                    {
                        category = "Consciousness",
                        title = "High Consciousness Levels Observed",
                        description = $"Average consciousness level of {avgConsciousness:F2} suggests strong emergent awareness",
                        significance = ResearchSignificance.High,
                        supportingData = recentData.Count,
                        timestamp = DateTime.UtcNow
                    });
                }
                
                // Consciousness development insight
                var firstHalf = consciousnessData.Take(consciousnessData.Count / 2);
                var secondHalf = consciousnessData.Skip(consciousnessData.Count / 2);
                
                var firstAvg = firstHalf.Average(d => d.consciousnessProfile.overallScore);
                var secondAvg = secondHalf.Average(d => d.consciousnessProfile.overallScore);
                
                if (secondAvg > firstAvg * 1.2f)
                {
                    insights.Add(new ResearchInsight
                    {
                        category = "Consciousness",
                        title = "Consciousness Development Over Time",
                        description = $"Consciousness increased by {((secondAvg / firstAvg - 1) * 100):F1}% during experiment",
                        significance = ResearchSignificance.VeryHigh,
                        supportingData = consciousnessData.Count,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            
            return insights;
        }
        
        private List<ResearchInsight> GenerateSocialInsights()
        {
            var insights = new List<ResearchInsight>();
            
            if (socialInteractionData.Count > 5)
            {
                // Social network density insight
                var uniqueAgents = socialInteractionData.SelectMany(d => new[] { d.agent1Id, d.agent2Id }).Distinct().Count();
                var possibleConnections = uniqueAgents * (uniqueAgents - 1) / 2;
                var actualConnections = socialInteractionData.Count;
                var networkDensity = (float)actualConnections / possibleConnections;
                
                if (networkDensity > 0.5f)
                {
                    insights.Add(new ResearchInsight
                    {
                        category = "Social Dynamics",
                        title = "High Social Network Density",
                        description = $"Network density of {networkDensity:F2} indicates strong social connectivity",
                        significance = ResearchSignificance.Medium,
                        supportingData = socialInteractionData.Count,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            
            return insights;
        }
        
        private List<ResearchInsight> GenerateConflictInsights()
        {
            var insights = new List<ResearchInsight>();
            
            if (conflictData.Count > 0)
            {
                var escalatedConflicts = conflictData.Count(d => d.escalationLevel >= ConflictEscalationLevel.Intense);
                var escalationRate = (float)escalatedConflicts / conflictData.Count;
                
                if (escalationRate > 0.3f)
                {
                    insights.Add(new ResearchInsight
                    {
                        category = "Conflict Dynamics",
                        title = "High Conflict Escalation Rate",
                        description = $"{escalationRate:P0} of conflicts escalated to intense levels",
                        significance = ResearchSignificance.High,
                        supportingData = conflictData.Count,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            
            return insights;
        }
        
        private List<ResearchInsight> GenerateMoodInsights()
        {
            var insights = new List<ResearchInsight>();
            
            if (moodData.Count > 10)
            {
                var negativeTransitions = moodData.Count(d => 
                    IsPositiveMood(d.oldMood) && IsNegativeMood(d.newMood));
                var positiveTransitions = moodData.Count(d => 
                    IsNegativeMood(d.oldMood) && IsPositiveMood(d.newMood));
                
                if (positiveTransitions > negativeTransitions * 1.5f)
                {
                    insights.Add(new ResearchInsight
                    {
                        category = "Mood Dynamics",
                        title = "Positive Mood Recovery Patterns",
                        description = $"Agents show strong tendency to recover from negative moods ({positiveTransitions} vs {negativeTransitions} transitions)",
                        significance = ResearchSignificance.Medium,
                        supportingData = moodData.Count,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            
            return insights;
        }
        
        private bool IsPositiveMood(MoodState mood)
        {
            return mood == MoodState.Happy || mood == MoodState.Excited || mood == MoodState.Content;
        }
        
        private bool IsNegativeMood(MoodState mood)
        {
            return mood == MoodState.Angry || mood == MoodState.Frustrated || mood == MoodState.Hostile ||
                   mood == MoodState.Anxious || mood == MoodState.Disappointed;
        }
        
        #endregion
        
        #region Data Export
        
        private void AutoExportData()
        {
            if (!isCollecting) return;
            
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"AutoExport_{currentExperiment?.experimentName ?? "Unknown"}_{timestamp}";
                
                ExportExperimentData(filename, true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Research Data Manager] Auto-export failed: {e.Message}");
            }
        }
        
        public void ExportExperimentData(string filename, bool isAutoSave = false)
        {
            try
            {
                var exportData = new ExperimentDataExport
                {
                    experimentName = currentExperiment?.experimentName ?? "Unknown",
                    exportTimestamp = DateTime.UtcNow,
                    collectionStartTime = collectionStartTime,
                    totalDataPoints = TotalDataPoints,
                    isAutoSave = isAutoSave,
                    
                    consciousnessData = new List<ConsciousnessDataPoint>(consciousnessData),
                    socialInteractionData = new List<SocialInteractionDataPoint>(socialInteractionData),
                    conflictData = new List<ConflictDataPoint>(conflictData),
                    locationUsageData = new List<LocationUsageDataPoint>(locationUsageData),
                    resourceCollectionData = new List<ResourceCollectionDataPoint>(resourceCollectionData),
                    moodData = new List<MoodDataPoint>(moodData),
                    knowledgeData = new List<KnowledgeDataPoint>(knowledgeData),
                    
                    analysisResults = new Dictionary<string, object>(currentAnalysisResults),
                    researchInsights = new List<ResearchInsight>(generatedInsights)
                };
                
                var fullPath = ExportDataInFormat(exportData, filename);
                OnDataExported?.Invoke(fullPath);
                
                Debug.Log($"[Research Data Manager] Data exported to: {fullPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Research Data Manager] Export failed: {e.Message}");
            }
        }
        
        private string ExportDataInFormat(ExperimentDataExport data, string filename)
        {
            var extension = exportFormat switch
            {
                DataExportFormat.JSON => ".json",
                DataExportFormat.CSV => ".csv",
                DataExportFormat.XML => ".xml",
                _ => ".json"
            };
            
            var fullFilename = filename + extension;
            var fullPath = System.IO.Path.Combine(dataExportPath, fullFilename);
            
            switch (exportFormat)
            {
                case DataExportFormat.JSON:
                    var json = JsonUtility.ToJson(data, true);
                    System.IO.File.WriteAllText(fullPath, json);
                    break;
                    
                case DataExportFormat.CSV:
                    var csv = ConvertToCSV(data);
                    System.IO.File.WriteAllText(fullPath, csv);
                    break;
                    
                case DataExportFormat.XML:
                    var xml = ConvertToXML(data);
                    System.IO.File.WriteAllText(fullPath, xml);
                    break;
            }
            
            return fullPath;
        }
        
        private string ConvertToCSV(ExperimentDataExport data)
        {
            var csv = new System.Text.StringBuilder();
            
            // Consciousness data CSV
            csv.AppendLine("Data Type,Timestamp,Agent ID,Consciousness Level,Self Awareness,Meta Cognition,Theory of Mind,Creative Expression,Temporal Integration");
            foreach (var item in data.consciousnessData)
            {
                csv.AppendLine($"Consciousness,{item.timestamp:yyyy-MM-dd HH:mm:ss},{item.agentId},{item.consciousnessProfile.overallScore:F3}," +
                             $"{item.consciousnessProfile.primaryIndicators.Count},{item.consciousnessProfile.consciousnessType},,,,");
            }
            
            // Add other data types as needed...
            
            return csv.ToString();
        }
        
        private string ConvertToXML(ExperimentDataExport data)
        {
            // Basic XML export - could be enhanced with proper XML serialization
            var xml = new System.Text.StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<ExperimentData>");
            xml.AppendLine($"  <ExperimentName>{data.experimentName}</ExperimentName>");
            xml.AppendLine($"  <ExportTimestamp>{data.exportTimestamp:yyyy-MM-dd HH:mm:ss}</ExportTimestamp>");
            xml.AppendLine($"  <TotalDataPoints>{data.totalDataPoints}</TotalDataPoints>");
            xml.AppendLine("</ExperimentData>");
            
            return xml.ToString();
        }
        
        #endregion
        
        #region Research Data Access
        
        public object GetExperimentResearchData()
        {
            return new
            {
                totalDataPoints = TotalDataPoints,
                consciousnessDataPoints = consciousnessData.Count,
                socialInteractionPoints = socialInteractionData.Count,
                conflictEvents = conflictData.Count,
                locationUsageEvents = locationUsageData.Count,
                resourceCollectionEvents = resourceCollectionData.Count,
                moodChangeEvents = moodData.Count,
                knowledgeEvents = knowledgeData.Count,
                collectionDuration = (DateTime.UtcNow - collectionStartTime).TotalMinutes,
                generatedInsights = generatedInsights.Count,
                analysisResults = currentAnalysisResults
            };
        }
        
        // Helper methods for getting agent data
        private List<string> GetAgentActiveConversations(Agent agent)
        {
            // Placeholder - would integrate with conversation system
            return new List<string>();
        }
        
        private List<string> GetAgentSocialConnections(Agent agent)
        {
            // Placeholder - would integrate with social network system
            return new List<string>();
        }
        
        private float GetAgentSocialMoodInfluence(Agent agent)
        {
            // Placeholder - would integrate with mood system
            return 0f;
        }
        
        private int GetRecentInteractionCount(Agent agent)
        {
            // Placeholder - would count recent interactions
            return 0;
        }
        
        #endregion
        
        #region Cleanup
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (experimentManager != null)
            {
                ExperimentManager.OnExperimentStarted -= OnExperimentStarted;
                ExperimentManager.OnExperimentCompleted -= OnExperimentCompleted;
            }
            
            // Stop data collection
            StopDataCollection();
            
            // Cancel all invokes
            CancelInvoke();
        }
        
        #endregion
    }
    
    #region Data Structures
    
    [System.Serializable]
    public class ResearchDataSnapshot
    {
        public DateTime timestamp;
        public double experimentTime;
        public int totalDataPoints;
        public List<AgentConsciousnessState> consciousnessSnapshot;
        public List<AgentSocialState> socialSnapshot;
        public List<LocationState> locationSnapshot;
        public List<ResourceState> resourceSnapshot;
        public List<AgentMoodStateClass> moodSnapshot;
        public KnowledgeSystemResearchData knowledgeSnapshot;
    }
    
    [System.Serializable]
    public class ConsciousnessDataPoint
    {
        public DateTime timestamp;
        public string agentId;
        public ConsciousnessProfile consciousnessProfile;
        public double experimentTime;
    }
    
    [System.Serializable]
    public class SocialInteractionDataPoint
    {
        public DateTime timestamp;
        public string agent1Id;
        public string agent2Id;
        public string interactionType;
        public float duration;
        public double experimentTime;
    }
    
    [System.Serializable]
    public class ConflictDataPoint
    {
        public DateTime timestamp;
        public string agent1Id;
        public string agent2Id;
        public string conflictType;
        public float intensity;
        public ConflictEscalationLevel escalationLevel;
        public ConflictOutcome outcome;
        public double experimentTime;
    }
    
    [System.Serializable]
    public class LocationUsageDataPoint
    {
        public DateTime timestamp;
        public string agentId;
        public string locationName;
        public LocationType locationType;
        public string action;
        public int occupancyAfter;
        public float utilizationRate;
        public double experimentTime;
    }
    
    [System.Serializable]
    public class ResourceCollectionDataPoint
    {
        public DateTime timestamp;
        public string agentId;
        public string resourceName;
        public ResourceType resourceType;
        public int amountCollected;
        public int resourceValue;
        public float scarcityLevel;
        public double experimentTime;
    }
    
    [System.Serializable]
    public class MoodDataPoint
    {
        public DateTime timestamp;
        public string agentId;
        public MoodState oldMood;
        public MoodState newMood;
        public float moodIntensity;
        public double experimentTime;
    }
    
    [System.Serializable]
    public class MoodEventDataPoint
    {
        public DateTime timestamp;
        public string agentId;
        public string eventType;
        public float intensity;
        public MoodState currentMood;
        public double experimentTime;
    }
    
    [System.Serializable]
    public class KnowledgeDataPoint
    {
        public DateTime timestamp;
        public string agentId;
        public string knowledgeCategory;
        public string knowledgeTitle;
        public float importance;
        public float contextRelevance;
        public string acquisitionMethod;
        public string sourceAgentId;
        public double experimentTime;
    }
    
    [System.Serializable]
    public class AgentConsciousnessState
    {
        public string agentId;
        public float consciousnessLevel;
        public float selfAwareness;
        public float metaCognition;
        public float theoryOfMind;
        public float creativeExpression;
        public float temporalIntegration;
        public DateTime timestamp;
    }
    
    [System.Serializable]
    public class AgentSocialState
    {
        public string agentId;
        public List<string> activeConversations;
        public List<string> socialConnections;
        public float socialMoodInfluence;
        public int recentInteractionCount;
        public DateTime timestamp;
    }
    
    [System.Serializable]
    public class LocationState
    {
        public string locationName;
        public int currentOccupancy;
        public int capacity;
        public float utilizationRate;
        public List<string> currentOccupants;
        public DateTime timestamp;
    }
    
    [System.Serializable]
    public class ResourceState
    {
        public string resourceName;
        public int currentQuantity;
        public bool isAvailable;
        public bool isBeingCollected;
        public float timeSinceCollection;
        public string currentCollector;
        public DateTime timestamp;
    }
    
    [System.Serializable]
    public class AgentMoodStateClass
    {
        public string agentId;
        public MoodState currentMood;
        public float moodIntensity;
        public float moodStability;
        public bool isInConflictMood;
        public bool isInPositiveMood;
        public DateTime timestamp;
    }
    
    [System.Serializable]
    public class ExperimentDataExport
    {
        public string experimentName;
        public DateTime exportTimestamp;
        public DateTime collectionStartTime;
        public int totalDataPoints;
        public bool isAutoSave;
        
        public List<ConsciousnessDataPoint> consciousnessData;
        public List<SocialInteractionDataPoint> socialInteractionData;
        public List<ConflictDataPoint> conflictData;
        public List<LocationUsageDataPoint> locationUsageData;
        public List<ResourceCollectionDataPoint> resourceCollectionData;
        public List<MoodDataPoint> moodData;
        public List<KnowledgeDataPoint> knowledgeData;
        
        public Dictionary<string, object> analysisResults;
        public List<ResearchInsight> researchInsights;
    }
    
    [System.Serializable]
    public class ResearchInsight
    {
        public string category;
        public string title;
        public string description;
        public ResearchSignificance significance;
        public int supportingData;
        public DateTime timestamp;
    }
    
    public enum ResearchSignificance
    {
        Low,
        Medium,
        High,
        VeryHigh
    }
    
    #endregion
}