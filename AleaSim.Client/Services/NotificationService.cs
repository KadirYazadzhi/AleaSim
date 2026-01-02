namespace AleaSim.Client.Services;

public class NotificationService {
    public List<NotificationItem> History { get; } = new();
    public int UnreadCount => History.Count(x => !x.IsRead);

    public event Action? OnNotificationsChanged;

    public void Add(string title, string message, string type = "Info") {
        History.Insert(0, new NotificationItem {
            Title = title,
            Message = message,
            Type = type,
            Timestamp = DateTime.Now
        });
        OnNotificationsChanged?.Invoke();
    }

    public void MarkAllAsRead() {
        History.ForEach(x => x.IsRead = true);
        OnNotificationsChanged?.Invoke();
    }
}

public class NotificationItem {
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Type { get; set; } = "Info";
    public DateTime Timestamp { get; set; }
    public bool IsRead { get; set; }
}
