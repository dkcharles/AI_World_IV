using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Data;

namespace AIWorld.Needs
{
    /// <summary>
    /// Manages all needs for an agent and generates desires based on Maslow's hierarchy
    /// Integrates with the agent's personality and decision-making systems
    /// </summary>
    public class NeedsManager : MonoBehaviour
    {
        [Header("Needs Configuration")]
        public bool enableNeedsSystem = true;
        public float needsUpdateInterval = 2f; // Update needs every 2 seconds
        public bool usePersonalityBasedNeeds = true;
        
        [Header("Need Definitions")]
        public List<Need> activeNeeds = new List<Need>();
        
        [Header("State Monitoring")]
        [SerializeField] private Need currentPriorityNeed;
        [SerializeField] private float overallWellbeing;
        [SerializeField] private int urgentNeedsCount;
        
        [Header("Debug")]
        public bool logNeedChanges = true;
        public bool showNeedsInConsole = false;
        
        // Dependencies
        private AgentPersonality personality;
        
        // Internal state
        private float lastNeedsUpdate;
        private Dictionary<string, float> needSatisfactionHistory;
        
        // Events
        public event System.Action<Need> OnNeedBecameUrgent;
        public event System.Action<Need> OnNeedSatisfied;
        public event System.Action<List<Need>> OnNeedsUpdated;
        
        // Properties
        public Need CurrentPriorityNeed => currentPriorityNeed;
        public float OverallWellbeing => overallWellbeing;
        public List<Need> UrgentNeeds => activeNeeds.Where(n => n.IsUrgent).ToList();
        public List<Need> SatisfiedNeeds => activeNeeds.Where(n => n.IsSatisfied).ToList();
        
        private void Awake()
        {
            needSatisfactionHistory = new Dictionary<string, float>();
        }
        
        private void Start()
        {
            // Get personality from Agent component
            var agent = GetComponent<AIWorld.Agents.Agent>();
            personality = agent?.Personality;
            
            if (personality == null)
            {
                Debug.LogWarning($"⚠️ {gameObject.name}: No personality found on Agent component");
            }
            
            // Initialize needs based on personality or defaults
            if (activeNeeds.Count == 0)
            {
                InitializeDefaultNeeds();
            }
            
            // Set up need event handlers
            SetupNeedEventHandlers();
            
            Debug.Log($"🧠 NeedsManager initialized for {gameObject.name} with {activeNeeds.Count} needs");
        }
        
        private void Update()
        {
            if (!enableNeedsSystem) return;
            
            // Update needs at specified interval
            if (Time.time - lastNeedsUpdate >= needsUpdateInterval)
            {
                UpdateAllNeeds();
                lastNeedsUpdate = Time.time;
            }
        }
        
        /// <summary>
        /// Initialize default needs based on agent personality or role
        /// </summary>
        private void InitializeDefaultNeeds()
        {
            if (!usePersonalityBasedNeeds || personality == null)
            {
                activeNeeds = NeedDefinitions.GetResearcherNeeds();
                return;
            }
            
            // Choose needs based on personality role
            switch (personality.role.ToLower())
            {
                case "software engineer":
                case "engineer":
                    activeNeeds = NeedDefinitions.GetEngineerNeeds();
                    break;
                    
                case "ux designer":
                case "designer":
                    activeNeeds = NeedDefinitions.GetDesignerNeeds();
                    break;
                    
                default:
                    activeNeeds = NeedDefinitions.GetResearcherNeeds();
                    break;
            }
            
            // Adjust needs based on personality traits
            AdjustNeedsForPersonality();
        }
        
        /// <summary>
        /// Adjust need parameters based on agent personality traits
        /// </summary>
        private void AdjustNeedsForPersonality()
        {
            if (personality == null) return;
            
            foreach (var need in activeNeeds)
            {
                switch (need.needName)
                {
                    case "Social Connection":
                        // Extraverted agents need more social connection
                        need.importanceMultiplier *= (0.7f + personality.extraversion * 0.6f);
                        need.decayRate *= (0.8f + personality.extraversion * 0.4f);
                        break;
                        
                    case "Achievement":
                        // Conscientious agents value achievement more
                        need.importanceMultiplier *= (0.8f + personality.conscientiousness * 0.4f);
                        break;
                        
                    case "Creativity":
                        // Open agents need more creativity
                        need.importanceMultiplier *= (0.6f + personality.openness * 0.8f);
                        need.decayRate *= (0.7f + personality.openness * 0.6f);
                        break;
                        
                    case "Safety":
                        // Neurotic agents need more safety/security
                        need.importanceMultiplier *= (0.8f + personality.neuroticism * 0.4f);
                        break;
                        
                    case "Knowledge":
                        // Curious agents need more knowledge
                        need.importanceMultiplier *= (0.8f + personality.curiosity * 0.4f);
                        break;
                }
            }
            
            if (logNeedChanges)
            {
                Debug.Log($"🎭 Adjusted needs for {personality.agentName}'s personality traits");
            }
        }
        
        /// <summary>
        /// Set up event handlers for individual needs
        /// </summary>
        private void SetupNeedEventHandlers()
        {
            foreach (var need in activeNeeds)
            {
                need.OnBecameUrgent += HandleNeedBecameUrgent;
                need.OnBecomeSatisfied += HandleNeedSatisfied;
                need.OnCriticalLevel += HandleNeedCritical;
            }
        }
        
        /// <summary>
        /// Update all active needs
        /// </summary>
        private void UpdateAllNeeds()
        {
            float deltaTime = needsUpdateInterval;
            
            foreach (var need in activeNeeds)
            {
                need.UpdateNeed(deltaTime);
            }
            
            // Update derived statistics
            UpdateNeedsStatistics();
            
            // Trigger update event
            OnNeedsUpdated?.Invoke(activeNeeds);
            
            if (showNeedsInConsole)
            {
                LogCurrentNeedsState();
            }
        }
        
        /// <summary>
        /// Update overall statistics about needs state
        /// </summary>
        private void UpdateNeedsStatistics()
        {
            // Calculate overall wellbeing (average satisfaction)
            overallWellbeing = activeNeeds.Count > 0 ? 
                activeNeeds.Average(n => n.currentSatisfaction) : 1f;
            
            // Count urgent needs
            urgentNeedsCount = activeNeeds.Count(n => n.IsUrgent);
            
            // Find current priority need (most urgent)
            currentPriorityNeed = activeNeeds
                .Where(n => n.isActive)
                .OrderByDescending(n => n.GetUrgency())
                .FirstOrDefault();
        }
        
        /// <summary>
        /// Get the most urgent needs for goal generation
        /// </summary>
        public List<Need> GetMostUrgentNeeds(int maxCount = 3)
        {
            return activeNeeds
                .Where(n => n.isActive && !n.IsSatisfied)
                .OrderByDescending(n => n.GetUrgency())
                .Take(maxCount)
                .ToList();
        }
        
        /// <summary>
        /// Get needs that can be satisfied by a specific action
        /// </summary>
        public List<Need> GetNeedsSatisfiableBy(string action)
        {
            return activeNeeds
                .Where(n => n.isActive && 
                           n.satisfactionActions != null && 
                           n.satisfactionActions.Contains(action))
                .ToList();
        }
        
        /// <summary>
        /// Satisfy needs based on an action performed
        /// </summary>
        public void SatisfyNeedsFromAction(string action, float baseAmount = 0.2f)
        {
            var satisfiableNeeds = GetNeedsSatisfiableBy(action);
            
            foreach (var need in satisfiableNeeds)
            {
                // Vary satisfaction amount based on how urgent the need was
                float urgency = need.GetUrgency();
                float adjustedAmount = baseAmount * (0.5f + urgency);
                
                need.SatisfyNeed(adjustedAmount, action);
                
                // Track satisfaction history
                needSatisfactionHistory[need.needName] = 
                    needSatisfactionHistory.GetValueOrDefault(need.needName, 0f) + adjustedAmount;
            }
        }
        
        /// <summary>
        /// Satisfy needs from interacting with other agents
        /// </summary>
        public void SatisfyNeedsFromSocialInteraction(string interactionType, float intensity = 1f)
        {
            var socialNeeds = activeNeeds.Where(n => 
                n.needName.Contains("Social") || 
                n.level == NeedLevel.Love_Belonging);
                
            foreach (var need in socialNeeds)
            {
                float satisfactionAmount = 0.15f * intensity;
                need.SatisfyNeed(satisfactionAmount, $"Social: {interactionType}");
            }
        }
        
        /// <summary>
        /// Generate a motivation prompt for LLM based on current needs
        /// </summary>
        public string GenerateMotivationPrompt()
        {
            if (!enableNeedsSystem || activeNeeds.Count == 0)
            {
                return "";
            }
            
            var urgentNeeds = GetMostUrgentNeeds(2);
            if (urgentNeeds.Count == 0)
            {
                return "You feel generally content and satisfied with your current situation. ";
            }
            
            string prompt = "Your current priorities and feelings: ";
            
            foreach (var need in urgentNeeds)
            {
                switch (need.needName)
                {
                    case "Energy":
                        prompt += "You're feeling tired and need to rest or recharge. ";
                        break;
                    case "Social Connection":
                        prompt += "You're craving meaningful interaction and collaboration with others. ";
                        break;
                    case "Achievement":
                        prompt += "You feel driven to accomplish something significant and gain recognition. ";
                        break;
                    case "Creativity":
                        prompt += "You have a strong urge to express yourself creatively or explore new ideas. ";
                        break;
                    case "Knowledge":
                        prompt += "You're eager to learn something new or deepen your understanding. ";
                        break;
                    case "Safety":
                        prompt += "You're feeling uncertain and need stability or security. ";
                        break;
                }
            }
            
            return prompt;
        }
        
        /// <summary>
        /// Event handlers for need state changes
        /// </summary>
        private void HandleNeedBecameUrgent(Need need)
        {
            if (logNeedChanges)
            {
                Debug.Log($"🚨 {gameObject.name}: {need.needName} became URGENT! ({need.currentSatisfaction:F2})");
            }
            OnNeedBecameUrgent?.Invoke(need);
        }
        
        private void HandleNeedSatisfied(Need need)
        {
            if (logNeedChanges)
            {
                Debug.Log($"😊 {gameObject.name}: {need.needName} is now satisfied ({need.currentSatisfaction:F2})");
            }
            OnNeedSatisfied?.Invoke(need);
        }
        
        private void HandleNeedCritical(Need need)
        {
            if (logNeedChanges)
            {
                Debug.LogWarning($"💀 {gameObject.name}: {need.needName} is at CRITICAL level! ({need.currentSatisfaction:F2})");
            }
        }
        
        /// <summary>
        /// Log current state of all needs
        /// </summary>
        private void LogCurrentNeedsState()
        {
            string needsState = $"🧠 {gameObject.name} Needs Status:\n";
            needsState += $"Overall Wellbeing: {overallWellbeing:F2} | Urgent Needs: {urgentNeedsCount}\n";
            
            foreach (var need in activeNeeds.OrderBy(n => n.level))
            {
                needsState += $"  {need}\n";
            }
            
            Debug.Log(needsState);
        }
        
        /// <summary>
        /// Get formatted needs status for UI or debugging
        /// </summary>
        public string GetNeedsStatusString()
        {
            string status = $"Wellbeing: {overallWellbeing:F2}\n";
            
            foreach (var need in activeNeeds.OrderByDescending(n => n.GetUrgency()))
            {
                status += $"{need}\n";
            }
            
            return status;
        }
        
        #if UNITY_EDITOR
        [Header("Editor Debug")]
        [SerializeField] private string needsStatusDisplay;
        
        private void OnValidate()
        {
            if (Application.isPlaying && activeNeeds != null)
            {
                needsStatusDisplay = GetNeedsStatusString();
            }
        }
        #endif
    }
}