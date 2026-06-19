using System.Threading.Channels;
using DigitalSignDocuments.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalSignDocuments.Web.Services;

public interface IBlockchainQueue
{
    ValueTask EnqueueSignatureAsync(int signatureId, CancellationToken cancellationToken = default);

    ValueTask<int> DequeueSignatureAsync(CancellationToken cancellationToken);
}

public class BlockchainQueue : IBlockchainQueue
{
    private readonly Channel<int> channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueSignatureAsync(int signatureId, CancellationToken cancellationToken = default)
    {
        return channel.Writer.WriteAsync(signatureId, cancellationToken);
    }

    public ValueTask<int> DequeueSignatureAsync(CancellationToken cancellationToken)
    {
        return channel.Reader.ReadAsync(cancellationToken);
    }
}

public class BlockchainWorker(
    IBlockchainQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BlockchainWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var signatureId = await queue.DequeueSignatureAsync(stoppingToken);

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var writer = scope.ServiceProvider.GetRequiredService<IBlockchainBlockWriter>();
                await writer.AppendBlockAsync(signatureId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to append blockchain block for signature {SignatureId}.", signatureId);
            }
        }
    }
}

public interface IBlockchainBlockWriter
{
    Task AppendBlockAsync(int signatureId, CancellationToken cancellationToken = default);
}

public class BlockchainBlockWriter(ApplicationDbContext dbContext) : IBlockchainBlockWriter
{
    public async Task AppendBlockAsync(int signatureId, CancellationToken cancellationToken = default)
    {
        if (await dbContext.BlockchainBlocks.AnyAsync(block => block.DocumentSignatureId == signatureId, cancellationToken))
        {
            return;
        }

        var signature = await dbContext.DocumentSignatures
            .SingleAsync(item => item.Id == signatureId, cancellationToken);

        var previousBlock = await dbContext.BlockchainBlocks
            .OrderByDescending(block => block.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var material = string.Join(
            "|",
            previousBlock?.BlockHash ?? string.Empty,
            signature.Id,
            signature.SignatureValue,
            signature.PayloadHash,
            signature.SignedAt.ToUnixTimeMilliseconds());

        dbContext.BlockchainBlocks.Add(new BlockchainBlock
        {
            DocumentSignatureId = signature.Id,
            Signature = signature.SignatureValue,
            PreviousBlockHash = previousBlock?.BlockHash,
            BlockHash = CryptoHelpers.Sha256(material)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
