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
    /// Enhanced Agent class with intelligent multi-model LLM routing
    /// Uses different AI models based on task complexity and location context
    /// Integrates with cognitive architecture (Needs + BDI) for authentic behavior
    /// </summary>
    public class Agent : MonoBehaviour
    {
        [Header("Enhanced AI Configuration")]
        public bool useEnhancedAI = true;
        public bool adaptToLocation = true;
        public bool enableIntelligentRouting = true;
        
        [Header("Cognitive Architecture")]
        public bool enableCognitiveArchitecture = true;
        public bool showCognitiveStatus = false;
        
        [Header("Agent Configuration")]
        [SerializeField] private AgentPersonality personalityAsset; // THE single source of truth
        public bool enableInnerDialogue = true;
        public bool enableCommunication = true;
        
        [Header("Behaviour Timing")]
        public float innerDialogueInterval = 15f; // Seconds between inner thoughts
        public float communicationInterval = 20f; // Seconds between communication attempts
        public float planningInterval = 30f; // Seconds between goal planning
        public float perceptionRange = 8f; // Range to detect other agents and areas
        
        [Header("Location-Based Behavior")]
        public string currentLocation = "";
        public float locationInfluenceStrength = 1.0f;
        
        [Header("Debug")]
        public bool logAllMessages = true;
        public bool showDebugInfo = true;
        
        // Enhanced AI components
        private OllamaClientEnhanced enhancedLLMClient;
        private OllamaClient fallbackLLMClient; // Backup for compatibility
        
        // Core components
        private NeedsManager needsManager;
        private BDIEngine bdiEngine;
        private List<Message> conversationHistory;
        private List<Agent> nearbyAgents;
        
        // Coroutines for different AI behaviors
        private Coroutine innerDialogueCoroutine;
        private Coroutine communicationCoroutine;
        private Coroutine planningCoroutine;
        
        // Agent state
        private float lastInnerDialogue;
        private float lastCommunication;
        private float lastPlanning;
        private int messageCount = 0;
        private string lastModelUsed = string.Empty;
        private float lastResponseTime = 0f;
        private float lastResponseQuality = 0f;
        
        // Location detection
        private List<string> detectedAreas = new List<string>();
        
        // Properties - SINGLE source of truth from ScriptableObject
        public string AgentName => personalityAsset?.agentName ?? "Unknown Agent";
        public string AgentId => personalityAsset?.AgentId ?? "unknown-id";
        public AgentPersonality Personality => personalityAsset;
        public List<Message> ConversationHistory => conversationHistory;
        public NeedsManager Needs => needsManager;
        public BDIEngine BDI => bdiEngine;
        public string CurrentLocation => currentLocation;
        public string LastModelUsed => lastModelUsed;
        
        /// <summary>
        /// Force update agent name (used by GameManager for duplicate resolution)
        /// Updates the ScriptableObject - the single source of truth
        /// </summary>
        public void ForceUpdateName(string newName)
        {
            if (personalityAsset != null)
            {
                personalityAsset.agentName = newName;
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(personalityAsset); // Mark ScriptableObject for saving
                #endif
            }
            else
            {
                Debug.LogError($"❌ {gameObject.name}: Cannot update name - no personality asset assigned!");
                return;
            }
            
            if (enableCognitiveArchitecture && bdiEngine != null)
            {
                bdiEngine.AddBelief("agent_name", new[] { newName }, 1.0f, "I know my own name");
            }
            
            if (logAllMessages)
            {
                Debug.Log($"🔧 {gameObject.name}: Name updated to '{newName}' in ScriptableObject");
            }
        }
        
        private void Awake()
        {
            // Initialize collections
            conversationHistory = new List<Message>();
            nearbyAgents = new List<Agent>();
            detectedAreas = new List<string>();
            
            // Validate personality asset
            if (personalityAsset == null)
            {
                Debug.LogError($"❌ {gameObject.name}: No AgentPersonality asset assigned! Agent will not function properly.");
            }
        }
        
        private void Start()
        {
            // Register with GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterAgent(this);
            }
            else
            {
                Debug.LogWarning($"⚠️ {AgentName}: GameManager not found during registration");
            }
            
            // Initialize AI clients
            InitializeAIClients();
            
            // Initialize cognitive architecture
            if (enableCognitiveArchitecture)
            {
                InitializeCognitiveArchitecture();
            }
            
            // Validate personality asset
            if (personalityAsset == null)
            {
                Debug.LogWarning($"⚠️ {AgentName}: No personality asset assigned, agent will have limited functionality");
            }
            
            // Detect initial location
            UpdateLocationContext();
            
            // Start enhanced autonomous behaviour
            StartEnhancedBehaviour();
            
            Debug.Log($"🤖 {AgentName} initialized at {transform.position} in {currentLocation}");
        }
        
        private void InitializeAIClients()
        {
            // Try to get enhanced client first
            if (useEnhancedAI)
            {
                enhancedLLMClient = FindFirstObjectByType<OllamaClientEnhanced>();
                if (enhancedLLMClient != null)
                {
                    Debug.Log($"✅ {AgentName}: Connected to Enhanced AI Client");
                }
                else
                {
                    Debug.LogWarning($"⚠️ {AgentName}: Enhanced AI Client not found, falling back to basic client");
                    useEnhancedAI = false;
                }
            }
            
            // Get fallback client
            fallbackLLMClient = FindFirstObjectByType<OllamaClient>();
            if (fallbackLLMClient == null && enhancedLLMClient == null)
            {
                Debug.LogError($"❌ {AgentName}: No AI client found in scene!");
                return;
            }
        }
        
        private void InitializeCognitiveArchitecture()
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
        
        private void OnDestroy()
        {
            GameManager.Instance?.UnregisterAgent(this);
            
            if (innerDialogueCoroutine != null) StopCoroutine(innerDialogueCoroutine);
            if (communicationCoroutine != null) StopCoroutine(communicationCoroutine);
            if (planningCoroutine != null) StopCoroutine(planningCoroutine);
        }
        
        /// <summary>
        /// Start enhanced autonomous behaviour with intelligent routing
        /// </summary>
        private void StartEnhancedBehaviour()
        {
            if (enableInnerDialogue)
            {
                innerDialogueCoroutine = StartCoroutine(EnhancedInnerDialogueLoop());
            }
            
            if (enableCommunication)
            {
                communicationCoroutine = StartCoroutine(EnhancedCommunicationLoop());
            }
            
            // New: Strategic planning loop for BDI reasoning
            if (enableCognitiveArchitecture)
            {
                planningCoroutine = StartCoroutine(StrategicPlanningLoop());
            }
        }
        
        /// <summary>
        /// Enhanced inner dialogue with location-aware task routing
        /// </summary>
        private IEnumerator EnhancedInnerDialogueLoop()
        {
            while (enabled)
            {
                float baseInterval = innerDialogueInterval;
                float personalityModifier = personalityAsset?.GetInnerDialogueFrequency() ?? 0.5f;
                float actualInterval = baseInterval * (0.5f + personalityModifier);
                
                yield return new WaitForSeconds(actualInterval);
                
                if (HasAIClient())
                {
                    PerformEnhancedInnerDialogue();
                }
            }
        }
        
        /// <summary>
        /// Enhanced communication with context-aware model selection
        /// </summary>
        private IEnumerator EnhancedCommunicationLoop()
        {
            while (enabled)
            {
                float baseInterval = communicationInterval;
                float personalityModifier = personalityAsset?.GetConversationStarterProbability() ?? 0.5f;
                float actualInterval = baseInterval * (2f - personalityModifier);
                
                yield return new WaitForSeconds(actualInterval);
                
                if (HasAIClient())
                {
                    AttemptEnhancedCommunication();
                }
            }
        }
        
        /// <summary>
        /// NEW: Strategic planning loop for goal setting and BDI reasoning
        /// </summary>
        private IEnumerator StrategicPlanningLoop()
        {
            while (enabled)
            {
                yield return new WaitForSeconds(planningInterval);
                
                if (HasAIClient() && ShouldPerformPlanning())
                {
                    PerformStrategicPlanning();
                }
            }
        }
        
        /// <summary>
        /// Enhanced inner dialogue using appropriate AI model based on context
        /// </summary>
        private void PerformEnhancedInnerDialogue()
        {
            UpdateLocationContext();
            UpdateNearbyAgents();
            
            string reflectionPrompt = GenerateInnerDialoguePrompt();
            OllamaClientEnhanced.TaskType taskType = DetermineInnerDialogueTaskType();
            OllamaClientEnhanced.TaskUrgency urgency = DetermineUrgency();
            
            if (useEnhancedAI && enhancedLLMClient != null)
            {
                enhancedLLMClient.SendInnerDialogueRequestEnhanced(
                    AgentId,
                    reflectionPrompt,
                    Personality,
                    (response) => ProcessInnerDialogueResponse(response, taskType),
                    GetCurrentNeed(),
                    GetCurrentIntention()
                );
            }
            else
            {
                // Fallback to basic client
                fallbackLLMClient?.SendInnerDialogueRequest(AgentId, reflectionPrompt, Personality, 
                    (response) => ProcessInnerDialogueResponse(response, taskType));
            }
        }
        
        /// <summary>
        /// Enhanced communication with intelligent model routing
        /// </summary>
        private void AttemptEnhancedCommunication()
        {
            UpdateLocationContext();
            UpdateNearbyAgents();
            
            if (nearbyAgents.Count == 0) return;
            
            Agent targetAgent = SelectCommunicationTarget();
            if (targetAgent == null) return;
            
            string communicationPrompt = GenerateCommunicationPrompt(targetAgent);
            OllamaClientEnhanced.TaskType taskType = DetermineCommunicationTaskType(targetAgent);
            OllamaClientEnhanced.TaskUrgency urgency = DetermineUrgency();
            
            if (useEnhancedAI && enhancedLLMClient != null)
            {
                enhancedLLMClient.SendChatRequestEnhanced(
                    AgentId,
                    communicationPrompt,
                    taskType,
                    Personality,
                    conversationHistory,
                    (response) => ProcessCommunicationResponse(response, targetAgent, taskType),
                    urgency
                );
            }
            else
            {
                // Fallback to basic client
                fallbackLLMClient?.SendChatRequest(AgentId, communicationPrompt, Personality, conversationHistory,
                    (response) => ProcessCommunicationResponse(response, targetAgent, taskType));
            }
        }
        
        /// <summary>
        /// NEW: Strategic planning for goal setting and complex reasoning
        /// </summary>
        private void PerformStrategicPlanning()
        {
            if (!enableCognitiveArchitecture || bdiEngine == null) return;
            
            string planningPrompt = GeneratePlanningPrompt();
            
            if (useEnhancedAI && enhancedLLMClient != null)
            {
                enhancedLLMClient.SendChatRequestEnhanced(
                    AgentId,
                    planningPrompt,
                    OllamaClientEnhanced.TaskType.BDIReasoning, // Use thinking mode for planning
                    Personality,
                    conversationHistory,
                    (response) => ProcessPlanningResponse(response),
                    OllamaClientEnhanced.TaskUrgency.Low // Take time for good planning
                );
            }
            else
            {
                fallbackLLMClient?.SendChatRequest(AgentId, planningPrompt, Personality, conversationHistory,
                    (response) => ProcessPlanningResponse(response));
            }
        }
        
        #region Enhanced Response Processing
        
        private void ProcessInnerDialogueResponse(LLMResponse response, OllamaClientEnhanced.TaskType taskType)
        {
            // Track response metrics
            lastResponseTime = Time.time;
            lastResponseQuality = response.success ? 1.0f : 0.0f;
            lastModelUsed = "enhanced_client";
            
            if (response.success)
            {
                var innerMessage = new Message(response.content, AgentId)
                {
                    type = MessageType.InnerDialogue,
                    emotionalTone = response.emotionalTone,
                    senderPosition = transform.position
                };
                
                AddToConversationHistory(innerMessage);
                
                // Extract insights for BDI system
                if (enableCognitiveArchitecture && bdiEngine != null)
                {
                    ExtractBeliefsFromThoughts(response.content);
                }
                
                if (logAllMessages)
                {
                    Debug.Log($"💭 {AgentName} ({taskType}): {innerMessage.GetFormattedOutput()}");
                }
            }
            else
            {
                Debug.LogError($"❌ {AgentName}: Inner dialogue failed - {response.error}");
            }
        }
        
        private void ProcessCommunicationResponse(LLMResponse response, Agent targetAgent, OllamaClientEnhanced.TaskType taskType)
        {
            // Track response metrics
            lastResponseTime = Time.time;
            lastResponseQuality = response.success ? 1.0f : 0.0f;
            lastModelUsed = "enhanced_client";
            
            if (response.success)
            {
                var message = new Message(response.content, AgentId, targetAgent.AgentId)
                {
                    type = MessageType.InterAgent,
                    emotionalTone = response.emotionalTone,
                    senderPosition = transform.position,
                    turnNumber = messageCount++
                };
                
                AddToConversationHistory(message);
                targetAgent.ReceiveMessage(message);
                
                // Satisfy social needs
                if (enableCognitiveArchitecture && needsManager != null)
                {
                    needsManager.SatisfyNeedsFromSocialInteraction("conversation", 0.7f);
                }
                
                if (logAllMessages)
                {
                    Debug.Log($"💬 {AgentName} → {targetAgent.AgentName} ({taskType}): {message.GetFormattedOutput()}");
                }
            }
            else
            {
                Debug.LogError($"❌ {AgentName}: Communication failed - {response.error}");
            }
        }
        
        private void ProcessPlanningResponse(LLMResponse response)
        {
            // Track response metrics
            lastResponseTime = Time.time;
            lastResponseQuality = response.success ? 1.0f : 0.0f;
            lastModelUsed = "enhanced_client";
            
            if (response.success)
            {
                if (logAllMessages)
                {
                    Debug.Log($"🎯 {AgentName} Planning: {response.content}");
                }
                
                // Extract goals and intentions for BDI system
                if (enableCognitiveArchitecture && bdiEngine != null)
                {
                    ExtractGoalsFromPlanning(response.content);
                }
            }
            else
            {
                Debug.LogError($"❌ {AgentName}: Planning failed - {response.error}");
            }
        }
        
        #endregion
        
        #region Task Type Determination (Location-Aware)
        
        private OllamaClientEnhanced.TaskType DetermineInnerDialogueTaskType()
        {
            // Base on current location and needs
            switch (currentLocation.ToLower())
            {
                case "research station":
                case "research":
                    return OllamaClientEnhanced.TaskType.SelfReflection;
                    
                case "rest area":
                case "rest":
                    return OllamaClientEnhanced.TaskType.InnerDialogue;
                    
                default:
                    return OllamaClientEnhanced.TaskType.InnerDialogue;
            }
        }
        
        private OllamaClientEnhanced.TaskType DetermineCommunicationTaskType(Agent targetAgent)
        {
            // Base on location and relationship with target
            switch (currentLocation.ToLower())
            {
                case "research station":
                case "research":
                    return OllamaClientEnhanced.TaskType.Conversation; // Collaborative discussions
                    
                case "creative workstation":
                case "creative":
                    return OllamaClientEnhanced.TaskType.PersonalityExpression; // Creative expression
                    
                case "social hub":
                case "social":
                    return OllamaClientEnhanced.TaskType.Conversation; // Social interaction
                    
                default:
                    return OllamaClientEnhanced.TaskType.Conversation;
            }
        }
        
        private OllamaClientEnhanced.TaskUrgency DetermineUrgency()
        {
            if (enableCognitiveArchitecture && needsManager != null)
            {
                if (needsManager.UrgentNeeds.Count > 2)
                {
                    return OllamaClientEnhanced.TaskUrgency.High;
                }
                else if (needsManager.OverallWellbeing < 0.3f)
                {
                    return OllamaClientEnhanced.TaskUrgency.High;
                }
            }
            
            return OllamaClientEnhanced.TaskUrgency.Normal;
        }
        
        #endregion
        
        #region Location Detection & Context
        
        /// <summary>
        /// Update location context by detecting nearby named objects
        /// </summary>
        private void UpdateLocationContext()
        {
            if (!adaptToLocation) return;
            
            detectedAreas.Clear();
            string newLocation = "";
            
            // Find nearby GameObjects that might indicate location
            Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, perceptionRange * 1.5f);
            
            foreach (var obj in nearbyObjects)
            {
                string objName = obj.gameObject.name.ToLower();
                
                // Detect location types
                if (objName.Contains("research") && objName.Contains("station"))
                {
                    newLocation = "Research Station";
                    detectedAreas.Add("Research Station");
                }
                else if (objName.Contains("creative") && objName.Contains("workstation"))
                {
                    newLocation = "Creative Workstation";
                    detectedAreas.Add("Creative Workstation");
                }
                else if (objName.Contains("social") && objName.Contains("hub"))
                {
                    newLocation = "Social Hub";
                    detectedAreas.Add("Social Hub");
                }
                else if (objName.Contains("rest") && objName.Contains("area"))
                {
                    newLocation = "Rest Area";
                    detectedAreas.Add("Rest Area");
                }
            }
            
            // Update current location if changed
            if (!string.IsNullOrEmpty(newLocation) && newLocation != currentLocation)
            {
                string previousLocation = currentLocation;
                currentLocation = newLocation;
                
                if (logAllMessages && !string.IsNullOrEmpty(previousLocation))
                {
                    Debug.Log($"📍 {AgentName}: Moved from {previousLocation} to {currentLocation}");
                }
                
                // Update beliefs about location
                if (enableCognitiveArchitecture && bdiEngine != null)
                {
                    bdiEngine.AddBelief("current_location", new[] { currentLocation }, 1.0f, "I know where I am");
                }
            }
        }
        
        #endregion
        
        #region Enhanced Prompt Generation
        
        private string GenerateInnerDialoguePrompt()
        {
            List<string> context = new List<string>();
            
            context.Add($"You are {AgentName}, currently in {currentLocation}.");
            
            // Add location-specific context
            if (!string.IsNullOrEmpty(currentLocation))
            {
                switch (currentLocation.ToLower())
                {
                    case "research station":
                        context.Add("This is a place for deep thinking and research. Consider your academic goals and intellectual pursuits.");
                        break;
                    case "creative workstation":
                        context.Add("This space inspires creativity and innovation. Think about your creative projects and artistic expression.");
                        break;
                    case "social hub":
                        context.Add("This is a social space for interaction and collaboration. Consider your relationships and social connections.");
                        break;
                    case "rest area":
                        context.Add("This is a peaceful space for reflection and relaxation. Take time for personal thoughts and self-care.");
                        break;
                }
            }
            
            // Add cognitive architecture context
            if (enableCognitiveArchitecture && needsManager != null)
            {
                string motivationPrompt = needsManager.GenerateMotivationPrompt();
                if (!string.IsNullOrEmpty(motivationPrompt))
                {
                    context.Add(motivationPrompt);
                }
                
                if (bdiEngine != null && bdiEngine.CurrentIntention != null)
                {
                    context.Add($"You're currently focused on: {bdiEngine.CurrentIntention.description}.");
                }
            }
            
            // Add social context
            if (nearbyAgents.Count > 0)
            {
                string agentNames = string.Join(", ", nearbyAgents.ConvertAll(a => a.AgentName));
                context.Add($"You can see: {agentNames}.");
            }
            
            context.Add("Reflect on your current situation thoughtfully. Keep your thoughts authentic to your personality.");
            
            return string.Join(" ", context);
        }
        
        private string GenerateCommunicationPrompt(Agent targetAgent)
        {
            List<string> context = new List<string>();
            
            context.Add($"You see {targetAgent.AgentName} in {currentLocation}.");
            
            // Add location-specific communication style
            if (!string.IsNullOrEmpty(currentLocation))
            {
                switch (currentLocation.ToLower())
                {
                    case "research station":
                        context.Add("This is a research environment. Consider discussing academic topics, sharing ideas, or collaborating on intellectual pursuits.");
                        break;
                    case "creative workstation":
                        context.Add("This is a creative space. Think about discussing artistic projects, creative inspirations, or collaborative creative work.");
                        break;
                    case "social hub":
                        context.Add("This is a social space. Feel free to have casual conversations, share personal interests, or strengthen social bonds.");
                        break;
                    case "rest area":
                        context.Add("This is a relaxing space. Consider having gentle, supportive conversations or sharing personal reflections.");
                        break;
                }
            }
            
            // Add cognitive context
            if (enableCognitiveArchitecture && needsManager != null)
            {
                string motivationPrompt = needsManager.GenerateMotivationPrompt();
                if (!string.IsNullOrEmpty(motivationPrompt))
                {
                    context.Add(motivationPrompt);
                }
            }
            
            context.Add("Start a natural conversation that fits your personality and the environment.");
            
            return string.Join(" ", context);
        }
        
        private string GeneratePlanningPrompt()
        {
            List<string> context = new List<string>();
            
            context.Add($"You are {AgentName}, currently in {currentLocation}.");
            context.Add("Take time to think strategically about your goals and plans.");
            
            // Add current beliefs and desires from BDI
            if (bdiEngine != null)
            {
                if (bdiEngine.Beliefs.Count > 0)
                {
                    var recentBeliefs = bdiEngine.Beliefs.Take(3);
                    context.Add($"Your current beliefs include: {string.Join(", ", recentBeliefs.Select(b => b.description))}.");
                }
                
                if (bdiEngine.Desires.Count > 0)
                {
                    var topDesires = bdiEngine.Desires.Take(3);
                    context.Add($"Your current desires include: {string.Join(", ", topDesires.Select(d => d.description))}.");
                }
            }
            
            // Add needs context
            if (needsManager != null)
            {
                context.Add(needsManager.GenerateMotivationPrompt());
            }
            
            context.Add("Based on your situation, think step-by-step about what goals you should pursue and how to achieve them. Be specific and practical.");
            
            return string.Join(" ", context);
        }
        
        #endregion
        
        #region Utility Methods
        
        private bool HasAIClient()
        {
            return (useEnhancedAI && enhancedLLMClient != null) || fallbackLLMClient != null;
        }
        
        private bool ShouldPerformPlanning()
        {
            if (!enableCognitiveArchitecture || bdiEngine == null) return false;
            
            // Plan more frequently if we have urgent needs or no current intention
            if (needsManager != null && needsManager.UrgentNeeds.Count > 1) return true;
            if (bdiEngine.CurrentIntention == null) return true;
            
            return Random.value < 0.3f; // 30% chance normally
        }
        
        private Agent SelectCommunicationTarget()
        {
            if (nearbyAgents.Count == 0) return null;
            
            // Prefer agents we haven't talked to recently
            var sortedAgents = nearbyAgents.OrderBy(a => 
                conversationHistory.Count(m => m.senderId == a.AgentId || m.receiverId == a.AgentId)
            ).ToList();
            
            return sortedAgents[Random.Range(0, Mathf.Min(3, sortedAgents.Count))];
        }
        
        private string GetCurrentNeed()
        {
            if (needsManager == null) return null;
            var urgentNeed = needsManager.UrgentNeeds.FirstOrDefault();
            return urgentNeed?.needName;
        }
        
        private string GetCurrentIntention()
        {
            if (bdiEngine == null) return null;
            return bdiEngine.CurrentIntention?.description;
        }
        
        private void ExtractBeliefsFromThoughts(string thoughts)
        {
            // Simple belief extraction - could be enhanced with NLP
            if (thoughts.Contains("I believe") || thoughts.Contains("I think"))
            {
                bdiEngine.AddBelief("self_reflection", new[] { thoughts.Substring(0, Mathf.Min(100, thoughts.Length)) }, 0.7f, "From inner dialogue");
            }
        }
        
        private void ExtractGoalsFromPlanning(string planning)
        {
            // Simple goal extraction - could be enhanced with NLP
            if (planning.Contains("I should") || planning.Contains("I want to") || planning.Contains("My goal"))
            {
                // Note: Desires are automatically generated from needs in BDI system
                // Goals extracted from planning are handled by the BDI engine's natural cycle
            }
        }
        
        #endregion
        
        #region Message Handling (Existing methods with minor enhancements)
        
        public void ReceiveMessage(Message message)
        {
            AddToConversationHistory(message);
            
            if (enableCognitiveArchitecture && needsManager != null)
            {
                needsManager.SatisfyNeedsFromSocialInteraction("received_message", 0.5f);
            }
            
            if (ShouldRespondToMessage(message))
            {
                StartCoroutine(DelayedEnhancedResponse(message, Random.Range(1f, 3f)));
            }
        }
        
        private IEnumerator DelayedEnhancedResponse(Message originalMessage, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            string responsePrompt = GenerateResponsePrompt(originalMessage);
            OllamaClientEnhanced.TaskType taskType = DetermineCommunicationTaskType(
                GameManager.Instance?.GetAgent(originalMessage.senderId)
            );
            
            if (useEnhancedAI && enhancedLLMClient != null)
            {
                enhancedLLMClient.SendChatRequestEnhanced(
                    AgentId,
                    responsePrompt,
                    taskType,
                    Personality,
                    conversationHistory,
                    (response) => ProcessResponseToMessage(response, originalMessage),
                    DetermineUrgency()
                );
            }
            else
            {
                fallbackLLMClient?.SendChatRequest(AgentId, responsePrompt, Personality, conversationHistory,
                    (response) => ProcessResponseToMessage(response, originalMessage));
            }
        }
        
        private void ProcessResponseToMessage(LLMResponse response, Message originalMessage)
        {
            // Track response metrics
            lastResponseTime = Time.time;
            lastResponseQuality = response.success ? 1.0f : 0.0f;
            lastModelUsed = "enhanced_client";
            
            if (response.success)
            {
                var responseMessage = new Message(response.content, AgentId, originalMessage.senderId)
                {
                    type = MessageType.InterAgent,
                    emotionalTone = response.emotionalTone,
                    senderPosition = transform.position,
                    previousMessageId = originalMessage.messageId,
                    turnNumber = messageCount++
                };
                
                AddToConversationHistory(responseMessage);
                
                var senderAgent = GameManager.Instance?.GetAgent(originalMessage.senderId);
                senderAgent?.ReceiveMessage(responseMessage);
                
                if (enableCognitiveArchitecture && needsManager != null)
                {
                    needsManager.SatisfyNeedsFromSocialInteraction("response", 0.6f);
                }
                
                if (logAllMessages)
                {
                    Debug.Log($"💬 {AgentName} → {originalMessage.senderId}: {responseMessage.GetFormattedOutput()}");
                }
            }
        }
        
        private string GenerateResponsePrompt(Message originalMessage)
        {
            string prompt = $"{originalMessage.senderId} just said to you: '{originalMessage.content}'. ";
            
            // Add location context to response
            if (!string.IsNullOrEmpty(currentLocation))
            {
                prompt += $"You're both in {currentLocation}. ";
            }
            
            if (enableCognitiveArchitecture && needsManager != null)
            {
                string motivationPrompt = needsManager.GenerateMotivationPrompt();
                if (!string.IsNullOrEmpty(motivationPrompt))
                {
                    prompt += motivationPrompt + " ";
                }
            }
            
            prompt += "Respond naturally according to your personality and the environment.";
            
            return prompt;
        }
        
        private bool ShouldRespondToMessage(Message message)
        {
            if (!message.expectsResponse) return false;
            
            float baseProbability = 0.8f;
            float personalityModifier = Personality?.agreeableness ?? 0.5f;
            
            if (enableCognitiveArchitecture && needsManager != null)
            {
                var socialNeeds = needsManager.activeNeeds.Where(n => n.needName.Contains("Social"));
                if (socialNeeds.Any(n => n.IsUrgent))
                {
                    baseProbability += 0.2f;
                }
            }
            
            return Random.value < (baseProbability * personalityModifier);
        }
        
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
        
        private void AddToConversationHistory(Message message)
        {
            conversationHistory.Add(message);
            
            if (conversationHistory.Count > 50)
            {
                conversationHistory.RemoveAt(0);
            }
        }
        
        #endregion
        
        #region Debug & Visualization
        
        private void OnDrawGizmosSelected()
        {
            if (!showDebugInfo) return;
            
            // Draw perception range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, perceptionRange);
            
            // Draw location detection range
            if (adaptToLocation)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, perceptionRange * 1.5f);
            }
            
            // Draw connections to nearby agents
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
            
            // Show cognitive state
            if (enableCognitiveArchitecture && needsManager != null && showCognitiveStatus)
            {
                Gizmos.color = Color.Lerp(Color.red, Color.green, needsManager.OverallWellbeing);
                Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.3f);
            }
        }
        
        #if UNITY_EDITOR
        [Header("Enhanced Debug Info")]
        [SerializeField] private int historyCount;
        [SerializeField] private int nearbyCount;
        [SerializeField] private string detectedAreasDisplay;
        [SerializeField] private string lastAIModel;
        [SerializeField] private string cognitiveStatus;
        [SerializeField] private string responseMetrics;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                historyCount = conversationHistory?.Count ?? 0;
                nearbyCount = nearbyAgents?.Count ?? 0;
                detectedAreasDisplay = string.Join(", ", detectedAreas);
                lastAIModel = lastModelUsed;
                
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
                            status += $"\\nCurrent Goal: {bdiEngine.CurrentIntention.intentionName}";
                        }
                    }
                    
                    cognitiveStatus = status;
                }
                
                // Update response metrics display
                if (lastResponseTime > 0)
                {
                    float timeSinceLastResponse = Time.time - lastResponseTime;
                    responseMetrics = $"Last Response: {timeSinceLastResponse:F1}s ago | Quality: {lastResponseQuality:F2} | Model: {lastModelUsed}";
                }
                else
                {
                    responseMetrics = "No responses yet";
                }
            }
        }
        #endif
        
        #endregion
    }
}
