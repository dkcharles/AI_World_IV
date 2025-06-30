using System;
using UnityEngine;

namespace AIWorld.Data
{
    /// <summary>
    /// Core message structure for agent communication
    /// Supports both inter-agent communication and inner dialogue
    /// </summary>
    [System.Serializable]
    public class Message
    {
        [Header("Message Identity")]
        public string messageId;
        public string conversationId;
        public MessageType type;
        
        [Header("Participants")]
        public string senderId;
        public string receiverId; // null for inner dialogue
        
        [Header("Content")]
        public string content;
        public float emotionalTone; // -1 (negative) to 1 (positive)
        public MessageCategory category;
        
        [Header("Context")]
        public Vector3 senderPosition;
        public DateTime timestamp;
        public string[] topics; // extracted topics for knowledge building
        
        [Header("Conversation Flow")]
        public int turnNumber;
        public string previousMessageId;
        public bool expectsResponse;
        
        public Message()
        {
            messageId = Guid.NewGuid().ToString();
            timestamp = DateTime.Now;
            emotionalTone = 0f;
            expectsResponse = true;
        }
        
        public Message(string content, string senderId, string receiverId = null) : this()
        {
            this.content = content;
            this.senderId = senderId;
            this.receiverId = receiverId;
            this.type = receiverId == null ? MessageType.InnerDialogue : MessageType.InterAgent;
        }
        
        /// <summary>
        /// Check if this is an inner dialogue message
        /// </summary>
        public bool IsInnerDialogue => type == MessageType.InnerDialogue || string.IsNullOrEmpty(receiverId);
        
        /// <summary>
        /// Get formatted string for console logging
        /// </summary>
        public string GetFormattedOutput()
        {
            string prefix = IsInnerDialogue ? "💭" : "💬";
            string recipient = IsInnerDialogue ? "(thinking)" : $"→ {receiverId}";
            string emotion = emotionalTone > 0.3f ? "😊" : emotionalTone < -0.3f ? "😔" : "😐";
            
            return $"{prefix} {senderId} {recipient} {emotion}: \"{content}\"";
        }
    }
    
    public enum MessageType
    {
        InterAgent,     // Communication between different agents
        InnerDialogue,  // Agent talking to themselves
        System,         // System messages (environment events, etc.)
        Observation     // Agent observations of the world
    }
    
    public enum MessageCategory
    {
        Greeting,
        Academic,
        Creative,
        Strategic,
        Philosophical,
        Empathetic,
        Planning,
        Reflection,
        Question,
        Statement,
        Request,
        Observation
    }
}