using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIWorld.Communication;
using AIWorld.Data;

namespace AIWorld.Testing
{
    /// <summary>
    /// Test script for the Enhanced Ollama Client with intelligent model routing
    /// </summary>
    public class EnhancedClientTester : MonoBehaviour
    {
        [Header("Test Configuration")]
        public AgentPersonality testPersonality;
        public OllamaClientEnhanced enhancedClient;
        public bool runTestsOnStart = true;
        public float testDelay = 2f;
        
        [Header("Test Controls")]
        [Space]
        public bool testBasicConnectivity = true;
        public bool testModelRouting = true;
        public bool testDifferentTaskTypes = true;
        public bool testPersonalityVariations = true;
        
        [Header("Debug Info")]
        [SerializeField] private int testsCompleted = 0;
        [SerializeField] private int testsSuccessful = 0;
        [SerializeField] private string lastModelSelected = "";
        
        void Start()
        {
            if (enhancedClient == null)
                enhancedClient = FindObjectOfType<OllamaClientEnhanced>();
                
            if (enhancedClient == null)
            {
                Debug.LogError("❌ No OllamaClientEnhanced found! Please add one to the scene.");
                return;
            }
            
            if (testPersonality == null)
            {
                Debug.LogWarning("⚠️ No test personality assigned. Some tests will use null personality.");
            }
            
            if (runTestsOnStart)
            {
                Debug.Log("🚀 Starting Enhanced Ollama Client Tests...");
                StartCoroutine(RunAllTests());
            }
        }
        
        IEnumerator RunAllTests()
        {
            Debug.Log("=== 🧪 ENHANCED OLLAMA CLIENT TEST SUITE ===");
            
            if (testBasicConnectivity)
            {
                yield return StartCoroutine(TestBasicConnectivity());
                yield return new WaitForSeconds(testDelay);
            }
            
            if (testModelRouting)
            {
                yield return StartCoroutine(TestModelRoutingLogic());
                yield return new WaitForSeconds(testDelay);
            }
            
            if (testDifferentTaskTypes)
            {
                yield return StartCoroutine(TestDifferentTaskTypes());
                yield return new WaitForSeconds(testDelay);
            }
            
            if (testPersonalityVariations)
            {
                yield return StartCoroutine(TestPersonalityVariations());
            }
            
            Debug.Log($"=== 📊 TEST RESULTS: {testsSuccessful}/{testsCompleted} PASSED ===");
        }
        
        IEnumerator TestBasicConnectivity()
        {
            Debug.Log("🔌 Testing Basic Connectivity...");
            bool responseReceived = false;
            
            enhancedClient.SendChatRequestEnhanced(
                "connectivity-test",
                "Hello, can you respond to confirm connectivity?",
                OllamaClientEnhanced.TaskType.StatusUpdate,
                testPersonality,
                new List<Message>(),
                response => {
                    responseReceived = true;
                    if (response.success)
                    {
                        Debug.Log($"✅ Connectivity Test PASSED: {response.content}");
                        testsSuccessful++;
                    }
                    else
                    {
                        Debug.LogError($"❌ Connectivity Test FAILED: {response.error}");
                    }
                    testsCompleted++;
                },
                OllamaClientEnhanced.TaskUrgency.High
            );
            
            // Wait for response with timeout
            float timeout = 30f;
            while (!responseReceived && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }
            
            if (!responseReceived)
            {
                Debug.LogError("❌ Connectivity Test TIMEOUT");
                testsCompleted++;
            }
        }
        
        IEnumerator TestModelRoutingLogic()
        {
            Debug.Log("🎯 Testing Model Routing Logic...");
            
            var taskTypes = new[]
            {
                OllamaClientEnhanced.TaskType.BDIReasoning,
                OllamaClientEnhanced.TaskType.Conversation,
                OllamaClientEnhanced.TaskType.StatusUpdate,
                OllamaClientEnhanced.TaskType.InnerDialogue
            };
            
            foreach (var taskType in taskTypes)
            {
                bool routingReceived = false;
                
                enhancedClient.TestModelRouting(
                    taskType,
                    testPersonality,
                    routingResult => {
                        routingReceived = true;
                        if (routingResult.success)
                        {
                            lastModelSelected = routingResult.routing.selectedModel;
                            Debug.Log($"✅ {taskType} → {lastModelSelected} ({routingResult.routing.reason})");
                            testsSuccessful++;
                        }
                        else
                        {
                            Debug.LogError($"❌ Routing failed for {taskType}: {routingResult.error}");
                        }
                        testsCompleted++;
                    }
                );
                
                // Wait for routing response
                float timeout = 10f;
                while (!routingReceived && timeout > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                    timeout -= 0.1f;
                }
                
                yield return new WaitForSeconds(0.5f); // Brief pause between tests
            }
        }
        
        IEnumerator TestDifferentTaskTypes()
        {
            Debug.Log("📋 Testing Different Task Types with Real Requests...");
            
            var testCases = new[]
            {
                new { 
                    taskType = OllamaClientEnhanced.TaskType.BDIReasoning, 
                    prompt = "I need to plan my research goals systematically. Help me think through this step by step.",
                    urgency = OllamaClientEnhanced.TaskUrgency.Normal,
                    expectedModel = "qwen3:4b",
                    icon = "🧠"
                },
                new { 
                    taskType = OllamaClientEnhanced.TaskType.Conversation, 
                    prompt = "What's your favorite aspect of creative problem-solving?",
                    urgency = OllamaClientEnhanced.TaskUrgency.Normal,
                    expectedModel = "wizardlm2:7b",
                    icon = "💬"
                },
                new { 
                    taskType = OllamaClientEnhanced.TaskType.StatusUpdate, 
                    prompt = "Are you ready?",
                    urgency = OllamaClientEnhanced.TaskUrgency.High,
                    expectedModel = "gemma3:1b",
                    icon = "⚡"
                }
            };
            
            foreach (var test in testCases)
            {
                bool responseReceived = false;
                
                Debug.Log($"{test.icon} Testing {test.taskType} (expecting {test.expectedModel})...");
                
                enhancedClient.SendChatRequestEnhanced(
                    "task-test",
                    test.prompt,
                    test.taskType,
                    testPersonality,
                    new List<Message>(),
                    response => {
                        responseReceived = true;
                        if (response.success)
                        {
                            Debug.Log($"✅ {test.taskType} Response: {response.content.Substring(0, Mathf.Min(100, response.content.Length))}...");
                            testsSuccessful++;
                        }
                        else
                        {
                            Debug.LogError($"❌ {test.taskType} Failed: {response.error}");
                        }
                        testsCompleted++;
                    },
                    test.urgency
                );
                
                // Wait for response
                float timeout = 60f;
                while (!responseReceived && timeout > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                    timeout -= 0.1f;
                }
                
                yield return new WaitForSeconds(testDelay);
            }
        }
        
        IEnumerator TestPersonalityVariations()
        {
            Debug.Log("🎭 Testing Personality-Driven Responses...");
            
            if (testPersonality == null)
            {
                Debug.LogWarning("⚠️ Skipping personality tests - no personality assigned");
                yield break;
            }
            
            bool responseReceived = false;
            
            enhancedClient.SendChatRequestEnhanced(
                "personality-test",
                "Tell me about your approach to creative work and collaboration.",
                OllamaClientEnhanced.TaskType.PersonalityExpression,
                testPersonality,
                new List<Message>(),
                response => {
                    responseReceived = true;
                    if (response.success)
                    {
                        Debug.Log($"✅ Personality Expression: {response.content}");
                        testsSuccessful++;
                    }
                    else
                    {
                        Debug.LogError($"❌ Personality test failed: {response.error}");
                    }
                    testsCompleted++;
                },
                OllamaClientEnhanced.TaskUrgency.Normal
            );
            
            float timeout = 60f;
            while (!responseReceived && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }
        }
        
        // Manual test buttons for Inspector
        [ContextMenu("Test Basic Connectivity")]
        public void ManualTestConnectivity()
        {
            StartCoroutine(TestBasicConnectivity());
        }
        
        [ContextMenu("Test Model Routing")]
        public void ManualTestRouting()
        {
            StartCoroutine(TestModelRoutingLogic());
        }
        
        [ContextMenu("Test All Task Types")]
        public void ManualTestTaskTypes()
        {
            StartCoroutine(TestDifferentTaskTypes());
        }
    }
}
