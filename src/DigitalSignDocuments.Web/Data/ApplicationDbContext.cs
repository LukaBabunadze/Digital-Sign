using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DigitalSignDocuments.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RegistrationKey> RegistrationKeys => Set<RegistrationKey>();

    public DbSet<PublicKeyReplacementRequest> PublicKeyReplacementRequests => Set<PublicKeyReplacementRequest>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<SigningProcess> SigningProcesses => Set<SigningProcess>();

    public DbSet<Signatory> Signatories => Set<Signatory>();

    public DbSet<DocumentSignature> DocumentSignatures => Set<DocumentSignature>();

    public DbSet<BlockchainBlock> BlockchainBlocks => Set<BlockchainBlock>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .Property(user => user.FirstName)
            .HasMaxLength(100);

        builder.Entity<ApplicationUser>()
            .Property(user => user.LastName)
            .HasMaxLength(100);

        builder.Entity<ApplicationUser>()
            .Property(user => user.MiddleName)
            .HasMaxLength(100);

        builder.Entity<RegistrationKey>()
            .HasIndex(key => key.KeyHash)
            .IsUnique();

        builder.Entity<RegistrationKey>()
            .HasOne(key => key.User)
            .WithMany()
            .HasForeignKey(key => key.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Document>()
            .HasOne(document => document.Author)
            .WithMany()
            .HasForeignKey(document => document.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SigningProcess>()
            .HasOne(process => process.Document)
            .WithOne(document => document.SigningProcess)
            .HasForeignKey<SigningProcess>(process => process.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SigningProcess>()
            .HasOne(process => process.Author)
            .WithMany()
            .HasForeignKey(process => process.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Signatory>()
            .HasIndex(signatory => new { signatory.SigningProcessId, signatory.Sequence })
            .IsUnique();

        builder.Entity<Signatory>()
            .HasIndex(signatory => new { signatory.SigningProcessId, signatory.UserId })
            .IsUnique();

        builder.Entity<Signatory>()
            .HasOne(signatory => signatory.User)
            .WithMany()
            .HasForeignKey(signatory => signatory.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DocumentSignature>()
            .HasIndex(signature => signature.SignatoryId)
            .IsUnique()
            .HasFilter("[WithdrawnAt] IS NULL");

        builder.Entity<DocumentSignature>()
            .HasOne(signature => signature.User)
            .WithMany()
            .HasForeignKey(signature => signature.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DocumentSignature>()
            .HasOne(signature => signature.SigningProcess)
            .WithMany(process => process.Signatures)
            .HasForeignKey(signature => signature.SigningProcessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DocumentSignature>()
            .HasOne(signature => signature.Signatory)
            .WithMany()
            .HasForeignKey(signature => signature.SignatoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<BlockchainBlock>()
            .HasIndex(block => block.DocumentSignatureId)
            .IsUnique();

        builder.Entity<BlockchainBlock>()
            .HasIndex(block => block.BlockHash)
            .IsUnique();

        builder.Entity<Notification>()
            .HasOne(notification => notification.User)
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Conversation>()
            .HasOne(conversation => conversation.SigningProcess)
            .WithOne(process => process.Conversation)
            .HasForeignKey<Conversation>(conversation => conversation.SigningProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ConversationParticipant>()
            .HasIndex(participant => new { participant.ConversationId, participant.UserId })
            .IsUnique();

        builder.Entity<ConversationParticipant>()
            .HasOne(participant => participant.User)
            .WithMany()
            .HasForeignKey(participant => participant.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatMessage>()
            .HasOne(message => message.Sender)
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
