using System.Collections.Generic;
using UnityEngine;
using AIWorld.Research;
using AIWorld.Agents;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Shared enums and data structures for the experimental system
    /// Centralized to avoid namespace conflicts and duplication
    /// </summary>
    
    #region Core Enums
    
    public enum SimulationContext
    {
        University,     // Academic "Publish or Perish" environment
        Survival,       // Resource scarcity and cooperation
        Social,         // "Love Island" style social dynamics
        Generic         // Generic social simulation
    }
    
    public enum LocationType
    {
        WorkArea,
        SocialArea,
        ResourceSite,
        ShelterArea,
        KnowledgeArea,
        MeetingArea,
        SafetyArea,
        RelaxationArea,
        IntimateArea,
        GatheringArea
    }
    
    public enum ResourceType
    {
        Currency,
        Tool,
        Consumable,
        Essential,
        Achievement,
        Social,
        Special
    }
    
    public enum DataExportFormat
    {
        JSON,
        CSV,
        XML
    }
    
    public enum ExperimentPhase
    {
        Inactive,
        Preparation,
        WorldGeneration,
        AgentCreation,
        SystemInitialization,
        Running,
        DataCollection,
        Completed,
        Terminated
    }
    
    public enum ExperimentEventType
    {
        ExperimentStarted,
        PhaseStarted,
        PhaseCompleted,
        SystemEvent,
        Error,
        ExperimentCompleted,
        ExperimentStopped,
        ExperimentTerminated
    }
    
    public enum ExperimentTerminationReason
    {
        Completed,
        UserStopped,
        Error,
        SystemFailure,
        TimeoutReached
    }
    
    #endregion
    
    #region Conflict System Data Structures
    
    [System.Serializable]
    public class ActiveConflict
    {
        public string conflictId;
        public Agent agent1;
        public Agent agent2;
        public string conflictType;
        public float intensity;
        public System.DateTime startTime;
        public System.DateTime endTime;
        public ConflictEscalationLevel escalationLevel;
        public bool isActive;
    }
    
    [System.Serializable]
    public class ConflictEvent
    {
        public System.DateTime timestamp;
        public string agent1Id;
        public string agent2Id;
        public string conflictType;
        public float intensity;
        public ConflictEscalationLevel escalationLevel;
        public ConflictOutcome outcome;
        public string context;
        public string triggerReason;
    }
    
    [System.Serializable]
    public class ConflictResearchData
    {
        public System.DateTime timestamp;
        public int activeConflictCount;
        public int totalConflictsRecorded;
        public float averageConflictIntensity;
        public Dictionary<string, int> conflictsByType;
        public Dictionary<string, int> conflictsByEscalation;
        public Dictionary<string, float> agentStressLevels;
        public List<ConflictEvent> recentConflicts;
    }
    
    public enum ConflictEscalationLevel
    {
        Initial,
        Heated,
        Intense,
        Hostile
    }
    
    public enum ConflictOutcome
    {
        Ongoing,
        Winner_Agent1,
        Winner_Agent2,
        Stalemate,
        Reconciliation,
        Escalated,
        Unresolved
    }
    
    #endregion
    
    #region Legacy Data Structures (for compatibility)
    
    [System.Serializable]
    public class ExperimentConfiguration
    {
        // Metadata
        public string experimentName;
        public string researcherName;
        public string institutionName;
        public string studyDescription;
        public string ethicsApproval;
        public System.DateTime createdAt;
        
        // Agent configuration
        public int totalAgentCount;
        public List<ExperimentalGroup> agentGroups;
        public bool enablePersonalityVariation;
        
        // World configuration
        public Vector2 worldSize;
        public List<WorldLocation> worldLocations;
        public List<WorldResource> worldResources;
        public int maxResourceInstances;
        
        // Context configuration
        public SimulationContext simulationContext;
        public ContextConfiguration contextConfig;
        public float simulationDuration;
        
        // Systems configuration
        public bool enableConflictSystem;
        public float conflictProbability;
        public bool enableResourceCompetition;
        public bool enableMoodVariation;
        public MoodConfiguration moodConfig;
        
        // Research configuration
        public bool enableDataLogging;
        public float dataLoggingInterval;
        public bool enableConsciousnessTracking;
    }
    
    [System.Serializable]
    public class ExperimentalGroup
    {
        public string groupName;
        public int agentCount;
        public string llmModel;
        public AgentArchetype archetype;
        public float personalityVariance; // 0-1, how much personality traits vary within group
        public bool enableSpecialBehaviors;
        public string groupDescription;
    }
    
    [System.Serializable]
    public class WorldLocation
    {
        public string name;
        public LocationType type;
        public Vector3 position;
        public int capacity;
        public string[] needsSatisfied;
        public float accessCost;
        public bool requiresPermission;
    }
    
    [System.Serializable]
    public class WorldResource
    {
        public string name;
        public ResourceType type;
        public float scarcity; // 0-1, lower = more scarce
        public int value;
        public float regenerationRate; // per second
        public int maxQuantity;
        public string description;
    }
    
    [System.Serializable]
    public class ContextConfiguration
    {
        public SimulationContext context;
        public float competitionLevel; // 0-1
        public float cooperationBonus; // multiplier for cooperative actions
        public float resourceScarcityMultiplier; // affects resource availability
        public float socialImportance; // how important social connections are
        public List<string> goalTypes; // types of goals available in this context
    }
    
    [System.Serializable]
    public class MoodConfiguration
    {
        public bool enableMoodSystem;
        public float moodChangeFrequency; // seconds between mood updates
        public float moodVolatility; // how much moods change
        public float conflictMoodImpact; // how much conflicts affect mood
        public float successMoodBoost; // mood improvement from success
        public float failureMoodPenalty; // mood decrease from failure
        public float socialMoodInfluence; // how much other agents affect mood
    }
    
    #endregion
    
    #region Research Data Structures
    
    [System.Serializable]
    public class ConflictContextConfig
    {
        public float resourceCompetitionWeight;
        public float personalityClashWeight;
        public float territorialWeight;
        public float goalConflictWeight;
        public float stressEscalationRate;
        public float cooperationPenalty;
        public string[] commonConflictTypes;
    }
    
    [System.Serializable]
    public class WorldGenerationStats
    {
        public SimulationContext worldContext;
        public Vector2 worldSize;
        public int locationCount;
        public int resourceCount;
        public int decorationCount;
        public Dictionary<string, int> locationTypes;
        public Dictionary<string, int> resourceTypes;
    }
    
    [System.Serializable]
    public class LocationKnowledge
    {
        public string name;
        public LocationType type;
        public Vector3 position;
        public int capacity;
        public string accessRequirements;
        public string primaryPurpose;
        public List<string> needsSatisfied;
    }
    
    [System.Serializable]
    public class ResourceKnowledge
    {
        public string name;
        public ResourceType type;
        public int value;
        public float scarcity;
        public float regenerationRate;
        public int availableQuantity;
        public string acquisitionMethod;
        public float competitionLevel;
    }
    
    [System.Serializable]
    public class WorldKnowledgeData
    {
        public SimulationContext worldContext;
        public Vector2 worldSize;
        public List<LocationKnowledge> locations;
        public List<ResourceKnowledge> resources;
        public List<string> contextualRules;
        public List<string> socialDynamics;
        public List<string> objectives;
    }
    
    #endregion
    
    #region Data Collection Flags
    
    [System.Flags]
    public enum DataCollectionFlags
    {
        None = 0,
        MoodChanges = 1 << 0,
        ConflictEvents = 1 << 1,
        ResourceUsage = 1 << 2,
        LocationUsage = 1 << 3,
        ConversationContent = 1 << 4,
        PersonalityChanges = 1 << 5,
        GoalProgression = 1 << 6,
        SocialRelationships = 1 << 7,
        ConsciousnessMetrics = 1 << 8,
        SocialNetworkAnalysis = 1 << 9,
        All = ~0
    }
    
    #endregion
}