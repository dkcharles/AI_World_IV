using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIWorld.Needs
{
    /// <summary>
    /// Maslow's hierarchy of needs implementation for AI agents
    /// Provides intrinsic motivation and goal-directed behaviour
    /// </summary>
    public enum NeedLevel
    {
        Physiological = 0,    // Food, water, rest, energy
        Safety = 1,           // Shelter, security, predictability, health
        Love_Belonging = 2,   // Social connection, friendship, cooperation, communication
        Esteem = 3,          // Recognition, achievement, competence, respect
        SelfActualization = 4 // Creativity, problem-solving, meaning, personal growth
    }

    /// <summary>
    /// Individual need that can motivate agent behaviour
    /// </summary>
    [System.Serializable]
    public class Need
    {
        [Header("Need Identity")]
        public string needName;
        public NeedLevel level;
        public string description;
        
        [Header("Current State")]
        [Range(0f, 1f)] public float currentSatisfaction = 1f; // 0 = desperate, 1 = completely satisfied
        [Range(0f, 1f)] public float urgencyThreshold = 0.3f;  // Below this = urgent need
        [Range(0f, 1f)] public float satisfiedThreshold = 0.8f; // Above this = need is satisfied
        
        [Header("Dynamics")]
        [Range(0f, 0.1f)] public float decayRate = 0.01f; // How fast need increases over time
        [Range(0f, 2f)] public float importanceMultiplier = 1f; // How important this need is for this agent
        public bool isActive = true; // Whether this need affects the agent
        
        [Header("Satisfaction Actions")]
        public string[] satisfactionActions; // Actions that can satisfy this need
        public string[] relatedObjects;      // Objects that help satisfy this need
        public string[] relatedAgents;       // Agent types that help satisfy this need
        
        [Header("State Tracking")]
        public float timeSinceLastSatisfied;
        public float lastSatisfactionAmount;
        public DateTime lastUpdated;
        
        // Events
        public event System.Action<Need> OnBecameUrgent;
        public event System.Action<Need> OnBecomeSatisfied;
        public event System.Action<Need> OnCriticalLevel;
        
        public Need()
        {
            lastUpdated = DateTime.Now;
        }
        
        public Need(string name, NeedLevel level, string description) : this()
        {
            this.needName = name;
            this.level = level;
            this.description = description;
        }
        
        /// <summary>
        /// Update need satisfaction over time
        /// </summary>
        public void UpdateNeed(float deltaTime)
        {
            if (!isActive) return;
            
            float previousSatisfaction = currentSatisfaction;
            
            // Decay satisfaction over time
            currentSatisfaction = Mathf.Max(0f, currentSatisfaction - (decayRate * deltaTime));
            timeSinceLastSatisfied += deltaTime;
            lastUpdated = DateTime.Now;
            
            // Check for state changes
            CheckStateTransitions(previousSatisfaction);
        }
        
        /// <summary>
        /// Satisfy this need by a specific amount
        /// </summary>
        public void SatisfyNeed(float amount, string source = "")
        {
            float previousSatisfaction = currentSatisfaction;
            currentSatisfaction = Mathf.Min(1f, currentSatisfaction + amount);
            lastSatisfactionAmount = amount;
            timeSinceLastSatisfied = 0f;
            lastUpdated = DateTime.Now;
            
            CheckStateTransitions(previousSatisfaction);
            
            Debug.Log($"🎯 Need '{needName}' satisfied by {amount:F2} from {source}. New level: {currentSatisfaction:F2}");
        }
        
        /// <summary>
        /// Check for important state transitions and fire events
        /// </summary>
        private void CheckStateTransitions(float previousSatisfaction)
        {
            // Became urgent
            if (previousSatisfaction > urgencyThreshold && currentSatisfaction <= urgencyThreshold)
            {
                OnBecameUrgent?.Invoke(this);
            }
            
            // Became satisfied
            if (previousSatisfaction < satisfiedThreshold && currentSatisfaction >= satisfiedThreshold)
            {
                OnBecomeSatisfied?.Invoke(this);
            }
            
            // Critical level (very low)
            if (currentSatisfaction <= 0.1f && previousSatisfaction > 0.1f)
            {
                OnCriticalLevel?.Invoke(this);
            }
        }
        
        /// <summary>
        /// Get the urgency of this need (0-1, higher = more urgent)
        /// </summary>
        public float GetUrgency()
        {
            if (!isActive) return 0f;
            
            float urgency = (1f - currentSatisfaction) * importanceMultiplier;
            
            // Maslow's hierarchy - lower level needs are more urgent when unsatisfied
            float hierarchyMultiplier = (5 - (int)level) / 5f; // Higher for lower levels
            urgency *= hierarchyMultiplier;
            
            return Mathf.Clamp01(urgency);
        }
        
        /// <summary>
        /// Check if this need is currently urgent
        /// </summary>
        public bool IsUrgent => isActive && currentSatisfaction <= urgencyThreshold;
        
        /// <summary>
        /// Check if this need is currently satisfied
        /// </summary>
        public bool IsSatisfied => !isActive || currentSatisfaction >= satisfiedThreshold;
        
        /// <summary>
        /// Get current satisfaction level (0-1)
        /// </summary>
        public float GetSatisfactionLevel()
        {
            return currentSatisfaction;
        }
        
        /// <summary>
        /// Get a description of the current need state
        /// </summary>
        public string GetStateDescription()
        {
            if (!isActive) return "Inactive";
            
            if (currentSatisfaction <= 0.1f) return "Critical";
            if (currentSatisfaction <= urgencyThreshold) return "Urgent";
            if (currentSatisfaction >= satisfiedThreshold) return "Satisfied";
            return "Moderate";
        }
        
        /// <summary>
        /// Get emoji representation of need state
        /// </summary>
        public string GetStateEmoji()
        {
            if (!isActive) return "⚫";
            
            if (currentSatisfaction <= 0.1f) return "🔴";
            if (currentSatisfaction <= urgencyThreshold) return "🟡";
            if (currentSatisfaction >= satisfiedThreshold) return "🟢";
            return "🟠";
        }
        
        /// <summary>
        /// Create a formatted string for debugging
        /// </summary>
        public override string ToString()
        {
            return $"{GetStateEmoji()} {needName}: {currentSatisfaction:F2} ({GetStateDescription()})";
        }
    }
    
    /// <summary>
    /// Collection of predefined needs for easy agent setup
    /// </summary>
    public static class NeedDefinitions
    {
        public static Need CreateEnergyNeed()
        {
            return new Need("Energy", NeedLevel.Physiological, "Physical and mental energy to perform tasks")
            {
                decayRate = 0.008f,
                urgencyThreshold = 0.25f,
                satisfactionActions = new[] { "Rest", "Sleep", "Relax" },
                relatedObjects = new[] { "Chair", "Bed", "CoffeeStation" }
            };
        }
        
        public static Need CreateSocialConnectionNeed()
        {
            return new Need("Social Connection", NeedLevel.Love_Belonging, "Need for meaningful interaction with others")
            {
                decayRate = 0.012f,
                urgencyThreshold = 0.4f,
                satisfactionActions = new[] { "Communicate", "Collaborate", "Help" },
                relatedAgents = new[] { "Any" }
            };
        }
        
        public static Need CreateSafetyNeed()
        {
            return new Need("Safety", NeedLevel.Safety, "Feeling secure and protected in the environment")
            {
                decayRate = 0.005f,
                urgencyThreshold = 0.3f,
                satisfactionActions = new[] { "FindShelter", "AvoidDanger", "SecureArea" },
                relatedObjects = new[] { "SafeZone", "Shelter" }
            };
        }
        
        public static Need CreateAchievementNeed()
        {
            return new Need("Achievement", NeedLevel.Esteem, "Accomplishing goals and gaining recognition")
            {
                decayRate = 0.006f,
                urgencyThreshold = 0.35f,
                satisfactionActions = new[] { "CompleteTask", "SolveProblems", "CreateSomething" },
                relatedObjects = new[] { "Project", "Task", "Challenge" }
            };
        }
        
        public static Need CreateCreativityNeed()
        {
            return new Need("Creativity", NeedLevel.SelfActualization, "Self-expression and creative fulfillment")
            {
                decayRate = 0.004f,
                urgencyThreshold = 0.4f,
                satisfactionActions = new[] { "Create", "Innovate", "Express", "Explore" },
                relatedObjects = new[] { "Canvas", "Workspace", "Tools" }
            };
        }
        
        public static Need CreateKnowledgeNeed()
        {
            return new Need("Knowledge", NeedLevel.SelfActualization, "Learning and understanding new concepts")
            {
                decayRate = 0.007f,
                urgencyThreshold = 0.35f,
                satisfactionActions = new[] { "Learn", "Research", "Discover", "Analyze" },
                relatedObjects = new[] { "Book", "Computer", "Library" },
                relatedAgents = new[] { "Researcher", "Teacher", "Expert" }
            };
        }
        
        /// <summary>
        /// Get a basic set of needs for a research-oriented agent
        /// </summary>
        public static List<Need> GetResearcherNeeds()
        {
            return new List<Need>
            {
                CreateEnergyNeed(),
                CreateSocialConnectionNeed(),
                CreateSafetyNeed(),
                CreateAchievementNeed(),
                CreateKnowledgeNeed(),
                CreateCreativityNeed()
            };
        }
        
        /// <summary>
        /// Get a basic set of needs for a practical-oriented agent
        /// </summary>
        public static List<Need> GetEngineerNeeds()
        {
            var needs = GetResearcherNeeds();
            
            // Modify for engineer personality
            var achievementNeed = needs.Find(n => n.needName == "Achievement");
            if (achievementNeed != null)
            {
                achievementNeed.importanceMultiplier = 1.3f; // Engineers value achievement more
                achievementNeed.decayRate = 0.008f;
            }
            
            var creativityNeed = needs.Find(n => n.needName == "Creativity");
            if (creativityNeed != null)
            {
                creativityNeed.importanceMultiplier = 0.8f; // Less focused on pure creativity
                creativityNeed.satisfactionActions = new[] { "SolveProblem", "Optimize", "Build", "Improve" };
            }
            
            return needs;
        }
        
        /// <summary>
        /// Get a basic set of needs for a creative-oriented agent
        /// </summary>
        public static List<Need> GetDesignerNeeds()
        {
            var needs = GetResearcherNeeds();
            
            // Modify for designer personality
            var creativityNeed = needs.Find(n => n.needName == "Creativity");
            if (creativityNeed != null)
            {
                creativityNeed.importanceMultiplier = 1.4f; // Designers prioritize creativity
                creativityNeed.decayRate = 0.01f;
            }
            
            var socialNeed = needs.Find(n => n.needName == "Social Connection");
            if (socialNeed != null)
            {
                socialNeed.importanceMultiplier = 1.2f; // Designers value collaboration
            }
            
            return needs;
        }
    }
}