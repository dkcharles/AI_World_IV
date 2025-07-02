using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using AIWorld.Data;

namespace AIWorld.Communication
{
    /// <summary>
    /// Enhanced Ollama Client with intelligent model routing support
    /// Supports task-based model selection for optimal performance
    /// </summary>
    public class OllamaClientEnhanced : MonoBehaviour
    {
        [Header("Enhanced Server Configuration")]
        public string serverUrl = "http://localhost:3000";
        public bool useEnhancedEndpoints = true;
        public bool enableTaskRouting = true;
        public bool logModelSelection = true;
        
        [Header("Model Routing Settings")]
        public string forceModel = ""; // Leave empty for automatic selection
        public TaskUrgency defaultUrgency = TaskUrgency.Normal;
        
        [Header("Debug Information")]
        [SerializeField] private string lastSelectedModel;
        [SerializeField] private string lastRoutingReason;
        [SerializeField] private float lastResponseTime;
        [SerializeField] private float lastQuality;
        
        // Available task types for agents
        public enum TaskType
        {
            // BDI System Tasks
            BDIReasoning,
            GoalPlanning, 
            BeliefUpdating,
            
            // Social Interaction
            Conversation,
            PersonalityExpression,
            EmotionalResponse,
            
            // Memory & Continuity
            LongConversation,
            MemoryRetrieval,
            ContextMaintenance,
            
            // Quick Responses
            StatusUpdate,
            SimpleAcknowledgment,
            ReactiveResponse,
            
            // Inner Dialogue
            InnerDialogue,
            SelfReflection
        }
        
        public enum TaskUrgency
        {
            Low,    // Prioritize quality over speed
            Normal, // Balanced approach
            High    // Prioritize speed over quality
        }
        
        /// <summary>
        /// Enhanced chat request with intelligent model routing
        /// </summary>
        public void SendChatRequestEnhanced(
            string agentId,
            string prompt,
            TaskType taskType,
            AgentPersonality personality,
            List<Message> conversationHistory,
            System.Action<LLMResponse> callback,
            TaskUrgency urgency = TaskUrgency.Normal,
            string systemPrompt = null)
        {
            StartCoroutine(SendChatRequestEnhancedCoroutine(
                agentId, prompt, taskType, personality, 
                conversationHistory, callback, urgency, systemPrompt));
        }
        
        /// <summary>
        /// Enhanced inner dialogue request with task-specific routing
        /// </summary>
        public void SendInnerDialogueRequestEnhanced(
            string agentId,
            string prompt,
            AgentPersonality personality,
            System.Action<LLMResponse> callback,
            string currentNeed = null,
            string currentIntention = null,
            string systemPrompt = null)
        {
            StartCoroutine(SendInnerDialogueRequestEnhancedCoroutine(
                agentId, prompt, personality, callback, 
                currentNeed, currentIntention, systemPrompt));
        }
        
        /// <summary>
        /// Test model routing for a specific task type
        /// </summary>
        public void TestModelRouting(
            TaskType taskType,
            AgentPersonality personality,
            System.Action<ModelRoutingResponse> callback,
            TaskUrgency urgency = TaskUrgency.Normal,
            int conversationLength = 0)
        {
            StartCoroutine(TestModelRoutingCoroutine(
                taskType, personality, callback, urgency, conversationLength));
        }
        
        private IEnumerator SendChatRequestEnhancedCoroutine(
            string agentId,
            string prompt,
            TaskType taskType,
            AgentPersonality personality,
            List<Message> conversationHistory,
            System.Action<LLMResponse> callback,
            TaskUrgency urgency,
            string systemPrompt)
        {
            string endpoint = useEnhancedEndpoints ? "/chat-enhanced" : "/chat";
            string url = serverUrl + endpoint;
            
            // Build request data - let server make parameter decisions with Unity hints
            var requestData = new EnhancedChatRequest
            {
                agentId = agentId,
                prompt = prompt,
                taskType = TaskTypeToString(taskType),
                urgency = urgency.ToString().ToLower(),
                conversationHistory = conversationHistory?.ToArray() ?? new Message[0],
                temperatureHint = GetTemperatureForTask(taskType),  // Hint only, server decides
                tokenHint = GetTokensForTask(taskType),             // Hint only, server decides
                model = string.IsNullOrEmpty(forceModel) ? null : forceModel,
                systemPrompt = !string.IsNullOrEmpty(systemPrompt) ? systemPrompt : 
                              (personality != null ? GeneratePersonalitySystemPrompt(personality) : null),
                personality = personality != null ? SerializePersonality(personality) : null
            };
            
            string jsonData = JsonUtility.ToJson(requestData);
            
            if (logModelSelection)
            {
                Debug.Log($"🚀 Enhanced Chat Request: {agentId} | Task: {taskType} | Urgency: {urgency} | Temp Hint: {requestData.temperatureHint:F2} | Token Hint: {requestData.tokenHint}");
            }
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 120; // Increased timeout for thinking mode
                
                float startTime = Time.time;
                yield return request.SendWebRequest();
                float responseTime = (Time.time - startTime) * 1000f; // Convert to milliseconds
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"📨 Raw response received: {request.downloadHandler.text.Substring(0, Mathf.Min(200, request.downloadHandler.text.Length))}...");
                    
                    try
                    {
                        Debug.Log($"🔍 Parsing JSON response...");
                        var response = JsonUtility.FromJson<EnhancedLLMResponse>(request.downloadHandler.text);
                        Debug.Log($"✅ JSON parsed successfully. Content length: {response.content?.Length ?? 0}");
                        
                        // Log model selection information
                        if (logModelSelection && response.modelInfo != null)
                        {
                            lastSelectedModel = response.modelInfo.selectedModel;
                            lastRoutingReason = response.modelInfo.reason;
                            lastResponseTime = responseTime;
                            lastQuality = response.responseMetadata?.quality ?? 0f;
                            
                            Debug.Log($"🎯 Model Selected: {lastSelectedModel} ({lastRoutingReason})");
                            Debug.Log($"⚡ Response: {responseTime:F0}ms | Quality: {lastQuality:F2}");
                            
                            if (response.modelInfo.features != null && response.modelInfo.features.Length > 0)
                            {
                                Debug.Log($"✨ Features: {string.Join(", ", response.modelInfo.features)}");
                            }
                        }
                        
                        Debug.Log($"🔄 Converting to standard response...");
                        // Convert to standard LLMResponse for compatibility
                        var standardResponse = new LLMResponse
                        {
                            success = response.success,
                            content = response.content,
                            emotionalTone = response.emotionalTone,
                            error = response.error
                        };
                        
                        Debug.Log($"📤 Invoking callback with response...");
                        callback?.Invoke(standardResponse);
                        Debug.Log($"✅ Callback completed successfully");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"❌ Failed to parse enhanced response: {e.Message}");
                        callback?.Invoke(new LLMResponse
                        {
                            success = false,
                            error = $"Parse error: {e.Message}"
                        });
                    }
                }
                else
                {
                    Debug.LogError($"❌ Enhanced chat request failed: {request.error}");
                    callback?.Invoke(new LLMResponse
                    {
                        success = false,
                        error = request.error
                    });
                }
            }
        }
        
        private IEnumerator SendInnerDialogueRequestEnhancedCoroutine(
            string agentId,
            string prompt,
            AgentPersonality personality,
            System.Action<LLMResponse> callback,
            string currentNeed,
            string currentIntention,
            string systemPrompt)
        {
            string endpoint = useEnhancedEndpoints ? "/inner-dialogue-enhanced" : "/inner-dialogue";
            string url = serverUrl + endpoint;
            
            var requestData = new InnerDialogueRequest
            {
                agentId = agentId,
                prompt = prompt,
                temperatureHint = 0.7f,  // Let server optimize inner dialogue temperature
                tokenHint = 400,         // Let server optimize inner dialogue tokens
                model = string.IsNullOrEmpty(forceModel) ? null : forceModel,
                systemPrompt = !string.IsNullOrEmpty(systemPrompt) ? systemPrompt :
                              (personality != null ? GenerateInnerDialogueSystemPrompt(personality) : null),
                personality = personality != null ? SerializePersonality(personality) : null,
                currentNeed = currentNeed,
                currentIntention = currentIntention
            };
            
            string jsonData = JsonUtility.ToJson(requestData);
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 120; // Increased timeout for thinking mode
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<EnhancedLLMResponse>(request.downloadHandler.text);
                        
                        if (logModelSelection && response.modelInfo != null)
                        {
                            Debug.Log($"🧠 Inner Dialogue Model: {response.modelInfo.selectedModel}");
                            if (response.modelInfo.thinkingModeEnabled)
                            {
                                Debug.Log($"💭 Thinking Mode: ENABLED");
                            }
                        }
                        
                        var standardResponse = new LLMResponse
                        {
                            success = response.success,
                            content = response.content,
                            emotionalTone = response.emotionalTone,
                            error = response.error
                        };
                        
                        callback?.Invoke(standardResponse);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"❌ Failed to parse enhanced inner dialogue response: {e.Message}");
                        callback?.Invoke(new LLMResponse
                        {
                            success = false,
                            error = $"Parse error: {e.Message}"
                        });
                    }
                }
                else
                {
                    Debug.LogError($"❌ Enhanced inner dialogue request failed: {request.error}");
                    callback?.Invoke(new LLMResponse
                    {
                        success = false,
                        error = request.error
                    });
                }
            }
        }
        
        private IEnumerator TestModelRoutingCoroutine(
            TaskType taskType,
            AgentPersonality personality,
            System.Action<ModelRoutingResponse> callback,
            TaskUrgency urgency,
            int conversationLength)
        {
            string url = serverUrl + "/models/route-test";
            
            var requestData = new ModelRoutingRequest
            {
                taskType = TaskTypeToString(taskType),
                urgency = urgency.ToString().ToLower(),
                conversationLength = conversationLength,
                agentPersonality = personality != null ? SerializePersonality(personality) : null,
                forceModel = string.IsNullOrEmpty(forceModel) ? null : forceModel
            };
            
            string jsonData = JsonUtility.ToJson(requestData);
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<ModelRoutingResponse>(request.downloadHandler.text);
                        callback?.Invoke(response);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"❌ Failed to parse routing test response: {e.Message}");
                        callback?.Invoke(new ModelRoutingResponse { success = false, error = e.Message });
                    }
                }
                else
                {
                    Debug.LogError($"❌ Model routing test failed: {request.error}");
                    callback?.Invoke(new ModelRoutingResponse { success = false, error = request.error });
                }
            }
        }
        
        #region Helper Methods
        
        private string TaskTypeToString(TaskType taskType)
        {
            return taskType switch
            {
                TaskType.BDIReasoning => "bdi_reasoning",
                TaskType.GoalPlanning => "goal_planning",
                TaskType.BeliefUpdating => "belief_updating",
                TaskType.Conversation => "conversation",
                TaskType.PersonalityExpression => "personality_expression",
                TaskType.EmotionalResponse => "emotional_response",
                TaskType.LongConversation => "long_conversation",
                TaskType.MemoryRetrieval => "memory_retrieval",
                TaskType.ContextMaintenance => "context_maintenance",
                TaskType.StatusUpdate => "status_update",
                TaskType.SimpleAcknowledgment => "simple_acknowledgment",
                TaskType.ReactiveResponse => "reactive_response",
                TaskType.InnerDialogue => "inner_dialogue",
                TaskType.SelfReflection => "self_reflection",
                _ => "conversation"
            };
        }
        
        private float GetTemperatureForTask(TaskType taskType)
        {
            return taskType switch
            {
                TaskType.BDIReasoning => 0.6f,     // Lower for focused reasoning
                TaskType.GoalPlanning => 0.6f,      // Lower for strategic thinking
                TaskType.Conversation => 0.8f,      // Higher for natural dialogue
                TaskType.PersonalityExpression => 0.9f, // Higher for creativity
                TaskType.InnerDialogue => 0.7f,     // Balanced for reflection
                TaskType.ReactiveResponse => 0.5f,  // Lower for consistent reactions
                _ => 0.7f // Default
            };
        }
        
        private int GetTokensForTask(TaskType taskType)
        {
            // Unity provides hints, but server will optimize based on selected model
            // These are reasonable baseline hints for any model
            return taskType switch
            {
                TaskType.BDIReasoning => 600,       // Complex reasoning (server will boost for Qwen3)
                TaskType.GoalPlanning => 550,        // Planning complexity
                TaskType.LongConversation => 800,    // Extended dialogue (server will boost for Llama3.2)
                TaskType.PersonalityExpression => 600, // Rich personality (server will optimize per model)
                TaskType.EmotionalResponse => 500,   // Emotional depth
                TaskType.Conversation => 500,        // Standard dialogue
                TaskType.InnerDialogue => 500,       // Reflection depth
                TaskType.SelfReflection => 550,      // Deep thoughts
                TaskType.MemoryRetrieval => 600,     // Memory complexity
                TaskType.ContextMaintenance => 550,  // Context handling
                TaskType.StatusUpdate => 200,        // Brief updates (server will reduce for Gemma3)
                TaskType.SimpleAcknowledgment => 150, // Very brief (server will minimize for Gemma3)
                TaskType.ReactiveResponse => 250,    // Quick responses (server optimizes for speed)
                _ => 450 // Reasonable default
            };
        }
        
        private string GeneratePersonalitySystemPrompt(AgentPersonality personality)
        {
            if (personality == null) return "";
            
            string interests = personality.primaryInterests != null && personality.primaryInterests.Length > 0 
                ? $" Your main interests: {string.Join(", ", personality.primaryInterests)}." 
                : "";
            
            return $"You are {personality.agentName}, {personality.role}. {personality.background} " +
                   $"Express your personality naturally in conversation. " +
                   $"Your traits: Creative ({personality.creativityLevel:P0}), " +
                   $"Extraverted ({personality.extraversion:P0}), " +
                   $"Agreeable ({personality.agreeableness:P0}).{interests}";
        }
        
        private string GenerateInnerDialogueSystemPrompt(AgentPersonality personality)
        {
            if (personality == null) return "";
            
            return $"You are {personality.agentName}, {personality.role}, reflecting on your thoughts and feelings. " +
                   $"{personality.background} Be introspective and authentic to your personality. " +
                   $"Consider your goals, needs, and current situation. " +
                   $"Reflection frequency: {personality.reflectionFrequency:P0}.";
        }
        
        private SerializablePersonality SerializePersonality(AgentPersonality personality)
        {
            if (personality == null) return null;
            
            return new SerializablePersonality
            {
                agentName = personality.agentName,
                role = personality.role,
                background = personality.background,
                creativityLevel = personality.creativityLevel,
                extraversion = personality.extraversion,
                agreeableness = personality.agreeableness,
                conscientiousness = personality.conscientiousness,
                neuroticism = personality.neuroticism,
                openness = personality.openness,
                formality = personality.formality,
                verbosity = personality.verbosity,
                curiosity = personality.curiosity,
                empathy = personality.empathy,
                initiativeLevel = personality.initiativeLevel,
                reflectionFrequency = personality.reflectionFrequency,
                interests = personality.primaryInterests ?? new string[0],
                expertiseAreas = personality.expertiseAreas ?? new string[0]
            };
        }
        
        #endregion
        
        #region Serializable Request Classes for Unity JSON
        
        [System.Serializable]
        public class EnhancedChatRequest
        {
            public string agentId;
            public string prompt;
            public string taskType;
            public string urgency;
            public Message[] conversationHistory;
            public float temperatureHint;  // Unity's suggested temperature (server decides final value)
            public int tokenHint;          // Unity's suggested token count (server decides final value)
            public string model;
            public string systemPrompt;
            public SerializablePersonality personality;
        }
        
        [System.Serializable]
        public class InnerDialogueRequest
        {
            public string agentId;
            public string prompt;
            public float temperatureHint;  // Unity's suggested temperature (server decides final value)
            public int tokenHint;          // Unity's suggested token count (server decides final value)
            public string model;
            public string systemPrompt;
            public SerializablePersonality personality;
            public string currentNeed;
            public string currentIntention;
        }
        
        [System.Serializable]
        public class ModelRoutingRequest
        {
            public string taskType;
            public string urgency;
            public int conversationLength;
            public SerializablePersonality agentPersonality;
            public string forceModel;
        }
        
        [System.Serializable]
        public class SerializablePersonality
        {
            public string agentName;
            public string role;
            public string background;
            public float creativityLevel;
            public float extraversion;
            public float agreeableness;
            public float conscientiousness;
            public float neuroticism;
            public float openness;
            public float formality;
            public float verbosity;
            public float curiosity;
            public float empathy;
            public float initiativeLevel;
            public float reflectionFrequency;
            public string[] interests;
            public string[] expertiseAreas;
        }
        
        #endregion
        
        #region Response Classes
        
        [System.Serializable]
        public class EnhancedLLMResponse
        {
            public bool success;
            public string content;
            public string agentId;
            public string taskType;
            public float emotionalTone;
            public string[] extractedTopics;
            public long timestamp;
            public ModelInfo modelInfo;
            public ResponseMetadata responseMetadata;
            public string error;
        }
        
        [System.Serializable]
        public class ModelInfo
        {
            public string selectedModel;
            public string reason;
            public float confidence;
            public string[] features;
            public string[] specialties;
            public int contextLimit;
            public bool thinkingModeEnabled;
        }
        
        [System.Serializable]
        public class ResponseMetadata
        {
            public int tokensUsed;
            public float responseTime;
            public bool wasRetried;
            public int attemptCount;
            public float quality;
            public bool seemsComplete;
            public int responseLength;
            public string[] modelConfig;
        }
        
        [System.Serializable]
        public class ModelRoutingResponse
        {
            public bool success;
            public string taskType;
            public RoutingInfo routing;
            public string[] availableModels;
            public long timestamp;
            public string error;
        }
        
        [System.Serializable]
        public class RoutingInfo
        {
            public string selectedModel;
            public string reason;
            public float confidence;
            public TaskConfig taskConfig;
            public ModelInfo modelInfo;
        }
        
        [System.Serializable]
        public class TaskConfig
        {
            public string primary;
            public string fallback;
            public string description;
            public string[] requiredFeatures;
        }
        
        #endregion
    }
}
