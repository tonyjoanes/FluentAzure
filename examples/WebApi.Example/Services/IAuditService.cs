namespace WebApi.Example.Services;

/// <summary>
/// Service interface for audit logging operations.
/// </summary>
public interface IAuditService
{
    Task LogUserActionAsync(string userId, string action, string resource, string? details = null);
    Task LogSystemEventAsync(string eventType, string message, string? userId = null);
    Task LogSecurityEventAsync(string eventType, string userId, string? ipAddress = null, string? userAgent = null);
    Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null);
    Task LogUserCreatedAsync(int userId, string email);
    Task LogUserUpdatedAsync(int userId, string email);
    Task LogUserDeletedAsync(int userId, string email);
}

/// <summary>
/// Audit log entry model.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
