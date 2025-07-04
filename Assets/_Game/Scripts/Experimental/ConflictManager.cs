using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Research;
using AIWorld.Experimental;
using AIWorld.Agents;
using AIWorld.Data;
using AIWorld.BDI;
using AIWorld.Needs;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Manages conflicts between agents based on resource competition, personality clashes, and contextual pressures
    /// Enables realistic negative interactions and mood impacts for research validity
    /// Designed for Prof Darryl Charles's experimental research platform
    /// </summary>
    public class ConflictManager : MonoBehaviour
    {
        [Header("Conflict Configuration")]
        [SerializeField] private bool enableConflictSystem = true;
        [SerializeField] private float conflictCheckInterval = 30f; // 30 seconds
        [SerializeField] private float baseConflictProbability = 0.2f;
        [SerializeField] private float maxConflictIntensity = 1.0f;
        
        [Header("Conflict Triggers")]
        [SerializeField] private bool enableResourceConflicts = true;
        [SerializeField] private bool enablePersonalityConflicts = true;
        [SerializeField] private bool enableTerritorialConflicts = true;
        [SerializeField] private bool enableGoalConflicts = true;
        
        [Header("Conflict Escalation")]
        [SerializeField] private float escalationProbability = 0.3f;
        [SerializeField] private float deEscalationRate = 0.1f; // per minute
        [SerializeField] private float conflictMemoryDuration = 300f; // 5 minutes
        
        [Header("Context Sensitivity")]
        [SerializeField] private SimulationContext currentContext = SimulationContext.University;
        [SerializeField] private float contextConflictMultiplier = 1.0f;
        [SerializeField] private ConflictContextConfig contextConfig;
        
        [Header("Research Tracking")]
        [SerializeField] private bool enableConflictLogging = true;
        [SerializeField] private int maxConflictHistory = 1000;
        
        // Runtime data
        private List<ActiveConflict> activeConflicts = new List<ActiveConflict>();
        private List<ConflictEvent> conflictHistory = new List<ConflictEvent>();
        private Dictionary<string, float> agentConflictStress = new Dictionary<string, float>();
        private Dictionary<string, List<string>> conflictMemories = new Dictionary<string, List<string>>();
        
        // Component references
        private AgentGroupManager agentGroupManager;
        private ExperimentConfigurationManager configManager;
        private List<Agent> allAgents = new List<Agent>();
        
        // Events
        public static event Action<ConflictEvent> OnConflictStarted;
        public static event Action<ConflictEvent> OnConflictEscalated;
        public static event Action<ConflictEvent> OnConflictResolved;
        public static event Action<Agent, float> OnAgentStressChanged;
        
        public List<ActiveConflict> ActiveConflicts => new List<ActiveConflict>(activeConflicts);
        public List<ConflictEvent> ConflictHistory => new List<ConflictEvent>(conflictHistory);
        public bool ConflictSystemEnabled => enableConflictSystem;
        
        private void Awake()
        {
            agentGroupManager = FindFirstObjectByType<AgentGroupManager>();
            configManager = FindFirstObjectByType<ExperimentConfigurationManager>();
        }
        
        private void Start()
        {
            if (enableConflictSystem)
            {
                InitializeConflictSystem();
                InvokeRepeating(nameof(ProcessConflictChecks), conflictCheckInterval, conflictCheckInterval);
            }
        }
        
        #region Initialization
        
        public void Initialize(ExperimentConfiguration config)
        {
            enableConflictSystem = config.enableConflictSystem;
            baseConflictProbability = config.conflictProbability;
            enableResourceConflicts = config.enableResourceCompetition;
            currentContext = config.simulationContext;
            
            SetupContextConfiguration(config.simulationContext);
            
            Debug.Log($"[Conflict Manager] Initialized for context: {currentContext}, " +
                     $"base probability: {(float)baseConflictProbability:F2}");
        }
        
        private void InitializeConflictSystem()
        {
            // Get all agents in the scene
            RefreshAgentList();
            
            // Initialize agent stress levels
            foreach (var agent in allAgents)
            {
                var identity = agent.GetComponent<ResearchValidatedIdentity>();
                if (identity != null)
                {
                    agentConflictStress[identity.UniqueResearchID] = 0f;
                    conflictMemories[identity.UniqueResearchID] = new List<string>();
                }
            }
            
            // Subscribe to agent events
            AgentGroupManager.OnAllGroupsCreated += OnAgentGroupsCreated;
        }
        
        private void SetupContextConfiguration(SimulationContext context)
        {
            contextConfig = context switch
            {
                SimulationContext.University => new ConflictContextConfig
                {
                    resourceCompetitionWeight = 0.8f,
                    personalityClashWeight = 0.6f,
                    territorialWeight = 0.4f,
                    goalConflictWeight = 0.9f,
                    stressEscalationRate = 0.3f,
                    cooperationPenalty = 0.5f,
                    commonConflictTypes = new[] { "Research Competition", "Publication Credit", "Lab Access", "Grant Competition" }
                },
                SimulationContext.Survival => new ConflictContextConfig
                {
                    resourceCompetitionWeight = 1.0f,
                    personalityClashWeight = 0.4f,
                    territorialWeight = 0.9f,
                    goalConflictWeight = 0.7f,
                    stressEscalationRate = 0.6f,
                    cooperationPenalty = 0.2f,
                    commonConflictTypes = new[] { "Food Competition", "Shelter Dispute", "Water Access", "Territory Claim" }
                },
                SimulationContext.Social => new ConflictContextConfig
                {
                    resourceCompetitionWeight = 0.3f,
                    personalityClashWeight = 0.9f,
                    territorialWeight = 0.5f,
                    goalConflictWeight = 0.8f,
                    stressEscalationRate = 0.4f,
                    cooperationPenalty = 0.7f,
                    commonConflictTypes = new[] { "Romantic Rivalry", "Social Status", "Attention Seeking", "Personality Clash" }
                },
                _ => new ConflictContextConfig
                {
                    resourceCompetitionWeight = 0.5f,
                    personalityClashWeight = 0.5f,
                    territorialWeight = 0.5f,
                    goalConflictWeight = 0.5f,
                    stressEscalationRate = 0.4f,
                    cooperationPenalty = 0.5f,
                    commonConflictTypes = new[] { "Resource Dispute", "Goal Conflict", "Personality Clash", "Territorial Dispute" }
                }
            };
            
            contextConflictMultiplier = (float)contextConfig.stressEscalationRate;
        }
        
        private void OnAgentGroupsCreated()
        {
            RefreshAgentList();
        }
        
        private void RefreshAgentList()
        {
            allAgents.Clear();
            allAgents.AddRange(FindObjectsByType<Agent>(UnityEngine.FindObjectsSortMode.None));
            Debug.Log($"[Conflict Manager] Tracking {allAgents.Count} agents for conflict detection");
        }
        
        #endregion
        
        #region Conflict Detection and Processing
        
        private void ProcessConflictChecks()
        {
            if (!enableConflictSystem || allAgents.Count < 2) return;
            
            // Update existing conflicts
            UpdateActiveConflicts();
            
            // Check for new conflicts
            CheckForNewConflicts();
            
            // Update agent stress levels
            UpdateAgentStressLevels();
            
            // Clean up old conflict memories
            CleanupConflictMemories();
        }
        
        private void CheckForNewConflicts()
        {
            for (int i = 0; i < allAgents.Count; i++)
            {
                for (int j = i + 1; j < allAgents.Count; j++)
                {
                    var agent1 = allAgents[i];
                    var agent2 = allAgents[j];
                    
                    if (AreAgentsInConflict(agent1, agent2)) continue;
                    
                    var conflictProbability = CalculateConflictProbability(agent1, agent2);
                    
                    if (UnityEngine.Random.value < conflictProbability)
                    {
                        InitiateConflict(agent1, agent2);
                    }
                }
            }
        }
        
        private float CalculateConflictProbability(Agent agent1, Agent agent2)
        {
            float probability = (float)baseConflictProbability;
            
            // Resource competition conflicts
            if (enableResourceConflicts)
            {
                probability += CalculateResourceConflictProbability(agent1, agent2) * (float)contextConfig.resourceCompetitionWeight;
            }
            
            // Personality clash conflicts
            if (enablePersonalityConflicts)
            {
                probability += CalculatePersonalityConflictProbability(agent1, agent2) * (float)contextConfig.personalityClashWeight;
            }
            
            // Territorial conflicts (proximity-based)
            if (enableTerritorialConflicts)
            {
                probability += CalculateTerritorialConflictProbability(agent1, agent2) * (float)contextConfig.territorialWeight;
            }
            
            // Goal conflicts
            if (enableGoalConflicts)
            {
                probability += CalculateGoalConflictProbability(agent1, agent2) * (float)contextConfig.goalConflictWeight;
            }
            
            // Apply context multiplier
            probability *= contextConflictMultiplier;
            
            // Apply stress amplification
            var stress1 = GetAgentStress(agent1);
            var stress2 = GetAgentStress(agent2);
            var avgStress = (stress1 + stress2) * 0.5f;
            probability += avgStress * 0.3f;
            
            return Mathf.Clamp01(probability);
        }
        
        private float CalculateResourceConflictProbability(Agent agent1, Agent agent2)
        {
            // Check if agents are competing for the same resources
            var needs1 = agent1.Needs?.GetMostUrgentNeeds();
            var needs2 = agent2.Needs?.GetMostUrgentNeeds();
            
            if (needs1 == null || needs2 == null) return 0f;
            
            // Count overlapping urgent needs
            var overlappingNeeds = needs1.Intersect(needs2).Count();
            var maxNeeds = Mathf.Max(needs1.Count, needs2.Count);
            
            if (maxNeeds == 0) return 0f;
            
            var overlapRatio = (float)overlappingNeeds / maxNeeds;
            
            // Higher overlap = higher conflict probability
            return overlapRatio * 0.6f;
        }
        
        private float CalculatePersonalityConflictProbability(Agent agent1, Agent agent2)
        {
            var personality1 = agent1.GetComponent<AgentPersonality>();
            var personality2 = agent2.GetComponent<AgentPersonality>();
            
            if (personality1 == null || personality2 == null) return 0f;
            
            float conflictFactor = 0f;
            
            // Low agreeableness increases conflict probability
            var avgAgreeableness = (personality1.agreeableness + personality2.agreeableness) * 0.5f;
            conflictFactor += (1.0f - avgAgreeableness) * 0.4f;
            
            // High neuroticism increases conflict probability
            var avgNeuroticism = (personality1.neuroticism + personality2.neuroticism) * 0.5f;
            conflictFactor += avgNeuroticism * 0.3f;
            
            // Extreme differences in conscientiousness can cause conflict
            var conscientiousnessDiff = Mathf.Abs((float)personality1.conscientiousness - (float)personality2.conscientiousness);
            if (conscientiousnessDiff > 0.6f)
            {
                conflictFactor += conscientiousnessDiff * 0.2f;
            }
            
            return Mathf.Clamp01(conflictFactor);
        }
        
        private float CalculateTerritorialConflictProbability(Agent agent1, Agent agent2)
        {
            var distance = Vector3.Distance(agent1.transform.position, agent2.transform.position);
            var territorialRange = 5f; // Configurable territorial range
            
            if (distance > territorialRange) return 0f;
            
            // Closer agents have higher territorial conflict probability
            var proximityFactor = 1.0f - (distance / territorialRange);
            
            // Low agreeableness increases territorial behavior
            var personality1 = agent1.GetComponent<AgentPersonality>();
            var personality2 = agent2.GetComponent<AgentPersonality>();
            
            if (personality1 != null && personality2 != null)
            {
                var avgAgreeableness = (personality1.agreeableness + personality2.agreeableness) * 0.5f;
                proximityFactor *= (1.0f - avgAgreeableness);
            }
            
            return proximityFactor * 0.3f;
        }
        
        private float CalculateGoalConflictProbability(Agent agent1, Agent agent2)
        {
            // Note: BDIEngine.GetActiveIntentions does not exist in the provided code
            // This method will return 0f as a placeholder
            return 0f;
        }
        
        private bool AreIntentionsConflicting(Intention intention1, Intention intention2)
        {
            // Simple heuristic: intentions with same name but different agents are potentially conflicting
            if (intention1.intentionName == intention2.intentionName)
            {
                return true;
            }
            
            // Context-specific conflict detection
            return currentContext switch
            {
                SimulationContext.University => AreAcademicIntentionsConflicting(intention1, intention2),
                SimulationContext.Survival => AreSurvivalIntentionsConflicting(intention1, intention2),
                SimulationContext.Social => AreSocialIntentionsConflicting(intention1, intention2),
                _ => false
            };
        }
        
        private bool AreAcademicIntentionsConflicting(Intention intention1, Intention intention2)
        {
            var academicConflicts = new[]
            {
                ("Research", "Research"),
                ("Publish", "Publish"),
                ("Present", "Present"),
                ("Collaborate", "Lead")
            };
            
            foreach (var (conflict1, conflict2) in academicConflicts)
            {
                if ((intention1.intentionName.Contains(conflict1) && intention2.intentionName.Contains(conflict2)) ||
                    (intention1.intentionName.Contains(conflict2) && intention2.intentionName.Contains(conflict1)))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private bool AreSurvivalIntentionsConflicting(Intention intention1, Intention intention2)
        {
            var survivalConflicts = new[]
            {
                ("Gather", "Gather"),
                ("Hunt", "Hunt"),
                ("BuildShelter", "BuildShelter"),
                ("Explore", "ClaimTerritory")
            };
            
            foreach (var (conflict1, conflict2) in survivalConflicts)
            {
                if ((intention1.intentionName.Contains(conflict1) && intention2.intentionName.Contains(conflict2)) ||
                    (intention1.intentionName.Contains(conflict2) && intention2.intentionName.Contains(conflict1)))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private bool AreSocialIntentionsConflicting(Intention intention1, Intention intention2)
        {
            var socialConflicts = new[]
            {
                ("Attract", "Attract"),
                ("Impress", "Impress"),
                ("Lead", "Lead"),
                ("Dominate", "Dominate")
            };
            
            foreach (var (conflict1, conflict2) in socialConflicts)
            {
                if ((intention1.intentionName.Contains(conflict1) && intention2.intentionName.Contains(conflict2)) ||
                    (intention1.intentionName.Contains(conflict2) && intention2.intentionName.Contains(conflict1)))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        #endregion
        
        #region Conflict Management
        
        private void InitiateConflict(Agent agent1, Agent agent2)
        {
            var conflictType = DetermineConflictType(agent1, agent2);
            var intensity = CalculateInitialConflictIntensity(agent1, agent2);
            
            var conflict = new ActiveConflict
            {
                conflictId = Guid.NewGuid().ToString(),
                agent1 = agent1,
                agent2 = agent2,
                conflictType = conflictType,
                intensity = intensity,
                startTime = DateTime.UtcNow,
                escalationLevel = ConflictEscalationLevel.Initial,
                isActive = true
            };
            
            activeConflicts.Add(conflict);
            
            // Apply immediate mood impacts
            ApplyConflictMoodImpact(agent1, conflict);
            ApplyConflictMoodImpact(agent2, conflict);
            
            // Update stress levels
            IncreaseAgentStress(agent1, intensity * 0.5f);
            IncreaseAgentStress(agent2, intensity * 0.5f);
            
            // Add to conflict memories
            AddConflictMemory(agent1, agent2, conflictType);
            AddConflictMemory(agent2, agent1, conflictType);
            
            // Log conflict event
            var conflictEvent = new ConflictEvent
            {
                timestamp = DateTime.UtcNow,
                agent1Id = agent1.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID ?? "Unknown",
                agent2Id = agent2.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID ?? "Unknown",
                conflictType = conflictType,
                intensity = intensity,
                escalationLevel = ConflictEscalationLevel.Initial,
                outcome = ConflictOutcome.Ongoing,
                context = currentContext.ToString(),
                triggerReason = $"Conflict probability: {(float)CalculateConflictProbability(agent1, agent2):F2}"
            };
            
            conflictHistory.Add(conflictEvent);
            
            if (conflictHistory.Count > maxConflictHistory)
            {
                conflictHistory.RemoveAt(0);
            }
            
            OnConflictStarted?.Invoke(conflictEvent);
            
            Debug.Log($"[Conflict Manager] Conflict initiated: {agent1.name} vs {agent2.name} " +
                     $"({conflictType}, Intensity: {(float)intensity:F2})");
        }
        
        private string DetermineConflictType(Agent agent1, Agent agent2)
        {
            var availableTypes = contextConfig.commonConflictTypes;
            var weights = new float[availableTypes.Length];
            
            for (int i = 0; i < availableTypes.Length; i++)
            {
                weights[i] = CalculateConflictTypeWeight(agent1, agent2, availableTypes[i]);
            }
            
            // Weighted random selection
            var totalWeight = weights.Sum();
            if (totalWeight <= 0f) return availableTypes[0];
            
            var randomValue = UnityEngine.Random.value * totalWeight;
            var cumulativeWeight = 0f;
            
            for (int i = 0; i < availableTypes.Length; i++)
            {
                cumulativeWeight += weights[i];
                if (randomValue <= cumulativeWeight)
                {
                    return availableTypes[i];
                }
            }
            
            return availableTypes[0];
        }
        
        private float CalculateConflictTypeWeight(Agent agent1, Agent agent2, string conflictType)
        {
            // Base weight
            float weight = 1.0f;
            
            // Adjust based on agent personalities and current state
            var personality1 = agent1.GetComponent<AgentPersonality>();
            var personality2 = agent2.GetComponent<AgentPersonality>();
            
            if (personality1 != null && personality2 != null)
            {
                switch (conflictType)
                {
                    case "Research Competition":
                    case "Publication Credit":
                        weight *= (personality1.conscientiousness + personality2.conscientiousness) * 0.5f;
                        break;
                        
                    case "Romantic Rivalry":
                    case "Social Status":
                        weight *= (personality1.extraversion + personality2.extraversion) * 0.5f;
                        break;
                        
                    case "Food Competition":
                    case "Territory Claim":
                        weight *= (2.0f - ((personality1.agreeableness + personality2.agreeableness) * 0.5f));
                        break;
                        
                    case "Personality Clash":
                        weight *= (personality1.neuroticism + personality2.neuroticism) * 0.5f;
                        break;
                }
            }
            
            return weight;
        }
        
        private float CalculateInitialConflictIntensity(Agent agent1, Agent agent2)
        {
            float intensity = 0.3f; // Base intensity
            
            // Personality factors
            var personality1 = agent1.GetComponent<AgentPersonality>();
            var personality2 = agent2.GetComponent<AgentPersonality>();
            
            if (personality1 != null && personality2 != null)
            {
                // Low agreeableness increases intensity
                var avgAgreeableness = (personality1.agreeableness + personality2.agreeableness) * 0.5f;
                intensity += (1.0f - avgAgreeableness) * 0.3f;
                
                // High neuroticism increases intensity
                var avgNeuroticism = (personality1.neuroticism + personality2.neuroticism) * 0.5f;
                intensity += avgNeuroticism * 0.2f;
            }
            
            // Stress factors
            var stress1 = GetAgentStress(agent1);
            var stress2 = GetAgentStress(agent2);
            intensity += (stress1 + stress2) * 0.25f;
            
            // Mood factors
            var mood1 = agent1.GetComponent<AgentMoodSystem>();
            var mood2 = agent2.GetComponent<AgentMoodSystem>();
            
            if (mood1 != null && mood1.IsInConflictMood)
            {
                intensity += mood1.MoodIntensity * 0.2f;
            }
            
            if (mood2 != null && mood2.IsInConflictMood)
            {
                intensity += mood2.MoodIntensity * 0.2f;
            }
            
            return Mathf.Clamp(intensity, 0.1f, maxConflictIntensity);
        }
        
        private void UpdateActiveConflicts()
        {
            for (int i = activeConflicts.Count - 1; i >= 0; i--)
            {
                var conflict = activeConflicts[i];
                
                if (!conflict.isActive || conflict.agent1 == null || conflict.agent2 == null)
                {
                    activeConflicts.RemoveAt(i);
                    continue;
                }
                
                // Check for escalation
                if (ShouldConflictEscalate(conflict))
                {
                    EscalateConflict(conflict);
                }
                
                // Check for natural de-escalation
                if (ShouldConflictDeEscalate(conflict))
                {
                    DeEscalateConflict(conflict);
                }
                
                // Check for resolution
                if (ShouldConflictResolve(conflict))
                {
                    ResolveConflict(conflict);
                    activeConflicts.RemoveAt(i);
                }
            }
        }
        
        private bool ShouldConflictEscalate(ActiveConflict conflict)
        {
            var duration = (float)(DateTime.UtcNow - conflict.startTime).TotalSeconds;
            
            // Escalation probability increases with time and intensity
            var escalationProb = escalationProbability * (conflict.intensity / maxConflictIntensity);
            escalationProb *= (duration / 60f) * 0.1f; // Increases over time
            
            // Low agreeableness agents escalate more
            var personality1 = conflict.agent1.GetComponent<AgentPersonality>();
            var personality2 = conflict.agent2.GetComponent<AgentPersonality>();
            
            if (personality1 != null && personality2 != null)
            {
                var avgAgreeableness = (personality1.agreeableness + personality2.agreeableness) * 0.5f;
                escalationProb *= (1.5f - avgAgreeableness);
            }
            
            return UnityEngine.Random.value < escalationProb;
        }
        
        private bool ShouldConflictDeEscalate(ActiveConflict conflict)
        {
            var duration = (float)(DateTime.UtcNow - conflict.startTime).TotalSeconds;
            var deEscalationProb = deEscalationRate * (duration / 60f); // Increases with time
            
            // High agreeableness agents de-escalate more
            var personality1 = conflict.agent1.GetComponent<AgentPersonality>();
            var personality2 = conflict.agent2.GetComponent<AgentPersonality>();
            
            if (personality1 != null && personality2 != null)
            {
                var avgAgreeableness = (personality1.agreeableness + personality2.agreeableness) * 0.5f;
                deEscalationProb *= (0.5f + avgAgreeableness);
            }
            
            return UnityEngine.Random.value < deEscalationProb;
        }
        
        private bool ShouldConflictResolve(ActiveConflict conflict)
        {
            var duration = (float)(DateTime.UtcNow - conflict.startTime).TotalSeconds;
            
            // Conflicts naturally resolve after some time
            if (duration > conflictMemoryDuration)
            {
                return true;
            }
            
            // Low intensity conflicts resolve faster
            if (conflict.intensity < 0.3f && duration > 60f)
            {
                return UnityEngine.Random.value < 0.3f;
            }
            
            return false;
        }
        
        private void EscalateConflict(ActiveConflict conflict)
        {
            conflict.escalationLevel = conflict.escalationLevel switch
            {
                ConflictEscalationLevel.Initial => ConflictEscalationLevel.Heated,
                ConflictEscalationLevel.Heated => ConflictEscalationLevel.Intense,
                ConflictEscalationLevel.Intense => ConflictEscalationLevel.Hostile,
                _ => conflict.escalationLevel
            };
            
            conflict.intensity = Mathf.Min(conflict.intensity * 1.3f, maxConflictIntensity);
            
            // Apply escalated mood impacts
            ApplyConflictMoodImpact(conflict.agent1, conflict);
            ApplyConflictMoodImpact(conflict.agent2, conflict);
            
            // Increase stress
            IncreaseAgentStress(conflict.agent1, 0.2f);
            IncreaseAgentStress(conflict.agent2, 0.2f);
            
            var conflictEvent = new ConflictEvent
            {
                timestamp = DateTime.UtcNow,
                agent1Id = conflict.agent1.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID ?? "Unknown",
                agent2Id = conflict.agent2.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID ?? "Unknown",
                conflictType = conflict.conflictType,
                intensity = conflict.intensity,
                escalationLevel = conflict.escalationLevel,
                outcome = ConflictOutcome.Escalated,
                context = currentContext.ToString(),
                triggerReason = "Natural escalation"
            };
            
            conflictHistory.Add(conflictEvent);
            OnConflictEscalated?.Invoke(conflictEvent);
            
            Debug.Log($"[Conflict Manager] Conflict escalated: {conflict.agent1.name} vs {conflict.agent2.name} " +
                     $"to {conflict.escalationLevel} (Intensity: {(float)conflict.intensity:F2})");
        }
        
        private void DeEscalateConflict(ActiveConflict conflict)
        {
            conflict.intensity = Mathf.Max(conflict.intensity * 0.8f, 0.1f);
            
            if (conflict.intensity < 0.3f)
            {
                conflict.escalationLevel = ConflictEscalationLevel.Initial;
            }
            
            Debug.Log($"[Conflict Manager] Conflict de-escalated: {conflict.agent1.name} vs {conflict.agent2.name} " +
                     $"(Intensity: {(float)conflict.intensity:F2})");
        }
        
        private void ResolveConflict(ActiveConflict conflict)
        {
            conflict.isActive = false;
            conflict.endTime = DateTime.UtcNow;
            
            // Determine outcome
            var outcome = DetermineConflictOutcome(conflict);
            
            // Apply resolution effects
            ApplyConflictResolutionEffects(conflict, outcome);
            
            var conflictEvent = new ConflictEvent
            {
                timestamp = DateTime.UtcNow,
                agent1Id = conflict.agent1.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID ?? "Unknown",
                agent2Id = conflict.agent2.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID ?? "Unknown",
                conflictType = conflict.conflictType,
                intensity = conflict.intensity,
                escalationLevel = conflict.escalationLevel,
                outcome = outcome,
                context = currentContext.ToString(),
                triggerReason = "Natural resolution"
            };
            
            conflictHistory.Add(conflictEvent);
            OnConflictResolved?.Invoke(conflictEvent);
            
            Debug.Log($"[Conflict Manager] Conflict resolved: {conflict.agent1.name} vs {conflict.agent2.name} " +
                     $"(Outcome: {(float)outcome})");
        }
        
        private ConflictOutcome DetermineConflictOutcome(ActiveConflict conflict)
        {
            // Simple outcome determination based on personalities and escalation
            var personality1 = conflict.agent1.GetComponent<AgentPersonality>();
            var personality2 = conflict.agent2.GetComponent<AgentPersonality>();
            
            if (personality1 != null && personality2 != null)
            {
                var avgAgreeableness = (personality1.agreeableness + personality2.agreeableness) * 0.5f;
                
                if (avgAgreeableness > 0.7f)
                {
                    return ConflictOutcome.Reconciliation;
                }
                else if (conflict.escalationLevel >= ConflictEscalationLevel.Hostile)
                {
                    return ConflictOutcome.Unresolved;
                }
                else if (UnityEngine.Random.value > 0.5f)
                {
                    return ConflictOutcome.Winner_Agent1;
                }
                else
                {
                    return ConflictOutcome.Winner_Agent2;
                }
            }
            
            return ConflictOutcome.Stalemate;
        }
        
        #endregion
        
        #region Mood and Stress Management
        
        private void ApplyConflictMoodImpact(Agent agent, ActiveConflict conflict)
        {
            var moodSystem = agent.GetComponent<AgentMoodSystem>();
            if (moodSystem == null) return;
            
            var conflictTypeText = $"{conflict.conflictType} with {GetOtherAgent(conflict, agent).name}";
            moodSystem.TriggerConflictMood(conflictTypeText, conflict.intensity);
        }
        
        private void ApplyConflictResolutionEffects(ActiveConflict conflict, ConflictOutcome outcome)
        {
            var moodSystem1 = conflict.agent1.GetComponent<AgentMoodSystem>();
            var moodSystem2 = conflict.agent2.GetComponent<AgentMoodSystem>();
            
            switch (outcome)
            {
                case ConflictOutcome.Winner_Agent1:
                    moodSystem1?.TriggerConversationMoodResponse(true, 0.6f);
                    moodSystem2?.TriggerConversationMoodResponse(false, 0.4f);
                    DecreaseAgentStress(conflict.agent1, 0.3f);
                    break;
                    
                case ConflictOutcome.Winner_Agent2:
                    moodSystem2?.TriggerConversationMoodResponse(true, 0.6f);
                    moodSystem1?.TriggerConversationMoodResponse(false, 0.4f);
                    DecreaseAgentStress(conflict.agent2, 0.3f);
                    break;
                    
                case ConflictOutcome.Reconciliation:
                    moodSystem1?.TriggerConversationMoodResponse(true, 0.4f);
                    moodSystem2?.TriggerConversationMoodResponse(true, 0.4f);
                    DecreaseAgentStress(conflict.agent1, 0.5f);
                    DecreaseAgentStress(conflict.agent2, 0.5f);
                    break;
                    
                case ConflictOutcome.Stalemate:
                case ConflictOutcome.Unresolved:
                    DecreaseAgentStress(conflict.agent1, 0.1f);
                    DecreaseAgentStress(conflict.agent2, 0.1f);
                    break;
            }
        }
        
        private void UpdateAgentStressLevels()
        {
            foreach (var kvp in agentConflictStress.ToList())
            {
                var agentId = kvp.Key;
                var currentStress = kvp.Value;
                
                // Natural stress decay
                var newStress = Mathf.Max(0f, currentStress - (deEscalationRate * Time.deltaTime));
                agentConflictStress[agentId] = newStress;
                
                // Find agent and notify of stress change
                var agent = allAgents.FirstOrDefault(a => 
                    a.GetComponent<ResearchValidatedIdentity>()?.UniqueResearchID == agentId);
                
                if (agent != null && Mathf.Abs(currentStress - newStress) > 0.01f)
                {
                    OnAgentStressChanged?.Invoke(agent, newStress);
                }
            }
        }
        
        private float GetAgentStress(Agent agent)
        {
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return 0f;
            
            return agentConflictStress.TryGetValue(identity.UniqueResearchID, out var stress) ? stress : 0f;
        }
        
        private void IncreaseAgentStress(Agent agent, float amount)
        {
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return;
            
            var currentStress = GetAgentStress(agent);
            var newStress = Mathf.Clamp01(currentStress + amount);
            agentConflictStress[identity.UniqueResearchID] = newStress;
            
            OnAgentStressChanged?.Invoke(agent, newStress);
        }
        
        private void DecreaseAgentStress(Agent agent, float amount)
        {
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            if (identity == null) return;
            
            var currentStress = GetAgentStress(agent);
            var newStress = Mathf.Max(0f, currentStress - amount);
            agentConflictStress[identity.UniqueResearchID] = newStress;
            
            OnAgentStressChanged?.Invoke(agent, newStress);
        }
        
        #endregion
        
        #region Utility Methods
        
        private bool AreAgentsInConflict(Agent agent1, Agent agent2)
        {
            return activeConflicts.Any(c => c.isActive && 
                ((c.agent1 == agent1 && c.agent2 == agent2) || 
                 (c.agent1 == agent2 && c.agent2 == agent1)));
        }
        
        private Agent GetOtherAgent(ActiveConflict conflict, Agent agent)
        {
            return conflict.agent1 == agent ? conflict.agent2 : conflict.agent1;
        }
        
        private void AddConflictMemory(Agent agent, Agent otherAgent, string conflictType)
        {
            var identity = agent.GetComponent<ResearchValidatedIdentity>();
            var otherIdentity = otherAgent.GetComponent<ResearchValidatedIdentity>();
            
            if (identity == null || otherIdentity == null) return;
            
            var memoryKey = identity.UniqueResearchID;
            var memoryValue = $"{otherIdentity.UniqueResearchID}:{conflictType}:{DateTime.UtcNow:yyyy-MM-dd HH:mm}";
            
            if (!conflictMemories.ContainsKey(memoryKey))
            {
                conflictMemories[memoryKey] = new List<string>();
            }
            
            conflictMemories[memoryKey].Add(memoryValue);
        }
        
        private void CleanupConflictMemories()
        {
            var cutoffTime = DateTime.UtcNow.AddSeconds(-conflictMemoryDuration);
            
            foreach (var kvp in conflictMemories.ToList())
            {
                var agentId = kvp.Key;
                var memories = kvp.Value;
                
                for (int i = memories.Count - 1; i >= 0; i--)
                {
                    var memoryParts = memories[i].Split(':');
                    if (memoryParts.Length >= 3 && DateTime.TryParse($"{memoryParts[2]} {memoryParts[3]}", out var memoryTime))
                    {
                        if (memoryTime < cutoffTime)
                        {
                            memories.RemoveAt(i);
                        }
                    }
                }
                
                if (memories.Count == 0)
                {
                    conflictMemories.Remove(agentId);
                }
            }
        }
        
        /// <summary>
        /// Get conflict research data for analysis
        /// </summary>
        public ConflictResearchData GetConflictResearchData()
        {
            return new ConflictResearchData
            {
                timestamp = DateTime.UtcNow,
                activeConflictCount = activeConflicts.Count,
                totalConflictsRecorded = conflictHistory.Count,
                averageConflictIntensity = activeConflicts.Count > 0 ? 
                    activeConflicts.Average(c => c.intensity) : 0f,
                conflictsByType = conflictHistory.GroupBy(c => c.conflictType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                conflictsByEscalation = conflictHistory.GroupBy(c => c.escalationLevel)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                agentStressLevels = new Dictionary<string, float>(agentConflictStress),
                recentConflicts = conflictHistory.TakeLast(10).ToList()
            };
        }
        
        private void OnDestroy()
        {
            CancelInvoke();
            AgentGroupManager.OnAllGroupsCreated -= OnAgentGroupsCreated;
        }
        
        #endregion
    }
}