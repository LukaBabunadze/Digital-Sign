using DigitalSignDocuments.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalSignDocuments.Web.Services;

public interface INotificationService
{
    Task NotifyAsync(string userId, string title, string message, string? link = null, CancellationToken cancellationToken = default);

    Task<List<Notification>> GetUnreadAsync(string userId, CancellationToken cancellationToken = default);

    Task MarkReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default);
}

public class NotificationService(ApplicationDbContext dbContext) : INotificationService
{
    public async Task NotifyAsync(string userId, string title, string message, string? link = null, CancellationToken cancellationToken = default)
    {
        dbContext.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Link = link
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Notification>> GetUnreadAsync(string userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Notifications
            .Where(notification => notification.UserId == userId && notification.ReadAt == null)
            .OrderByDescending(notification => notification.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId, cancellationToken);

        if (notification is null)
        {
            return;
        }

        notification.ReadAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
