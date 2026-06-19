using DigitalSignDocuments.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DigitalSignDocuments.Web.Services;

public record IssuedRegistrationKey(string UserId, string RegistrationKey, DateTimeOffset ExpiresAt);

public interface IRegistrationService
{
    Task<IssuedRegistrationKey> ApproveUserAndIssueKeyAsync(string userId, string adminUserId, CancellationToken cancellationToken = default);

    Task<IssuedRegistrationKey> ApproveKeyReplacementAndIssueKeyAsync(int requestId, string adminUserId, CancellationToken cancellationToken = default);

    Task EnrollPublicKeyAsync(string userId, string registrationKey, string publicKeyPem, CancellationToken cancellationToken = default);

    Task RequestKeyReplacementAsync(string userId, string? reason, CancellationToken cancellationToken = default);
}

public class RegistrationService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : IRegistrationService
{
    public async Task<IssuedRegistrationKey> ApproveUserAndIssueKeyAsync(string userId, string adminUserId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User was not found.");

        user.ApprovalStatus = UserApprovalStatus.RegistrationKeyIssued;
        user.ApprovedAt = DateTimeOffset.UtcNow;
        user.ApprovedByUserId = adminUserId;
        user.EmailConfirmed = true;

        await userManager.UpdateAsync(user);
        return await IssueKeyAsync(userId, adminUserId, RegistrationKeyPurpose.InitialPublicKey, cancellationToken);
    }

    public async Task<IssuedRegistrationKey> ApproveKeyReplacementAndIssueKeyAsync(int requestId, string adminUserId, CancellationToken cancellationToken = default)
    {
        var request = await dbContext.PublicKeyReplacementRequests
            .Include(item => item.User)
            .SingleAsync(item => item.Id == requestId, cancellationToken);

        request.Status = KeyReplacementRequestStatus.Approved;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedByUserId = adminUserId;
        request.User.ApprovalStatus = UserApprovalStatus.RegistrationKeyIssued;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await IssueKeyAsync(request.UserId, adminUserId, RegistrationKeyPurpose.PublicKeyReplacement, cancellationToken);
    }

    public async Task EnrollPublicKeyAsync(string userId, string registrationKey, string publicKeyPem, CancellationToken cancellationToken = default)
    {
        if (!publicKeyPem.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The public key must be a PEM encoded SubjectPublicKeyInfo key.");
        }

        var keyHash = CryptoHelpers.Sha256(registrationKey);
        var key = await dbContext.RegistrationKeys
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.UserId == userId && item.KeyHash == keyHash, cancellationToken);

        if (key is null || key.IsUsed || key.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Registration key is invalid, expired, or already used.");
        }

        key.UsedAt = DateTimeOffset.UtcNow;
        key.User.PublicKeyPem = publicKeyPem;
        key.User.PublicKeyRegisteredAt = DateTimeOffset.UtcNow;
        key.User.ApprovalStatus = UserApprovalStatus.Active;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestKeyReplacementAsync(string userId, string? reason, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User was not found.");
        user.ApprovalStatus = UserApprovalStatus.KeyReplacementPending;

        dbContext.PublicKeyReplacementRequests.Add(new PublicKeyReplacementRequest
        {
            UserId = userId,
            Reason = reason
        });

        await userManager.UpdateAsync(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IssuedRegistrationKey> IssueKeyAsync(
        string userId,
        string adminUserId,
        RegistrationKeyPurpose purpose,
        CancellationToken cancellationToken)
    {
        var rawKey = CryptoHelpers.CreateRegistrationKey();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(14);

        dbContext.RegistrationKeys.Add(new RegistrationKey
        {
            UserId = userId,
            KeyHash = CryptoHelpers.Sha256(rawKey),
            Purpose = purpose,
            CreatedByUserId = adminUserId,
            ExpiresAt = expiresAt
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new IssuedRegistrationKey(userId, rawKey, expiresAt);
    }
}
