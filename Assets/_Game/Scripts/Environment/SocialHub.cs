using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;

namespace AIWorld.Environment
{
    /// <summary>
    /// Social hub for gatherings and communication
    /// </summary>
    public class SocialHub : InteractableObject
    {
        [Header("Social Configuration")]
        public string socialActivity = "Discussion and Networking";
        public float socialGainAmount = 0.4f;
        public int optimalGroupSize = 3;
        
        public override string InteractionPrompt => $"Join {socialActivity}";
        
        protected override void Start()
        {
            objectName = "Social Hub";
            description = $"A gathering place for {socialActivity}";
            primaryInteractionType = InteractionType.Socialize;
            maxSimultaneousUsers = 5; // Allow group interactions
            
            // Configure need satisfactions
            needSatisfactions = new NeedSatisfaction[]
            {
                new NeedSatisfaction { needName = "Social Connection", satisfactionAmount = socialGainAmount },
                new NeedSatisfaction { needName = "Esteem", satisfactionAmount = 0.15f }
            };
            
            base.Start();
        }
        
        protected override void OnInteractionStarted(Agent agent)
        {
            if (logInteractions)
            {
                Debug.Log($"👥 {agent.AgentName} joined the social hub ({CurrentUserCount} total)");
            }
            
            // Boost social satisfaction for all current users
            foreach (var user in currentUsers)
            {
                if (user.Needs != null)
                {
                    float socialBoost = CurrentUserCount >= optimalGroupSize ? 0.2f : 0.1f;
                    user.Needs.SatisfyNeedsFromSocialInteraction("group_gathering", socialBoost);
                }
            }
        }
        
        protected override void OnInteractionCompleted(Agent agent)
        {
            if (logInteractions)
            {
                Debug.Log($"👋 {agent.AgentName} completed social interaction at hub");
            }
            
            // Add social experience to beliefs
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("participated_in", new[] { socialActivity }, 0.8f,
                    $"I participated in {socialActivity} at the social hub");
                    
                if (CurrentUserCount >= optimalGroupSize)
                {
                    agent.BDI.AddBelief("group_experience", new[] { "positive" }, 0.9f,
                        "I had a positive group social experience");
                }
            }
        }
        
        public override float GetAttractiveness(Agent agent)
        {
            float baseAttractiveness = base.GetAttractiveness(agent);
            
            // More attractive when there are already people there (but not overcrowded)
            if (CurrentUserCount > 0 && CurrentUserCount < maxSimultaneousUsers)
            {
                float socialBonus = 0.3f * (CurrentUserCount / (float)optimalGroupSize);
                baseAttractiveness += socialBonus;
            }
            
            return Mathf.Clamp01(baseAttractiveness);
        }
    }
}