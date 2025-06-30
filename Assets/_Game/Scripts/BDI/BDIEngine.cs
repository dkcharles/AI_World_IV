using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Needs;
using AIWorld.Data;
using AIWorld.Communication;

namespace AIWorld.BDI
{
    /// <summary>
    /// Core BDI (Belief-Desire-Intention) reasoning engine for autonomous agents
    /// Manages beliefs about the world, desires from needs, and committed intentions
    /// Enhanced with movement and object interaction capabilities
    /// </summary>
    public class BDIEngine : MonoBehaviour
    {
        [Header("BDI Configuration")]
        public bool enableBDIReasoning = true;
        public float reasoningInterval = 3f; // How often to run BDI cycle
        public int maxActiveIntentions = 2;   // Max concurrent intentions
        public int maxActiveDesires = 5;      // Max concurrent desires
        
        [Header("Belief Management")]
        public float beliefsDecayRate = 0.01f;  // How fast belief confidence decays
        public float beliefsUpdateThreshold = 0.1f; // Min change to update belief
        
        [Header("Current State")]
        [SerializeField] private List<Belief> beliefs = new List<Belief>();
        [SerializeField] private List<Desire> desires = new List<Desire>();
        [SerializeField] private List<Intention> intentions = new List<Intention>();
        [SerializeField] private Intention currentIntention;
        
        [Header("Debug")]
        public bool logBDICycle = true;
        public bool logBeliefUpdates = false;
        public bool showStateInConsole = false;
        
        // Dependencies
        private NeedsManager needsManager;
        private OllamaClient llmClient;
        private AgentPersonality personality;
        
        // Internal state
        private float lastReasoningTime;
        private int bdoCycleCount = 0;
        
        // Events
        public event System.Action<Intention> OnIntentionAdopted;
        public event System.Action<Intention> OnIntentionCompleted;
        public event System.Action<Intention> OnIntentionAbandoned;
        public event System.Action<Belief> OnBeliefUpdated;
        
        // Properties
        public List<Belief> Beliefs => beliefs;
        public List<Desire> Desires => desires;
        public List<Intention> Intentions => intentions;
        public Intention CurrentIntention => currentIntention;
        public bool HasActiveIntentions => intentions.Any(i => i.status == IntentionStatus.Active);
        
        private void Awake()
        {
            // Get required components
            needsManager = GetComponent<NeedsManager>();
            llmClient = FindFirstObjectByType<OllamaClient>();
            
            // Get personality from Agent component
            var agent = GetComponent<AIWorld.Agents.Agent>();
            personality = agent?.personality;
        }
        
        private void Start()
        {
            // Initialize basic beliefs about self and world
            InitializeBasicBeliefs();
            
            // Subscribe to needs updates
            if (needsManager != null)
            {
                needsManager.OnNeedBecameUrgent += HandleUrgentNeed;
                needsManager.OnNeedSatisfied += HandleSatisfiedNeed;
            }
            
            Debug.Log($"🧠 BDI Engine initialized for {gameObject.name}");
        }
        
        private void Update()
        {
            if (!enableBDIReasoning) return;
            
            // Run BDI reasoning cycle at specified interval
            if (Time.time - lastReasoningTime >= reasoningInterval)
            {
                RunBDICycle();
                lastReasoningTime = Time.time;
            }
        }
        
        /// <summary>
        /// Initialize basic beliefs about self and world
        /// </summary>
        private void InitializeBasicBeliefs()
        {
            beliefs.AddRange(BDIFactory.CreateBasicWorldBeliefs(transform.position));
            
            // Add personality-based beliefs
            if (personality != null)
            {
                AddBelief("agent_name", new[] { personality.agentName }, 1f, "I know my own name");
                AddBelief("agent_role", new[] { personality.role }, 1f, "I know my role");
                
                // Add beliefs about capabilities based on personality
                if (personality.creativityLevel > 0.7f)
                {
                    AddBelief("capable_of", new[] { "creative_work" }, 0.8f, "I believe I'm good at creative work");
                }
                
                if (personality.extraversion > 0.6f)
                {
                    AddBelief("enjoys", new[] { "social_interaction" }, 0.9f, "I enjoy interacting with others");
                }
            }
        }
        
        /// <summary>
        /// Main BDI reasoning cycle: Beliefs → Desires → Intentions
        /// </summary>
        private void RunBDICycle()
        {
            bdoCycleCount++;
            
            if (logBDICycle)
            {
                Debug.Log($"🔄 {gameObject.name}: Running BDI cycle #{bdoCycleCount}");
            }
            
            // 1. Update and maintain beliefs
            UpdateBeliefs();
            
            // 2. Generate desires from needs and opportunities
            GenerateDesires();
            
            // 3. Select and commit to intentions
            ManageIntentions();
            
            // 4. Execute current intention
            ExecuteCurrentIntention();
            
            // 5. Clean up expired/invalid entries
            CleanupBDIState();
            
            if (showStateInConsole)
            {
                LogBDIState();
            }
        }
        
        /// <summary>
        /// Update existing beliefs and decay confidence over time
        /// </summary>
        private void UpdateBeliefs()
        {
            // Update agent location belief
            var locationBelief = beliefs.FirstOrDefault(b => b.predicate == "agent_location");
            if (locationBelief != null)
            {
                string newLocation = $"{transform.position.x:F1},{transform.position.y:F1},{transform.position.z:F1}";
                if (locationBelief.arguments[1] != newLocation)
                {
                    locationBelief.arguments[1] = newLocation;
                    locationBelief.lastUpdated = System.DateTime.Now;
                }
            }
            
            // Decay belief confidence over time for non-permanent beliefs
            foreach (var belief in beliefs.Where(b => b.timeToLive > 0))
            {
                belief.confidence = Mathf.Max(0.1f, belief.confidence - beliefsDecayRate * reasoningInterval);
            }
        }
        
        /// <summary>
        /// Generate desires from current needs and opportunities
        /// </summary>
        private void GenerateDesires()
        {
            if (needsManager == null) return;
            
            // Get urgent needs that don't already have corresponding desires
            var urgentNeeds = needsManager.GetMostUrgentNeeds(3);
            
            foreach (var need in urgentNeeds)
            {
                // Check if we already have a desire for this need
                bool hasDesireForNeed = desires.Any(d => d.sourceNeed == need.needName && d.IsValid());
                
                if (!hasDesireForNeed && desires.Count < maxActiveDesires)
                {
                    var newDesire = BDIFactory.CreateDesireFromNeed(need);
                    desires.Add(newDesire);
                    
                    if (logBDICycle)
                    {
                        Debug.Log($"🎯 {gameObject.name}: Generated desire '{newDesire.goalName}' from need '{need.needName}'");
                    }
                }
            }
            
            // Update intensity of existing desires based on current need states
            foreach (var desire in desires.Where(d => d.IsValid() && !string.IsNullOrEmpty(d.sourceNeed)))
            {
                var correspondingNeed = needsManager.activeNeeds.FirstOrDefault(n => n.needName == desire.sourceNeed);
                if (correspondingNeed != null)
                {
                    float newIntensity = correspondingNeed.GetUrgency();
                    if (Mathf.Abs(desire.intensity - newIntensity) > 0.2f)
                    {
                        desire.UpdateIntensity(newIntensity, $"Need urgency changed");
                    }
                }
            }
        }
        
        /// <summary>
        /// Manage current intentions: select new ones, update existing ones
        /// </summary>
        private void ManageIntentions()
        {
            // Remove completed, failed, or abandoned intentions
            var completedIntentions = intentions.Where(i => 
                i.status == IntentionStatus.Completed || 
                i.status == IntentionStatus.Failed || 
                i.status == IntentionStatus.Abandoned).ToList();
                
            foreach (var intention in completedIntentions)
            {
                intentions.Remove(intention);
                if (intention == currentIntention)
                {
                    currentIntention = null;
                }
                
                if (intention.status == IntentionStatus.Completed)
                {
                    OnIntentionCompleted?.Invoke(intention);
                    HandleIntentionCompleted(intention);
                }
                else
                {
                    OnIntentionAbandoned?.Invoke(intention);
                }
            }
            
            // Check if current intentions should be abandoned
            foreach (var intention in intentions.ToList())
            {
                if (intention.ShouldAbandon())
                {
                    intention.status = IntentionStatus.Abandoned;
                    if (logBDICycle)
                    {
                        Debug.Log($"💔 {gameObject.name}: Abandoning intention '{intention.intentionName}'");
                    }
                }
            }
            
            // Adopt new intentions if we have capacity and suitable desires
            if (intentions.Count(i => i.status == IntentionStatus.Active) < maxActiveIntentions)
            {
                var bestDesire = desires
                    .Where(d => d.IsValid() && !intentions.Any(i => i.sourceDesire == d))
                    .OrderByDescending(d => d.GetPriority())
                    .FirstOrDefault();
                    
                if (bestDesire != null && bestDesire.GetPriority() > 0.3f)
                {
                    var newIntention = BDIFactory.CreateSimpleIntention(bestDesire);
                    intentions.Add(newIntention);
                    
                    if (currentIntention == null)
                    {
                        currentIntention = newIntention;
                    }
                    
                    OnIntentionAdopted?.Invoke(newIntention);
                    
                    if (logBDICycle)
                    {
                        Debug.Log($"💡 {gameObject.name}: Adopted intention '{newIntention.intentionName}' (priority: {bestDesire.GetPriority():F2})");
                    }
                }
            }
            
            // Select current intention if none is active
            if (currentIntention == null || currentIntention.status != IntentionStatus.Active)
            {
                currentIntention = intentions
                    .Where(i => i.status == IntentionStatus.Active)
                    .OrderByDescending(i => i.commitmentStrength)
                    .FirstOrDefault();
            }
        }
        
        /// <summary>
        /// Execute the current intention by performing its next action
        /// </summary>
        private void ExecuteCurrentIntention()
        {
            if (currentIntention == null) return;
            
            string currentAction = currentIntention.GetCurrentAction();
            if (string.IsNullOrEmpty(currentAction)) return;
            
            // Execute the action based on its type
            bool actionCompleted = ExecuteAction(currentAction, currentIntention);
            
            if (actionCompleted)
            {
                if (!currentIntention.AdvanceToNextAction())
                {
                    // Intention completed
                    if (logBDICycle)
                    {
                        Debug.Log($"✅ {gameObject.name}: Completed intention '{currentIntention.intentionName}'");
                    }
                }
            }
        }
        
        /// <summary>
        /// Execute a specific action
        /// </summary>
        private bool ExecuteAction(string action, Intention intention)
        {
            switch (action)
            {
                case "StartConversation":
                case "Communicate":
                    return ExecuteCommunicateAction(intention);
                    
                case "Rest":
                case "Relax":
                    return ExecuteRestAction(intention);
                    
                case "Think":
                case "Reflect":
                    return ExecuteThinkAction(intention);
                    
                case "Research":
                case "Learn":
                    return ExecuteLearnAction(intention);
                    
                case "Create":
                case "Brainstorm":
                    return ExecuteCreateAction(intention);
                    
                case "FindQuietSpace":
                case "FindSafeSpace":
                case "FindSocialSpace":
                    return ExecuteMovementAction(intention);
                    
                case "UseObject":
                case "InteractWithObject":
                    return ExecuteObjectInteractionAction(intention);
                    
                default:
                    // Generic action - just mark as completed after a delay
                    return ExecuteGenericAction(action, intention);
            }
        }
        
        /// <summary>
        /// Execute communication action
        /// </summary>
        private bool ExecuteCommunicateAction(Intention intention)
        {
            // Check if there are other agents nearby
            var gameManager = FindFirstObjectByType<AIWorld.Managers.GameManager>();
            if (gameManager != null)
            {
                var nearbyAgents = gameManager.GetAgentsInRange(transform.position, 5f);
                if (nearbyAgents.Count > 1) // More than just this agent
                {
                    // Satisfy social needs from this action
                    needsManager?.SatisfyNeedsFromSocialInteraction("conversation", 0.8f);
                    
                    if (logBDICycle)
                    {
                        Debug.Log($"💬 {gameObject.name}: Executed communication action for intention '{intention.intentionName}'");
                    }
                    
                    return true;
                }
            }
            
            // No one to talk to - reduce commitment
            intention.ReduceCommitment(0.2f, "No one available to communicate with");
            return false;
        }
        
        /// <summary>
        /// Execute rest action
        /// </summary>
        private bool ExecuteRestAction(Intention intention)
        {
            // Satisfy energy-related needs
            needsManager?.SatisfyNeedsFromAction("Rest", 0.3f);
            
            if (logBDICycle)
            {
                Debug.Log($"😴 {gameObject.name}: Executed rest action for intention '{intention.intentionName}'");
            }
            
            return true;
        }
        
        /// <summary>
        /// Execute thinking/reflection action
        /// </summary>
        private bool ExecuteThinkAction(Intention intention)
        {
            // This could trigger an inner dialogue request to LLM
            if (llmClient != null && personality != null)
            {
                string thoughtPrompt = $"I'm currently focused on {intention.description}. Let me reflect on my progress and next steps.";
                
                llmClient.SendInnerDialogueRequest(gameObject.name, thoughtPrompt, personality, (response) =>
                {
                    if (response.success)
                    {
                        if (logBDICycle)
                        {
                            Debug.Log($"💭 {gameObject.name}: Inner thought about intention - {response.content}");
                        }
                    }
                });
            }
            
            // Satisfy knowledge or achievement needs
            needsManager?.SatisfyNeedsFromAction("Think", 0.2f);
            
            return true;
        }
        
        /// <summary>
        /// Execute learning action
        /// </summary>
        private bool ExecuteLearnAction(Intention intention)
        {
            needsManager?.SatisfyNeedsFromAction("Learn", 0.4f);
            
            if (logBDICycle)
            {
                Debug.Log($"📚 {gameObject.name}: Executed learning action for intention '{intention.intentionName}'");
            }
            
            return true;
        }
        
        /// <summary>
        /// Execute creative action
        /// </summary>
        private bool ExecuteCreateAction(Intention intention)
        {
            needsManager?.SatisfyNeedsFromAction("Create", 0.5f);
            
            if (logBDICycle)
            {
                Debug.Log($"🎨 {gameObject.name}: Executed creative action for intention '{intention.intentionName}'");
            }
            
            return true;
        }
        
        /// <summary>
        /// Execute movement action (handled by movement system)
        /// </summary>
        private bool ExecuteMovementAction(Intention intention)
        {
            var movementComponent = GetComponent<AIWorld.Movement.AgentMovement>();
            if (movementComponent != null)
            {
                // Movement system will handle this based on the intention
                if (logBDICycle)
                {
                    Debug.Log($"🚶 {gameObject.name}: Executed movement action for intention '{intention.intentionName}'");
                }
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Execute object interaction action
        /// </summary>
        private bool ExecuteObjectInteractionAction(Intention intention)
        {
            // Find best interactive object for current needs
            var bestObject = FindBestInteractiveObject();
            
            if (bestObject != null)
            {
                // Check if agent is close enough to interact
                float distance = Vector3.Distance(transform.position, bestObject.transform.position);
                if (distance <= bestObject.interactionRange)
                {
                    // Try to interact with object
                    var agent = GetComponent<AIWorld.Agents.Agent>();
                    if (bestObject.StartInteraction(agent))
                    {
                        if (logBDICycle)
                        {
                            Debug.Log($"🔧 {gameObject.name}: Started interaction with {bestObject.objectName}");
                        }
                        return true;
                    }
                }
                else
                {
                    // Need to move closer - set movement goal
                    var movementComponent = GetComponent<AIWorld.Movement.AgentMovement>();
                    if (movementComponent != null)
                    {
                        var movementGoal = new AIWorld.Movement.MovementGoal
                        {
                            targetPosition = bestObject.transform.position,
                            goalType = AIWorld.Movement.MovementGoalType.Task,
                            purpose = $"interact with {bestObject.objectName}",
                            priority = 0.8f,
                            timeLimit = 20f
                        };
                        
                        movementComponent.SetMovementGoal(movementGoal);
                        
                        if (logBDICycle)
                        {
                            Debug.Log($"🎯 {gameObject.name}: Moving toward {bestObject.objectName} to interact");
                        }
                        
                        return false; // Not completed yet, still moving
                    }
                }
            }
            
            // No suitable object found or interaction failed
            intention.ReduceCommitment(0.3f, "No suitable object for interaction");
            return false;
        }
        
        /// <summary>
        /// Find the best interactive object for current needs
        /// </summary>
        private AIWorld.Environment.InteractableObject FindBestInteractiveObject()
        {
            var allObjects = FindObjectsByType<AIWorld.Environment.InteractableObject>(FindObjectsSortMode.None);
            if (allObjects.Length == 0) return null;
            
            var agent = GetComponent<AIWorld.Agents.Agent>();
            if (agent == null) return null;
            
            AIWorld.Environment.InteractableObject bestObject = null;
            float bestAttractiveness = 0f;
            
            foreach (var obj in allObjects)
            {
                float attractiveness = obj.GetAttractiveness(agent);
                if (attractiveness > bestAttractiveness)
                {
                    bestAttractiveness = attractiveness;
                    bestObject = obj;
                }
            }
            
            return bestAttractiveness > 0.3f ? bestObject : null;
        }
        
        /// <summary>
        /// Execute generic action
        /// </summary>
        private bool ExecuteGenericAction(string action, Intention intention)
        {
            needsManager?.SatisfyNeedsFromAction(action, 0.2f);
            
            if (logBDICycle)
            {
                Debug.Log($"⚙️ {gameObject.name}: Executed generic action '{action}' for intention '{intention.intentionName}'");
            }
            
            return true;
        }
        
        /// <summary>
        /// Handle completion of an intention
        /// </summary>
        private void HandleIntentionCompleted(Intention intention)
        {
            // Remove the corresponding desire since it's been satisfied
            var correspondingDesire = desires.FirstOrDefault(d => d == intention.sourceDesire);
            if (correspondingDesire != null)
            {
                desires.Remove(correspondingDesire);
            }
            
            // Update beliefs about our capabilities
            AddBelief("succeeded_at", new[] { intention.intentionName }, 0.8f, 
                $"I successfully completed {intention.intentionName}");
        }
        
        /// <summary>
        /// Clean up expired or invalid BDI state
        /// </summary>
        private void CleanupBDIState()
        {
            // Remove invalid beliefs
            beliefs.RemoveAll(b => !b.IsValid() || b.confidence < 0.1f);
            
            // Remove invalid desires
            desires.RemoveAll(d => !d.IsValid());
            
            // Remove old completed/failed intentions
            var oldIntentions = intentions.Where(i => 
                (i.status == IntentionStatus.Completed || i.status == IntentionStatus.Failed) &&
                (System.DateTime.Now - i.created).TotalMinutes > 5).ToList();
                
            foreach (var intention in oldIntentions)
            {
                intentions.Remove(intention);
            }
        }
        
        /// <summary>
        /// Add or update a belief
        /// </summary>
        public void AddBelief(string predicate, string[] arguments, float confidence, string description = "")
        {
            var existingBelief = beliefs.FirstOrDefault(b => b.Matches(predicate, arguments));
            
            if (existingBelief != null)
            {
                // Update existing belief
                existingBelief.UpdateConfidence(confidence, 0.3f);
                if (!string.IsNullOrEmpty(description))
                {
                    existingBelief.description = description;
                }
            }
            else
            {
                // Add new belief
                var newBelief = new Belief(predicate, arguments, confidence)
                {
                    description = description
                };
                beliefs.Add(newBelief);
                
                if (logBeliefUpdates)
                {
                    Debug.Log($"💡 {gameObject.name}: New belief - {newBelief}");
                }
            }
            
            OnBeliefUpdated?.Invoke(existingBelief ?? beliefs.Last());
        }
        
        /// <summary>
        /// Query beliefs for specific information
        /// </summary>
        public List<Belief> QueryBeliefs(string predicate, string[] arguments = null)
        {
            return beliefs.Where(b => b.Matches(predicate, arguments) && b.IsValid()).ToList();
        }
        
        /// <summary>
        /// Handle urgent need events
        /// </summary>
        private void HandleUrgentNeed(Need need)
        {
            // Boost priority of related desires
            var relatedDesires = desires.Where(d => d.sourceNeed == need.needName);
            foreach (var desire in relatedDesires)
            {
                desire.UpdateIntensity(Mathf.Min(1f, desire.intensity + 0.3f), "Need became urgent");
            }
        }
        
        /// <summary>
        /// Handle satisfied need events
        /// </summary>
        private void HandleSatisfiedNeed(Need need)
        {
            // Reduce priority of related desires
            var relatedDesires = desires.Where(d => d.sourceNeed == need.needName);
            foreach (var desire in relatedDesires)
            {
                desire.UpdateIntensity(Mathf.Max(0f, desire.intensity - 0.4f), "Need was satisfied");
            }
        }
        
        /// <summary>
        /// Log current BDI state for debugging
        /// </summary>
        private void LogBDIState()
        {
            string state = $"\n🧠 {gameObject.name} BDI State:\n";
            state += $"Beliefs: {beliefs.Count} | Desires: {desires.Count} | Intentions: {intentions.Count}\n";
            
            if (currentIntention != null)
            {
                state += $"Current Intention: {currentIntention}\n";
            }
            
            if (desires.Count > 0)
            {
                state += "Top Desires:\n";
                foreach (var desire in desires.OrderByDescending(d => d.GetPriority()).Take(3))
                {
                    state += $"  🎯 {desire}\n";
                }
            }
            
            Debug.Log(state);
        }
        
        #if UNITY_EDITOR
        [Header("Editor Debug Info")]
        [SerializeField] private string currentBDIState;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                currentBDIState = $"Beliefs: {beliefs.Count} | Desires: {desires.Count} | Intentions: {intentions.Count}";
                if (currentIntention != null)
                {
                    currentBDIState += $"\nCurrent: {currentIntention.intentionName}";
                }
            }
        }
        #endif
    }
}