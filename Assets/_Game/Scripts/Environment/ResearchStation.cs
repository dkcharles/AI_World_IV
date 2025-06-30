using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;

namespace AIWorld.Environment
{
    /// <summary>
    /// Research station for knowledge and learning activities
    /// </summary>
    public class ResearchStation : InteractableObject
    {
        [Header("Research Configuration")]
        public string researchTopic = "AI and Machine Learning";
        public float knowledgeGainAmount = 0.4f;
        public bool providesAchievementSatisfaction = true;
        
        public override string InteractionPrompt => $"Research {researchTopic}";
        
        protected override void Start()
        {
            objectName = "Research Station";
            description = $"A research station for studying {researchTopic}";
            primaryInteractionType = InteractionType.Study;
            
            // Configure need satisfactions
            needSatisfactions = new NeedSatisfaction[]
            {
                new NeedSatisfaction { needName = "Knowledge", satisfactionAmount = knowledgeGainAmount },
                new NeedSatisfaction { needName = "Achievement", satisfactionAmount = providesAchievementSatisfaction ? 0.2f : 0f }
            };
            
            base.Start();
        }
        
        protected override void OnInteractionStarted(Agent agent)
        {
            if (logInteractions)
            {
                Debug.Log($"📚 {agent.AgentName} is researching {researchTopic}");
            }
            
            // Add research topic to agent's beliefs
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("researched_topic", new[] { researchTopic }, 0.8f, 
                    $"I studied {researchTopic} at the research station");
            }
        }
        
        protected override void OnInteractionCompleted(Agent agent)
        {
            if (logInteractions)
            {
                Debug.Log($"🎓 {agent.AgentName} completed research on {researchTopic}");
            }
            
            // Boost agent's knowledge belief
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("knowledgeable_about", new[] { researchTopic }, 0.9f,
                    $"I now have good knowledge about {researchTopic}");
            }
        }
    }
}