using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIWorld.Experimental;
using AIWorld.Data;
using AIWorld.Core;

namespace AIWorld.Research
{
    /// <summary>
    /// Research-grade agent identity management ensuring unique participants across studies
    /// Designed for academic publication and experimental validation
    /// </summary>
    public class ResearchValidatedIdentity : MonoBehaviour
    {
        [Header("Research Identity Configuration")]
        [SerializeField] private string uniqueResearchID;
        [SerializeField] private string firstName;
        [SerializeField] private string lastName;
        [SerializeField] private string academicTitle = "Dr.";
        [SerializeField] private string department;
        [SerializeField] private int publicationCount;
        
        [Header("Research Validation")]
        [SerializeField] private bool identityValidated = false;
        [SerializeField] private DateTime identityCreatedAt;
        [SerializeField] private string studyParticipantID;
        
        [Header("Experimental Group Assignment")]
        [SerializeField] private string experimentalGroup = "Control";
        [SerializeField] private string assignedLLMModel = "qwen2.5:3b";
        [SerializeField] private AgentArchetype agentArchetype;
        
        // Static tracking for uniqueness across all agents
        private static HashSet<string> usedNames = new HashSet<string>();
        private static Dictionary<string, ResearchValidatedIdentity> activeIdentities = 
            new Dictionary<string, ResearchValidatedIdentity>();
        
        // Public properties for research access
        public string UniqueResearchID => uniqueResearchID;
        public string FirstName => firstName;
        public string LastName => lastName;
        public string AcademicTitle => academicTitle;
        public string Department => department;
        public string ExperimentalGroup => experimentalGroup;
        public string AssignedLLMModel => assignedLLMModel;
        public AgentArchetype Archetype => agentArchetype;
        
        public string GetFullName() => $"{academicTitle} {firstName} {lastName}";
        public string GetCasualName() => $"{firstName} {lastName}";
        public string GetResearchCitation() => $"{lastName}, {firstName[0]}. ({DateTime.Now.Year})";
        public string GetStudyParticipantID() => studyParticipantID;
        
        private void Awake()
        {
            if (string.IsNullOrEmpty(uniqueResearchID))
            {
                GenerateUniqueIdentity();
            }
            ValidateUniqueIdentity();
        }
        
        /// <summary>
        /// Generate a completely unique identity for this agent
        /// Called during agent creation to ensure no duplicates
        /// </summary>
        public void GenerateUniqueIdentity()
        {
            // Generate unique research ID
            uniqueResearchID = Guid.NewGuid().ToString("N")[..8];
            studyParticipantID = $"P_{uniqueResearchID}";
            identityCreatedAt = DateTime.UtcNow;
            
            // Generate unique name combination
            GenerateUniqueName();
            
            // Set default academic background
            GenerateAcademicBackground();
            
            // Register this identity
            activeIdentities[uniqueResearchID] = this;
            identityValidated = true;
            
            Debug.Log($"[Research Identity] Created unique identity: {GetFullName()} (ID: {uniqueResearchID})");
        }
        
        /// <summary>
        /// Assign this agent to an experimental group with specific configuration
        /// </summary>
        public void AssignToExperimentalGroup(ExperimentalGroup group)
        {
            experimentalGroup = group.groupName;
            assignedLLMModel = group.llmModel;
            agentArchetype = group.archetype;
            
            // Update personality generation based on archetype
            if (TryGetComponent<AgentPersonality>(out var personality))
            {
                ApplyArchetypeToPersonality(personality, agentArchetype);
            }
            
            Debug.Log($"[Research Identity] {GetFullName()} assigned to group '{experimentalGroup}' with model '{assignedLLMModel}'");
        }
        
        private void GenerateUniqueName()
        {
            var nameGenerator = FindFirstObjectByType<PersonalityGenerator>();
            if (nameGenerator == null)
            {
                // Fallback names if no generator available
                var (firstName, lastName) = GenerateRandomName();
            }
            else
            {
                // Use existing personality generator but ensure uniqueness
                var nameAttempts = 0;
                (string firstName, string lastName) nameAttempt;
                do
                {
                    nameAttempt = GenerateRandomName();
                    firstName = nameAttempt.firstName;
                    lastName = nameAttempt.lastName;
                    nameAttempts++;
                    
                    if (nameAttempts > 100)
                    {
                        // Fallback: append number to ensure uniqueness
                        lastName += UnityEngine.Random.Range(1000, 9999).ToString();
                        break;
                    }
                } while (usedNames.Contains($"{firstName}_{lastName}"));
            }
            
            usedNames.Add($"{firstName}_{lastName}");
        }
        
        private (string firstName, string lastName) GenerateRandomName()
        {
            var firstNames = new[] { "Alex", "Jordan", "Casey", "Morgan", "Riley", "Avery", "Quinn", "Sage" };
            var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis" };
            string firstName, lastName;
            do
            {
                firstName = firstNames[UnityEngine.Random.Range(0, firstNames.Length)];
                lastName = lastNames[UnityEngine.Random.Range(0, lastNames.Length)];
            } while (usedNames.Contains($"{firstName}_{lastName}"));
            return (firstName, lastName);
        }
        
        private void GenerateAcademicBackground()
        {
            var titles = new[] { "Dr.", "Prof.", "Mr.", "Ms.", "Dr." }; // Weight towards Dr.
            var departments = new[] 
            { 
                "Computer Science", "Psychology", "Cognitive Science", "Philosophy", 
                "Neuroscience", "Artificial Intelligence", "Data Science", "Linguistics" 
            };
            
            academicTitle = titles[UnityEngine.Random.Range(0, titles.Length)];
            department = departments[UnityEngine.Random.Range(0, departments.Length)];
            publicationCount = UnityEngine.Random.Range(0, 50);
        }
        
        private void ValidateUniqueIdentity()
        {
            var existingIdentities = FindObjectsByType<ResearchValidatedIdentity>(UnityEngine.FindObjectsSortMode.None);
            var duplicates = existingIdentities.Where(id => 
                id != this && 
                id.firstName == firstName && 
                id.lastName == lastName).ToList();
            
            if (duplicates.Any())
            {
                Debug.LogError($"[Research Identity] CRITICAL: Duplicate identity found for {GetFullName()}! " +
                             $"This will corrupt research data. Regenerating identity...");
                usedNames.Remove($"{firstName}_{lastName}");
                GenerateUniqueIdentity();
            }
        }
        
        /// <summary>
        /// Get research-compliant data for this agent
        /// </summary>
        public ResearchParticipantData GetResearchData()
        {
            return new ResearchParticipantData
            {
                participantID = studyParticipantID,
                experimentalGroup = experimentalGroup,
                agentArchetype = agentArchetype.ToString(),
                llmModel = assignedLLMModel,
                createdAt = identityCreatedAt,
                academicBackground = new AcademicBackground
                {
                    title = academicTitle,
                    department = department,
                    publicationCount = publicationCount
                }
            };
        }
        
        private void OnDestroy()
        {
            // Clean up tracking when agent is destroyed
            if (!string.IsNullOrEmpty(uniqueResearchID) && activeIdentities.ContainsKey(uniqueResearchID))
            {
                activeIdentities.Remove(uniqueResearchID);
            }
            
            if (!string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
            {
                usedNames.Remove($"{firstName}_{lastName}");
            }
        }
        
        /// <summary>
        /// Get all currently active research identities (for validation and monitoring)
        /// </summary>
        public static List<ResearchValidatedIdentity> GetAllActiveIdentities()
        {
            return activeIdentities.Values.ToList();
        }
        
        /// <summary>
        /// Validate that all active identities are unique (for research quality assurance)
        /// </summary>
        public static bool ValidateAllIdentitiesUnique()
        {
            var names = activeIdentities.Values.Select(id => $"{id.firstName}_{id.lastName}").ToList();
            return names.Count == names.Distinct().Count();
        }
        
        private void ApplyArchetypeToPersonality(AgentPersonality personality, AgentArchetype archetype)
        {
            // Implementation of ApplyArchetypeToPersonality method
        }
    }
    
    [System.Serializable]
    public class ResearchParticipantData
    {
        public string participantID;
        public string experimentalGroup;
        public string agentArchetype;
        public string llmModel;
        public DateTime createdAt;
        public AcademicBackground academicBackground;
    }
    
    [System.Serializable]
    public class AcademicBackground
    {
        public string title;
        public string department;
        public int publicationCount;
    }
    
    public enum AgentArchetype
    {
        Collaborator,    // High agreeableness, seeks cooperation
        Competitor,      // High conscientiousness, achievement-focused
        Innovator,       // High openness, creativity-focused
        Socializer,      // High extraversion, relationship-focused
        Analyst,         // High intellect, knowledge-focused
        Balanced         // Moderate on all traits
    }
}