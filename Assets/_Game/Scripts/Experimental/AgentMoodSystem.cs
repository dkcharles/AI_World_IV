using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Research;
using AIWorld.Agents;
using AIWorld.Data;
using AIWorld.BDI;
using AIWorld.Needs;

namespace AIWorld.Experimental
{
    /// <summary>
    /// Advanced mood system enabling emotional states and sentiment variation
    /// Supports conflict generation and realistic personality-driven emotional responses
    /// Designed for Prof Darryl Charles's experimental research platform
    /// </summary>
    public class AgentMoodSystem : MonoBehaviour
    {
        [Header("Current Mood State")]
        [SerializeField] private MoodState currentMood = MoodState.Neutral;
        [SerializeField] private float moodIntensity = 0.5f; // 0-1
        [SerializeField] private float moodStability = 0.7f; // how resistant to mood changes
        
        [Header("Mood Configuration")]
        [SerializeField] private bool enableMoodSystem = true;
        [SerializeField] private float moodUpdateInterval = 60f; // 1 minute
        [SerializeField] private float moodDecayRate = 0.1f; // how fast mood returns to neutral
        [SerializeField] private float personalityMoodInfluence = 0.6f;
        
        [Header("Conflict and Social Influence")]
        [SerializeField] private float conflictMoodImpact = 0.8f;
        [SerializeField] private float socialMoodInfluence = 0.4f;
        [SerializeField] private float successMoodBoost = 0.6f;
        [SerializeField] private float failureMoodPenalty = 0.5f;
        
        [Header("Conversation Impact")]
        [SerializeField] private float positiveConversationBoost = 0.3f;
        [SerializeField] private float negativeConversationPenalty = 0.4f;
        [SerializeField] private float moodConversationInfluence = 0.7f; // how much mood affects conversation tone
        
        // Component references
        private Agent agent;
        private AgentPersonality personality;
        private ResearchValidatedIdentity identity;
        private BDIEngine bdiEngine;
        
        // Mood tracking for research
        private List<MoodEvent> moodHistory = new List<MoodEvent>();
        private Dictionary<string, float> moodTriggers = new Dictionary<string, float>();
        private float lastMoodUpdate;
        
        // Social mood influences
        private Dictionary<string, float> socialMoodInfluences = new Dictionary<string, float>();
        
        // Events
        public static event Action<AgentMoodSystem, MoodState, MoodState> OnMoodChanged;
        public static event Action<AgentMoodSystem, string, float> OnMoodEvent;
        
        public MoodState CurrentMood => currentMood;
        public float MoodIntensity => moodIntensity;
        public float MoodStability => moodStability;
        public bool IsInConflictMood => currentMood == MoodState.Angry || currentMood == MoodState.Frustrated || currentMood == MoodState.Hostile;
        public bool IsInPositiveMood => currentMood == MoodState.Happy || currentMood == MoodState.Excited || currentMood == MoodState.Content;
        
        private void Awake()
        {
            agent = GetComponent<Agent>();
            personality = GetComponent<AgentPersonality>();
            identity = GetComponent<ResearchValidatedIdentity>();
            bdiEngine = GetComponent<BDIEngine>();
        }
        
        private void Start()
        {
            InitializeMoodSystem();
        }
        
        private void Update()
        {
            if (enableMoodSystem && Time.time - lastMoodUpdate >= moodUpdateInterval)
            {
                UpdateMoodSystem();
                lastMoodUpdate = Time.time;
            }
        }
        
        #region Initialization
        
        private void InitializeMoodSystem()
        {
            // Set initial mood based on personality
            if (personality != null)
            {
                InitializeMoodFromPersonality();
            }
            else
            {
                currentMood = MoodState.Neutral;
                moodIntensity = 0.5f;
            }
            
            // Set mood stability based on emotional stability trait
            if (personality != null)
            {
                moodStability = Mathf.Clamp01(1.0f - personality.neuroticism + 0.3f);
            }
            
            LogMoodEvent("InitialMood", moodIntensity, $"Agent initialized with mood: {currentMood}");
            
            Debug.Log($"[Mood System] {identity?.GetCasualName()} initialized with mood: {currentMood} " +
                     $"(Intensity: {moodIntensity:F2}, Stability: {moodStability:F2})");
        }
        
        private void InitializeMoodFromPersonality()
        {
            // Higher extraversion -> more likely to start happy
            if (personality.extraversion > 0.7f)
            {
                currentMood = UnityEngine.Random.value > 0.5f ? MoodState.Happy : MoodState.Excited;
                moodIntensity = 0.6f + (personality.extraversion - 0.7f);
            }
            // Higher neuroticism -> more likely to start anxious or frustrated
            else if (personality.neuroticism > 0.7f)
            {
                currentMood = UnityEngine.Random.value > 0.5f ? MoodState.Anxious : MoodState.Frustrated;
                moodIntensity = 0.5f + (personality.neuroticism - 0.7f);
            }
            // Balanced personality -> neutral or content
            else
            {
                currentMood = UnityEngine.Random.value > 0.7f ? MoodState.Content : MoodState.Neutral;
                moodIntensity = 0.4f + UnityEngine.Random.Range(0f, 0.3f);
            }
        }
        
        #endregion
        
        #region Mood Updates
        
        private void UpdateMoodSystem()
        {
            // Natural mood decay towards neutral
            ApplyMoodDecay();
            
            // Update mood based on current needs satisfaction
            UpdateMoodFromNeeds();
            
            // Update mood based on recent social interactions
            UpdateMoodFromSocialInfluences();
            
            // Update mood based on recent achievements/failures
            UpdateMoodFromBDIOutcomes();
            
            // Apply personality-based mood modulation
            ApplyPersonalityMoodInfluence();
            
            // Normalize mood intensity
            moodIntensity = Mathf.Clamp01(moodIntensity);
        }
        
        private void ApplyMoodDecay()
        {
            // Mood naturally decays towards neutral over time
            if (currentMood != MoodState.Neutral)
            {
                var decayAmount = moodDecayRate * Time.deltaTime * (1.0f - moodStability);
                moodIntensity -= decayAmount;
                
                if (moodIntensity <= 0.2f)
                {
                    TransitionToMood(MoodState.Neutral, 0.4f, "Natural mood decay");
                }
            }
        }
        
        private void UpdateMoodFromNeeds()
        {
            if (agent?.Needs == null) return;
            
            var needStates = agent.Needs.activeNeeds.ToDictionary(n => n.needName, n => n.currentSatisfaction);
            var averageNeedSatisfaction = needStates.Values.Average();
            
            // Very low need satisfaction leads to negative moods
            if (averageNeedSatisfaction < 0.3f)
            {
                var moodPenalty = (0.3f - averageNeedSatisfaction) * 2f;
                ApplyMoodChange(-moodPenalty, "Low need satisfaction");
                
                // Specific mood based on which needs are lowest
                var lowestNeed = needStates.OrderBy(kvp => kvp.Value).First().Key;
                TriggerNeedBasedMood(lowestNeed);
            }
            // High need satisfaction leads to positive moods
            else if (averageNeedSatisfaction > 0.8f)
            {
                var moodBoost = (averageNeedSatisfaction - 0.8f) * 1.5f;
                ApplyMoodChange(moodBoost, "High need satisfaction");
                
                if (currentMood == MoodState.Neutral || IsInNegativeMood())
                {
                    TransitionToMood(MoodState.Content, 0.6f, "Needs well satisfied");
                }
            }
        }
        
        private void TriggerNeedBasedMood(string needType)
        {
            switch (needType.ToLower())
            {
                case "energy":
                    if (currentMood != MoodState.Tired)
                        TransitionToMood(MoodState.Tired, 0.6f, "Energy need critical");
                    break;
                case "social":
                    if (currentMood != MoodState.Lonely)
                        TransitionToMood(MoodState.Lonely, 0.5f, "Social need critical");
                    break;
                case "achievement":
                    if (currentMood != MoodState.Frustrated)
                        TransitionToMood(MoodState.Frustrated, 0.7f, "Achievement need critical");
                    break;
                case "safety":
                    if (currentMood != MoodState.Anxious)
                        TransitionToMood(MoodState.Anxious, 0.8f, "Safety need critical");
                    break;
            }
        }
        
        private void UpdateMoodFromSocialInfluences()
        {
            // Apply accumulated social mood influences
            foreach (var influence in socialMoodInfluences.ToList())
            {
                var decayedInfluence = influence.Value * (1.0f - Time.deltaTime * 0.1f);
                socialMoodInfluences[influence.Key] = decayedInfluence;
                
                if (decayedInfluence < 0.1f)
                {
                    socialMoodInfluences.Remove(influence.Key);
                }
            }
            
            // Calculate net social influence
            var netSocialInfluence = socialMoodInfluences.Values.Sum();
            if (Mathf.Abs(netSocialInfluence) > 0.2f)
            {
                ApplyMoodChange(netSocialInfluence * socialMoodInfluence, "Social influences");
            }
        }
        
        private void UpdateMoodFromBDIOutcomes()
        {
            // BDIEngine.GetRecentlyCompletedIntentions does not exist. Remove or comment out any such calls.
        }
        
        private void ApplyPersonalityMoodInfluence()
        {
            if (personality == null) return;
            
            // High neuroticism makes negative moods more intense and last longer
            if (IsInNegativeMood() && personality.neuroticism > 0.6f)
            {
                moodIntensity += personality.neuroticism * personalityMoodInfluence * 0.1f;
            }
            
            // High extraversion provides resistance to negative moods
            if (IsInNegativeMood() && personality.extraversion > 0.6f)
            {
                moodIntensity -= personality.extraversion * personalityMoodInfluence * 0.05f;
            }
            
            // Low agreeableness increases likelihood of hostile moods in conflict
            if (currentMood == MoodState.Frustrated && personality.agreeableness < 0.4f)
            {
                if (UnityEngine.Random.value > 0.7f)
                {
                    TransitionToMood(MoodState.Hostile, 0.7f, "Low agreeableness escalation");
                }
            }
        }
        
        #endregion
        
        #region Mood Triggers and Events
        
        /// <summary>
        /// Apply a direct mood change from external events
        /// </summary>
        public void ApplyMoodChange(float moodChange, string reason)
        {
            var oldIntensity = moodIntensity;
            moodIntensity += moodChange * (1.0f - moodStability);
            moodIntensity = Mathf.Clamp01(moodIntensity);
            
            LogMoodEvent(reason, moodChange, $"Mood intensity changed: {oldIntensity:F2} -> {moodIntensity:F2}");
            
            // Check if mood change is significant enough to trigger mood state change
            if (Mathf.Abs(moodChange) > 0.3f)
            {
                EvaluateMoodStateChange(moodChange, reason);
            }
        }
        
        /// <summary>
        /// Trigger a conflict-related mood change
        /// </summary>
        public void TriggerConflictMood(string conflictType, float intensity)
        {
            var moodChange = -conflictMoodImpact * intensity;
            ApplyMoodChange(moodChange, $"Conflict: {conflictType}");
            
            // Determine conflict mood based on personality and conflict type
            var conflictMood = DetermineConflictMood(conflictType);
            TransitionToMood(conflictMood, intensity, $"Conflict response: {conflictType}");
        }
        
        private MoodState DetermineConflictMood(string conflictType)
        {
            if (personality == null) return MoodState.Frustrated;
            
            // Low agreeableness -> more likely to become hostile
            if (personality.agreeableness < 0.4f)
            {
                return UnityEngine.Random.value > 0.5f ? MoodState.Hostile : MoodState.Angry;
            }
            // High neuroticism -> more likely to become anxious
            else if (personality.neuroticism > 0.6f)
            {
                return UnityEngine.Random.value > 0.6f ? MoodState.Anxious : MoodState.Frustrated;
            }
            // Default to frustrated for most conflicts
            else
            {
                return MoodState.Frustrated;
            }
        }
        
        /// <summary>
        /// Apply social mood influence from another agent
        /// </summary>
        public void ApplySocialMoodInfluence(AgentMoodSystem otherAgent, float influenceStrength)
        {
            if (otherAgent == null || !enableMoodSystem) return;
            
            var otherAgentId = otherAgent.identity?.UniqueResearchID ?? "Unknown";
            
            // Positive moods are somewhat contagious
            if (otherAgent.IsInPositiveMood)
            {
                var positiveInfluence = influenceStrength * 0.3f;
                socialMoodInfluences[otherAgentId] = positiveInfluence;
                LogMoodEvent("SocialPositive", positiveInfluence, 
                           $"Positive social influence from {otherAgent.identity?.GetCasualName()}");
            }
            // Negative moods can also spread, especially with high neuroticism
            else if (otherAgent.IsInNegativeMood() && personality?.neuroticism > 0.5f)
            {
                var negativeInfluence = -influenceStrength * 0.2f * personality.neuroticism;
                socialMoodInfluences[otherAgentId] = negativeInfluence;
                LogMoodEvent("SocialNegative", negativeInfluence,
                           $"Negative social influence from {otherAgent.identity?.GetCasualName()}");
            }
        }
        
        /// <summary>
        /// Trigger mood response to conversation outcomes
        /// </summary>
        public void TriggerConversationMoodResponse(bool conversationWasPositive, float intensity)
        {
            if (conversationWasPositive)
            {
                ApplyMoodChange(positiveConversationBoost * intensity, "Positive conversation");
                
                // Good conversations can lift mood
                if (IsInNegativeMood() && intensity > 0.7f)
                {
                    TransitionToMood(MoodState.Content, 0.5f, "Uplifting conversation");
                }
            }
            else
            {
                ApplyMoodChange(-negativeConversationPenalty * intensity, "Negative conversation");
                
                // Bad conversations can worsen mood
                if (intensity > 0.6f && UnityEngine.Random.value > 0.6f)
                {
                    var negMood = personality?.agreeableness < 0.5f ? MoodState.Frustrated : MoodState.Disappointed;
                    TransitionToMood(negMood, 0.5f, "Disappointing conversation");
                }
            }
        }
        
        #endregion
        
        #region Mood State Management
        
        private void TransitionToMood(MoodState newMood, float intensity, string reason)
        {
            if (currentMood == newMood) return;
            
            var oldMood = currentMood;
            currentMood = newMood;
            moodIntensity = Mathf.Clamp01(intensity);
            
            LogMoodEvent("MoodTransition", intensity, $"{oldMood} -> {newMood}: {reason}");
            
            OnMoodChanged?.Invoke(this, oldMood, newMood);
            
            Debug.Log($"[Mood System] {identity?.GetCasualName()} mood changed: {oldMood} -> {newMood} " +
                     $"(Intensity: {moodIntensity:F2}) - {reason}");
        }
        
        private void EvaluateMoodStateChange(float moodChange, string reason)
        {
            // Significant positive mood change
            if (moodChange > 0.3f && moodIntensity > 0.7f)
            {
                if (IsInNegativeMood() || currentMood == MoodState.Neutral)
                {
                    TransitionToMood(MoodState.Happy, moodIntensity, reason);
                }
            }
            // Significant negative mood change
            else if (moodChange < -0.3f && moodIntensity > 0.6f)
            {
                if (IsInPositiveMood || currentMood == MoodState.Neutral)
                {
                    TransitionToMood(MoodState.Frustrated, moodIntensity, reason);
                }
            }
        }
        
        public bool IsInNegativeMood()
        {
            return currentMood == MoodState.Angry || currentMood == MoodState.Frustrated || 
                   currentMood == MoodState.Hostile || currentMood == MoodState.Anxious || 
                   currentMood == MoodState.Disappointed || currentMood == MoodState.Lonely ||
                   currentMood == MoodState.Tired;
        }
        
        #endregion
        
        #region Conversation Influence
        
        /// <summary>
        /// Get conversation sentiment modifier based on current mood
        /// </summary>
        public ConversationMoodModifier GetConversationMoodModifier()
        {
            var modifier = new ConversationMoodModifier
            {
                sentimentBias = GetSentimentBias(),
                aggressionLevel = GetAggressionLevel(),
                cooperationWillingness = GetCooperationWillingness(),
                emotionalExpression = GetEmotionalExpression(),
                conflictTolerance = GetConflictTolerance()
            };
            
            return modifier;
        }
        
        private float GetSentimentBias()
        {
            return currentMood switch
            {
                MoodState.Happy => 0.8f * moodIntensity,
                MoodState.Excited => 0.9f * moodIntensity,
                MoodState.Content => 0.5f * moodIntensity,
                MoodState.Neutral => 0f,
                MoodState.Frustrated => -0.6f * moodIntensity,
                MoodState.Angry => -0.8f * moodIntensity,
                MoodState.Hostile => -0.9f * moodIntensity,
                MoodState.Anxious => -0.4f * moodIntensity,
                MoodState.Disappointed => -0.5f * moodIntensity,
                MoodState.Lonely => -0.3f * moodIntensity,
                MoodState.Tired => -0.2f * moodIntensity,
                _ => 0f
            };
        }
        
        private float GetAggressionLevel()
        {
            var baseAggression = personality?.agreeableness < 0.5f ? 0.3f : 0.1f;
            
            return currentMood switch
            {
                MoodState.Angry => baseAggression + (0.7f * moodIntensity),
                MoodState.Hostile => baseAggression + (0.9f * moodIntensity),
                MoodState.Frustrated => baseAggression + (0.4f * moodIntensity),
                _ => baseAggression
            };
        }
        
        private float GetCooperationWillingness()
        {
            var baseCooperation = personality?.agreeableness ?? 0.5f;
            
            return currentMood switch
            {
                MoodState.Happy => baseCooperation + (0.3f * moodIntensity),
                MoodState.Content => baseCooperation + (0.2f * moodIntensity),
                MoodState.Hostile => baseCooperation - (0.6f * moodIntensity),
                MoodState.Angry => baseCooperation - (0.4f * moodIntensity),
                MoodState.Frustrated => baseCooperation - (0.3f * moodIntensity),
                _ => baseCooperation
            };
        }
        
        private float GetEmotionalExpression()
        {
            var baseExpression = personality?.extraversion ?? 0.5f;
            
            return currentMood switch
            {
                MoodState.Excited => baseExpression + (0.4f * moodIntensity),
                MoodState.Angry => baseExpression + (0.5f * moodIntensity),
                MoodState.Happy => baseExpression + (0.3f * moodIntensity),
                MoodState.Anxious => baseExpression - (0.2f * moodIntensity),
                _ => baseExpression
            };
        }
        
        private float GetConflictTolerance()
        {
            var baseTolerance = (personality?.agreeableness ?? 0.5f) + (1.0f - (personality?.neuroticism ?? 0.5f));
            baseTolerance *= 0.5f; // Normalize
            
            return currentMood switch
            {
                MoodState.Content => baseTolerance + (0.3f * moodIntensity),
                MoodState.Happy => baseTolerance + (0.2f * moodIntensity),
                MoodState.Frustrated => baseTolerance - (0.4f * moodIntensity),
                MoodState.Angry => baseTolerance - (0.6f * moodIntensity),
                MoodState.Hostile => baseTolerance - (0.8f * moodIntensity),
                _ => baseTolerance
            };
        }
        
        #endregion
        
        #region Research Data and Logging
        
        private void LogMoodEvent(string eventType, float intensity, string description)
        {
            var moodEvent = new MoodEvent
            {
                timestamp = DateTime.UtcNow,
                agentId = identity?.UniqueResearchID ?? "Unknown",
                eventType = eventType,
                moodBefore = currentMood,
                moodAfter = currentMood,
                intensityChange = intensity,
                description = description,
                personalityContext = personality != null ? new PersonalitySnapshot
                {
                    extraversion = personality.extraversion,
                    agreeableness = personality.agreeableness,
                    neuroticism = personality.neuroticism
                } : null
            };
            
            moodHistory.Add(moodEvent);
            
            // Keep only recent mood events (last 100)
            if (moodHistory.Count > 100)
            {
                moodHistory.RemoveAt(0);
            }
            
            OnMoodEvent?.Invoke(this, eventType, intensity);
        }
        
        /// <summary>
        /// Get mood data for research analysis
        /// </summary>
        public AgentMoodResearchData GetMoodResearchData()
        {
            return new AgentMoodResearchData
            {
                agentId = identity?.UniqueResearchID ?? "Unknown",
                currentMoodState = currentMood,
                moodIntensity = moodIntensity,
                moodStability = moodStability,
                moodHistory = new List<MoodEvent>(moodHistory),
                personalityInfluence = personality != null ? new PersonalitySnapshot
                {
                    extraversion = personality.extraversion,
                    agreeableness = personality.agreeableness,
                    neuroticism = personality.neuroticism
                } : null,
                conversationMoodModifier = GetConversationMoodModifier()
            };
        }
        
        #endregion
    }
    
    #region Data Structures
    
    public enum MoodState
    {
        Neutral,
        Happy,
        Excited,
        Content,
        Frustrated,
        Angry,
        Hostile,
        Anxious,
        Disappointed,
        Lonely,
        Tired
    }
    
    [System.Serializable]
    public class ConversationMoodModifier
    {
        public float sentimentBias;        // -1 to 1, affects conversation tone
        public float aggressionLevel;      // 0 to 1, likelihood of aggressive responses
        public float cooperationWillingness; // 0 to 1, willingness to cooperate
        public float emotionalExpression;  // 0 to 1, how emotionally expressive
        public float conflictTolerance;    // 0 to 1, tolerance for disagreement
    }
    
    [System.Serializable]
    public class MoodEvent
    {
        public DateTime timestamp;
        public string agentId;
        public string eventType;
        public MoodState moodBefore;
        public MoodState moodAfter;
        public float intensityChange;
        public string description;
        public PersonalitySnapshot personalityContext;
    }
    
    [System.Serializable]
    public class PersonalitySnapshot
    {
        public float extraversion;
        public float agreeableness;
        public float neuroticism;
    }
    
    [System.Serializable]
    public class AgentMoodResearchData
    {
        public string agentId;
        public MoodState currentMoodState;
        public float moodIntensity;
        public float moodStability;
        public List<MoodEvent> moodHistory;
        public PersonalitySnapshot personalityInfluence;
        public ConversationMoodModifier conversationMoodModifier;
    }
    
    #endregion
}