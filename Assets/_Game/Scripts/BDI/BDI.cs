using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Needs;

namespace AIWorld.BDI
{
    /// <summary>
    /// Represents an agent's belief about the world
    /// Beliefs can be uncertain and updated with new evidence
    /// </summary>
    [System.Serializable]
    public class Belief
    {
        [Header("Belief Content")]
        public string predicate;           // What the belief is about (e.g., "location_of")
        public string[] arguments;         // Arguments (e.g., ["apple", "kitchen"])
        public string description;         // Human-readable description
        
        [Header("Belief Properties")]
        [Range(0f, 1f)] public float confidence = 1f;  // How confident we are (0-1)
        public BeliefType type = BeliefType.Observation;
        public string source = "self";     // Where this belief came from
        
        [Header("Temporal Info")]
        public DateTime created;
        public DateTime lastUpdated;
        public float timeToLive = -1f;     // -1 means permanent
        
        public Belief()
        {
            created = DateTime.Now;
            lastUpdated = DateTime.Now;
        }
        
        public Belief(string predicate, string[] arguments, float confidence = 1f) : this()
        {
            this.predicate = predicate;
            this.arguments = arguments;
            this.confidence = confidence;
        }
        
        /// <summary>
        /// Update belief confidence based on new evidence
        /// Uses simple Bayesian updating
        /// </summary>
        public void UpdateConfidence(float evidence, float evidenceWeight = 0.3f)
        {
            // Simple Bayesian update: combine prior with new evidence
            confidence = confidence * (1f - evidenceWeight) + evidence * evidenceWeight;
            confidence = Mathf.Clamp01(confidence);
            lastUpdated = DateTime.Now;
        }
        
        /// <summary>
        /// Check if this belief matches a query
        /// </summary>
        public bool Matches(string queryPredicate, string[] queryArguments = null)
        {
            if (predicate != queryPredicate) return false;
            
            if (queryArguments == null) return true;
            
            if (arguments == null || arguments.Length != queryArguments.Length) return false;
            
            for (int i = 0; i < arguments.Length; i++)
            {
                if (queryArguments[i] != "*" && arguments[i] != queryArguments[i])
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if this belief is still valid (hasn't expired)
        /// </summary>
        public bool IsValid()
        {
            if (timeToLive < 0) return true; // Permanent belief
            
            return (DateTime.Now - created).TotalSeconds < timeToLive;
        }
        
        public override string ToString()
        {
            string args = arguments != null ? string.Join(", ", arguments) : "";
            return $"{predicate}({args}) [{confidence:F2}]";
        }
    }
    
    /// <summary>
    /// Represents a desire or goal the agent wants to achieve
    /// Generated from needs, opportunities, or social interactions
    /// </summary>
    [System.Serializable]
    public class Desire
    {
        [Header("Desire Content")]
        public string goalName;
        public string description;
        public DesireType type;
        
        [Header("Desire Properties")]
        [Range(0f, 1f)] public float intensity = 0.5f;    // How much the agent wants this
        [Range(0f, 1f)] public float urgency = 0.5f;      // How soon it needs to be done
        [Range(0f, 1f)] public float feasibility = 1f;    // How achievable it seems
        
        [Header("Source Information")]
        public string sourceNeed;          // Which need generated this desire
        public string[] requiredResources; // What's needed to achieve this
        public string[] satisfactionActions; // Actions that could satisfy this desire
        
        [Header("Temporal Info")]
        public DateTime created;
        public float timeToLive = 300f;    // How long this desire lasts (seconds)
        
        public Desire()
        {
            created = DateTime.Now;
        }
        
        public Desire(string goalName, string description, DesireType type) : this()
        {
            this.goalName = goalName;
            this.description = description;
            this.type = type;
        }
        
        /// <summary>
        /// Calculate the overall priority of this desire
        /// </summary>
        public float GetPriority()
        {
            return intensity * urgency * feasibility;
        }
        
        /// <summary>
        /// Check if this desire is still valid (hasn't expired)
        /// </summary>
        public bool IsValid()
        {
            return (DateTime.Now - created).TotalSeconds < timeToLive;
        }
        
        /// <summary>
        /// Update desire intensity based on current situation
        /// </summary>
        public void UpdateIntensity(float newIntensity, string reason = "")
        {
            intensity = Mathf.Clamp01(newIntensity);
            Debug.Log($"🎯 Desire '{goalName}' intensity updated to {intensity:F2} - {reason}");
        }
        
        public override string ToString()
        {
            return $"{goalName} (Priority: {GetPriority():F2}, Intensity: {intensity:F2})";
        }
    }
    
    /// <summary>
    /// Represents a committed intention to achieve a specific goal
    /// Includes a plan of actions to execute
    /// </summary>
    [System.Serializable]
    public class Intention
    {
        [Header("Intention Content")]
        public string intentionName;
        public string description;
        public Desire sourceDesire;
        
        [Header("Plan Information")]
        public List<string> plannedActions = new List<string>();
        public int currentActionIndex = 0;
        public Dictionary<string, object> parameters = new Dictionary<string, object>();
        
        [Header("Execution State")]
        public IntentionStatus status = IntentionStatus.Active;
        [Range(0f, 1f)] public float progress = 0f;
        [Range(0f, 1f)] public float commitmentStrength = 1f;
        
        [Header("Temporal Info")]
        public DateTime created;
        public DateTime lastActionTime;
        public float maxExecutionTime = 600f; // 10 minutes max
        
        public Intention()
        {
            created = DateTime.Now;
            lastActionTime = DateTime.Now;
        }
        
        public Intention(string name, Desire desire) : this()
        {
            intentionName = name;
            sourceDesire = desire;
            description = $"Achieve: {desire.description}";
        }
        
        /// <summary>
        /// Get the current action to execute
        /// </summary>
        public string GetCurrentAction()
        {
            if (currentActionIndex >= 0 && currentActionIndex < plannedActions.Count)
                return plannedActions[currentActionIndex];
            return null;
        }
        
        /// <summary>
        /// Advance to the next action in the plan
        /// </summary>
        public bool AdvanceToNextAction()
        {
            currentActionIndex++;
            lastActionTime = DateTime.Now;
            
            if (currentActionIndex >= plannedActions.Count)
            {
                status = IntentionStatus.Completed;
                progress = 1f;
                return false;
            }
            
            progress = (float)currentActionIndex / plannedActions.Count;
            return true;
        }
        
        /// <summary>
        /// Check if this intention should be abandoned
        /// </summary>
        public bool ShouldAbandon()
        {
            // Abandon if commitment is too low
            if (commitmentStrength < 0.3f) return true;
            
            // Abandon if taking too long
            if ((DateTime.Now - created).TotalSeconds > maxExecutionTime) return true;
            
            // Abandon if stuck on one action too long
            if ((DateTime.Now - lastActionTime).TotalSeconds > 120f) return true;
            
            return false;
        }
        
        /// <summary>
        /// Reduce commitment strength (e.g., due to obstacles)
        /// </summary>
        public void ReduceCommitment(float amount, string reason = "")
        {
            commitmentStrength = Mathf.Max(0f, commitmentStrength - amount);
            Debug.Log($"💔 Intention '{intentionName}' commitment reduced to {commitmentStrength:F2} - {reason}");
        }
        
        public override string ToString()
        {
            return $"{intentionName} [{status}] - Action {currentActionIndex + 1}/{plannedActions.Count} ({progress:P0})";
        }
    }
    
    /// <summary>
    /// Enums for categorizing beliefs, desires, and intentions
    /// </summary>
    public enum BeliefType
    {
        Observation,    // Direct observation
        Communication,  // Told by another agent
        Inference,      // Inferred from other beliefs
        Assumption,     // Assumed to be true
        Memory         // Remembered from past
    }
    
    public enum DesireType
    {
        Basic,          // From basic needs
        Social,         // From social interactions
        Achievement,    // From accomplishment drives
        Creative,       // From self-actualization
        Maintenance,    // From upkeep requirements
        Opportunistic   // From environmental opportunities
    }
    
    public enum IntentionStatus
    {
        Active,         // Currently being executed
        Suspended,      // Temporarily paused
        Completed,      // Successfully finished
        Failed,         // Failed to complete
        Abandoned       // Deliberately given up
    }
    
    /// <summary>
    /// Helper class for creating common beliefs, desires, and intentions
    /// </summary>
    public static class BDIFactory
    {
        /// <summary>
        /// Create a desire from a need
        /// </summary>
        public static Desire CreateDesireFromNeed(Need need)
        {
            var desire = new Desire();
            
            switch (need.needName)
            {
                case "Energy":
                    desire.goalName = "RestoreEnergy";
                    desire.description = "Find a way to restore energy and feel refreshed";
                    desire.type = DesireType.Basic;
                    desire.satisfactionActions = new[] { "Rest", "Relax", "FindQuietSpace", "UseObject" };
                    break;
                    
                case "Social Connection":
                    desire.goalName = "ConnectWithOthers";
                    desire.description = "Engage in meaningful social interaction";
                    desire.type = DesireType.Social;
                    desire.satisfactionActions = new[] { "StartConversation", "Collaborate", "Help", "FindSocialSpace", "UseObject" };
                    break;
                    
                case "Achievement":
                    desire.goalName = "AccomplishSomething";
                    desire.description = "Complete a task or achieve a goal";
                    desire.type = DesireType.Achievement;
                    desire.satisfactionActions = new[] { "CompleteTask", "SolveProblems", "CreateProject", "UseObject" };
                    break;
                    
                case "Creativity":
                    desire.goalName = "ExpressCreativity";
                    desire.description = "Engage in creative or innovative activity";
                    desire.type = DesireType.Creative;
                    desire.satisfactionActions = new[] { "Create", "Brainstorm", "Innovate", "Design", "UseObject" };
                    break;
                    
                case "Knowledge":
                    desire.goalName = "LearnSomething";
                    desire.description = "Acquire new knowledge or understanding";
                    desire.type = DesireType.Achievement;
                    desire.satisfactionActions = new[] { "Research", "Ask", "Explore", "Analyze", "UseObject" };
                    break;
                    
                case "Safety":
                    desire.goalName = "EnsureSafety";
                    desire.description = "Feel secure and protected";
                    desire.type = DesireType.Basic;
                    desire.satisfactionActions = new[] { "FindSafeSpace", "AvoidRisks", "SecureEnvironment", "UseObject" };
                    break;
                    
                default:
                    desire.goalName = "SatisfyNeed";
                    desire.description = $"Satisfy the {need.needName} need";
                    desire.type = DesireType.Basic;
                    desire.satisfactionActions = need.satisfactionActions ?? new[] { "UseObject" };
                    break;
            }
            
            // Set properties based on need urgency
            desire.intensity = Mathf.Clamp01(need.GetUrgency());
            desire.urgency = need.IsUrgent ? 0.8f : 0.4f;
            desire.sourceNeed = need.needName;
            
            return desire;
        }
        
        /// <summary>
        /// Create a simple intention from a desire
        /// </summary>
        public static Intention CreateSimpleIntention(Desire desire)
        {
            var intention = new Intention(desire.goalName, desire);
            
            // Create an enhanced plan based on satisfaction actions
            if (desire.satisfactionActions != null && desire.satisfactionActions.Length > 0)
            {
                // Choose the best action based on desire type
                string chosenAction = SelectBestAction(desire);
                intention.plannedActions.Add(chosenAction);
                
                // Add follow-up actions for more complex intentions
                if (desire.type == DesireType.Social && desire.satisfactionActions.Contains("FindSocialSpace"))
                {
                    intention.plannedActions.Add("StartConversation");
                }
                else if (desire.type == DesireType.Creative && desire.satisfactionActions.Contains("UseObject"))
                {
                    intention.plannedActions.Add("Create");
                }
            }
            else
            {
                // Fallback generic action
                intention.plannedActions.Add("Think");
            }
            
            intention.commitmentStrength = desire.GetPriority();
            
            return intention;
        }
        
        /// <summary>
        /// Select the best action for a desire
        /// </summary>
        private static string SelectBestAction(Desire desire)
        {
            if (desire.satisfactionActions == null || desire.satisfactionActions.Length == 0)
                return "Think";
            
            // Prefer object interactions for physical needs
            if (desire.type == DesireType.Basic || desire.type == DesireType.Achievement)
            {
                if (desire.satisfactionActions.Contains("UseObject"))
                    return "UseObject";
            }
            
            // Prefer movement actions for social needs
            if (desire.type == DesireType.Social)
            {
                if (desire.satisfactionActions.Contains("FindSocialSpace"))
                    return "FindSocialSpace";
                if (desire.satisfactionActions.Contains("StartConversation"))
                    return "StartConversation";
            }
            
            // Default to first available action
            return desire.satisfactionActions[0];
        }
        
        /// <summary>
        /// Create common world beliefs
        /// </summary>
        public static List<Belief> CreateBasicWorldBeliefs(Vector3 agentPosition)
        {
            var beliefs = new List<Belief>();
            
            // Self-location belief
            beliefs.Add(new Belief("agent_location", 
                new[] { "self", $"{agentPosition.x:F1},{agentPosition.y:F1},{agentPosition.z:F1}" }, 1f)
            {
                type = BeliefType.Observation,
                description = "I know where I am located"
            });
            
            // Basic environment beliefs
            beliefs.Add(new Belief("environment_type", new[] { "research_environment" }, 0.8f)
            {
                type = BeliefType.Inference,
                description = "This appears to be a research environment"
            });
            
            // Capability beliefs
            beliefs.Add(new Belief("can_move", new[] { "true" }, 1f)
            {
                type = BeliefType.Observation,
                description = "I am capable of moving around"
            });
            
            beliefs.Add(new Belief("can_interact", new[] { "true" }, 1f)
            {
                type = BeliefType.Observation,
                description = "I can interact with objects and other agents"
            });
            
            return beliefs;
        }
    }
}