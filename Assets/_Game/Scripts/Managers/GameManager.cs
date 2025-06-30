using System.Collections.Generic;
using UnityEngine;
using AIWorld.Agents;
using AIWorld.Data;

namespace AIWorld.Managers
{
    /// <summary>
    /// Singleton GameManager that coordinates all agents and manages the simulation
    /// Provides central access to agent registry and simulation control
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GameManager>();
                    if (_instance == null)
                    {
                        GameObject gameManagerObject = new GameObject("GameManager");
                        _instance = gameManagerObject.AddComponent<GameManager>();
                        DontDestroyOnLoad(gameManagerObject);
                    }
                }
                return _instance;
            }
        }
        
        [Header("Simulation Configuration")]
        public bool autoStartSimulation = true;
        public float simulationSpeed = 1f;
        public int maxAgentCount = 10;
        
        [Header("Debug & Monitoring")]
        public bool logAgentRegistration = true;
        public bool showSimulationStats = true;
        public float statsUpdateInterval = 5f;
        
        // Agent management
        private Dictionary<string, Agent> agentRegistry;
        private List<Agent> allAgents;
        
        // Simulation stats
        private int totalMessages = 0;
        private int totalInnerDialogues = 0;
        private float simulationStartTime;
        
        // Properties
        public int ActiveAgentCount => allAgents?.Count ?? 0;
        public bool IsSimulationRunning { get; private set; }
        public float SimulationTime => Time.time - simulationStartTime;
        
        private void Awake()
        {
            // Singleton pattern
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
                
                #if UNITY_EDITOR
                // Subscribe to play mode state changes in editor
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                #endif
            }
            else if (_instance != this)
            {
                Debug.LogWarning("Multiple GameManager instances detected. Destroying duplicate.");
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            if (autoStartSimulation)
            {
                StartSimulation();
            }
            
            // Start stats monitoring
            if (showSimulationStats)
            {
                InvokeRepeating(nameof(LogSimulationStats), statsUpdateInterval, statsUpdateInterval);
            }
        }
        
        /// <summary>
        /// Initialize the GameManager
        /// </summary>
        private void Initialize()
        {
            agentRegistry = new Dictionary<string, Agent>();
            allAgents = new List<Agent>();
            
            Debug.Log("🎮 GameManager initialized");
        }
        
        /// <summary>
        /// Start the simulation
        /// </summary>
        public void StartSimulation()
        {
            if (IsSimulationRunning)
            {
                Debug.LogWarning("Simulation is already running");
                return;
            }
            
            // Ensure collections are initialized
            if (allAgents == null) Initialize();
            
            simulationStartTime = Time.time;
            IsSimulationRunning = true;
            Time.timeScale = simulationSpeed;
            
            Debug.Log($"🚀 AI World Simulation started with {ActiveAgentCount} agents");
            
            // Log initial agent states
            if (allAgents != null)
            {
                foreach (var agent in allAgents)
                {
                    if (agent != null)
                    {
                        Debug.Log($"📍 {agent.AgentName} positioned at {agent.transform.position}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Stop the simulation
        /// </summary>
        public void StopSimulation()
        {
            if (!IsSimulationRunning)
            {
                Debug.LogWarning("Simulation is not running");
                return;
            }
            
            IsSimulationRunning = false;
            Time.timeScale = 1f;
            
            Debug.Log($"⏹️ Simulation stopped after {SimulationTime:F1} seconds");
            LogFinalStats();
        }
        
        /// <summary>
        /// Pause/resume the simulation
        /// </summary>
        public void ToggleSimulationPause()
        {
            if (IsSimulationRunning)
            {
                Time.timeScale = Time.timeScale > 0 ? 0 : simulationSpeed;
                Debug.Log(Time.timeScale > 0 ? "▶️ Simulation resumed" : "⏸️ Simulation paused");
            }
        }
        
        /// <summary>
        /// Register an agent with the manager
        /// </summary>
        public void RegisterAgent(Agent agent)
        {
            if (agent == null)
            {
                Debug.LogError("Cannot register null agent");
                return;
            }
            
            // Ensure collections are initialized
            if (agentRegistry == null) Initialize();
            
            if (allAgents.Count >= maxAgentCount)
            {
                Debug.LogWarning($"Maximum agent count ({maxAgentCount}) reached. Cannot register {agent.AgentName}");
                return;
            }
            
            if (agentRegistry.ContainsKey(agent.agentId))
            {
                Debug.LogWarning($"Agent {agent.agentId} is already registered");
                return;
            }
            
            agentRegistry[agent.agentId] = agent;
            allAgents.Add(agent);
            
            if (logAgentRegistration)
            {
                Debug.Log($"✅ Registered agent: {agent.AgentName} (ID: {agent.agentId})");
            }
        }
        
        /// <summary>
        /// Unregister an agent from the manager
        /// </summary>
        public void UnregisterAgent(Agent agent)
        {
            if (agent == null) return;
            
            if (agentRegistry.ContainsKey(agent.agentId))
            {
                agentRegistry.Remove(agent.agentId);
                allAgents.Remove(agent);
                
                if (logAgentRegistration)
                {
                    Debug.Log($"❌ Unregistered agent: {agent.AgentName} (ID: {agent.agentId})");
                }
            }
        }
        
        /// <summary>
        /// Get agent by ID
        /// </summary>
        public Agent GetAgent(string agentId)
        {
            if (agentRegistry == null) return null;
            agentRegistry.TryGetValue(agentId, out Agent agent);
            return agent;
        }
        
        /// <summary>
        /// Get all registered agents
        /// </summary>
        public List<Agent> GetAllAgents()
        {
            if (allAgents == null) return new List<Agent>();
            return new List<Agent>(allAgents);
        }
        
        /// <summary>
        /// Get agents within a specific range of a position
        /// </summary>
        public List<Agent> GetAgentsInRange(Vector3 position, float range)
        {
            List<Agent> agentsInRange = new List<Agent>();
            
            // Ensure collections are initialized
            if (allAgents == null) return agentsInRange;
            
            foreach (var agent in allAgents)
            {
                if (agent != null && Vector3.Distance(position, agent.transform.position) <= range)
                {
                    agentsInRange.Add(agent);
                }
            }
            
            return agentsInRange;
        }
        
        /// <summary>
        /// Track message statistics
        /// </summary>
        public void OnMessageSent(Message message)
        {
            if (message.IsInnerDialogue)
            {
                totalInnerDialogues++;
            }
            else
            {
                totalMessages++;
            }
        }
        
        /// <summary>
        /// Log current simulation statistics
        /// </summary>
        private void LogSimulationStats()
        {
            if (!IsSimulationRunning) return;
            
            Debug.Log($"📊 Simulation Stats - Time: {SimulationTime:F1}s | Agents: {ActiveAgentCount} | " +
                     $"Messages: {totalMessages} | Inner Dialogues: {totalInnerDialogues}");
        }
        
        /// <summary>
        /// Log final simulation statistics
        /// </summary>
        private void LogFinalStats()
        {
            Debug.Log("═══════════════════════════════════════════════════");
            Debug.Log("🏁 FINAL SIMULATION STATISTICS");
            Debug.Log("═══════════════════════════════════════════════════");
            Debug.Log($"⏱️  Total Simulation Time: {SimulationTime:F1} seconds");
            Debug.Log($"🤖 Total Agents: {ActiveAgentCount}");
            Debug.Log($"💬 Total Inter-Agent Messages: {totalMessages}");
            Debug.Log($"💭 Total Inner Dialogues: {totalInnerDialogues}");
            Debug.Log($"📈 Total Communications: {totalMessages + totalInnerDialogues}");
            
            if (ActiveAgentCount > 0)
            {
                float avgMessagesPerAgent = (float)(totalMessages + totalInnerDialogues) / ActiveAgentCount;
                Debug.Log($"📊 Average Messages per Agent: {avgMessagesPerAgent:F1}");
            }
            
            Debug.Log("═══════════════════════════════════════════════════");
        }
        
        /// <summary>
        /// Get agent conversation summaries for debugging
        /// </summary>
        [ContextMenu("Log All Agent Conversations")]
        public void LogAllConversations()
        {
            foreach (var agent in allAgents)
            {
                Debug.Log($"\n🤖 {agent.AgentName} - Conversation History ({agent.ConversationHistory.Count} messages):");
                
                foreach (var message in agent.ConversationHistory)
                {
                    Debug.Log($"  {message.GetFormattedOutput()}");
                }
            }
        }
        
        /// <summary>
        /// Reset simulation (clear all data)
        /// </summary>
        [ContextMenu("Reset Simulation")]
        public void ResetSimulation()
        {
            StopSimulation();
            
            totalMessages = 0;
            totalInnerDialogues = 0;
            
            // Clear conversation histories
            foreach (var agent in allAgents)
            {
                agent.ConversationHistory.Clear();
            }
            
            Debug.Log("🔄 Simulation reset");
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && IsSimulationRunning)
            {
                Time.timeScale = 0;
            }
        }
        
        private void OnDestroy()
        {
            // Stop any running simulations
            if (IsSimulationRunning)
            {
                LogFinalStats();
                StopSimulation();
            }
            
            // Cancel any invoked repeating methods
            CancelInvoke();
            
            // Clear agent references
            if (agentRegistry != null)
            {
                agentRegistry.Clear();
            }
            if (allAgents != null)
            {
                allAgents.Clear();
            }
            
            // Reset singleton instance if this is the current instance
            if (_instance == this)
            {
                _instance = null;
            }
            
            #if UNITY_EDITOR
            // Unsubscribe from play mode state changes in editor
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            #endif
            
            Debug.Log("🗑️ GameManager destroyed and cleaned up");
        }
        
        #if UNITY_EDITOR
        private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // Clean up when exiting play mode in editor
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                if (_instance == this)
                {
                    Debug.Log("🚪 Exiting play mode - cleaning up GameManager");
                    
                    // Stop simulation
                    if (IsSimulationRunning)
                    {
                        StopSimulation();
                    }
                    
                    // Clear references
                    if (agentRegistry != null) agentRegistry.Clear();
                    if (allAgents != null) allAgents.Clear();
                    
                    _instance = null;
                }
            }
        }
        #endif
        
        private void OnApplicationQuit()
        {
            // Ensure clean shutdown when application quits
            if (IsSimulationRunning)
            {
                StopSimulation();
            }
        }
    }
}