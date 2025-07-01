using System.Collections.Generic;
using UnityEngine;
using AIWorld.Agents;
using AIWorld.Data;
using System.Linq;

namespace AIWorld.Managers
{
    /// <summary>
    /// Enhanced GameManager with agent identity validation and unique name enforcement
    /// Prevents duplicate agent names and provides comprehensive agent management
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
        
        [Header("Identity Management")]
        public bool enforceUniqueNames = true;
        public bool autoFixDuplicateNames = true;
        public bool logIdentityValidation = true;
        
        [Header("Debug & Monitoring")]
        public bool logAgentRegistration = true;
        public bool showSimulationStats = true;
        public float statsUpdateInterval = 5f;
        
        // Agent management
        private Dictionary<string, Agent> agentRegistry;
        private Dictionary<string, Agent> agentsByName; // NEW: Track agents by display name
        private List<Agent> allAgents;
        
        // Identity tracking
        private HashSet<string> usedAgentNames;
        private Dictionary<string, int> nameCounters; // For auto-fixing duplicates
        
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
        /// Initialize the GameManager with enhanced identity tracking
        /// </summary>
        private void Initialize()
        {
            agentRegistry = new Dictionary<string, Agent>();
            agentsByName = new Dictionary<string, Agent>();
            allAgents = new List<Agent>();
            usedAgentNames = new HashSet<string>();
            nameCounters = new Dictionary<string, int>();
            
            Debug.Log("🎮 GameManager initialized with identity validation");
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
            
            // Validate all agent identities before starting
            ValidateAllAgentIdentities();
            
            simulationStartTime = Time.time;
            IsSimulationRunning = true;
            Time.timeScale = simulationSpeed;
            
            Debug.Log($"🚀 AI World Simulation started with {ActiveAgentCount} agents");
            LogAgentIdentitySummary();
            
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
        /// Register an agent with enhanced identity validation
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
            
            // Validate and potentially fix agent identity
            string validatedName = ValidateAgentIdentity(agent);
            
            // Update agent name if it was changed
            if (validatedName != agent.AgentName)
            {
                Debug.Log($"🔧 Updated agent name: {agent.AgentName} → {validatedName}");
                // Note: ForceUpdateName method needs to be added to Agent.cs
                if (agent.GetType().GetMethod("ForceUpdateName") != null)
                {
                    agent.GetType().GetMethod("ForceUpdateName").Invoke(agent, new object[] { validatedName });
                }
                else
                {
                    Debug.LogWarning($"⚠️ Agent.ForceUpdateName method not found. Please add to Agent.cs");
                }
            }
            
            // Register agent
            agentRegistry[agent.agentId] = agent;
            agentsByName[agent.AgentName] = agent;
            allAgents.Add(agent);
            usedAgentNames.Add(agent.AgentName);
            
            if (logAgentRegistration)
            {
                Debug.Log($"✅ Registered agent: {agent.AgentName} (ID: {agent.agentId})");
            }
        }
        
        /// <summary>
        /// Validate agent identity and ensure uniqueness
        /// </summary>
        private string ValidateAgentIdentity(Agent agent)
        {
            string originalName = agent.AgentName;
            string validatedName = originalName;
            
            if (enforceUniqueNames)
            {
                // Check for name collision
                if (usedAgentNames.Contains(originalName) || agentsByName.ContainsKey(originalName))
                {
                    if (autoFixDuplicateNames)
                    {
                        validatedName = GenerateUniqueVariant(originalName);
                        
                        if (logIdentityValidation)
                        {
                            Debug.LogWarning($"⚠️ Name collision detected: '{originalName}' → '{validatedName}'");
                        }
                    }
                    else
                    {
                        Debug.LogError($"❌ Agent name '{originalName}' is already in use! Set autoFixDuplicateNames = true to auto-resolve.");
                        return originalName; // Return original name, let caller handle
                    }
                }
            }
            
            if (logIdentityValidation && validatedName == originalName)
            {
                Debug.Log($"✅ Agent identity validated: {validatedName}");
            }
            
            return validatedName;
        }
        
        /// <summary>
        /// Generate a unique variant of a name by appending a number
        /// </summary>
        private string GenerateUniqueVariant(string baseName)
        {
            // Extract base name without any existing number suffix
            string cleanBaseName = baseName;
            if (System.Text.RegularExpressions.Regex.IsMatch(baseName, @" \d+$"))
            {
                cleanBaseName = System.Text.RegularExpressions.Regex.Replace(baseName, @" \d+$", "");
            }
            
            if (!nameCounters.ContainsKey(cleanBaseName))
            {
                nameCounters[cleanBaseName] = 1;
            }
            
            string candidate;
            do
            {
                nameCounters[cleanBaseName]++;
                candidate = $"{cleanBaseName} {nameCounters[cleanBaseName]}";
            }
            while (usedAgentNames.Contains(candidate) || agentsByName.ContainsKey(candidate));
            
            return candidate;
        }
        
        /// <summary>
        /// Validate all currently registered agent identities
        /// </summary>
        private void ValidateAllAgentIdentities()
        {
            if (allAgents == null || allAgents.Count == 0) return;
            
            var duplicateGroups = allAgents
                .Where(a => a != null)
                .GroupBy(a => a.AgentName)
                .Where(g => g.Count() > 1);
            
            foreach (var group in duplicateGroups)
            {
                Debug.LogWarning($"⚠️ Found {group.Count()} agents with name '{group.Key}'");
                
                if (autoFixDuplicateNames)
                {
                    var agents = group.ToList();
                    for (int i = 1; i < agents.Count; i++) // Keep first agent's name, rename others
                    {
                        string newName = GenerateUniqueVariant(agents[i].AgentName);
                        string oldName = agents[i].AgentName;
                        
                        // Update tracking dictionaries
                        agentsByName.Remove(oldName);
                        usedAgentNames.Remove(oldName);
                        
                        // Update agent name
                        if (agents[i].GetType().GetMethod("ForceUpdateName") != null)
                        {
                            agents[i].GetType().GetMethod("ForceUpdateName").Invoke(agents[i], new object[] { newName });
                        }
                        
                        agentsByName[newName] = agents[i];
                        usedAgentNames.Add(newName);
                        
                        Debug.Log($"🔧 Auto-fixed duplicate: '{oldName}' → '{newName}'");
                    }
                }
            }
        }
        
        /// <summary>
        /// Log summary of all agent identities
        /// </summary>
        private void LogAgentIdentitySummary()
        {
            if (allAgents == null || allAgents.Count == 0) return;
            
            Debug.Log("👥 Agent Identity Summary:");
            foreach (var agent in allAgents)
            {
                if (agent != null)
                {
                    Debug.Log($"   🤖 {agent.AgentName} ({agent.Personality?.role ?? "Unknown Role"})");
                }
            }
        }
        
        /// <summary>
        /// Check if an agent name is already in use
        /// </summary>
        public bool IsNameInUse(string name)
        {
            return usedAgentNames.Contains(name) || agentsByName.ContainsKey(name);
        }
        
        /// <summary>
        /// Get agent by display name
        /// </summary>
        public Agent GetAgentByName(string name)
        {
            agentsByName.TryGetValue(name, out Agent agent);
            return agent;
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
                agentsByName.Remove(agent.AgentName);
                allAgents.Remove(agent);
                usedAgentNames.Remove(agent.AgentName);
                
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
        /// Log final simulation statistics with identity information
        /// </summary>
        private void LogFinalStats()
        {
            Debug.Log("═══════════════════════════════════════════════════");
            Debug.Log("🏁 FINAL SIMULATION STATISTICS");
            Debug.Log("═══════════════════════════════════════════════════");
            Debug.Log($"⏱️  Total Simulation Time: {SimulationTime:F1} seconds");
            Debug.Log($"🤖 Total Agents: {ActiveAgentCount}");
            Debug.Log($"📛 Unique Agent Names: {usedAgentNames.Count}");
            Debug.Log($"💬 Total Inter-Agent Messages: {totalMessages}");
            Debug.Log($"💭 Total Inner Dialogues: {totalInnerDialogues}");
            Debug.Log($"📈 Total Communications: {totalMessages + totalInnerDialogues}");
            
            if (ActiveAgentCount > 0)
            {
                float avgMessagesPerAgent = (float)(totalMessages + totalInnerDialogues) / ActiveAgentCount;
                Debug.Log($"📊 Average Messages per Agent: {avgMessagesPerAgent:F1}");
            }
            
            // Show any name duplications that were resolved
            if (nameCounters.Count > 0)
            {
                Debug.Log($"🔧 Name Variants Created: {nameCounters.Count}");
                foreach (var kvp in nameCounters)
                {
                    Debug.Log($"   '{kvp.Key}' → {kvp.Value} variants");
                }
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
        /// Check for and report any identity issues
        /// </summary>
        [ContextMenu("Validate Agent Identities")]
        public void ValidateAgentIdentitiesManual()
        {
            ValidateAllAgentIdentities();
            LogAgentIdentitySummary();
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
            
            // Clear identity tracking
            usedAgentNames.Clear();
            nameCounters.Clear();
            agentsByName.Clear();
            
            // Clear conversation histories
            foreach (var agent in allAgents)
            {
                agent.ConversationHistory.Clear();
            }
            
            Debug.Log("🔄 Simulation reset with identity tracking cleared");
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
            if (agentRegistry != null) agentRegistry.Clear();
            if (agentsByName != null) agentsByName.Clear();
            if (allAgents != null) allAgents.Clear();
            if (usedAgentNames != null) usedAgentNames.Clear();
            if (nameCounters != null) nameCounters.Clear();
            
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
                    if (agentsByName != null) agentsByName.Clear();
                    if (allAgents != null) allAgents.Clear();
                    if (usedAgentNames != null) usedAgentNames.Clear();
                    if (nameCounters != null) nameCounters.Clear();
                    
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