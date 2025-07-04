using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;

namespace AIWorld.Environment
{
    /// <summary>
    /// Advanced social hub that facilitates group interactions and conversations
    /// Supports multiple conversation groups and social activities
    /// </summary>
    public class CollaborativeHub : InteractableObject
    {
        [Header("Social Hub Configuration")]
        public HubType hubType = HubType.General;
        public int maxParticipants = 6;
        public float socialBoostMultiplier = 1.5f;
        public bool enableGroupConversations = true;
        
        [Header("Group Management")]
        public float groupFormationThreshold = 0.7f;
        public int minGroupSize = 2;
        public int maxGroupSize = 4;
        public float conversationSyncBonus = 0.3f;
        
        [Header("Social Dynamics")]
        public bool trackSocialConnections = true;
        public float relationshipBuildingRate = 0.1f;
        public bool enableConflictResolution = true;
        
        private List<ConversationGroup> activeGroups;
        private Dictionary<Agent, float> agentSocialScores;
        private float hubSocialEnergy = 1f;
        
        public override string InteractionPrompt => 
            $"Join {hubType} discussion{(activeGroups.Count > 0 ? $" ({activeGroups.Count} groups active)" : "")}";
        
        protected override void Start()
        {
            // Configure for social activities
            objectName = $"{hubType} Hub";
            description = $"Collaborative space for {hubType} interactions";
            primaryInteractionType = InteractionType.Socialize;
            interactionDuration = 12f; // Longer for meaningful conversations
            maxSimultaneousUsers = maxParticipants;
            cooldownTime = 2f; // Short cooldown for social interaction
            
            // Setup need satisfactions
            needSatisfactions = new NeedSatisfaction[]
            {
                new NeedSatisfaction { needName = "Social Connection", satisfactionAmount = 0.5f, description = "Connect with others" },
                new NeedSatisfaction { needName = "Achievement", satisfactionAmount = 0.2f, description = "Accomplish through collaboration" },
                new NeedSatisfaction { needName = "Knowledge", satisfactionAmount = 0.3f, description = "Learn from others" }
            };
            
            // NavMesh setup - social hubs allow gathering around them
            autoSetupNavMeshObstacle = true;
            isWalkableObject = false;
            customObstacleSize = true;
            obstacleSize = new Vector3(4f, 1f, 4f); // Larger area, lower height for gathering
            
            // Initialize social tracking
            activeGroups = new List<ConversationGroup>();
            agentSocialScores = new Dictionary<Agent, float>();
            
            base.Start();
        }
        
        protected override void Update()
        {
            base.Update();
            
            if (IsInUse)
            {
                UpdateSocialDynamics();
                ManageConversationGroups();
            }
        }
        
        protected override void OnInteractionStarted(Agent agent)
        {
            if (!agentSocialScores.ContainsKey(agent))
            {
                agentSocialScores[agent] = 0.5f; // Neutral social score
            }
            
            if (logInteractions)
            {
                Debug.Log($"👥 {agent.AgentName} joined {objectName} ({CurrentUserCount}/{maxSimultaneousUsers})");
            }
            
            // Try to add agent to an existing group or create new one
            if (enableGroupConversations)
            {
                AssignAgentToGroup(agent);
            }
            
            // Add social beliefs to agent's BDI
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("social_interaction",
                    new[] { hubType.ToString(), CurrentUserCount.ToString() },
                    1f, $"I am socializing at {hubType} hub with {CurrentUserCount} people");
                    
                if (CurrentUserCount > 1)
                {
                    agent.BDI.AddBelief("group_interaction",
                        new[] { (CurrentUserCount - 1).ToString() },
                        0.9f, "I am part of a group conversation");
                }
            }
        }
        
        protected override void OnInteractionCompleted(Agent agent)
        {
            // Remove agent from any groups
            RemoveAgentFromGroups(agent);
            
            // Calculate social benefits
            float baseSocialGain = needSatisfactions[0].satisfactionAmount; // Social Connection
            float groupBonus = CalculateGroupBonus(agent);
            float hubEnergyMultiplier = hubSocialEnergy;
            
            float totalSocialGain = baseSocialGain * socialBoostMultiplier * hubEnergyMultiplier * (1f + groupBonus);
            
            // Enhanced need satisfaction
            if (agent.Needs != null)
            {
                foreach (var satisfaction in needSatisfactions)
                {
                    var matchingNeeds = agent.Needs.activeNeeds.FindAll(n => 
                        satisfaction.needName == "Any" || n.needName == satisfaction.needName);
                    
                    foreach (var need in matchingNeeds)
                    {
                        float enhancedSatisfaction = need.needName == "Social Connection" 
                            ? totalSocialGain 
                            : satisfaction.satisfactionAmount * (1f + groupBonus * 0.5f);
                            
                        need.SatisfyNeed(enhancedSatisfaction, $"{objectName} (social boost: {1f + groupBonus:F1}x)");
                    }
                }
            }
            
            // Update agent's social score
            if (agentSocialScores.ContainsKey(agent))
            {
                agentSocialScores[agent] = Mathf.Clamp01(agentSocialScores[agent] + relationshipBuildingRate);
            }
            
            // Add social completion belief
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("social_interaction_completed",
                    new[] { hubType.ToString(), totalSocialGain.ToString("F2") },
                    0.9f, $"I had a fulfilling social interaction (+{totalSocialGain:F2} social satisfaction)");
            }
            
            if (logInteractions)
            {
                Debug.Log($"✅ {agent.AgentName} completed social interaction (+{totalSocialGain:F2} social satisfaction, group bonus: {groupBonus:F1}x)");
            }
            
            // Update hub energy based on interaction quality
            hubSocialEnergy = Mathf.Clamp01(hubSocialEnergy + 0.05f);
        }
        
        protected override void OnInteractionStopped(Agent agent)
        {
            RemoveAgentFromGroups(agent);
            
            if (logInteractions)
            {
                Debug.Log($"🛑 {agent.AgentName} left {objectName}");
            }
        }
        
        /// <summary>
        /// Update social dynamics and hub energy
        /// </summary>
        private void UpdateSocialDynamics()
        {
            // Update hub social energy based on occupancy
            float targetEnergy = Mathf.Clamp01(CurrentUserCount / (float)maxParticipants);
            hubSocialEnergy = Mathf.Lerp(hubSocialEnergy, targetEnergy, Time.deltaTime * 0.5f);
            
            // Update agent social scores based on interaction quality
            foreach (var agent in currentUsers.ToList())
            {
                if (agent != null && agentSocialScores.ContainsKey(agent))
                {
                    float personalityBonus = agent.Personality?.extraversion ?? 0.5f;
                    agentSocialScores[agent] += personalityBonus * Time.deltaTime * 0.1f;
                    agentSocialScores[agent] = Mathf.Clamp01(agentSocialScores[agent]);
                }
            }
        }
        
        /// <summary>
        /// Manage conversation groups within the hub
        /// </summary>
        private void ManageConversationGroups()
        {
            if (!enableGroupConversations) return;
            
            // Remove groups with too few members
            activeGroups.RemoveAll(group => group.members.Count < minGroupSize);
            
            // Try to balance group sizes
            if (activeGroups.Count > 0)
            {
                var largestGroup = activeGroups.OrderByDescending(g => g.members.Count).First();
                if (largestGroup.members.Count > maxGroupSize)
                {
                    SplitGroup(largestGroup);
                }
            }
        }
        
        /// <summary>
        /// Assign agent to appropriate conversation group
        /// </summary>
        private void AssignAgentToGroup(Agent agent)
        {
            if (activeGroups.Count == 0)
            {
                // Create first group
                CreateNewGroup(agent);
                return;
            }
            
            // Find best group for this agent based on compatibility
            ConversationGroup bestGroup = null;
            float bestCompatibility = 0f;
            
            foreach (var group in activeGroups)
            {
                if (group.members.Count >= maxGroupSize) continue;
                
                float compatibility = CalculateGroupCompatibility(agent, group);
                if (compatibility > bestCompatibility && compatibility > groupFormationThreshold)
                {
                    bestCompatibility = compatibility;
                    bestGroup = group;
                }
            }
            
            if (bestGroup != null)
            {
                bestGroup.members.Add(agent);
                
                if (logInteractions)
                {
                    Debug.Log($"👥 {agent.AgentName} joined conversation group {bestGroup.id} (compatibility: {bestCompatibility:F2})");
                }
            }
            else
            {
                // Create new group
                CreateNewGroup(agent);
            }
        }
        
        /// <summary>
        /// Create a new conversation group
        /// </summary>
        private void CreateNewGroup(Agent agent)
        {
            var newGroup = new ConversationGroup
            {
                id = activeGroups.Count + 1,
                members = new List<Agent> { agent },
                topic = DetermineGroupTopic(agent),
                energy = 1f
            };
            
            activeGroups.Add(newGroup);
            
            if (logInteractions)
            {
                Debug.Log($"🆕 Created new conversation group {newGroup.id} with topic: {newGroup.topic}");
            }
        }
        
        /// <summary>
        /// Remove agent from all conversation groups
        /// </summary>
        private void RemoveAgentFromGroups(Agent agent)
        {
            foreach (var group in activeGroups.ToList())
            {
                if (group.members.Contains(agent))
                {
                    group.members.Remove(agent);
                    
                    if (group.members.Count == 0)
                    {
                        activeGroups.Remove(group);
                    }
                }
            }
        }
        
        /// <summary>
        /// Calculate compatibility between agent and group
        /// </summary>
        private float CalculateGroupCompatibility(Agent agent, ConversationGroup group)
        {
            if (agent.Personality == null) return 0.5f;
            
            float compatibility = 0f;
            int comparisons = 0;
            
            foreach (var member in group.members)
            {
                if (member.Personality != null)
                {
                    // Compare personality traits
                    float extraversionMatch = 1f - Mathf.Abs(agent.Personality.extraversion - member.Personality.extraversion);
                    float agreeablenessMatch = 1f - Mathf.Abs(agent.Personality.agreeableness - member.Personality.agreeableness);
                    float opennessMatch = 1f - Mathf.Abs(agent.Personality.openness - member.Personality.openness);
                    
                    compatibility += (extraversionMatch + agreeablenessMatch + opennessMatch) / 3f;
                    comparisons++;
                }
            }
            
            return comparisons > 0 ? compatibility / comparisons : 0.5f;
        }
        
        /// <summary>
        /// Calculate group interaction bonus for agent
        /// </summary>
        private float CalculateGroupBonus(Agent agent)
        {
            var group = activeGroups.FirstOrDefault(g => g.members.Contains(agent));
            if (group == null) return 0f;
            
            float groupSize = group.members.Count;
            float sizeBonus = (groupSize - 1) * conversationSyncBonus;
            float energyBonus = group.energy * 0.2f;
            
            return sizeBonus + energyBonus;
        }
        
        /// <summary>
        /// Determine conversation topic for new group
        /// </summary>
        private string DetermineGroupTopic(Agent agent)
        {
            if (agent.Personality?.primaryInterests != null && agent.Personality.primaryInterests.Length > 0)
            {
                return agent.Personality.primaryInterests[Random.Range(0, agent.Personality.primaryInterests.Length)];
            }
            
            return hubType.ToString().Replace("_", " ");
        }
        
        /// <summary>
        /// Split large group into smaller ones
        /// </summary>
        private void SplitGroup(ConversationGroup group)
        {
            if (group.members.Count <= maxGroupSize) return;
            
            int splitPoint = group.members.Count / 2;
            var newGroup = new ConversationGroup
            {
                id = activeGroups.Count + 1,
                members = group.members.GetRange(splitPoint, group.members.Count - splitPoint),
                topic = group.topic,
                energy = group.energy * 0.8f
            };
            
            group.members.RemoveRange(splitPoint, group.members.Count - splitPoint);
            activeGroups.Add(newGroup);
            
            if (logInteractions)
            {
                Debug.Log($"✂️ Split group {group.id} - created group {newGroup.id}");
            }
        }
        
        /// <summary>
        /// Get social interaction statistics
        /// </summary>
        public string GetSocialStats()
        {
            string stats = $"Active Groups: {activeGroups.Count}\n";
            stats += $"Total Participants: {CurrentUserCount}\n";
            stats += $"Hub Energy: {hubSocialEnergy:F2}\n";
            
            foreach (var group in activeGroups)
            {
                stats += $"Group {group.id}: {group.members.Count} members, Topic: {group.topic}\n";
            }
            
            return stats;
        }
        
        #if UNITY_EDITOR
        [Header("Social Hub Debug")]
        [SerializeField] private int activeGroupCount;
        [SerializeField] private float socialEnergy;
        [SerializeField] private string socialStats;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            if (Application.isPlaying)
            {
                activeGroupCount = activeGroups?.Count ?? 0;
                socialEnergy = hubSocialEnergy;
                socialStats = GetSocialStats();
            }
        }
        #endif
    }
    
    /// <summary>
    /// Types of collaborative hubs
    /// </summary>
    public enum HubType
    {
        General,
        Academic_Discussion,
        Creative_Brainstorming,
        Project_Planning,
        Social_Gathering,
        Problem_Solving,
        Knowledge_Sharing
    }
    
    /// <summary>
    /// Data structure for conversation groups
    /// </summary>
    [System.Serializable]
    public class ConversationGroup
    {
        public int id;
        public List<Agent> members;
        public string topic;
        public float energy;
        public float startTime;
        
        public ConversationGroup()
        {
            members = new List<Agent>();
            startTime = Time.time;
        }
    }
}