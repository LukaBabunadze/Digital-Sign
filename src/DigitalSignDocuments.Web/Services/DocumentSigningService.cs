using System.Security.Cryptography;
using System.Text;
using DigitalSignDocuments.Web.Data;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace DigitalSignDocuments.Web.Services;

public record SigningPayload(int ProcessId, int SignatoryId, string Payload, string PayloadHash);

public interface IDocumentSigningService
{
    Task<Document> UploadDocumentAsync(IBrowserFile file, string authorId, CancellationToken cancellationToken = default);

    Task<SigningProcess> StartSigningProcessAsync(int documentId, string authorId, IReadOnlyList<string> signatoryUserIds, CancellationToken cancellationToken = default);

    Task<SigningPayload> GetSigningPayloadAsync(int processId, string userId, CancellationToken cancellationToken = default);

    Task SubmitSignatureAsync(int processId, string userId, string signatureBase64, CancellationToken cancellationToken = default);

    Task RejectAsync(int processId, string userId, string? reason, CancellationToken cancellationToken = default);

    Task WithdrawSignatureAsync(int processId, string userId, CancellationToken cancellationToken = default);

    Task CancelAsync(int processId, string authorId, CancellationToken cancellationToken = default);

    Task ReinitiateAsync(int processId, string authorId, CancellationToken cancellationToken = default);
}

public class DocumentSigningService(
    ApplicationDbContext dbContext,
    IWebHostEnvironment environment,
    INotificationService notifications,
    IChatService chat,
    IBlockchainQueue blockchainQueue) : IDocumentSigningService
{
    private const long MaxDocumentBytes = 25 * 1024 * 1024;

    public async Task<Document> UploadDocumentAsync(IBrowserFile file, string authorId, CancellationToken cancellationToken = default)
    {
        if (file.Size is <= 0 or > MaxDocumentBytes)
        {
            throw new InvalidOperationException("Document must be between 1 byte and 25 MB.");
        }

        var storageRoot = Path.Combine(environment.ContentRootPath, "App_Data", "Documents");
        Directory.CreateDirectory(storageRoot);

        var safeFileName = Path.GetFileName(file.Name);
        var storedFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        var storagePath = Path.Combine(storageRoot, storedFileName);

        await using var input = file.OpenReadStream(MaxDocumentBytes, cancellationToken);
        await using var output = File.Create(storagePath);
        using var sha256 = SHA256.Create();

        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sha256.TransformBlock(buffer, 0, read, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);

        var document = new Document
        {
            AuthorId = authorId,
            FileName = safeFileName,
            ContentType = file.ContentType,
            Length = file.Size,
            StoragePath = storagePath,
            Sha256Hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant()
        };

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<SigningProcess> StartSigningProcessAsync(int documentId, string authorId, IReadOnlyList<string> signatoryUserIds, CancellationToken cancellationToken = default)
    {
        var distinctSignatories = signatoryUserIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctSignatories.Count == 0)
        {
            throw new InvalidOperationException("At least one signatory is required.");
        }

        var document = await dbContext.Documents.SingleAsync(item => item.Id == documentId && item.AuthorId == authorId, cancellationToken);
        if (document.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only draft documents can start a signing process.");
        }

        var usersCanSign = await dbContext.Users
            .Where(user => distinctSignatories.Contains(user.Id))
            .CountAsync(user => user.ApprovalStatus == UserApprovalStatus.Active && user.PublicKeyPem != null, cancellationToken);

        if (usersCanSign != distinctSignatories.Count)
        {
            throw new InvalidOperationException("Every signatory must be active and have a public key.");
        }

        var process = new SigningProcess
        {
            DocumentId = document.Id,
            AuthorId = authorId,
            Signatories = distinctSignatories
                .Select((userId, index) => new Signatory
                {
                    UserId = userId,
                    Sequence = index + 1,
                    Status = index == 0 ? SignatoryStatus.AwaitingSignature : SignatoryStatus.Pending,
                    RequestedAt = index == 0 ? DateTimeOffset.UtcNow : null
                })
                .ToList()
        };

        document.Status = DocumentStatus.Signing;
        dbContext.SigningProcesses.Add(process);
        await dbContext.SaveChangesAsync(cancellationToken);

        await chat.CreateForSigningProcessAsync(process.Id, authorId, distinctSignatories, cancellationToken);
        await notifications.NotifyAsync(distinctSignatories[0], "Signature requested", $"You are requested to sign {document.FileName}.", $"/signing/{process.Id}", cancellationToken);

        return process;
    }

    public async Task<SigningPayload> GetSigningPayloadAsync(int processId, string userId, CancellationToken cancellationToken = default)
    {
        var process = await LoadProcessAsync(processId, cancellationToken);
        var signatory = GetCurrentSignatory(process);

        if (signatory.UserId != userId)
        {
            throw new InvalidOperationException("It is not this user's turn to sign.");
        }

        var payload = BuildPayload(process, signatory.Sequence);
        return new SigningPayload(process.Id, signatory.Id, payload, CryptoHelpers.Sha256(payload));
    }

    public async Task SubmitSignatureAsync(int processId, string userId, string signatureBase64, CancellationToken cancellationToken = default)
    {
        var process = await LoadProcessAsync(processId, cancellationToken);
        var signatory = GetCurrentSignatory(process);

        if (signatory.UserId != userId)
        {
            throw new InvalidOperationException("It is not this user's turn to sign.");
        }

        if (string.IsNullOrWhiteSpace(signatory.User.PublicKeyPem))
        {
            throw new InvalidOperationException("The user does not have a registered public key.");
        }

        var payload = BuildPayload(process, signatory.Sequence);
        if (!CryptoHelpers.VerifySignature(signatory.User.PublicKeyPem, payload, signatureBase64))
        {
            throw new InvalidOperationException("Signature verification failed.");
        }

        var signature = new DocumentSignature
        {
            SigningProcessId = process.Id,
            SignatoryId = signatory.Id,
            UserId = userId,
            Sequence = signatory.Sequence,
            PayloadHash = CryptoHelpers.Sha256(payload),
            SignatureValue = signatureBase64
        };

        signatory.Status = SignatoryStatus.Signed;
        signatory.RespondedAt = DateTimeOffset.UtcNow;
        dbContext.DocumentSignatures.Add(signature);
        await dbContext.SaveChangesAsync(cancellationToken);

        await blockchainQueue.EnqueueSignatureAsync(signature.Id, cancellationToken);
        await AdvanceAfterSignatureAsync(process, signatory.Sequence, cancellationToken);
    }

    public async Task RejectAsync(int processId, string userId, string? reason, CancellationToken cancellationToken = default)
    {
        var process = await LoadProcessAsync(processId, cancellationToken);
        var signatory = GetCurrentSignatory(process);

        if (signatory.UserId != userId)
        {
            throw new InvalidOperationException("It is not this user's turn to reject.");
        }

        signatory.Status = SignatoryStatus.Rejected;
        signatory.RejectionReason = reason;
        signatory.RespondedAt = DateTimeOffset.UtcNow;
        process.Status = SigningProcessStatus.Suspended;
        process.Document.Status = DocumentStatus.Suspended;

        await dbContext.SaveChangesAsync(cancellationToken);
        await notifications.NotifyAsync(process.AuthorId, "Document rejected", $"{signatory.User.Email} rejected {process.Document.FileName}.", $"/documents/{process.DocumentId}", cancellationToken);
    }

    public async Task WithdrawSignatureAsync(int processId, string userId, CancellationToken cancellationToken = default)
    {
        var process = await LoadProcessAsync(processId, cancellationToken);
        if (process.Status == SigningProcessStatus.Completed)
        {
            throw new InvalidOperationException("Signatures cannot be withdrawn after all signatures have been collected.");
        }

        var signature = process.Signatures
            .Where(item => item.UserId == userId && item.WithdrawnAt == null)
            .OrderByDescending(item => item.Sequence)
            .FirstOrDefault() ?? throw new InvalidOperationException("No active signature was found for this user.");

        foreach (var item in process.Signatures.Where(item => item.Sequence >= signature.Sequence && item.WithdrawnAt == null))
        {
            item.WithdrawnAt = DateTimeOffset.UtcNow;
        }

        foreach (var item in process.Signatories.Where(item => item.Sequence >= signature.Sequence))
        {
            item.Status = item.Sequence == signature.Sequence ? SignatoryStatus.AwaitingSignature : SignatoryStatus.Pending;
            item.RequestedAt = item.Sequence == signature.Sequence ? DateTimeOffset.UtcNow : null;
            item.RespondedAt = null;
            item.RejectionReason = null;
        }

        process.Status = SigningProcessStatus.InProgress;
        process.Document.Status = DocumentStatus.Signing;

        await dbContext.SaveChangesAsync(cancellationToken);
        await notifications.NotifyAsync(userId, "Signature withdrawn", $"Your signature for {process.Document.FileName} was withdrawn.", $"/signing/{process.Id}", cancellationToken);
    }

    public async Task CancelAsync(int processId, string authorId, CancellationToken cancellationToken = default)
    {
        var process = await LoadProcessAsync(processId, cancellationToken);
        if (process.AuthorId != authorId)
        {
            throw new InvalidOperationException("Only the document author can cancel the process.");
        }

        process.Status = SigningProcessStatus.Cancelled;
        process.Document.Status = DocumentStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReinitiateAsync(int processId, string authorId, CancellationToken cancellationToken = default)
    {
        var process = await LoadProcessAsync(processId, cancellationToken);
        if (process.AuthorId != authorId)
        {
            throw new InvalidOperationException("Only the document author can reinitiate the process.");
        }

        if (process.Status != SigningProcessStatus.Suspended)
        {
            throw new InvalidOperationException("Only suspended processes can be reinitiated.");
        }

        foreach (var signatory in process.Signatories)
        {
            signatory.Status = signatory.Sequence == 1 ? SignatoryStatus.AwaitingSignature : SignatoryStatus.Pending;
            signatory.RequestedAt = signatory.Sequence == 1 ? DateTimeOffset.UtcNow : null;
            signatory.RespondedAt = null;
            signatory.RejectionReason = null;
        }

        foreach (var signature in process.Signatures.Where(item => item.WithdrawnAt == null))
        {
            signature.WithdrawnAt = DateTimeOffset.UtcNow;
        }

        process.Status = SigningProcessStatus.InProgress;
        process.Document.Status = DocumentStatus.Signing;
        await dbContext.SaveChangesAsync(cancellationToken);

        var firstSigner = process.Signatories.OrderBy(item => item.Sequence).First();
        await notifications.NotifyAsync(firstSigner.UserId, "Signature requested", $"The signing process for {process.Document.FileName} was reinitiated.", $"/signing/{process.Id}", cancellationToken);
    }

    private async Task AdvanceAfterSignatureAsync(SigningProcess process, int signedSequence, CancellationToken cancellationToken)
    {
        var next = process.Signatories
            .Where(item => item.Sequence > signedSequence)
            .OrderBy(item => item.Sequence)
            .FirstOrDefault();

        if (next is null)
        {
            process.Status = SigningProcessStatus.Completed;
            process.CompletedAt = DateTimeOffset.UtcNow;
            process.Document.Status = DocumentStatus.Active;
            await dbContext.SaveChangesAsync(cancellationToken);
            await notifications.NotifyAsync(process.AuthorId, "Document active", $"{process.Document.FileName} has all required signatures.", $"/documents/{process.DocumentId}", cancellationToken);
            return;
        }

        next.Status = SignatoryStatus.AwaitingSignature;
        next.RequestedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await notifications.NotifyAsync(next.UserId, "Signature requested", $"You are requested to sign {process.Document.FileName}.", $"/signing/{process.Id}", cancellationToken);
    }

    private async Task<SigningProcess> LoadProcessAsync(int processId, CancellationToken cancellationToken)
    {
        return await dbContext.SigningProcesses
            .Include(process => process.Document)
            .Include(process => process.Signatories.OrderBy(signatory => signatory.Sequence))
            .ThenInclude(signatory => signatory.User)
            .Include(process => process.Signatures.OrderBy(signature => signature.Sequence))
            .SingleAsync(process => process.Id == processId, cancellationToken);
    }

    private static Signatory GetCurrentSignatory(SigningProcess process)
    {
        if (process.Status != SigningProcessStatus.InProgress)
        {
            throw new InvalidOperationException("The signing process is not in progress.");
        }

        return process.Signatories
            .OrderBy(item => item.Sequence)
            .FirstOrDefault(item => item.Status == SignatoryStatus.AwaitingSignature)
            ?? throw new InvalidOperationException("No signatory is currently awaiting signature.");
    }

    private static string BuildPayload(SigningProcess process, int sequence)
    {
        var builder = new StringBuilder(process.Document.Sha256Hash);

        foreach (var signature in process.Signatures
            .Where(item => item.Sequence < sequence && item.WithdrawnAt == null)
            .OrderBy(item => item.Sequence))
        {
            builder.AppendLine();
            builder.Append(signature.SignatureValue);
        }

        return builder.ToString();
    }
}
