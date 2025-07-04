using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Agents;
using AIWorld.Needs;
using AIWorld.BDI;
using AIWorld.Environment;

namespace AIWorld.Communication
{
    /// <summary>
    /// Advanced conversation topic manager that generates diverse, contextual topics
    /// Based on agent needs, beliefs, environment, and conversation history
    /// Eliminates repetitive conversations through intelligent topic selection
    /// </summary>
    public class IntelligentTopicManager : MonoBehaviour
    {
        [Header("Topic Generation Configuration")]
        public bool enableDynamicTopics = true;
        public float topicRefreshInterval = 30f;
        public int maxTopicHistory = 10;
        public bool avoidRecentTopics = true;
        
        [Header("Context Awareness")]
        public bool useEnvironmentalContext = true;
        public bool useNeedDrivenTopics = true;
        public bool useBDIBeliefs = true;
        public float contextInfluenceStrength = 0.7f;
        
        [Header("Topic Diversity")]
        public float diversityThreshold = 0.6f;
        public bool enableCrossPolination = true;
        public int minTopicVariations = 5;
        public bool trackConversationQuality = true;
        
        [Header("Debug")]
        public bool logTopicGeneration = true;
        public bool showTopicAnalysis = true;
        
        // Topic tracking
        private Dictionary<string, List<ConversationTopic>> agentTopicHistory;
        private Dictionary<string, float> globalTopicUsage;
        private List<TopicTemplate> availableTemplates;
        private Dictionary<string, ConversationContext> activeContexts;
        
        // Quality metrics
        private float averageTopicDiversity = 0f;
        private int totalTopicsGenerated = 0;
        private int conversationLoopsAvoided = 0;
        
        // Properties
        public float AverageTopicDiversity => averageTopicDiversity;
        public int TotalTopicsGenerated => totalTopicsGenerated;
        public int ConversationLoopsAvoided => conversationLoopsAvoided;
        
        private void Start()
        {
            InitializeTopicSystem();
            
            if (enableDynamicTopics)
            {
                InvokeRepeating(nameof(RefreshTopicContexts), 5f, topicRefreshInterval);
            }
            
            Debug.Log($"🎭 IntelligentTopicManager initialized with {availableTemplates.Count} topic templates");
        }
        
        /// <summary>
        /// Initialize the topic management system
        /// </summary>
        private void InitializeTopicSystem()
        {
            agentTopicHistory = new Dictionary<string, List<ConversationTopic>>();
            globalTopicUsage = new Dictionary<string, float>();
            activeContexts = new Dictionary<string, ConversationContext>();
            
            // Load topic templates
            LoadTopicTemplates();
        }
        
        /// <summary>
        /// Generate intelligent conversation topic for agent
        /// </summary>
        public string GenerateConversationTopic(Agent agent, Agent targetAgent = null, string environmentContext = null)
        {
            if (!enableDynamicTopics)
            {
                return GetFallbackTopic(agent);
            }
            
            // Build conversation context
            var context = BuildConversationContext(agent, targetAgent, environmentContext);
            
            // Generate topic candidates
            var candidates = GenerateTopicCandidates(context);
            
            // Select best topic based on diversity and relevance
            var selectedTopic = SelectOptimalTopic(candidates, context);
            
            // Track topic usage
            TrackTopicUsage(agent, selectedTopic);
            
            if (logTopicGeneration)
            {
                Debug.Log($"🎭 Generated topic for {agent.AgentName}: '{selectedTopic.content}' (relevance: {selectedTopic.relevance:F2}, diversity: {selectedTopic.diversity:F2})");
            }
            
            return selectedTopic.content;
        }
        
        /// <summary>
        /// Generate contextual inner dialogue topic
        /// </summary>
        public string GenerateInnerDialogueTopic(Agent agent)
        {
            var context = BuildInnerDialogueContext(agent);
            var candidates = GenerateInnerDialogueCandidates(context);
            var selectedTopic = SelectOptimalTopic(candidates, context);
            
            TrackTopicUsage(agent, selectedTopic);
            
            if (logTopicGeneration)
            {
                Debug.Log($"💭 Generated inner dialogue topic for {agent.AgentName}: '{selectedTopic.content}' (depth: {selectedTopic.relevance:F2})");
            }
            
            return selectedTopic.content;
        }
        
        /// <summary>
        /// Build comprehensive conversation context
        /// </summary>
        private ConversationContext BuildConversationContext(Agent agent, Agent targetAgent, string environmentContext)
        {
            var context = new ConversationContext
            {
                primaryAgent = agent,
                targetAgent = targetAgent,
                environmentType = environmentContext ?? DetectEnvironmentType(agent),
                urgentNeeds = GetUrgentNeeds(agent),
                relevantBeliefs = GetRelevantBeliefs(agent),
                currentIntention = GetCurrentIntention(agent),
                personalityTraits = ExtractPersonalityTraits(agent),
                recentTopics = GetRecentTopics(agent),
                socialContext = BuildSocialContext(agent, targetAgent)
            };
            
            // Calculate context weights
            context.needWeight = useNeedDrivenTopics ? 0.4f : 0.1f;
            context.beliefWeight = useBDIBeliefs ? 0.3f : 0.1f;
            context.environmentWeight = useEnvironmentalContext ? 0.2f : 0.1f;
            context.personalityWeight = 0.1f;
            
            return context;
        }
        
        /// <summary>
        /// Build context for inner dialogue
        /// </summary>
        private ConversationContext BuildInnerDialogueContext(Agent agent)
        {
            var context = new ConversationContext
            {
                primaryAgent = agent,
                environmentType = DetectEnvironmentType(agent),
                urgentNeeds = GetUrgentNeeds(agent),
                relevantBeliefs = GetRelevantBeliefs(agent),
                currentIntention = GetCurrentIntention(agent),
                personalityTraits = ExtractPersonalityTraits(agent),
                recentTopics = GetRecentTopics(agent)
            };
            
            // Higher weights for internal context
            context.needWeight = 0.5f;
            context.beliefWeight = 0.4f;
            context.environmentWeight = 0.1f;
            
            return context;
        }
        
        /// <summary>
        /// Generate topic candidates based on context
        /// </summary>
        private List<ConversationTopic> GenerateTopicCandidates(ConversationContext context)
        {
            var candidates = new List<ConversationTopic>();
            
            // Need-driven topics
            if (useNeedDrivenTopics && context.urgentNeeds.Count > 0)
            {
                candidates.AddRange(GenerateNeedBasedTopics(context));
            }
            
            // Belief-driven topics
            if (useBDIBeliefs && context.relevantBeliefs.Count > 0)
            {
                candidates.AddRange(GenerateBeliefBasedTopics(context));
            }
            
            // Environment-driven topics
            if (useEnvironmentalContext && !string.IsNullOrEmpty(context.environmentType))
            {
                candidates.AddRange(GenerateEnvironmentBasedTopics(context));
            }
            
            // Personality-driven topics
            candidates.AddRange(GeneratePersonalityBasedTopics(context));
            
            // Cross-pollination topics (if enabled)
            if (enableCrossPolination && context.targetAgent != null)
            {
                candidates.AddRange(GenerateCrossPollinationTopics(context));
            }
            
            // Template-based topics (fallback)
            candidates.AddRange(GenerateTemplateBasedTopics(context));
            
            return candidates;
        }
        
        /// <summary>
        /// Generate inner dialogue candidates
        /// </summary>
        private List<ConversationTopic> GenerateInnerDialogueCandidates(ConversationContext context)
        {
            var candidates = new List<ConversationTopic>();
            
            // Self-reflection topics
            candidates.AddRange(GenerateSelfReflectionTopics(context));
            
            // Goal and intention topics
            if (context.currentIntention != null)
            {
                candidates.AddRange(GenerateIntentionReflectionTopics(context));
            }
            
            // Need-driven internal dialogue
            candidates.AddRange(GenerateNeedReflectionTopics(context));
            
            // Belief examination
            candidates.AddRange(GenerateBeliefReflectionTopics(context));
            
            return candidates;
        }
        
        /// <summary>
        /// Generate need-based conversation topics
        /// </summary>
        private List<ConversationTopic> GenerateNeedBasedTopics(ConversationContext context)
        {
            var topics = new List<ConversationTopic>();
            
            foreach (var need in context.urgentNeeds)
            {
                var template = GetNeedTemplate(need.needName);
                if (template != null)
                {
                    var topic = new ConversationTopic
                    {
                        content = PopulateTemplate(template.content, context),
                        category = TopicCategory.NeedDriven,
                        relevance = need.GetUrgency() * context.needWeight,
                        diversity = CalculateTopicDiversity(template.content, context.recentTopics),
                        source = $"Need: {need.needName}"
                    };
                    topics.Add(topic);
                }
            }
            
            return topics;
        }
        
        /// <summary>
        /// Generate belief-based conversation topics
        /// </summary>
        private List<ConversationTopic> GenerateBeliefBasedTopics(ConversationContext context)
        {
            var topics = new List<ConversationTopic>();
            
            foreach (var belief in context.relevantBeliefs)
            {
                var template = GetBeliefTemplate(belief.category);
                if (template != null)
                {
                    var topic = new ConversationTopic
                    {
                        content = PopulateBeliefTemplate(template.content, belief, context),
                        category = TopicCategory.BeliefDriven,
                        relevance = belief.confidence * context.beliefWeight,
                        diversity = CalculateTopicDiversity(template.content, context.recentTopics),
                        source = $"Belief: {belief.category}"
                    };
                    topics.Add(topic);
                }
            }
            
            return topics;
        }
        
        /// <summary>
        /// Generate environment-specific topics
        /// </summary>
        private List<ConversationTopic> GenerateEnvironmentBasedTopics(ConversationContext context)
        {
            var topics = new List<ConversationTopic>();
            var templates = GetEnvironmentTemplates(context.environmentType);
            
            foreach (var template in templates)
            {
                var topic = new ConversationTopic
                {
                    content = PopulateTemplate(template.content, context),
                    category = TopicCategory.Environmental,
                    relevance = template.relevance * context.environmentWeight,
                    diversity = CalculateTopicDiversity(template.content, context.recentTopics),
                    source = $"Environment: {context.environmentType}"
                };
                topics.Add(topic);
            }
            
            return topics;
        }
        
        /// <summary>
        /// Generate personality-driven topics
        /// </summary>
        private List<ConversationTopic> GeneratePersonalityBasedTopics(ConversationContext context)
        {
            var topics = new List<ConversationTopic>();
            var templates = GetPersonalityTemplates(context.personalityTraits);
            
            foreach (var template in templates)
            {
                var topic = new ConversationTopic
                {
                    content = PopulateTemplate(template.content, context),
                    category = TopicCategory.PersonalityDriven,
                    relevance = template.relevance * context.personalityWeight,
                    diversity = CalculateTopicDiversity(template.content, context.recentTopics),
                    source = "Personality traits"
                };
                topics.Add(topic);
            }
            
            return topics;
        }
        
        /// <summary>
        /// Generate cross-pollination topics between agents
        /// </summary>
        private List<ConversationTopic> GenerateCrossPollinationTopics(ConversationContext context)
        {
            var topics = new List<ConversationTopic>();
            
            if (context.targetAgent?.Personality != null)
            {
                // Find common interests
                var commonInterests = FindCommonInterests(context.primaryAgent, context.targetAgent);
                foreach (var interest in commonInterests)
                {
                    var template = GetInterestTemplate(interest);
                    if (template != null)
                    {
                        var topic = new ConversationTopic
                        {
                            content = PopulateTemplate(template.content, context),
                            category = TopicCategory.CrossPollination,
                            relevance = 0.8f * context.personalityWeight,
                            diversity = CalculateTopicDiversity(template.content, context.recentTopics),
                            source = $"Common interest: {interest}"
                        };
                        topics.Add(topic);
                    }
                }
                
                // Generate complementary topics based on different strengths
                var complementaryTopics = GenerateComplementaryTopics(context);
                topics.AddRange(complementaryTopics);
            }
            
            return topics;
        }
        
        /// <summary>
        /// Select optimal topic from candidates
        /// </summary>
        private ConversationTopic SelectOptimalTopic(List<ConversationTopic> candidates, ConversationContext context)
        {
            if (candidates.Count == 0)
            {
                return CreateFallbackTopic(context);
            }
            
            // Calculate combined scores (relevance + diversity)
            foreach (var candidate in candidates)
            {
                candidate.combinedScore = (candidate.relevance * 0.6f) + (candidate.diversity * 0.4f);
                
                // Apply context-specific bonuses
                ApplyContextualBonuses(candidate, context);
            }
            
            // Sort by combined score and select top candidate
            var bestTopic = candidates.OrderByDescending(t => t.combinedScore).First();
            
            // Check for conversation loop avoidance
            if (IsConversationLoop(bestTopic, context))
            {
                conversationLoopsAvoided++;
                var alternativeTopic = FindAlternativeTopic(candidates, context);
                if (alternativeTopic != null)
                {
                    bestTopic = alternativeTopic;
                }
            }
            
            return bestTopic;
        }
        
        /// <summary>
        /// Calculate topic diversity score
        /// </summary>
        private float CalculateTopicDiversity(string newTopic, List<ConversationTopic> recentTopics)
        {
            if (recentTopics.Count == 0) return 1f;
            
            float totalSimilarity = 0f;
            foreach (var recentTopic in recentTopics)
            {
                float similarity = CalculateTopicSimilarity(newTopic, recentTopic.content);
                totalSimilarity += similarity;
            }
            
            float averageSimilarity = totalSimilarity / recentTopics.Count;
            return 1f - averageSimilarity; // Higher diversity = lower similarity
        }
        
        /// <summary>
        /// Calculate similarity between two topics
        /// </summary>
        private float CalculateTopicSimilarity(string topic1, string topic2)
        {
            // Simple keyword-based similarity (could be enhanced with NLP)
            var words1 = topic1.ToLower().Split(' ');
            var words2 = topic2.ToLower().Split(' ');
            
            int commonWords = words1.Intersect(words2).Count();
            int totalWords = words1.Union(words2).Count();
            
            return totalWords > 0 ? (float)commonWords / totalWords : 0f;
        }
        
        /// <summary>
        /// Track topic usage and update metrics
        /// </summary>
        private void TrackTopicUsage(Agent agent, ConversationTopic topic)
        {
            // Add to agent's topic history
            if (!agentTopicHistory.ContainsKey(agent.AgentId))
            {
                agentTopicHistory[agent.AgentId] = new List<ConversationTopic>();
            }
            
            var agentHistory = agentTopicHistory[agent.AgentId];
            agentHistory.Add(topic);
            
            // Limit history size
            if (agentHistory.Count > maxTopicHistory)
            {
                agentHistory.RemoveAt(0);
            }
            
            // Update global usage
            if (!globalTopicUsage.ContainsKey(topic.content))
            {
                globalTopicUsage[topic.content] = 0f;
            }
            globalTopicUsage[topic.content]++;
            
            // Update metrics
            totalTopicsGenerated++;
            averageTopicDiversity = ((averageTopicDiversity * (totalTopicsGenerated - 1)) + topic.diversity) / totalTopicsGenerated;
        }
        
        // Helper methods for context extraction
        private List<Need> GetUrgentNeeds(Agent agent)
        {
            if (agent.Needs == null) return new List<Need>();
            return agent.Needs.activeNeeds.Where(n => n.GetUrgency() > 0.6f).ToList();
        }
        
        private List<Belief> GetRelevantBeliefs(Agent agent)
        {
            if (agent.BDI == null) return new List<Belief>();
            return agent.BDI.Beliefs.Where(b => b.confidence > 0.7f).Take(5).ToList();
        }
        
        private string GetCurrentIntention(Agent agent)
        {
            return agent.BDI?.CurrentIntention?.description ?? null;
        }
        
        private string DetectEnvironmentType(Agent agent)
        {
            // Simple environment detection based on nearby objects
            var nearbyObjects = Physics.OverlapSphere(agent.transform.position, 10f);
            
            foreach (var obj in nearbyObjects)
            {
                if (obj.name.ToLower().Contains("research")) return "Research";
                if (obj.name.ToLower().Contains("social")) return "Social";
                if (obj.name.ToLower().Contains("creative")) return "Creative";
                if (obj.name.ToLower().Contains("innovation")) return "Innovation";
            }
            
            return "General";
        }
        
        // Template and topic loading methods would be implemented here
        // For brevity, showing core structure
        
        private void LoadTopicTemplates()
        {
            availableTemplates = new List<TopicTemplate>();
            
            // Load default templates (could be loaded from JSON/ScriptableObjects)
            LoadDefaultTemplates();
        }
        
        private void LoadDefaultTemplates()
        {
            // Sample templates - in production these would be loaded from external files
            availableTemplates.AddRange(new TopicTemplate[]
            {
                new TopicTemplate { content = "I've been thinking about {current_intention} and how it relates to {environment_type}", category = "intention_environment", relevance = 0.8f },
                new TopicTemplate { content = "You know, I'm really focused on {urgent_need} right now. How do you approach that?", category = "need_sharing", relevance = 0.9f },
                new TopicTemplate { content = "I've discovered something interesting about {recent_belief}. What's your take on it?", category = "belief_sharing", relevance = 0.7f },
                new TopicTemplate { content = "This {environment_type} space really inspires me to think about {personality_trait} aspects of our work", category = "environment_personality", relevance = 0.6f }
            });
        }
        
        private string GetFallbackTopic(Agent agent)
        {
            string[] fallbackTopics = {
                "How has your day been going?",
                "What have you been working on lately?",
                "I've been reflecting on some interesting ideas",
                "There's something I've been curious about"
            };
            
            return fallbackTopics[Random.Range(0, fallbackTopics.Length)];
        }
        
        // Additional helper methods would be implemented here...
        
        /// <summary>
        /// Extract personality traits as a dictionary
        /// </summary>
        private Dictionary<string, float> ExtractPersonalityTraits(Agent agent)
        {
            var traits = new Dictionary<string, float>();
            
            if (agent.Personality != null)
            {
                traits["extraversion"] = agent.Personality.extraversion;
                traits["agreeableness"] = agent.Personality.agreeableness;
                traits["conscientiousness"] = agent.Personality.conscientiousness;
                traits["neuroticism"] = agent.Personality.neuroticism;
                traits["openness"] = agent.Personality.openness;
                traits["creativity"] = agent.Personality.creativityLevel;
            }
            
            return traits;
        }
        
        /// <summary>
        /// Get recent topics for an agent
        /// </summary>
        private List<ConversationTopic> GetRecentTopics(Agent agent)
        {
            if (!agentTopicHistory.ContainsKey(agent.AgentId))
            {
                return new List<ConversationTopic>();
            }
            
            return agentTopicHistory[agent.AgentId].TakeLast(5).ToList();
        }
        
        /// <summary>
        /// Build social context between agents
        /// </summary>
        private Dictionary<string, object> BuildSocialContext(Agent agent, Agent targetAgent)
        {
            var context = new Dictionary<string, object>();
            
            if (targetAgent != null)
            {
                context["target_personality"] = ExtractPersonalityTraits(targetAgent);
                context["relationship_history"] = GetRelationshipHistory(agent, targetAgent);
                context["compatibility_score"] = CalculateCompatibility(agent, targetAgent);
            }
            
            return context;
        }
        
        /// <summary>
        /// Calculate compatibility between two agents
        /// </summary>
        private float CalculateCompatibility(Agent agent1, Agent agent2)
        {
            if (agent1.Personality == null || agent2.Personality == null) return 0.5f;
            
            var p1 = agent1.Personality;
            var p2 = agent2.Personality;
            
            float compatibility = 0f;
            
            // Similar extraversion levels
            compatibility += 1f - Mathf.Abs(p1.extraversion - p2.extraversion);
            
            // High agreeableness is generally compatible
            compatibility += (p1.agreeableness + p2.agreeableness) / 2f;
            
            // Similar openness levels
            compatibility += 1f - Mathf.Abs(p1.openness - p2.openness);
            
            // Low neuroticism difference
            compatibility += 1f - Mathf.Abs(p1.neuroticism - p2.neuroticism);
            
            return compatibility / 4f; // Average of factors
        }
        
        /// <summary>
        /// Get relationship history between agents
        /// </summary>
        private int GetRelationshipHistory(Agent agent1, Agent agent2)
        {
            // Count previous conversations between these agents
            int conversationCount = 0;
            
            if (agentTopicHistory.ContainsKey(agent1.AgentId))
            {
                foreach (var topic in agentTopicHistory[agent1.AgentId])
                {
                    if (topic.source?.Contains(agent2.AgentName) == true)
                    {
                        conversationCount++;
                    }
                }
            }
            
            return conversationCount;
        }
        
        /// <summary>
        /// Get need-specific topic template
        /// </summary>
        private TopicTemplate GetNeedTemplate(string needName)
        {
            var needTemplates = availableTemplates.Where(t => t.category.Contains("need")).ToList();
            if (needTemplates.Count == 0) return null;
            
            // Match specific need types
            var specificTemplate = needTemplates.FirstOrDefault(t => t.content.ToLower().Contains(needName.ToLower()));
            if (specificTemplate != null) return specificTemplate;
            
            // Return generic need template
            return needTemplates[Random.Range(0, needTemplates.Count)];
        }
        
        /// <summary>
        /// Get belief-specific topic template
        /// </summary>
        private TopicTemplate GetBeliefTemplate(string beliefCategory)
        {
            var beliefTemplates = availableTemplates.Where(t => t.category.Contains("belief")).ToList();
            if (beliefTemplates.Count == 0) return null;
            
            return beliefTemplates[Random.Range(0, beliefTemplates.Count)];
        }
        
        /// <summary>
        /// Get environment-specific templates
        /// </summary>
        private List<TopicTemplate> GetEnvironmentTemplates(string environmentType)
        {
            return availableTemplates
                .Where(t => t.category.Contains("environment") || t.content.ToLower().Contains(environmentType.ToLower()))
                .ToList();
        }
        
        /// <summary>
        /// Get personality-specific templates
        /// </summary>
        private List<TopicTemplate> GetPersonalityTemplates(Dictionary<string, float> traits)
        {
            var templates = new List<TopicTemplate>();
            
            // Select templates based on dominant traits
            if (traits.ContainsKey("extraversion") && traits["extraversion"] > 0.7f)
            {
                templates.AddRange(availableTemplates.Where(t => t.category.Contains("social")));
            }
            
            if (traits.ContainsKey("creativity") && traits["creativity"] > 0.7f)
            {
                templates.AddRange(availableTemplates.Where(t => t.category.Contains("creative")));
            }
            
            if (traits.ContainsKey("openness") && traits["openness"] > 0.7f)
            {
                templates.AddRange(availableTemplates.Where(t => t.category.Contains("exploration")));
            }
            
            return templates.Distinct().ToList();
        }
        
        /// <summary>
        /// Get interest-specific template
        /// </summary>
        private TopicTemplate GetInterestTemplate(string interest)
        {
            var template = availableTemplates.FirstOrDefault(t => t.content.ToLower().Contains(interest.ToLower()));
            
            if (template == null)
            {
                // Create dynamic template for interest
                template = new TopicTemplate
                {
                    content = $"I've been really interested in {interest} lately. What's your take on it?",
                    category = "interest",
                    relevance = 0.8f
                };
            }
            
            return template;
        }
        
        /// <summary>
        /// Find common interests between agents
        /// </summary>
        private List<string> FindCommonInterests(Agent agent1, Agent agent2)
        {
            var interests = new List<string>();
            
            if (agent1.Personality?.primaryInterests != null && agent2.Personality?.primaryInterests != null)
            {
                interests.AddRange(agent1.Personality.primaryInterests.Intersect(agent2.Personality.primaryInterests));
            }
            
            return interests;
        }
        
        /// <summary>
        /// Generate complementary topics based on different agent strengths
        /// </summary>
        private List<ConversationTopic> GenerateComplementaryTopics(ConversationContext context)
        {
            var topics = new List<ConversationTopic>();
            
            if (context.targetAgent?.Personality == null) return topics;
            
            var p1 = context.primaryAgent.Personality;
            var p2 = context.targetAgent.Personality;
            
            // If one is more creative and the other more analytical
            if (Mathf.Abs(p1.creativityLevel - p2.creativityLevel) > 0.4f)
            {
                string topicContent = p1.creativityLevel > p2.creativityLevel
                    ? "I'd love to get your analytical perspective on this creative idea I've been developing"
                    : "I've been working on something that could use some creative input. What would you suggest?";
                
                topics.Add(new ConversationTopic
                {
                    content = topicContent,
                    category = TopicCategory.CrossPollination,
                    relevance = 0.8f,
                    diversity = 0.9f,
                    source = "Complementary strengths"
                });
            }
            
            return topics;
        }
        
        /// <summary>
        /// Generate self-reflection topics
        /// </summary>
        private List<ConversationTopic> GenerateSelfReflectionTopics(ConversationContext context)
        {
            var topics = new List<ConversationTopic>();
            
            string[] reflectionPrompts = {
                "What have I learned about myself recently?",
                "How have my goals and priorities been evolving?",
                "What aspects of my personality am I most proud of?",
                "How do I want to grow and develop further?",
                "What challenges have been helping me become stronger?"
            };
            
            foreach (var prompt in reflectionPrompts.Take(2))
            {
                topics.Add(new ConversationTopic
                {
                    content = prompt,
                    category = TopicCategory.SelfReflection,
                    relevance = 0.7f,
                    diversity = CalculateTopicDiversity(prompt, context.recentTopics),
                    source = "Self-reflection"
                });
            }
            
            return topics;
        }
        
        // Additional topic generation methods...
        private List<ConversationTopic> GenerateIntentionReflectionTopics(ConversationContext context) { return new List<ConversationTopic>(); }
        private List<ConversationTopic> GenerateNeedReflectionTopics(ConversationContext context) { return new List<ConversationTopic>(); }
        private List<ConversationTopic> GenerateBeliefReflectionTopics(ConversationContext context) { return new List<ConversationTopic>(); }
        private List<ConversationTopic> GenerateTemplateBasedTopics(ConversationContext context) { return new List<ConversationTopic>(); }
        
        /// <summary>
        /// Populate template with context variables
        /// </summary>
        private string PopulateTemplate(string template, ConversationContext context)
        {
            string result = template;
            
            // Replace common variables
            result = result.Replace("{current_intention}", context.currentIntention ?? "my current goals");
            result = result.Replace("{environment_type}", context.environmentType ?? "this space");
            result = result.Replace("{agent_name}", context.primaryAgent.AgentName);
            
            if (context.urgentNeeds.Count > 0)
            {
                result = result.Replace("{urgent_need}", context.urgentNeeds[0].needName);
            }
            
            if (context.personalityTraits.Count > 0)
            {
                var dominantTrait = context.personalityTraits.OrderByDescending(kvp => kvp.Value).First();
                result = result.Replace("{personality_trait}", dominantTrait.Key);
            }
            
            return result;
        }
        
        /// <summary>
        /// Populate belief template with specific belief information
        /// </summary>
        private string PopulateBeliefTemplate(string template, Belief belief, ConversationContext context)
        {
            string result = PopulateTemplate(template, context);
            result = result.Replace("{recent_belief}", belief.description);
            result = result.Replace("{belief_confidence}", (belief.confidence * 100f).ToString("F0") + "%");
            return result;
        }
        
        /// <summary>
        /// Apply contextual bonuses to topic candidates
        /// </summary>
        private void ApplyContextualBonuses(ConversationTopic topic, ConversationContext context)
        {
            // Bonus for matching current environment
            if (topic.content.ToLower().Contains(context.environmentType?.ToLower() ?? ""))
            {
                topic.combinedScore *= 1.2f;
            }
            
            // Bonus for addressing urgent needs
            foreach (var need in context.urgentNeeds)
            {
                if (topic.content.ToLower().Contains(need.needName.ToLower()))
                {
                    topic.combinedScore *= 1.3f;
                    break;
                }
            }
            
            // Bonus for high diversity (avoiding repetition)
            if (topic.diversity > diversityThreshold)
            {
                topic.combinedScore *= 1.1f;
            }
        }
        
        /// <summary>
        /// Check if topic would create a conversation loop
        /// </summary>
        private bool IsConversationLoop(ConversationTopic topic, ConversationContext context)
        {
            if (!avoidRecentTopics || context.recentTopics.Count == 0) return false;
            
            foreach (var recentTopic in context.recentTopics.TakeLast(3))
            {
                float similarity = CalculateTopicSimilarity(topic.content, recentTopic.content);
                if (similarity > 0.7f) // High similarity threshold
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Find alternative topic when loop is detected
        /// </summary>
        private ConversationTopic FindAlternativeTopic(List<ConversationTopic> candidates, ConversationContext context)
        {
            var alternatives = candidates.Where(t => !IsConversationLoop(t, context)).ToList();
            
            if (alternatives.Count > 0)
            {
                return alternatives.OrderByDescending(t => t.combinedScore).First();
            }
            
            return CreateFallbackTopic(context);
        }
        
        /// <summary>
        /// Create fallback topic when all else fails
        /// </summary>
        private ConversationTopic CreateFallbackTopic(ConversationContext context)
        {
            string[] fallbacks = {
                "I've been having some interesting thoughts lately",
                "There's something I've been curious about",
                "I wonder what your perspective would be on something",
                "I've been reflecting on some recent experiences"
            };
            
            return new ConversationTopic
            {
                content = fallbacks[Random.Range(0, fallbacks.Length)],
                category = TopicCategory.Fallback,
                relevance = 0.5f,
                diversity = 1f,
                source = "Fallback"
            };
        }
        
        /// <summary>
        /// Refresh topic contexts periodically
        /// </summary>
        private void RefreshTopicContexts()
        {
            // Clean up old topic history
            foreach (var agentId in agentTopicHistory.Keys.ToList())
            {
                var history = agentTopicHistory[agentId];
                var cutoffTime = System.DateTime.Now.AddMinutes(-30); // Keep 30 minutes of history
                
                history.RemoveAll(t => t.generated < cutoffTime);
            }
            
            // Update global topic usage statistics
            foreach (var topic in globalTopicUsage.Keys.ToList())
            {
                globalTopicUsage[topic] *= 0.95f; // Decay usage over time
                if (globalTopicUsage[topic] < 0.1f)
                {
                    globalTopicUsage.Remove(topic);
                }
            }
            
            if (logTopicGeneration)
            {
                Debug.Log($"🔄 Topic contexts refreshed. Active agents: {agentTopicHistory.Count}, Global topics: {globalTopicUsage.Count}");
            }
        }
        
        #if UNITY_EDITOR
        [Header("Topic Manager Debug")]
        [SerializeField] private float topicDiversity;
        [SerializeField] private int topicsGenerated;
        [SerializeField] private int loopsAvoided;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                topicDiversity = averageTopicDiversity;
                topicsGenerated = totalTopicsGenerated;
                loopsAvoided = conversationLoopsAvoided;
            }
        }
        #endif
    }
}

// Supporting data structures
namespace AIWorld.Communication
{
    [System.Serializable]
    public class ConversationContext
    {
        public Agent primaryAgent;
        public Agent targetAgent;
        public string environmentType;
        public List<Need> urgentNeeds;
        public List<Belief> relevantBeliefs;
        public string currentIntention;
        public Dictionary<string, float> personalityTraits;
        public List<ConversationTopic> recentTopics;
        public Dictionary<string, object> socialContext;
        
        // Weights for different context types
        public float needWeight;
        public float beliefWeight;
        public float environmentWeight;
        public float personalityWeight;
    }
    
    [System.Serializable]
    public class ConversationTopic
    {
        public string content;
        public TopicCategory category;
        public float relevance;
        public float diversity;
        public float combinedScore;
        public string source;
        public System.DateTime generated;
        
        public ConversationTopic()
        {
            generated = System.DateTime.Now;
        }
    }
    
    [System.Serializable]
    public class TopicTemplate
    {
        public string content;
        public string category;
        public float relevance;
        public string[] requiredContext;
        public string[] tags;
    }
    
    public enum TopicCategory
    {
        NeedDriven,
        BeliefDriven,
        Environmental,
        PersonalityDriven,
        CrossPollination,
        TemplateBased,
        SelfReflection,
        Fallback
    }
}