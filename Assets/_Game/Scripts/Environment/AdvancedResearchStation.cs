using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;

namespace AIWorld.Environment
{
    /// <summary>
    /// Research station that satisfies knowledge and achievement needs
    /// Supports collaborative research and individual study
    /// </summary>
    public class AdvancedResearchStation : InteractableObject
    {
        [Header("Research Station Configuration")]
        public ResearchType stationType = ResearchType.General;
        public int maxResearchers = 3;
        public float researchEfficiency = 1f;
        public bool enableCollaboration = true;
        
        [Header("Knowledge Generation")]
        public string[] researchTopics;
        public float knowledgeGenerationRate = 0.1f;
        public float collaborationBonus = 0.5f;
        
        private float accumulatedKnowledge = 0f;
        private int currentResearchers = 0;
        
        public override string InteractionPrompt => 
            $"Conduct {stationType} research{(enableCollaboration && currentResearchers > 0 ? " (collaborative)" : "")}";
        
        protected override void Start()
        {
            // Configure for research activities
            objectName = $"{stationType} Research Station";
            description = $"Advanced research facility for {stationType} studies";
            primaryInteractionType = InteractionType.Study;
            interactionDuration = 8f; // Longer for meaningful research
            maxSimultaneousUsers = maxResearchers;
            cooldownTime = 3f; // Short cooldown for research
            
            // Setup need satisfactions
            needSatisfactions = new NeedSatisfaction[]
            {
                new NeedSatisfaction { needName = "Knowledge", satisfactionAmount = 0.4f, description = "Gain knowledge through research" },
                new NeedSatisfaction { needName = "Achievement", satisfactionAmount = 0.3f, description = "Feel accomplished through discovery" },
                new NeedSatisfaction { needName = "Creativity", satisfactionAmount = 0.2f, description = "Creative problem solving" }
            };
            
            // NavMesh setup - research stations are obstacles
            autoSetupNavMeshObstacle = true;
            isWalkableObject = false;
            customObstacleSize = true;
            obstacleSize = new Vector3(3f, 2f, 2f);
            
            base.Start();
        }
        
        protected override void OnInteractionStarted(Agent agent)
        {
            currentResearchers++;
            
            if (logInteractions)
            {
                Debug.Log($"🔬 {agent.AgentName} started research at {objectName} ({currentResearchers}/{maxResearchers} researchers)");
            }
            
            // Add research beliefs to agent's BDI
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("conducting_research", 
                    new[] { stationType.ToString(), objectName },
                    1f, $"I am conducting {stationType} research");
                    
                if (currentResearchers > 1 && enableCollaboration)
                {
                    agent.BDI.AddBelief("collaborative_research",
                        new[] { currentResearchers.ToString() },
                        1f, "I am collaborating with other researchers");
                }
            }
        }
        
        protected override void OnInteractionCompleted(Agent agent)
        {
            currentResearchers--;
            
            // Calculate research outcome
            float baseKnowledge = knowledgeGenerationRate * researchEfficiency;
            float collaborationMultiplier = enableCollaboration && currentResearchers > 0 
                ? 1f + (collaborationBonus * currentResearchers) 
                : 1f;
            
            float knowledgeGained = baseKnowledge * collaborationMultiplier;
            accumulatedKnowledge += knowledgeGained;
            
            // Enhanced need satisfaction based on research quality
            if (agent.Needs != null)
            {
                foreach (var satisfaction in needSatisfactions)
                {
                    var matchingNeeds = agent.Needs.activeNeeds.FindAll(n => 
                        satisfaction.needName == "Any" || n.needName == satisfaction.needName);
                    
                    foreach (var need in matchingNeeds)
                    {
                        float enhancedSatisfaction = satisfaction.satisfactionAmount * collaborationMultiplier;
                        need.SatisfyNeed(enhancedSatisfaction, $"{objectName} (quality: {collaborationMultiplier:F1}x)");
                    }
                }
            }
            
            // Add research completion belief
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("research_completed",
                    new[] { stationType.ToString(), knowledgeGained.ToString("F2") },
                    0.9f, $"I completed research and gained {knowledgeGained:F2} knowledge");
            }
            
            if (logInteractions)
            {
                Debug.Log($"✅ {agent.AgentName} completed research (+{knowledgeGained:F2} knowledge, collaboration: {collaborationMultiplier:F1}x)");
            }
        }
        
        protected override void OnInteractionStopped(Agent agent)
        {
            currentResearchers--;
            
            if (logInteractions)
            {
                Debug.Log($"🛑 {agent.AgentName} stopped research at {objectName}");
            }
        }
        
        /// <summary>
        /// Get current research efficiency including collaboration bonus
        /// </summary>
        public float GetCurrentEfficiency()
        {
            if (!enableCollaboration || currentResearchers <= 1)
                return researchEfficiency;
            
            return researchEfficiency * (1f + (collaborationBonus * (currentResearchers - 1)));
        }
        
        /// <summary>
        /// Get total accumulated knowledge from this station
        /// </summary>
        public float GetAccumulatedKnowledge()
        {
            return accumulatedKnowledge;
        }
        
        /// <summary>
        /// Check if station has capacity for more researchers
        /// </summary>
        public bool HasCapacity()
        {
            return currentResearchers < maxResearchers;
        }
        
        protected override void UpdateVisualState()
        {
            base.UpdateVisualState();
            
            // Additional visual updates for research station
            if (interactionIndicator != null)
            {
                Renderer indicatorRenderer = interactionIndicator.GetComponent<Renderer>();
                if (indicatorRenderer != null)
                {
                    // Show collaboration state with different colors
                    if (currentResearchers > 1 && enableCollaboration)
                    {
                        indicatorRenderer.material.color = Color.blue; // Collaborative research
                    }
                    else if (IsInUse)
                    {
                        indicatorRenderer.material.color = inUseColor;
                    }
                    else if (IsAvailable)
                    {
                        indicatorRenderer.material.color = availableColor;
                    }
                }
            }
        }
        
        #if UNITY_EDITOR
        [Header("Research Station Debug")]
        [SerializeField] private float currentEfficiency;
        [SerializeField] private float totalKnowledge;
        [SerializeField] private int activeResearchers;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            if (Application.isPlaying)
            {
                currentEfficiency = GetCurrentEfficiency();
                totalKnowledge = accumulatedKnowledge;
                activeResearchers = currentResearchers;
            }
        }
        #endif
    }
    
    /// <summary>
    /// Types of research stations available
    /// </summary>
    public enum ResearchType
    {
        General,
        AI_Research,
        Neuroscience,
        Psychology,
        Engineering,
        Mathematics,
        Philosophy,
        Interdisciplinary
    }
}