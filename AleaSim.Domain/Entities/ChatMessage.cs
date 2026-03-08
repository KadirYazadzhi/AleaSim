using System;

namespace AleaSim.Domain.Entities;

public enum ChatMessageType {
    Global,
    Private
}

public class ChatMessage {
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public string SenderAvatarUrl { get; set; } = string.Empty;
    
    // For Private Chat
    public Guid? ReceiverId { get; set; }
    
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ChatMessageType Type { get; set; } = ChatMessageType.Global;
}
