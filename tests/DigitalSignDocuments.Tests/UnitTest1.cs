using System.Security.Cryptography;
using System.Text;
using DigitalSignDocuments.Web.Data;
using DigitalSignDocuments.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DigitalSignDocuments.Tests;

public class SigningWorkflowTests
{
    [Fact]
    public async Task SigningProcess_RequiresOrder_AndCompletesDocument()
    {
        using var fixture = new WorkflowFixture();
        await fixture.SeedActiveUsersAsync("author", "first", "second");
        var document = await fixture.AddDocumentAsync("author");
        var process = await fixture.SigningService.StartSigningProcessAsync(document.Id, "author", ["first", "second"]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.SigningService.GetSigningPayloadAsync(process.Id, "second"));

        await fixture.SignAsync(process.Id, "first");
        Assert.Single(fixture.Queue.SignatureIds);

        var secondPayload = await fixture.SigningService.GetSigningPayloadAsync(process.Id, "second");
        Assert.Contains(fixture.FirstSignature!, secondPayload.Payload);

        await fixture.SignAsync(process.Id, "second");

        var saved = await fixture.Db.SigningProcesses
            .Include(item => item.Document)
            .SingleAsync(item => item.Id == process.Id);

        Assert.Equal(SigningProcessStatus.Completed, saved.Status);
        Assert.Equal(DocumentStatus.Active, saved.Document.Status);
    }

    [Fact]
    public async Task Reject_SuspendsSigningProcess()
    {
        using var fixture = new WorkflowFixture();
        await fixture.SeedActiveUsersAsync("author", "first");
        var document = await fixture.AddDocumentAsync("author");
        var process = await fixture.SigningService.StartSigningProcessAsync(document.Id, "author", ["first"]);

        await fixture.SigningService.RejectAsync(process.Id, "first", "Need changes");

        var saved = await fixture.Db.SigningProcesses
            .Include(item => item.Document)
            .Include(item => item.Signatories)
            .SingleAsync(item => item.Id == process.Id);

        Assert.Equal(SigningProcessStatus.Suspended, saved.Status);
        Assert.Equal(DocumentStatus.Suspended, saved.Document.Status);
        Assert.Equal(SignatoryStatus.Rejected, saved.Signatories.Single().Status);
    }

    [Fact]
    public async Task Withdraw_BeforeCompletion_ReopensCurrentAndLaterSignatures()
    {
        using var fixture = new WorkflowFixture();
        await fixture.SeedActiveUsersAsync("author", "first", "second");
        var document = await fixture.AddDocumentAsync("author");
        var process = await fixture.SigningService.StartSigningProcessAsync(document.Id, "author", ["first", "second"]);

        await fixture.SignAsync(process.Id, "first");
        await fixture.SigningService.WithdrawSignatureAsync(process.Id, "first");

        var saved = await fixture.Db.SigningProcesses
            .Include(item => item.Signatories)
            .Include(item => item.Signatures)
            .SingleAsync(item => item.Id == process.Id);

        Assert.Equal(SignatoryStatus.AwaitingSignature, saved.Signatories.Single(item => item.UserId == "first").Status);
        Assert.Equal(SignatoryStatus.Pending, saved.Signatories.Single(item => item.UserId == "second").Status);
        Assert.NotNull(saved.Signatures.Single().WithdrawnAt);
    }

    [Fact]
    public async Task RegistrationKey_IsOneTimeAndMustMatch()
    {
        using var fixture = new WorkflowFixture();
        await fixture.SeedPendingUserAsync("pending");

        var issued = await fixture.RegistrationService.ApproveUserAndIssueKeyAsync("pending", "admin");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.RegistrationService.EnrollPublicKeyAsync("pending", "wrong-key", fixture.Keys["pending"].PublicKeyPem));

        await fixture.RegistrationService.EnrollPublicKeyAsync("pending", issued.RegistrationKey, fixture.Keys["pending"].PublicKeyPem);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.RegistrationService.EnrollPublicKeyAsync("pending", issued.RegistrationKey, fixture.Keys["pending"].PublicKeyPem));

        var user = await fixture.Db.Users.SingleAsync(item => item.Id == "pending");
        Assert.Equal(UserApprovalStatus.Active, user.ApprovalStatus);
        Assert.NotNull(user.PublicKeyRegisteredAt);
    }

    [Fact]
    public async Task BlockchainWriter_ChainsBlocksToPreviousHash()
    {
        using var fixture = new WorkflowFixture();
        await fixture.SeedActiveUsersAsync("author", "first", "second");
        var document = await fixture.AddDocumentAsync("author");
        var process = await fixture.SigningService.StartSigningProcessAsync(document.Id, "author", ["first", "second"]);

        await fixture.SignAsync(process.Id, "first");
        await fixture.SignAsync(process.Id, "second");

        var writer = new BlockchainBlockWriter(fixture.Db);
        foreach (var signatureId in fixture.Queue.SignatureIds)
        {
            await writer.AppendBlockAsync(signatureId);
        }

        var blocks = await fixture.Db.BlockchainBlocks.OrderBy(block => block.Id).ToListAsync();

        Assert.Equal(2, blocks.Count);
        Assert.Null(blocks[0].PreviousBlockHash);
        Assert.Equal(blocks[0].BlockHash, blocks[1].PreviousBlockHash);
    }

    private sealed class WorkflowFixture : IDisposable
    {
        private readonly string contentRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public WorkflowFixture()
        {
            Directory.CreateDirectory(contentRoot);
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);

            Queue = new CapturingBlockchainQueue();
            var notifications = new NotificationService(Db);
            var chat = new ChatService(Db);
            SigningService = new DocumentSigningService(Db, new FakeEnvironment(contentRoot), notifications, chat, Queue);
            RegistrationService = new RegistrationService(Db, CreateUserManager(Db));
        }

        public ApplicationDbContext Db { get; }

        public CapturingBlockchainQueue Queue { get; }

        public DocumentSigningService SigningService { get; }

        public RegistrationService RegistrationService { get; }

        public Dictionary<string, TestKeyPair> Keys { get; } = [];

        public string? FirstSignature { get; private set; }

        public async Task SeedActiveUsersAsync(params string[] userIds)
        {
            foreach (var userId in userIds)
            {
                var keys = CreateKeys();
                Keys[userId] = keys;
                Db.Users.Add(new ApplicationUser
                {
                    Id = userId,
                    UserName = $"{userId}@example.com",
                    Email = $"{userId}@example.com",
                    FirstName = userId,
                    LastName = "User",
                    DateOfBirth = new DateOnly(1990, 1, 1),
                    ApprovalStatus = UserApprovalStatus.Active,
                    PublicKeyPem = keys.PublicKeyPem,
                    PublicKeyRegisteredAt = DateTimeOffset.UtcNow,
                    EmailConfirmed = true
                });
            }

            await Db.SaveChangesAsync();
        }

        public async Task SeedPendingUserAsync(string userId)
        {
            var keys = CreateKeys();
            Keys[userId] = keys;
            Db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = $"{userId}@example.com",
                Email = $"{userId}@example.com",
                FirstName = userId,
                LastName = "User",
                DateOfBirth = new DateOnly(1990, 1, 1),
                ApprovalStatus = UserApprovalStatus.PendingApproval
            });
            Db.Users.Add(new ApplicationUser
            {
                Id = "admin",
                UserName = "admin@example.com",
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
                DateOfBirth = new DateOnly(1980, 1, 1),
                ApprovalStatus = UserApprovalStatus.Active,
                EmailConfirmed = true
            });
            await Db.SaveChangesAsync();
        }

        public async Task<Document> AddDocumentAsync(string authorId)
        {
            var document = new Document
            {
                AuthorId = authorId,
                FileName = "contract.pdf",
                ContentType = "application/pdf",
                StoragePath = Path.Combine(contentRoot, "contract.pdf"),
                Length = 12,
                Sha256Hash = CryptoHelpers.Sha256("contract bytes")
            };

            Db.Documents.Add(document);
            await Db.SaveChangesAsync();
            return document;
        }

        public async Task SignAsync(int processId, string userId)
        {
            var payload = await SigningService.GetSigningPayloadAsync(processId, userId);
            var signature = Keys[userId].Sign(payload.Payload);
            FirstSignature ??= signature;
            await SigningService.SubmitSignatureAsync(processId, userId, signature);
        }

        public void Dispose()
        {
            Db.Dispose();
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }

        private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext db)
        {
            var store = new UserStore<ApplicationUser>(db);
            return new UserManager<ApplicationUser>(
                store,
                Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                [],
                [],
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<UserManager<ApplicationUser>>.Instance);
        }

        private static TestKeyPair CreateKeys()
        {
            var rsa = RSA.Create(2048);
            return new TestKeyPair(rsa, rsa.ExportSubjectPublicKeyInfoPem());
        }
    }

    private sealed class CapturingBlockchainQueue : IBlockchainQueue
    {
        public List<int> SignatureIds { get; } = [];

        public ValueTask EnqueueSignatureAsync(int signatureId, CancellationToken cancellationToken = default)
        {
            SignatureIds.Add(signatureId);
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> DequeueSignatureAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record TestKeyPair(RSA Rsa, string PublicKeyPem)
    {
        public string Sign(string payload)
        {
            var signature = Rsa.SignData(
                Encoding.UTF8.GetBytes(payload),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);

            return Convert.ToBase64String(signature);
        }
    }

    private sealed class FakeEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
