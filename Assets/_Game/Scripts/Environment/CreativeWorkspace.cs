using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;

namespace AIWorld.Environment
{
    /// <summary>
    /// Creative workspace for artistic and innovative activities
    /// </summary>
    public class CreativeWorkspace : InteractableObject
    {
        [Header("Creative Configuration")]
        public string creativeActivity = "Design and Innovation";
        public float creativityGainAmount = 0.5f;
        public bool inspiresSocialSharing = true;
        
        public override string InteractionPrompt => $"Work on {creativeActivity}";
        
        protected override void Start()
        {
            objectName = "Creative Workspace";
            description = $"A space for {creativeActivity} work";
            primaryInteractionType = InteractionType.Create;
            maxSimultaneousUsers = 2; // Allow collaboration
            
            // Configure need satisfactions
            needSatisfactions = new NeedSatisfaction[]
            {
                new NeedSatisfaction { needName = "Creativity", satisfactionAmount = creativityGainAmount },
                new NeedSatisfaction { needName = "Achievement", satisfactionAmount = 0.3f }
            };
            
            base.Start();
        }
        
        protected override void OnInteractionStarted(Agent agent)
        {
            if (logInteractions)
            {
                Debug.Log($"🎨 {agent.AgentName} is working on {creativeActivity}");
            }
            
            // If multiple users, it's collaboration
            if (CurrentUserCount > 1)
            {
                foreach (var user in currentUsers)
                {
                    if (user != agent && user.Needs != null)
                    {
                        user.Needs.SatisfyNeedsFromSocialInteraction("creative_collaboration", 0.3f);
                    }
                }
            }
        }
        
        protected override void OnInteractionCompleted(Agent agent)
        {
            if (logInteractions)
            {
                Debug.Log($"✨ {agent.AgentName} completed creative work on {creativeActivity}");
            }
            
            // Add creative achievement to beliefs
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("created_work", new[] { creativeActivity }, 0.8f,
                    $"I created something in {creativeActivity}");
                    
                if (CurrentUserCount > 1)
                {
                    agent.BDI.AddBelief("collaborated_creatively", new[] { "true" }, 0.9f,
                        "I collaborated with others on creative work");
                }
            }
        }
    }
}