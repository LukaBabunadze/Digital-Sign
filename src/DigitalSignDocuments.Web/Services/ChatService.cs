using DigitalSignDocuments.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalSignDocuments.Web.Services;

public interface IChatService
{
    Task<Conversation> CreateForSigningProcessAsync(int signingProcessId, string authorId, IEnumerable<string> signatoryUserIds, CancellationToken cancellationToken = default);

    Task<List<ChatMessage>> GetMessagesAsync(int conversationId, string userId, CancellationToken cancellationToken = default);

    Task SendMessageAsync(int conversationId, string senderId, string body, CancellationToken cancellationToken = default);
}

public class ChatService(ApplicationDbContext dbContext) : IChatService
{
    public async Task<Conversation> CreateForSigningProcessAsync(int signingProcessId, string authorId, IEnumerable<string> signatoryUserIds, CancellationToken cancellationToken = default)
    {
        var participantIds = signatoryUserIds
            .Append(authorId)
            .Distinct(StringComparer.Ordinal)
            .Select(userId => new ConversationParticipant { UserId = userId })
            .ToList();

        var conversation = new Conversation
        {
            SigningProcessId = signingProcessId,
            Participants = participantIds
        };

        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(int conversationId, string userId, CancellationToken cancellationToken = default)
    {
        var isParticipant = await dbContext.ConversationParticipants
            .AnyAsync(participant => participant.ConversationId == conversationId && participant.UserId == userId, cancellationToken);

        if (!isParticipant)
        {
            throw new InvalidOperationException("Only conversation participants can read chat messages.");
        }

        return await dbContext.ChatMessages
            .Include(message => message.Sender)
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.SentAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SendMessageAsync(int conversationId, string senderId, string body, CancellationToken cancellationToken = default)
    {
        var isParticipant = await dbContext.ConversationParticipants
            .AnyAsync(participant => participant.ConversationId == conversationId && participant.UserId == senderId, cancellationToken);

        if (!isParticipant)
        {
            throw new InvalidOperationException("Only conversation participants can send chat messages.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        dbContext.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Body = body.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
