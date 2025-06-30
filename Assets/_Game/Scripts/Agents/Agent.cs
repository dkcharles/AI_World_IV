using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Data;
using AIWorld.Communication;
using AIWorld.Managers;
using AIWorld.Needs;
using AIWorld.BDI;

namespace AIWorld.Agents
{
    /// <summary>
    /// Core Agent class implementing basic autonomous AI behaviour
    /// Handles communication, inner dialogue, and basic decision making
    /// Now enhanced with cognitive architecture (Needs + BDI)
    /// </summary>
    public class Agent : MonoBehaviour
    {
        [Header("Cognitive Architecture")]
        public bool enableCognitiveArchitecture = true;
        public bool showCognitiveStatus = false;
        
        [Header("Agent Configuration")]
        public AgentPersonality personality;
        public string agentId;
        public bool enableInnerDialogue = true;
        public bool enableCommunication = true;
        
        [Header("Behaviour Timing")]
        public float innerDialogueInterval = 10f; // Seconds between inner thoughts
        public float communicationInterval = 15f; // Seconds between communication attempts
        public float perceptionRange = 5f; // Range to detect other agents
        
        [Header("Debug")]
        public bool logAllMessages = true;
        public bool showDebugInfo = true;
        
        // Core components
        private OllamaClient llmClient;
        private NeedsManager needsManager;
        private BDIEngine bdiEngine;
        private List<Message> conversationHistory;
        private List<Agent> nearbyAgents;
        private Coroutine innerDialogueCoroutine;
        private Coroutine communicationCoroutine;
        
        // Agent state
        private float lastInnerDialogue;
        private float lastCommunication;
        private int messageCount = 0;
        
        // Properties
        public string AgentName => personality?.agentName ?? agentId;
        public List<Message> ConversationHistory => conversationHistory;
        public NeedsManager Needs => needsManager;
        public BDIEngine BDI => bdiEngine;
        
        private void Awake()
        {
            // Generate unique ID if not set
            if (string.IsNullOrEmpty(agentId))
            {
                agentId = $"Agent_{GetInstanceID()}";
            }
            
            // Initialize collections
            conversationHistory = new List<Message>();
            nearbyAgents = new List<Agent>();
        }
        
        private void Start()
        {
            // Register with GameManager (do this in Start to ensure GameManager is ready)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterAgent(this);
            }
            else
            {
                Debug.LogWarning($"⚠️ {AgentName}: GameManager not found during registration");
            }
            // Find LLM client
            llmClient = FindFirstObjectByType<OllamaClient>();
            if (llmClient == null)
            {
                Debug.LogError($"❌ {AgentName}: No OllamaClient found in scene!");
                return;
            }
            
            // Get cognitive architecture components
            if (enableCognitiveArchitecture)
            {
                needsManager = GetComponent<NeedsManager>();
                bdiEngine = GetComponent<BDIEngine>();
                
                if (needsManager == null)
                {
                    Debug.LogWarning($"⚠️ {AgentName}: No NeedsManager found - adding default one");
                    needsManager = gameObject.AddComponent<NeedsManager>();
                }
                
                if (bdiEngine == null)
                {
                    Debug.LogWarning($"⚠️ {AgentName}: No BDIEngine found - adding default one");
                    bdiEngine = gameObject.AddComponent<BDIEngine>();
                }
            }
            
            // Validate personality
            if (personality == null)
            {
                Debug.LogWarning($"⚠️ {AgentName}: No personality assigned, using default behaviour");
            }
            
            // Start autonomous behaviour
            StartAutonomousBehaviour();
            
            Debug.Log($"🤖 {AgentName} initialized at {transform.position}");
        }
        
        private void OnDestroy()
        {
            // Unregister from GameManager
            GameManager.Instance?.UnregisterAgent(this);
            
            // Stop coroutines
            if (innerDialogueCoroutine != null) StopCoroutine(innerDialogueCoroutine);
            if (communicationCoroutine != null) StopCoroutine(communicationCoroutine);
        }
        
        /// <summary>
        /// Start autonomous behaviour patterns
        /// </summary>
        private void StartAutonomousBehaviour()
        {
            if (enableInnerDialogue)
            {
                innerDialogueCoroutine = StartCoroutine(InnerDialogueLoop());
            }
            
            if (enableCommunication)
            {
                communicationCoroutine = StartCoroutine(CommunicationLoop());
            }
        }
        
        /// <summary>
        /// Inner dialogue loop for self-reflection and planning
        /// </summary>
        private IEnumerator InnerDialogueLoop()
        {
            while (enabled)
            {
                float baseInterval = innerDialogueInterval;
                float personalityModifier = personality?.GetInnerDialogueFrequency() ?? 0.5f;
                float actualInterval = baseInterval * (0.5f + personalityModifier);
                
                yield return new WaitForSeconds(actualInterval);
                
                if (llmClient != null)
                {
                    PerformInnerDialogue();
                }
            }
        }
        
        /// <summary>
        /// Communication loop for interacting with other agents
        /// </summary>
        private IEnumerator CommunicationLoop()
        {
            while (enabled)
            {
                float baseInterval = communicationInterval;
                float personalityModifier = personality?.GetConversationStarterProbability() ?? 0.5f;
                float actualInterval = baseInterval * (2f - personalityModifier);
                
                yield return new WaitForSeconds(actualInterval);
                
                if (llmClient != null)
                {
                    AttemptCommunication();
                }
            }
        }
        
        /// <summary>
        /// Perform inner dialogue for self-reflection
        /// </summary>
        private void PerformInnerDialogue()
        {
            UpdateNearbyAgents();
            
            string reflectionPrompt = GenerateInnerDialoguePrompt();
            
            llmClient.SendInnerDialogueRequest(agentId, reflectionPrompt, personality, (response) =>
            {
                if (response.success)
                {
                    var innerMessage = new Message(response.content, agentId)
                    {
                        type = MessageType.InnerDialogue,
                        emotionalTone = response.emotionalTone,
                        senderPosition = transform.position
                    };
                    
                    AddToConversationHistory(innerMessage);
                    
                    if (logAllMessages)
                    {
                        Debug.Log(innerMessage.GetFormattedOutput());
                    }
                }
                else
                {
                    Debug.LogError($"❌ {AgentName}: Inner dialogue failed - {response.error}");
                }
            });
        }
        
        /// <summary>
        /// Attempt to start or continue a conversation with nearby agents
        /// </summary>
        private void AttemptCommunication()
        {
            UpdateNearbyAgents();
            
            if (nearbyAgents.Count == 0)
            {
                return; // No one to talk to
            }
            
            // Select a random nearby agent
            Agent targetAgent = nearbyAgents[Random.Range(0, nearbyAgents.Count)];
            
            string communicationPrompt = GenerateCommunicationPrompt(targetAgent);
            
            llmClient.SendChatRequest(agentId, communicationPrompt, personality, conversationHistory, (response) =>
            {
                if (response.success)
                {
                    var message = new Message(response.content, agentId, targetAgent.agentId)
                    {
                        type = MessageType.InterAgent,
                        emotionalTone = response.emotionalTone,
                        senderPosition = transform.position,
                        turnNumber = messageCount++
                    };
                    
                    AddToConversationHistory(message);
                    
                    // Send to target agent
                    targetAgent.ReceiveMessage(message);
                    
                    // Satisfy social needs from communication
                    if (enableCognitiveArchitecture && needsManager != null)
                    {
                        needsManager.SatisfyNeedsFromSocialInteraction("conversation", 0.7f);
                    }
                    
                    if (logAllMessages)
                    {
                        Debug.Log(message.GetFormattedOutput());
                    }
                }
                else
                {
                    Debug.LogError($"❌ {AgentName}: Communication failed - {response.error}");
                }
            });
        }
        
        /// <summary>
        /// Receive a message from another agent
        /// </summary>
        public void ReceiveMessage(Message message)
        {
            AddToConversationHistory(message);
            
            // Satisfy social needs from receiving communication
            if (enableCognitiveArchitecture && needsManager != null)
            {
                needsManager.SatisfyNeedsFromSocialInteraction("received_message", 0.5f);
            }
            
            // Decide whether to respond
            if (ShouldRespondToMessage(message))
            {
                // Delay response for natural conversation flow
                StartCoroutine(DelayedResponse(message, Random.Range(1f, 3f)));
            }
        }
        
        /// <summary>
        /// Send a delayed response to a message
        /// </summary>
        private IEnumerator DelayedResponse(Message originalMessage, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            string responsePrompt = GenerateResponsePrompt(originalMessage);
            
            llmClient.SendChatRequest(agentId, responsePrompt, personality, conversationHistory, (response) =>
            {
                if (response.success)
                {
                    var responseMessage = new Message(response.content, agentId, originalMessage.senderId)
                    {
                        type = MessageType.InterAgent,
                        emotionalTone = response.emotionalTone,
                        senderPosition = transform.position,
                        previousMessageId = originalMessage.messageId,
                        turnNumber = messageCount++
                    };
                    
                    AddToConversationHistory(responseMessage);
                    
                    // Send back to original sender
                    var senderAgent = GameManager.Instance?.GetAgent(originalMessage.senderId);
                    senderAgent?.ReceiveMessage(responseMessage);
                    
                    // Satisfy social needs from responding
                    if (enableCognitiveArchitecture && needsManager != null)
                    {
                        needsManager.SatisfyNeedsFromSocialInteraction("response", 0.6f);
                    }
                    
                    if (logAllMessages)
                    {
                        Debug.Log(responseMessage.GetFormattedOutput());
                    }
                }
            });
        }
        
        /// <summary>
        /// Generate prompt for inner dialogue
        /// </summary>
        private string GenerateInnerDialoguePrompt()
        {
            List<string> context = new List<string>();
            
            context.Add($"You are {AgentName}, currently at position {transform.position}.");
            
            // Add needs-based motivation if cognitive architecture is enabled
            if (enableCognitiveArchitecture && needsManager != null)
            {
                string motivationPrompt = needsManager.GenerateMotivationPrompt();
                if (!string.IsNullOrEmpty(motivationPrompt))
                {
                    context.Add(motivationPrompt);
                }
                
                // Add current intentions from BDI engine
                if (bdiEngine != null && bdiEngine.CurrentIntention != null)
                {
                    context.Add($"You're currently focused on: {bdiEngine.CurrentIntention.description}.");
                }
            }
            
            if (nearbyAgents.Count > 0)
            {
                string agentNames = string.Join(", ", nearbyAgents.ConvertAll(a => a.AgentName));
                context.Add($"You can see: {agentNames}.");
            }
            else
            {
                context.Add("You are alone right now.");
            }
            
            if (conversationHistory.Count > 0)
            {
                var lastMessage = conversationHistory[conversationHistory.Count - 1];
                context.Add($"Your last thought/message was: '{lastMessage.content}'");
            }
            
            context.Add("Reflect on your current situation and think about what you might want to do next. Keep your thoughts brief and authentic to your personality.");
            
            return string.Join(" ", context);
        }
        
        /// <summary>
        /// Generate prompt for starting a conversation
        /// </summary>
        private string GenerateCommunicationPrompt(Agent targetAgent)
        {
            string prompt = $"You see {targetAgent.AgentName} nearby. Start a natural conversation with them based on your personality and interests. ";
            
            // Add motivation context if cognitive architecture is enabled
            if (enableCognitiveArchitecture && needsManager != null)
            {
                string motivationPrompt = needsManager.GenerateMotivationPrompt();
                if (!string.IsNullOrEmpty(motivationPrompt))
                {
                    prompt += motivationPrompt + " ";
                }
            }
            
            prompt += "Consider your shared environment and any previous interactions. Keep it conversational and authentic.";
            
            return prompt;
        }
        
        /// <summary>
        /// Generate prompt for responding to a message
        /// </summary>
        private string GenerateResponsePrompt(Message originalMessage)
        {
            string prompt = $"{originalMessage.senderId} just said to you: '{originalMessage.content}'. ";
            
            // Add current motivation context
            if (enableCognitiveArchitecture && needsManager != null)
            {
                string motivationPrompt = needsManager.GenerateMotivationPrompt();
                if (!string.IsNullOrEmpty(motivationPrompt))
                {
                    prompt += motivationPrompt + " ";
                }
            }
            
            prompt += "Respond naturally according to your personality. Be conversational and engaging.";
            
            return prompt;
        }
        
        /// <summary>
        /// Determine if agent should respond to a message
        /// </summary>
        private bool ShouldRespondToMessage(Message message)
        {
            if (!message.expectsResponse) return false;
            
            // Personality-based response probability
            float baseProbability = 0.8f;
            float personalityModifier = personality?.agreeableness ?? 0.5f;
            
            // Social needs can influence response probability
            if (enableCognitiveArchitecture && needsManager != null)
            {
                var socialNeeds = needsManager.activeNeeds.Where(n => n.needName.Contains("Social"));
                if (socialNeeds.Any(n => n.IsUrgent))
                {
                    baseProbability += 0.2f; // More likely to respond when socially needy
                }
            }
            
            return Random.value < (baseProbability * personalityModifier);
        }
        
        /// <summary>
        /// Update list of nearby agents
        /// </summary>
        private void UpdateNearbyAgents()
        {
            nearbyAgents.Clear();
            
            if (GameManager.Instance == null) return;
            
            foreach (var agent in GameManager.Instance.GetAllAgents())
            {
                if (agent == this) continue;
                
                float distance = Vector3.Distance(transform.position, agent.transform.position);
                if (distance <= perceptionRange)
                {
                    nearbyAgents.Add(agent);
                }
            }
        }
        
        /// <summary>
        /// Add message to conversation history
        /// </summary>
        private void AddToConversationHistory(Message message)
        {
            conversationHistory.Add(message);
            
            // Keep history manageable
            if (conversationHistory.Count > 50)
            {
                conversationHistory.RemoveAt(0);
            }
        }
        
        /// <summary>
        /// Debug visualization
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!showDebugInfo) return;
            
            // Draw perception range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, perceptionRange);
            
            // Draw connections to nearby agents (only if initialized)
            if (nearbyAgents != null)
            {
                Gizmos.color = Color.green;
                foreach (var agent in nearbyAgents)
                {
                    if (agent != null)
                    {
                        Gizmos.DrawLine(transform.position, agent.transform.position);
                    }
                }
            }
            
            // Show needs state if cognitive architecture is enabled
            if (enableCognitiveArchitecture && needsManager != null && showCognitiveStatus)
            {
                // Draw wellbeing indicator
                Gizmos.color = Color.Lerp(Color.red, Color.green, needsManager.OverallWellbeing);
                Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.3f);
            }
        }
        
        #if UNITY_EDITOR
        [Header("Editor Info")]
        [SerializeField] private int historyCount;
        [SerializeField] private int nearbyCount;
        [SerializeField] private string cognitiveStatus;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                historyCount = conversationHistory?.Count ?? 0;
                nearbyCount = nearbyAgents?.Count ?? 0;
                
                if (enableCognitiveArchitecture && showCognitiveStatus)
                {
                    string status = "";
                    
                    if (needsManager != null)
                    {
                        status += $"Wellbeing: {needsManager.OverallWellbeing:F2} | ";
                        status += $"Urgent Needs: {needsManager.UrgentNeeds.Count} | ";
                    }
                    
                    if (bdiEngine != null)
                    {
                        status += $"Beliefs: {bdiEngine.Beliefs.Count} | ";
                        status += $"Desires: {bdiEngine.Desires.Count} | ";
                        status += $"Intentions: {bdiEngine.Intentions.Count}";
                        
                        if (bdiEngine.CurrentIntention != null)
                        {
                            status += $"\nCurrent Goal: {bdiEngine.CurrentIntention.intentionName}";
                        }
                    }
                    
                    cognitiveStatus = status;
                }
            }
        }
        #endif
    }
}