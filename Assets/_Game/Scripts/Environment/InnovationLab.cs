using System.Collections.Generic;
using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;

namespace AIWorld.Environment
{
    /// <summary>
    /// Advanced creative workspace that inspires innovation and artistic expression
    /// Provides tools and environment for creative thinking and brainstorming
    /// </summary>
    public class InnovationLab : InteractableObject
    {
        [Header("Innovation Lab Configuration")]
        public CreativeMode labMode = CreativeMode.General;
        public int maxCreators = 2; // Smaller groups for focused creativity
        public float inspirationMultiplier = 2f;
        public bool enableIdeaGeneration = true;
        
        [Header("Creative Tools")]
        public CreativeTool[] availableTools;
        public float toolSynergyBonus = 0.3f;
        public bool enableCrossPolination = true;
        
        [Header("Innovation Tracking")]
        public float ideaGenerationRate = 0.2f;
        public int maxStoredIdeas = 10;
        public bool trackBreakthroughs = true;
        
        private List<CreativeIdea> generatedIdeas;
        private Dictionary<Agent, CreativeSession> activeSessions;
        private float labInspiration = 1f;
        private int breakthroughCount = 0;
        
        public override string InteractionPrompt => 
            $"Create in {labMode} lab{(generatedIdeas.Count > 0 ? $" ({generatedIdeas.Count} ideas)" : "")}";
        
        protected override void Start()
        {
            // Configure for creative activities
            objectName = $"{labMode} Innovation Lab";
            description = $"State-of-the-art facility for {labMode} innovation";
            primaryInteractionType = InteractionType.Create;
            interactionDuration = 10f; // Longer for deep creative work
            maxSimultaneousUsers = maxCreators;
            cooldownTime = 5f; // Moderate cooldown for creativity
            
            // Setup need satisfactions
            needSatisfactions = new NeedSatisfaction[]
            {
                new NeedSatisfaction { needName = "Creativity", satisfactionAmount = 0.6f, description = "Express creative ideas" },
                new NeedSatisfaction { needName = "Achievement", satisfactionAmount = 0.4f, description = "Create something meaningful" },
                new NeedSatisfaction { needName = "Knowledge", satisfactionAmount = 0.2f, description = "Learn through creation" }
            };
            
            // NavMesh setup - labs are creative spaces
            autoSetupNavMeshObstacle = true;
            isWalkableObject = false;
            customObstacleSize = true;
            obstacleSize = new Vector3(3f, 1.5f, 3f);
            
            // Initialize creative tracking
            generatedIdeas = new List<CreativeIdea>();
            activeSessions = new Dictionary<Agent, CreativeSession>();
            
            // Setup default tools if none specified
            if (availableTools == null || availableTools.Length == 0)
            {
                SetupDefaultTools();
            }
            
            base.Start();
        }
        
        protected override void Update()
        {
            base.Update();
            
            if (IsInUse)
            {
                UpdateCreativeSessions();
                GenerateIdeas();
                UpdateLabInspiration();
            }
        }
        
        protected override void OnInteractionStarted(Agent agent)
        {
            // Create creative session for agent
            var session = new CreativeSession
            {
                agent = agent,
                startTime = Time.time,
                usedTools = new List<CreativeTool>(),
                inspirationLevel = agent.Personality?.creativityLevel ?? 0.5f,
                ideaCount = 0
            };
            
            activeSessions[agent] = session;
            
            if (logInteractions)
            {
                Debug.Log($"🎨 {agent.AgentName} started creative session in {objectName} (inspiration: {session.inspirationLevel:F2})");
            }
            
            // Assign creative tools based on agent personality and interests
            AssignOptimalTools(agent, session);
            
            // Add creative beliefs to agent's BDI
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("creative_work",
                    new[] { labMode.ToString(), objectName },
                    1f, $"I am working creatively in {labMode} lab");
                    
                if (session.usedTools.Count > 0)
                {
                    agent.BDI.AddBelief("using_creative_tools",
                        new[] { session.usedTools.Count.ToString() },
                        0.9f, $"I have access to {session.usedTools.Count} creative tools");
                }
            }
        }
        
        protected override void OnInteractionCompleted(Agent agent)
        {
            if (!activeSessions.ContainsKey(agent)) return;
            
            var session = activeSessions[agent];
            float sessionDuration = Time.time - session.startTime;
            
            // Calculate creative output
            float baseCreativity = needSatisfactions[0].satisfactionAmount; // Creativity
            float inspirationBonus = session.inspirationLevel * inspirationMultiplier;
            float toolBonus = CalculateToolSynergy(session.usedTools);
            float durationBonus = Mathf.Clamp01(sessionDuration / interactionDuration) * 0.5f;
            
            float totalCreativeGain = baseCreativity * (1f + inspirationBonus + toolBonus + durationBonus);
            
            // Check for breakthrough
            bool breakthrough = CheckForBreakthrough(session, totalCreativeGain);
            if (breakthrough)
            {
                breakthroughCount++;
                totalCreativeGain *= 1.5f; // Breakthrough bonus
            }
            
            // Enhanced need satisfaction
            if (agent.Needs != null)
            {
                foreach (var satisfaction in needSatisfactions)
                {
                    var matchingNeeds = agent.Needs.activeNeeds.FindAll(n => 
                        satisfaction.needName == "Any" || n.needName == satisfaction.needName);
                    
                    foreach (var need in matchingNeeds)
                    {
                        float enhancedSatisfaction = need.needName == "Creativity" 
                            ? totalCreativeGain 
                            : satisfaction.satisfactionAmount * (1f + toolBonus);
                            
                        need.SatisfyNeed(enhancedSatisfaction, 
                            $"{objectName} ({(breakthrough ? "BREAKTHROUGH" : "creative work")})");
                    }
                }
            }
            
            // Generate final ideas from session
            GenerateSessionIdeas(session, totalCreativeGain);
            
            // Add creative completion belief
            if (agent.BDI != null)
            {
                string beliefContent = breakthrough 
                    ? $"I had a creative breakthrough in {labMode} lab!"
                    : $"I completed creative work and generated {session.ideaCount} ideas";
                    
                agent.BDI.AddBelief("creative_session_completed",
                    new[] { labMode.ToString(), totalCreativeGain.ToString("F2"), breakthrough.ToString() },
                    0.9f, beliefContent);
            }
            
            if (logInteractions)
            {
                string result = breakthrough ? "🌟 BREAKTHROUGH" : "✅ Completed";
                Debug.Log($"{result} {agent.AgentName} creative session (+{totalCreativeGain:F2} creativity, {session.ideaCount} ideas)");
            }
            
            // Update lab inspiration
            labInspiration = Mathf.Clamp01(labInspiration + (breakthrough ? 0.2f : 0.1f));
            
            // Clean up session
            activeSessions.Remove(agent);
        }
        
        protected override void OnInteractionStopped(Agent agent)
        {
            if (activeSessions.ContainsKey(agent))
            {
                activeSessions.Remove(agent);
            }
            
            if (logInteractions)
            {
                Debug.Log($"🛑 {agent.AgentName} left innovation lab");
            }
        }
        
        /// <summary>
        /// Update active creative sessions
        /// </summary>
        private void UpdateCreativeSessions()
        {
            foreach (var session in activeSessions.Values)
            {
                // Increase inspiration over time
                session.inspirationLevel += Time.deltaTime * 0.1f * labInspiration;
                session.inspirationLevel = Mathf.Clamp01(session.inspirationLevel);
                
                // Check for tool discoveries
                if (enableCrossPolination && Random.value < 0.1f * Time.deltaTime)
                {
                    DiscoverNewTool(session);
                }
            }
        }
        
        /// <summary>
        /// Generate ideas during active sessions
        /// </summary>
        private void GenerateIdeas()
        {
            if (!enableIdeaGeneration || activeSessions.Count == 0) return;
            
            foreach (var session in activeSessions.Values)
            {
                if (Random.value < ideaGenerationRate * Time.deltaTime * session.inspirationLevel)
                {
                    GenerateIdea(session);
                }
            }
        }
        
        /// <summary>
        /// Generate a new creative idea
        /// </summary>
        private void GenerateIdea(CreativeSession session)
        {
            var idea = new CreativeIdea
            {
                creator = session.agent.AgentName,
                concept = GenerateIdeaConcept(session),
                quality = session.inspirationLevel * Random.Range(0.7f, 1.3f),
                timeGenerated = Time.time,
                tools = new List<CreativeTool>(session.usedTools)
            };
            
            generatedIdeas.Add(idea);
            session.ideaCount++;
            
            // Remove oldest ideas if at capacity
            if (generatedIdeas.Count > maxStoredIdeas)
            {
                generatedIdeas.RemoveAt(0);
            }
            
            if (logInteractions)
            {
                Debug.Log($"💡 {session.agent.AgentName} generated idea: '{idea.concept}' (quality: {idea.quality:F2})");
            }
        }
        
        /// <summary>
        /// Generate session ideas on completion
        /// </summary>
        private void GenerateSessionIdeas(CreativeSession session, float creativeGain)
        {
            int bonusIdeas = Mathf.FloorToInt(creativeGain);
            for (int i = 0; i < bonusIdeas; i++)
            {
                GenerateIdea(session);
            }
        }
        
        /// <summary>
        /// Update lab inspiration based on activity
        /// </summary>
        private void UpdateLabInspiration()
        {
            float targetInspiration = 0.5f + (activeSessions.Count / (float)maxCreators) * 0.5f;
            labInspiration = Mathf.Lerp(labInspiration, targetInspiration, Time.deltaTime * 0.3f);
        }
        
        /// <summary>
        /// Assign optimal creative tools to agent
        /// </summary>
        private void AssignOptimalTools(Agent agent, CreativeSession session)
        {
            if (availableTools == null) return;
            
            // Assign tools based on agent personality and interests
            foreach (var tool in availableTools)
            {
                if (IsToolCompatible(agent, tool))
                {
                    session.usedTools.Add(tool);
                }
            }
            
            // Ensure at least one tool
            if (session.usedTools.Count == 0 && availableTools.Length > 0)
            {
                session.usedTools.Add(availableTools[Random.Range(0, availableTools.Length)]);
            }
        }
        
        /// <summary>
        /// Check if tool is compatible with agent
        /// </summary>
        private bool IsToolCompatible(Agent agent, CreativeTool tool)
        {
            if (agent.Personality == null) return Random.value > 0.5f;
            
            // Match tools to personality traits
            switch (tool.category)
            {
                case ToolCategory.Digital:
                    return agent.Personality.openness > 0.6f;
                case ToolCategory.Traditional:
                    return agent.Personality.conscientiousness > 0.5f;
                case ToolCategory.Collaborative:
                    return agent.Personality.extraversion > 0.5f;
                case ToolCategory.Experimental:
                    return agent.Personality.openness > 0.7f && agent.Personality.creativityLevel > 0.6f;
                default:
                    return true;
            }
        }
        
        /// <summary>
        /// Calculate tool synergy bonus
        /// </summary>
        private float CalculateToolSynergy(List<CreativeTool> tools)
        {
            if (tools.Count <= 1) return 0f;
            
            float synergy = 0f;
            for (int i = 0; i < tools.Count - 1; i++)
            {
                for (int j = i + 1; j < tools.Count; j++)
                {
                    if (tools[i].category != tools[j].category)
                    {
                        synergy += toolSynergyBonus / tools.Count;
                    }
                }
            }
            
            return synergy;
        }
        
        /// <summary>
        /// Check for breakthrough moment
        /// </summary>
        private bool CheckForBreakthrough(CreativeSession session, float creativeGain)
        {
            if (!trackBreakthroughs) return false;
            
            float breakthroughThreshold = 2f;
            float personalityBonus = session.agent.Personality?.creativityLevel ?? 0.5f;
            float sessionBonus = session.ideaCount * 0.1f;
            
            return creativeGain > breakthroughThreshold && 
                   Random.value < (personalityBonus + sessionBonus) * 0.3f;
        }
        
        /// <summary>
        /// Discover new tool during session
        /// </summary>
        private void DiscoverNewTool(CreativeSession session)
        {
            var unusedTools = new List<CreativeTool>();
            foreach (var tool in availableTools)
            {
                if (!session.usedTools.Contains(tool))
                {
                    unusedTools.Add(tool);
                }
            }
            
            if (unusedTools.Count > 0)
            {
                var newTool = unusedTools[Random.Range(0, unusedTools.Count)];
                session.usedTools.Add(newTool);
                
                if (logInteractions)
                {
                    Debug.Log($"🔧 {session.agent.AgentName} discovered new tool: {newTool.name}");
                }
            }
        }
        
        /// <summary>
        /// Generate idea concept based on session
        /// </summary>
        private string GenerateIdeaConcept(CreativeSession session)
        {
            string[] concepts = {
                "Revolutionary approach", "Novel framework", "Creative synthesis",
                "Innovative design", "Breakthrough concept", "Artistic vision",
                "Technical innovation", "Paradigm shift", "Creative solution"
            };
            
            string baseConcept = concepts[Random.Range(0, concepts.Length)];
            string domain = session.usedTools.Count > 0 
                ? session.usedTools[0].category.ToString().ToLower()
                : labMode.ToString().ToLower();
            
            return $"{baseConcept} in {domain}";
        }
        
        /// <summary>
        /// Setup default creative tools
        /// </summary>
        private void SetupDefaultTools()
        {
            availableTools = new CreativeTool[]
            {
                new CreativeTool { name = "Digital Canvas", category = ToolCategory.Digital, effectiveness = 0.8f },
                new CreativeTool { name = "Brainstorming Board", category = ToolCategory.Collaborative, effectiveness = 0.7f },
                new CreativeTool { name = "Prototype Kit", category = ToolCategory.Traditional, effectiveness = 0.9f },
                new CreativeTool { name = "AI Assistant", category = ToolCategory.Digital, effectiveness = 0.85f },
                new CreativeTool { name = "Experimental Tools", category = ToolCategory.Experimental, effectiveness = 1.0f }
            };
        }
        
        /// <summary>
        /// Get innovation statistics
        /// </summary>
        public string GetInnovationStats()
        {
            string stats = $"Lab Inspiration: {labInspiration:F2}\n";
            stats += $"Generated Ideas: {generatedIdeas.Count}\n";
            stats += $"Breakthroughs: {breakthroughCount}\n";
            stats += $"Active Sessions: {activeSessions.Count}\n";
            
            if (generatedIdeas.Count > 0)
            {
                float avgQuality = 0f;
                foreach (var idea in generatedIdeas)
                {
                    avgQuality += idea.quality;
                }
                avgQuality /= generatedIdeas.Count;
                stats += $"Average Idea Quality: {avgQuality:F2}";
            }
            
            return stats;
        }
        
        #if UNITY_EDITOR
        [Header("Innovation Lab Debug")]
        [SerializeField] private float currentInspiration;
        [SerializeField] private int totalIdeas;
        [SerializeField] private int totalBreakthroughs;
        [SerializeField] private string innovationStats;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            if (Application.isPlaying)
            {
                currentInspiration = labInspiration;
                totalIdeas = generatedIdeas?.Count ?? 0;
                totalBreakthroughs = breakthroughCount;
                innovationStats = GetInnovationStats();
            }
        }
        #endif
    }
    
    /// <summary>
    /// Creative modes for innovation labs
    /// </summary>
    public enum CreativeMode
    {
        General,
        Digital_Art,
        Product_Design,
        Software_Development,
        Scientific_Research,
        Artistic_Expression,
        Problem_Solving
    }
    
    /// <summary>
    /// Categories of creative tools
    /// </summary>
    public enum ToolCategory
    {
        Digital,
        Traditional,
        Collaborative,
        Experimental
    }
    
    /// <summary>
    /// Creative tool definition
    /// </summary>
    [System.Serializable]
    public class CreativeTool
    {
        public string name;
        public ToolCategory category;
        public float effectiveness;
        public string description;
    }
    
    /// <summary>
    /// Creative session data
    /// </summary>
    [System.Serializable]
    public class CreativeSession
    {
        public Agent agent;
        public float startTime;
        public List<CreativeTool> usedTools;
        public float inspirationLevel;
        public int ideaCount;
    }
    
    /// <summary>
    /// Generated creative idea
    /// </summary>
    [System.Serializable]
    public class CreativeIdea
    {
        public string creator;
        public string concept;
        public float quality;
        public float timeGenerated;
        public List<CreativeTool> tools;
    }
}