using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AIWorld.BDI;
using AIWorld.Needs;

namespace AIWorld.Movement
{
    /// <summary>
    /// NavMesh-based movement system for AI agents
    /// Integrates with BDI engine for goal-directed movement
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AgentMovement : MonoBehaviour
    {
        [Header("Movement Configuration")]
        public bool enableMovement = true;
        public float baseSpeed = 3.5f;
        public float stoppingDistance = 1.5f;
        public float wanderRadius = 10f;
        public float idleMovementChance = 0.3f;
        
        [Header("Goal-Based Movement")]
        public float goalReachedThreshold = 2f;
        public float maxMovementTime = 30f;
        public bool prioritizeMovementGoals = true;
        
        [Header("Wandering Behaviour")]
        public float wanderInterval = 8f;
        public float minWanderDistance = 3f;
        public float maxWanderDistance = 8f;
        
        [Header("Debug")]
        public bool showMovementDebug = true;
        public bool logMovementActions = true;
        
        // Components
        private NavMeshAgent navAgent;
        private BDIEngine bdiEngine;
        private NeedsManager needsManager;
        
        // Movement state
        private MovementGoal currentGoal;
        private Vector3 lastPosition;
        private float timeSinceLastMovement;
        private float lastWanderTime;
        private bool isMovingToGoal = false;
        private Coroutine movementCoroutine;
        
        // Properties
        public bool IsMoving => navAgent != null && navAgent.velocity.magnitude > 0.1f;
        public bool HasMovementGoal => currentGoal != null;
        public Vector3 CurrentDestination => navAgent != null ? navAgent.destination : transform.position;
        public float DistanceToDestination => navAgent != null ? navAgent.remainingDistance : 0f;
        
        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            bdiEngine = GetComponent<BDIEngine>();
            needsManager = GetComponent<NeedsManager>();
        }
        
        private void Start()
        {
            if (navAgent != null)
            {
                navAgent.speed = baseSpeed;
                navAgent.stoppingDistance = stoppingDistance;
                navAgent.autoBraking = true;
                navAgent.autoRepath = true;
            }
            
            lastPosition = transform.position;
            
            if (enableMovement)
            {
                StartCoroutine(MovementUpdateLoop());
            }
            
            if (logMovementActions)
            {
                Debug.Log($"🚶 {gameObject.name}: Movement system initialized");
            }
        }
        
        /// <summary>
        /// Main movement update loop
        /// </summary>
        private IEnumerator MovementUpdateLoop()
        {
            while (enabled)
            {
                yield return new WaitForSeconds(1f);
                
                UpdateMovementState();
                ProcessMovementGoals();
                HandleIdleMovement();
                CheckMovementCompletion();
            }
        }
        
        /// <summary>
        /// Update current movement state
        /// </summary>
        private void UpdateMovementState()
        {
            if (navAgent == null) return;
            
            // Check if agent is stuck
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);
            if (distanceMoved < 0.1f)
            {
                timeSinceLastMovement += 1f;
            }
            else
            {
                timeSinceLastMovement = 0f;
                lastPosition = transform.position;
            }
            
            // Handle stuck agent
            if (timeSinceLastMovement > 5f && navAgent.hasPath)
            {
                HandleStuckAgent();
            }
        }
        
        /// <summary>
        /// Process movement goals from BDI engine
        /// </summary>
        private void ProcessMovementGoals()
        {
            if (bdiEngine == null || !prioritizeMovementGoals) return;
            
            // Check if current intention requires movement
            var currentIntention = bdiEngine.CurrentIntention;
            if (currentIntention != null && !isMovingToGoal)
            {
                var movementGoal = ExtractMovementGoal(currentIntention);
                if (movementGoal != null)
                {
                    SetMovementGoal(movementGoal);
                }
            }
        }
        
        /// <summary>
        /// Extract movement goal from BDI intention
        /// </summary>
        private MovementGoal ExtractMovementGoal(Intention intention)
        {
            // Check if intention involves movement actions
            string currentAction = intention.GetCurrentAction();
            
            switch (currentAction)
            {
                case "FindQuietSpace":
                case "FindSafeSpace":
                    return CreateExplorationGoal("safe space");
                    
                case "FindSocialSpace":
                case "StartConversation":
                    return CreateSocialGoal();
                    
                case "Explore":
                case "Research":
                    return CreateExplorationGoal("research area");
                    
                case "Create":
                case "Brainstorm":
                    return CreateCreativeSpaceGoal();
                    
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Create exploration movement goal
        /// </summary>
        private MovementGoal CreateExplorationGoal(string purpose)
        {
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;
            randomDirection.y = transform.position.y;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
            {
                return new MovementGoal
                {
                    targetPosition = hit.position,
                    goalType = MovementGoalType.Exploration,
                    purpose = purpose,
                    priority = 0.6f,
                    timeLimit = maxMovementTime
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// Create social movement goal (move toward other agents)
        /// </summary>
        private MovementGoal CreateSocialGoal()
        {
            // Find nearby agents
            var gameManager = FindFirstObjectByType<AIWorld.Managers.GameManager>();
            if (gameManager != null)
            {
                var nearbyAgents = gameManager.GetAgentsInRange(transform.position, wanderRadius);
                if (nearbyAgents.Count > 1) // More than just this agent
                {
                    var targetAgent = nearbyAgents[Random.Range(0, nearbyAgents.Count)];
                    if (targetAgent != null && targetAgent.gameObject != this.gameObject)
                    {
                        Vector3 targetPos = targetAgent.transform.position;
                        // Move close but not too close
                        Vector3 offset = (transform.position - targetPos).normalized * 2f;
                        targetPos += offset;
                        
                        return new MovementGoal
                        {
                            targetPosition = targetPos,
                            goalType = MovementGoalType.Social,
                            purpose = "social interaction",
                            targetAgent = targetAgent.gameObject,
                            priority = 0.8f,
                            timeLimit = maxMovementTime * 0.5f
                        };
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Create creative space movement goal
        /// </summary>
        private MovementGoal CreateCreativeSpaceGoal()
        {
            // For now, just find a quiet spot away from others
            Vector3 awayDirection = Vector3.zero;
            
            var gameManager = FindFirstObjectByType<AIWorld.Managers.GameManager>();
            if (gameManager != null)
            {
                var nearbyAgents = gameManager.GetAgentsInRange(transform.position, 10f);
                foreach (var agent in nearbyAgents)
                {
                    if (agent.gameObject != this.gameObject)
                    {
                        awayDirection += (transform.position - agent.transform.position).normalized;
                    }
                }
            }
            
            if (awayDirection == Vector3.zero)
            {
                awayDirection = Random.insideUnitSphere;
            }
            
            awayDirection.y = 0;
            Vector3 targetPos = transform.position + awayDirection.normalized * 5f;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, 10f, NavMesh.AllAreas))
            {
                return new MovementGoal
                {
                    targetPosition = hit.position,
                    goalType = MovementGoalType.Creative,
                    purpose = "creative space",
                    priority = 0.7f,
                    timeLimit = maxMovementTime
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// Set a new movement goal
        /// </summary>
        public void SetMovementGoal(MovementGoal goal)
        {
            if (goal == null || navAgent == null) return;
            
            currentGoal = goal;
            isMovingToGoal = true;
            
            if (navAgent.SetDestination(goal.targetPosition))
            {
                if (logMovementActions)
                {
                    Debug.Log($"🎯 {gameObject.name}: Moving to {goal.purpose} at {goal.targetPosition}");
                }
                
                // Start movement timeout coroutine
                if (movementCoroutine != null) StopCoroutine(movementCoroutine);
                movementCoroutine = StartCoroutine(MovementTimeoutCoroutine(goal.timeLimit));
            }
            else
            {
                if (logMovementActions)
                {
                    Debug.LogWarning($"⚠️ {gameObject.name}: Could not set destination to {goal.targetPosition}");
                }
                ClearMovementGoal();
            }
        }
        
        /// <summary>
        /// Handle idle wandering movement
        /// </summary>
        private void HandleIdleMovement()
        {
            if (isMovingToGoal || !enableMovement) return;
            
            if (Time.time - lastWanderTime > wanderInterval)
            {
                if (Random.value < idleMovementChance)
                {
                    PerformWanderMovement();
                }
                lastWanderTime = Time.time;
            }
        }
        
        /// <summary>
        /// Perform random wandering movement
        /// </summary>
        private void PerformWanderMovement()
        {
            float wanderDistance = Random.Range(minWanderDistance, maxWanderDistance);
            Vector3 randomDirection = Random.insideUnitSphere * wanderDistance;
            randomDirection += transform.position;
            randomDirection.y = transform.position.y;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, wanderDistance, NavMesh.AllAreas))
            {
                var wanderGoal = new MovementGoal
                {
                    targetPosition = hit.position,
                    goalType = MovementGoalType.Wander,
                    purpose = "wandering",
                    priority = 0.2f,
                    timeLimit = wanderInterval * 0.8f
                };
                
                SetMovementGoal(wanderGoal);
            }
        }
        
        /// <summary>
        /// Check if movement goals are completed
        /// </summary>
        private void CheckMovementCompletion()
        {
            if (!isMovingToGoal || currentGoal == null || navAgent == null) return;
            
            // Check if reached destination
            if (!navAgent.pathPending && navAgent.remainingDistance < goalReachedThreshold)
            {
                CompleteMovementGoal();
            }
        }
        
        /// <summary>
        /// Complete current movement goal
        /// </summary>
        private void CompleteMovementGoal()
        {
            if (currentGoal == null) return;
            
            if (logMovementActions)
            {
                Debug.Log($"✅ {gameObject.name}: Reached {currentGoal.purpose} at {transform.position}");
            }
            
            // Satisfy needs based on movement goal type
            if (needsManager != null)
            {
                switch (currentGoal.goalType)
                {
                    case MovementGoalType.Social:
                        needsManager.SatisfyNeedsFromAction("SocialMovement", 0.2f);
                        break;
                    case MovementGoalType.Creative:
                        needsManager.SatisfyNeedsFromAction("CreativeSpace", 0.15f);
                        break;
                    case MovementGoalType.Exploration:
                        needsManager.SatisfyNeedsFromAction("Explore", 0.1f);
                        break;
                }
            }
            
            ClearMovementGoal();
        }
        
        /// <summary>
        /// Clear current movement goal
        /// </summary>
        public void ClearMovementGoal()
        {
            currentGoal = null;
            isMovingToGoal = false;
            
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
                movementCoroutine = null;
            }
        }
        
        /// <summary>
        /// Handle agent getting stuck
        /// </summary>
        private void HandleStuckAgent()
        {
            if (logMovementActions)
            {
                Debug.LogWarning($"🚫 {gameObject.name}: Agent appears stuck, attempting unstuck");
            }
            
            // Try to find a new nearby position
            Vector3 unstuckPosition = transform.position + Random.insideUnitSphere * 3f;
            unstuckPosition.y = transform.position.y;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(unstuckPosition, out hit, 5f, NavMesh.AllAreas))
            {
                navAgent.SetDestination(hit.position);
            }
            else
            {
                // If can't find position, just clear current goal
                ClearMovementGoal();
            }
            
            timeSinceLastMovement = 0f;
        }
        
        /// <summary>
        /// Movement timeout coroutine
        /// </summary>
        private IEnumerator MovementTimeoutCoroutine(float timeLimit)
        {
            yield return new WaitForSeconds(timeLimit);
            
            if (isMovingToGoal)
            {
                if (logMovementActions)
                {
                    Debug.Log($"⏰ {gameObject.name}: Movement goal timed out");
                }
                ClearMovementGoal();
            }
        }
        
        /// <summary>
        /// Stop all movement
        /// </summary>
        public void StopMovement()
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.ResetPath();
            }
            ClearMovementGoal();
        }
        
        /// <summary>
        /// Get current movement state for debugging
        /// </summary>
        public string GetMovementState()
        {
            if (!enableMovement) return "Movement Disabled";
            
            if (isMovingToGoal && currentGoal != null)
            {
                return $"Moving to {currentGoal.purpose} ({DistanceToDestination:F1}m away)";
            }
            
            if (IsMoving)
            {
                return "Moving (Wandering)";
            }
            
            return "Stationary";
        }
        
        /// <summary>
        /// Debug visualization
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!showMovementDebug) return;
            
            // Draw movement goal
            if (currentGoal != null)
            {
                Gizmos.color = GetGoalColor(currentGoal.goalType);
                Gizmos.DrawSphere(currentGoal.targetPosition, 0.5f);
                Gizmos.DrawLine(transform.position, currentGoal.targetPosition);
            }
            
            // Draw wander radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, wanderRadius);
            
            // Draw NavMesh path
            if (navAgent != null && navAgent.hasPath)
            {
                Gizmos.color = Color.yellow;
                var path = navAgent.path.corners;
                for (int i = 1; i < path.Length; i++)
                {
                    Gizmos.DrawLine(path[i - 1], path[i]);
                }
            }
        }
        
        /// <summary>
        /// Get color for movement goal type
        /// </summary>
        private Color GetGoalColor(MovementGoalType goalType)
        {
            switch (goalType)
            {
                case MovementGoalType.Social: return Color.green;
                case MovementGoalType.Creative: return Color.magenta;
                case MovementGoalType.Exploration: return Color.blue;
                case MovementGoalType.Wander: return Color.gray;
                default: return Color.white;
            }
        }
        
        #if UNITY_EDITOR
        [Header("Editor Debug Info")]
        [SerializeField] private string movementStatus;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                movementStatus = GetMovementState();
            }
        }
        #endif
    }
    
    /// <summary>
    /// Data structure for movement goals
    /// </summary>
    [System.Serializable]
    public class MovementGoal
    {
        public Vector3 targetPosition;
        public MovementGoalType goalType;
        public string purpose;
        public GameObject targetAgent;
        public float priority = 0.5f;
        public float timeLimit = 30f;
        public System.DateTime created;
        
        public MovementGoal()
        {
            created = System.DateTime.Now;
        }
    }
    
    /// <summary>
    /// Types of movement goals
    /// </summary>
    public enum MovementGoalType
    {
        Wander,         // Random movement
        Social,         // Move toward other agents
        Exploration,    // Explore environment
        Creative,       // Find creative space
        Task,           // Task-specific movement
        Escape          // Move away from something
    }
}