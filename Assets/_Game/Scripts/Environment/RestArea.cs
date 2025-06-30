using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;

namespace AIWorld.Environment
{
    /// <summary>
    /// Rest area for energy recovery and relaxation
    /// </summary>
    public class RestArea : InteractableObject
    {
        [Header("Rest Configuration")]
        public string restActivity = "Relaxation and Recharge";
        public float energyGainAmount = 0.6f;
        public bool providesSecurityFeeling = true;
        
        public override string InteractionPrompt => $"Use {restActivity}";
        
        protected override void Start()
        {
            objectName = "Rest Area";
            description = $"A peaceful area for {restActivity}";
            primaryInteractionType = InteractionType.Rest;
            maxSimultaneousUsers = 3; // Quiet but can accommodate a few
            
            // Configure need satisfactions
            needSatisfactions = new NeedSatisfaction[]
            {
                new NeedSatisfaction { needName = "Energy", satisfactionAmount = energyGainAmount },
                new NeedSatisfaction { needName = "Safety", satisfactionAmount = providesSecurityFeeling ? 0.3f : 0f }
            };
            
            base.Start();
        }
        
        protected override void OnInteractionStarted(Agent agent)
        {
            if (logInteractions)
            {
                Debug.Log($"😴 {agent.AgentName} is resting and recharging");
            }
        }
        
        protected override void OnInteractionCompleted(Agent agent)
        {
            if (logInteractions)
            {
                Debug.Log($"⚡ {agent.AgentName} feels refreshed after resting");
            }
            
            // Add rest experience to beliefs
            if (agent.BDI != null)
            {
                agent.BDI.AddBelief("rested_at", new[] { objectName }, 0.9f,
                    "I feel refreshed after resting here");
                    
                if (providesSecurityFeeling)
                {
                    agent.BDI.AddBelief("safe_location", new[] { $"{transform.position.x:F1},{transform.position.y:F1},{transform.position.z:F1}" }, 0.8f,
                        "This is a safe and peaceful location");
                }
            }
        }
        
        public override float GetAttractiveness(Agent agent)
        {
            float baseAttractiveness = base.GetAttractiveness(agent);
            
            // Less attractive when crowded (rest areas should be peaceful)
            if (CurrentUserCount >= maxSimultaneousUsers - 1)
            {
                baseAttractiveness *= 0.5f;
            }
            
            return Mathf.Clamp01(baseAttractiveness);
        }
    }
}