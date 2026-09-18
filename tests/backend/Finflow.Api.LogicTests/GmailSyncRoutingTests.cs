using Microsoft.Extensions.Logging.Abstractions;
using MyFinance.API.Controllers;

namespace Finflow.Api.LogicTests;

public sealed class GmailSyncRoutingTests
{
    [Fact]
    public async Task Sync_RoutesEachRuleAndReturnsCounts_ThenDeduplicatesSameAttachments()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 1, Name = "Conta Principal", Type = "Checking" };
        var card = new Account { UserId = 1, Name = "Cartão Nubank", Type = "Checking", IsCreditCard = true, ClosingDay = 20, DueDay = 10 };
        db.Accounts.AddRange(account, card);
        var options = new GmailIntegrationOptions { Enabled = true, TokenEncryptionKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("01234567890123456789012345678901")) };
        var protector = new AesGcmGmailTokenProtector(options);
        db.GmailIntegrations.Add(new GmailIntegration { UserId = 1, Enabled = true, DefaultAccountId = account.Id, EncryptedRefreshToken = protector.Protect("refresh") });
        db.GmailImportRules.AddRange(
            new GmailImportRule { UserId = 1, Name = "Nubank — Conta", SearchQuery = "subject:conta", TargetAccountId = account.Id },
            new GmailImportRule { UserId = 1, Name = "Nubank — Cartão", SearchQuery = "subject:cartao", TargetAccountId = card.Id });
        await db.SaveChangesAsync();
        var gmail = new FakeGmailClient();
        var controller = new GmailIntegrationController(db, options, gmail, protector, new StatementImportService(db, finance), NullLogger<GmailIntegrationController>.Instance);
        TestContextFactory.AttachUser(controller);

        var first = TestContextFactory.ToJsonElement(((OkObjectResult)await controller.Sync(CancellationToken.None)).Value!);
        Assert.Equal(2, first.GetProperty("found").GetInt32());
        Assert.Equal(2, first.GetProperty("newBatches").GetInt32());
        Assert.Equal(0, first.GetProperty("alreadyProcessed").GetInt32());
        Assert.Equal(0, first.GetProperty("invalid").GetInt32());
        Assert.Equal(account.Id, (await db.ImportBatches.SingleAsync(x => x.FileName == "conta.ofx")).AccountId);
        Assert.Equal(card.Id, (await db.ImportBatches.SingleAsync(x => x.FileName == "cartao.ofx")).AccountId);
        Assert.All(await db.ExternalImportArtifacts.ToListAsync(), x => Assert.Equal("gmail", x.Provider));

        var second = TestContextFactory.ToJsonElement(((OkObjectResult)await controller.Sync(CancellationToken.None)).Value!);
        Assert.Equal(0, second.GetProperty("newBatches").GetInt32());
        Assert.Equal(2, second.GetProperty("alreadyProcessed").GetInt32());
    }

    [Fact]
    public async Task GmailBatches_UsesArtifactProvenance_NotOfxSource()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 1, Name = "Conta", Type = "Checking" };
        db.Accounts.Add(account);
        var batch = new ImportBatch { UserId = 1, AccountId = 1, FileName = "gmail.ofx", FileType = ".ofx", Source = "ofx", FileHash = "hash", ReviewCount = 1 };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        db.ExternalImportArtifacts.Add(new ExternalImportArtifact { UserId = 1, Provider = "gmail", ExternalMessageId = "m1", ExternalAttachmentId = "a1", FileName = batch.FileName, ImportBatchId = batch.Id });
        db.ImportBatches.Add(new ImportBatch { UserId = 1, AccountId = account.Id, FileName = "manual.ofx", FileType = ".ofx", Source = "ofx", FileHash = "manual" });
        await db.SaveChangesAsync();
        var controller = new GmailIntegrationController(db, new GmailIntegrationOptions(), new FakeGmailClient(), new AesGcmGmailTokenProtector(new GmailIntegrationOptions { TokenEncryptionKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("01234567890123456789012345678901")) }), new StatementImportService(db, finance), NullLogger<GmailIntegrationController>.Instance);
        TestContextFactory.AttachUser(controller);

        var result = TestContextFactory.ToJsonElement(((OkObjectResult)await controller.Batches(CancellationToken.None)).Value!);

        var item = Assert.Single(result.EnumerateArray());
        Assert.Equal(batch.Id, item.GetProperty("batchId").GetInt32());
    }

    [Fact]
    public async Task CreateRule_RejectsAccountOwnedByAnotherUser()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var other = new Account { UserId = 2, Name = "Privada", Type = "Checking" };
        db.Accounts.Add(other);
        await db.SaveChangesAsync();
        var controller = new GmailIntegrationController(db, new GmailIntegrationOptions(), new FakeGmailClient(), new AesGcmGmailTokenProtector(new GmailIntegrationOptions { TokenEncryptionKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("01234567890123456789012345678901")) }), new StatementImportService(db, finance), NullLogger<GmailIntegrationController>.Instance);
        TestContextFactory.AttachUser(controller);

        Assert.IsType<BadRequestObjectResult>(await controller.CreateRule(new GmailImportRuleRequest("Inválida", "subject:x", other.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Sync_DoesNotCallGmailWhenAllRulesAreDisabled()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 1, Name = "Conta", Type = "Checking" };
        db.Accounts.Add(account);
        var options = new GmailIntegrationOptions { Enabled = true, TokenEncryptionKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("01234567890123456789012345678901")) };
        var protector = new AesGcmGmailTokenProtector(options);
        db.GmailIntegrations.Add(new GmailIntegration { UserId = 1, Enabled = true, DefaultAccountId = 1, EncryptedRefreshToken = protector.Protect("refresh") });
        db.GmailImportRules.Add(new GmailImportRule { UserId = 1, Name = "Inativa", SearchQuery = "subject:x", TargetAccountId = 1, Enabled = false });
        await db.SaveChangesAsync();
        var fake = new FakeGmailClient();
        var controller = new GmailIntegrationController(db, options, fake, protector, new StatementImportService(db, finance), NullLogger<GmailIntegrationController>.Instance);
        TestContextFactory.AttachUser(controller);

        var result = await controller.Sync(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(fake.RefreshCalled);
    }

    private sealed class FakeGmailClient : IGmailClient
    {
        public bool RefreshCalled { get; private set; }
        public string BuildAuthorizationUrl(string state) => state;
        public Task<GmailToken> ExchangeCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(new GmailToken("access", "refresh", 3600));
        public Task<GmailToken> RefreshAsync(string refreshToken, CancellationToken cancellationToken) { RefreshCalled = true; return Task.FromResult(new GmailToken("access", null, 3600)); }
        public Task<string?> GetEmailAsync(string accessToken, CancellationToken cancellationToken) => Task.FromResult<string?>("test@example.com");
        public Task<IReadOnlyList<GmailMessageAttachment>> FindOfxAttachmentsAsync(string accessToken, string query, CancellationToken cancellationToken)
        {
            var suffix = query.Contains("cartao", StringComparison.OrdinalIgnoreCase) ? "cartao" : "conta";
            return Task.FromResult<IReadOnlyList<GmailMessageAttachment>>([new GmailMessageAttachment($"message-{suffix}", $"attachment-{suffix}", $"{suffix}.ofx", null, null)]);
        }
        public Task<byte[]> DownloadAttachmentAsync(string accessToken, GmailMessageAttachment attachment, CancellationToken cancellationToken) => Task.FromResult(Encoding.UTF8.GetBytes($"<OFX><BANKTRANLIST><STMTTRN><TRNAMT>-10</TRNAMT><DTPOSTED>20260918<FITID>{attachment.AttachmentId}</FITID><MEMO>{attachment.FileName}</MEMO></STMTTRN></BANKTRANLIST></OFX>"));
        public Task RevokeAsync(string token, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
