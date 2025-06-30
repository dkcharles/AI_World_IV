using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;

namespace AIWorld.Environment
{
    /// <summary>
    /// Observation point for environmental awareness
    /// </summary>
    public class ObservationPoint : InteractableObject
    {
        [Header("Observation Configuration")]
        public string observationFocus = "Environment and Activity";
        public float awarenessGainAmount = 0.3f;
        public float observationRadius = 15f;
        
        public override string InteractionPrompt => $"Observe {observationFocus}";
        
        protected override void Start()
        {
            objectName = "Observation Point";
            description = $"A vantage point for observing {observationFocus}";
            primaryInteractionType = InteractionType.Observe;
            maxSimultaneousUsers = 2;
            
            // Configure need satisfactions
            needSatisfactions = new NeedSatisfaction[]
            {
                new NeedSatisfaction { needName = "Knowledge", satisfactionAmount = awarenessGainAmount },
                new NeedSatisfaction { needName = "Safety", satisfactionAmount = 0.2f }
            };
            
            base.Start();
        }
        
        protected override void OnInteractionCompleted(Agent agent)
        {
            if (logInteractions)
            {
                Debug.Log($"👁️ {agent.AgentName} gained awareness from observation");
            }
            
            // Add environmental knowledge to beliefs
            if (agent.BDI != null)
            {
                // Observe other agents and objects in range
                var gameManager = FindFirstObjectByType<AIWorld.Managers.GameManager>();
                if (gameManager != null)
                {
                    var nearbyAgents = gameManager.GetAgentsInRange(transform.position, observationRadius);
                    foreach (var observedAgent in nearbyAgents)
                    {
                        if (observedAgent != agent)
                        {
                            agent.BDI.AddBelief("observed_agent", new[] { observedAgent.AgentName, "active" }, 0.7f,
                                $"I observed {observedAgent.AgentName} from the observation point");
                        }
                    }
                }
                
                agent.BDI.AddBelief("environmental_awareness", new[] { "high" }, 0.8f,
                    "I have good awareness of the environment from observation");
            }
        }
        
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // Draw observation radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, observationRadius);
        }
    }
}