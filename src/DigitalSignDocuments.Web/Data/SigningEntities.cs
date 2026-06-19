using System.ComponentModel.DataAnnotations;

namespace DigitalSignDocuments.Web.Data;

public class RegistrationKey
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = default!;

    public string KeyHash { get; set; } = string.Empty;

    public RegistrationKeyPurpose Purpose { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset? UsedAt { get; set; }

    public bool IsUsed => UsedAt.HasValue;
}

public enum RegistrationKeyPurpose
{
    InitialPublicKey = 0,
    PublicKeyReplacement = 1
}

public class PublicKeyReplacementRequest
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = default!;

    public KeyReplacementRequestStatus Status { get; set; } = KeyReplacementRequestStatus.Pending;

    public string? Reason { get; set; }

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewedByUserId { get; set; }
}

public enum KeyReplacementRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public class Document
{
    public int Id { get; set; }

    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    public string StoragePath { get; set; } = string.Empty;

    public long Length { get; set; }

    public string Sha256Hash { get; set; } = string.Empty;

    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = default!;

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SigningProcess? SigningProcess { get; set; }
}

public enum DocumentStatus
{
    Draft = 0,
    Signing = 1,
    Suspended = 2,
    Active = 3,
    Cancelled = 4
}

public class SigningProcess
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public Document Document { get; set; } = default!;

    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = default!;

    public SigningProcessStatus Status { get; set; } = SigningProcessStatus.InProgress;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public List<Signatory> Signatories { get; set; } = [];

    public List<DocumentSignature> Signatures { get; set; } = [];

    public Conversation? Conversation { get; set; }
}

public enum SigningProcessStatus
{
    InProgress = 0,
    Suspended = 1,
    Completed = 2,
    Cancelled = 3
}

public class Signatory
{
    public int Id { get; set; }

    public int SigningProcessId { get; set; }

    public SigningProcess SigningProcess { get; set; } = default!;

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = default!;

    public int Sequence { get; set; }

    public SignatoryStatus Status { get; set; } = SignatoryStatus.Pending;

    public DateTimeOffset? RequestedAt { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }

    public string? RejectionReason { get; set; }
}

public enum SignatoryStatus
{
    Pending = 0,
    AwaitingSignature = 1,
    Signed = 2,
    Rejected = 3,
    Withdrawn = 4
}

public class DocumentSignature
{
    public int Id { get; set; }

    public int SigningProcessId { get; set; }

    public SigningProcess SigningProcess { get; set; } = default!;

    public int SignatoryId { get; set; }

    public Signatory Signatory { get; set; } = default!;

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = default!;

    public int Sequence { get; set; }

    public string PayloadHash { get; set; } = string.Empty;

    public string SignatureValue { get; set; } = string.Empty;

    public DateTimeOffset SignedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? WithdrawnAt { get; set; }

    public BlockchainBlock? BlockchainBlock { get; set; }
}

public class BlockchainBlock
{
    public long Id { get; set; }

    public int DocumentSignatureId { get; set; }

    public DocumentSignature DocumentSignature { get; set; } = default!;

    public string Signature { get; set; } = string.Empty;

    public string? PreviousBlockHash { get; set; }

    public string BlockHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Notification
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = default!;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Link { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReadAt { get; set; }
}

public class Conversation
{
    public int Id { get; set; }

    public int SigningProcessId { get; set; }

    public SigningProcess SigningProcess { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ConversationParticipant> Participants { get; set; } = [];

    public List<ChatMessage> Messages { get; set; } = [];
}

public class ConversationParticipant
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public Conversation Conversation { get; set; } = default!;

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = default!;
}

public class ChatMessage
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public Conversation Conversation { get; set; } = default!;

    public string SenderId { get; set; } = string.Empty;

    public ApplicationUser Sender { get; set; } = default!;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
