using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using AIWorld.Data;

namespace AIWorld.Communication
{
    /// <summary>
    /// Handles communication with the local Node.js server that interfaces with Ollama
    /// Manages LLM requests for both inter-agent communication and inner dialogue
    /// </summary>
    public class OllamaClient : MonoBehaviour
    {
        [Header("Server Configuration")]
        public string serverUrl = "http://localhost:3000";
        public float requestTimeout = 30f;
        public int maxRetries = 3;
        
        [Header("Default LLM Settings")]
        public string defaultModel = "llama3.2:latest";
        public float defaultTemperature = 0.7f;
        public int defaultMaxTokens = 150;
        
        [Header("Debug")]
        public bool logRequests = true;
        public bool logResponses = true;
        
        // Events for response handling
        public event System.Action<LLMResponse> OnResponseReceived;
        public event System.Action<string> OnErrorOccurred;
        
        private void Start()
        {
            // Test connection on start
            StartCoroutine(TestConnection());
        }
        
        /// <summary>
        /// Test connection to the Node.js server
        /// </summary>
        public IEnumerator TestConnection()
        {
            string testUrl = $"{serverUrl}/health";
            
            Debug.Log($"🔗 Testing connection to: {testUrl}");
            
            using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
            {
                request.timeout = (int)requestTimeout;
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"✅ Connected to Ollama server at {serverUrl}");
                    
                    // Parse and log available models
                    try
                    {
                        var healthData = JsonUtility.FromJson<HealthResponse>(request.downloadHandler.text);
                        if (healthData.ollama != null && healthData.ollama.availableModels != null)
                        {
                            Debug.Log($"📋 Available models: {string.Join(", ", healthData.ollama.availableModels)}");
                            
                            // Check if our default model is available
                            bool modelAvailable = false;
                            foreach (var model in healthData.ollama.availableModels)
                            {
                                if (model == defaultModel)
                                {
                                    modelAvailable = true;
                                    break;
                                }
                            }
                            
                            if (modelAvailable)
                            {
                                Debug.Log($"✅ Model '{defaultModel}' is available");
                            }
                            else
                            {
                                Debug.LogWarning($"⚠️ Model '{defaultModel}' not found. Available: {string.Join(", ", healthData.ollama.availableModels)}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Could not parse health response: {e.Message}");
                        Debug.Log($"Raw response: {request.downloadHandler.text}");
                    }
                }
                else
                {
                    Debug.LogError($"❌ Failed to connect to Ollama server: {request.error}");
                    Debug.LogError($"Response Code: {request.responseCode}");
                    Debug.LogError($"Response Text: {request.downloadHandler.text}");
                    OnErrorOccurred?.Invoke($"Server connection failed: {request.error}");
                }
            }
        }
        
        /// <summary>
        /// Send a chat request to the LLM for agent communication
        /// </summary>
        public void SendChatRequest(string agentId, string prompt, AgentPersonality personality, 
            List<Message> conversationHistory = null, System.Action<LLMResponse> callback = null)
        {
            var request = new LLMRequest
            {
                agentId = agentId,
                model = defaultModel,
                prompt = prompt,
                temperature = personality?.llmTemperature ?? defaultTemperature,
                maxTokens = personality?.maxTokens ?? defaultMaxTokens,
                conversationHistory = conversationHistory ?? new List<Message>(),
                systemPrompt = personality?.GeneratePersonalityContext() ?? "You are a helpful AI assistant.",
                requestType = LLMRequestType.Chat
            };
            
            StartCoroutine(SendLLMRequest(request, callback));
        }
        
        /// <summary>
        /// Send an inner dialogue request to the LLM for agent self-reflection
        /// </summary>
        public void SendInnerDialogueRequest(string agentId, string reflectionPrompt, 
            AgentPersonality personality, System.Action<LLMResponse> callback = null)
        {
            var request = new LLMRequest
            {
                agentId = agentId,
                model = defaultModel,
                prompt = reflectionPrompt,
                temperature = personality?.llmTemperature ?? defaultTemperature,
                maxTokens = personality?.maxTokens ?? defaultMaxTokens,
                systemPrompt = personality?.GeneratePersonalityContext() + 
                              " You are reflecting on your thoughts and planning your next actions.",
                requestType = LLMRequestType.InnerDialogue
            };
            
            StartCoroutine(SendLLMRequest(request, callback));
        }
        
        /// <summary>
        /// Core LLM request coroutine
        /// </summary>
        private IEnumerator SendLLMRequest(LLMRequest request, System.Action<LLMResponse> callback)
        {
            string jsonData = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            
            string endpoint = request.requestType == LLMRequestType.InnerDialogue ? "/inner-dialogue" : "/chat";
            string fullUrl = $"{serverUrl}{endpoint}";
            
            if (logRequests)
            {
                Debug.Log($"🔄 Sending {request.requestType} request for {request.agentId}: {request.prompt}");
            }
            
            using (UnityWebRequest webRequest = new UnityWebRequest(fullUrl, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = (int)requestTimeout;
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        LLMResponse response = JsonUtility.FromJson<LLMResponse>(webRequest.downloadHandler.text);
                        response.originalRequest = request;
                        
                        if (logResponses)
                        {
                            Debug.Log($"✅ LLM Response for {request.agentId}: {response.content}");
                        }
                        
                        // Invoke callback and event
                        callback?.Invoke(response);
                        OnResponseReceived?.Invoke(response);
                    }
                    catch (Exception e)
                    {
                        string error = $"Failed to parse LLM response: {e.Message}";
                        Debug.LogError(error);
                        OnErrorOccurred?.Invoke(error);
                    }
                }
                else
                {
                    string error = $"LLM request failed: {webRequest.error}";
                    Debug.LogError(error);
                    OnErrorOccurred?.Invoke(error);
                }
            }
        }
    }
    
    /// <summary>
    /// Request structure for LLM API calls
    /// </summary>
    [System.Serializable]
    public class LLMRequest
    {
        public string agentId;
        public string model;
        public string prompt;
        public string systemPrompt;
        public float temperature;
        public int maxTokens;
        public List<Message> conversationHistory;
        public LLMRequestType requestType;
        public long timestamp;
        
        public LLMRequest()
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
    
    /// <summary>
    /// Response structure from LLM API
    /// </summary>
    [System.Serializable]
    public class LLMResponse
    {
        public string content;
        public string agentId;
        public bool success;
        public string error;
        public float emotionalTone;
        public string[] extractedTopics;
        public long timestamp;
        public LLMRequest originalRequest;
    }
    
    public enum LLMRequestType
    {
        Chat,
        InnerDialogue,
        PlanningReflection,
        EmotionalAssessment
    }
    
    /// <summary>
    /// Health check response structure for debugging
    /// </summary>
    [System.Serializable]
    public class HealthResponse
    {
        public string status;
        public OllamaInfo ollama;
    }
    
    [System.Serializable]
    public class OllamaInfo
    {
        public bool connected;
        public string host;
        public string[] availableModels;
    }
}