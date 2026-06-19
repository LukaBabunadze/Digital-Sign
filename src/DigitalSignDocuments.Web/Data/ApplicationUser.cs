using Microsoft.AspNetCore.Identity;

namespace DigitalSignDocuments.Web.Data;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public UserApprovalStatus ApprovalStatus { get; set; } = UserApprovalStatus.PendingApproval;

    public string? PublicKeyPem { get; set; }

    public DateTimeOffset? PublicKeyRegisteredAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ApprovedAt { get; set; }

    public string? ApprovedByUserId { get; set; }

    public bool CanSign => ApprovalStatus == UserApprovalStatus.Active && !string.IsNullOrWhiteSpace(PublicKeyPem);
}

public enum UserApprovalStatus
{
    PendingApproval = 0,
    RegistrationKeyIssued = 1,
    Active = 2,
    KeyReplacementPending = 3,
    Suspended = 4
}

