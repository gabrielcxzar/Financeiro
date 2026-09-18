using System.Globalization;
using System.Net;

namespace Finflow.Api.LogicTests;

public sealed class GmailImportHardeningTests
{
    [Fact]
    public async Task ManualThenGmail_UsesSameOfxPipelineAndFileHashDeduplication()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 1, Name = "Nubank", Type = "Checking" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var service = new StatementImportService(db, finance);
        var ofx = Ofx("fit-1", "Compra compartilhada", -12.50m);

        var manual = await service.PreviewAsync(new StatementImportRequest(1, account.Id, "manual.ofx", ofx), default);
        var gmail = await service.PreviewAsync(new StatementImportRequest(1, account.Id, "gmail.ofx", ofx), default);

        Assert.Equal("ofx", manual.Batch.Source);
        Assert.True(gmail.ExistingFileHash);
        Assert.Equal(manual.Batch.Id, gmail.Batch.Id);
        Assert.Empty(db.Transactions);
    }

    [Fact]
    public async Task SameFitIdWithDifferentFileHash_IsClassifiedAsDuplicateBySharedPipeline()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 1, Name = "Nubank", Type = "Checking" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var service = new StatementImportService(db, finance);

        await service.PreviewAsync(new StatementImportRequest(1, account.Id, "gmail.ofx", Ofx("fit-2", "Compra", -8m)), default);
        var second = await service.PreviewAsync(new StatementImportRequest(1, account.Id, "manual.ofx", Ofx("fit-2", "Compra ajustada", -8m)), default);

        Assert.False(second.ExistingFileHash);
        Assert.Equal(ImportItemStatuses.Duplicate, second.Batch.Items.Single().Status);
    }

    [Fact]
    public async Task GmailThenManual_UsesTheSameFileHashDeduplication()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 1, Name = "Nubank", Type = "Checking" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var service = new StatementImportService(db, finance);
        var ofx = Ofx("fit-3", "Compra reversa", -5m);

        var gmail = await service.PreviewAsync(new StatementImportRequest(1, account.Id, "gmail.ofx", ofx), default);
        var manual = await service.PreviewAsync(new StatementImportRequest(1, account.Id, "manual.ofx", ofx), default);

        Assert.False(gmail.ExistingFileHash);
        Assert.True(manual.ExistingFileHash);
        Assert.Equal(gmail.Batch.Id, manual.Batch.Id);
    }

    [Fact]
    public async Task Ofx1252_PreservesAccentedMemoAndStatementMetadata()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 1, Name = "Nubank", Type = "Checking" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var service = new StatementImportService(db, finance);
        var text = "OFXHEADER:100\r\nENCODING:1252\r\n<DTSTART>20260901<DTEND>20260930<BALAMT>500<STMTTRN><TRNAMT>-1<DTPOSTED>20260917<FITID>cp-1<MEMO>Caf\u00e9</MEMO></STMTTRN>";

        var result = await service.PreviewAsync(new StatementImportRequest(1, account.Id, "extrato.ofx", Encoding.Latin1.GetBytes(text)), default);

        var item = result.Batch.Items.Single();
        Assert.Equal("Café", item.Memo);
        Assert.Equal(500m, result.Batch.LedgerBalance);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), result.Batch.PeriodStart);
        Assert.Equal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc), result.Batch.PeriodEnd);
    }

    [Fact]
    public async Task Classification_CoversInvoiceInstallmentRuleRecurringAndNoRule()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var (bank, card, category, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        db.CategorizationRules.Add(new CategorizationRule { UserId = 1, TextPattern = "Regra Forte", Type = "Expense", CategoryId = category.Id, Confidence = 1m, Priority = 100 });
        db.RecurringTransactions.Add(new RecurringTransaction { UserId = 1, Description = "Streaming", Amount = 20m, Type = "Expense", DayOfMonth = 17, CategoryId = category.Id });
        await db.SaveChangesAsync();
        var service = new StatementImportService(db, finance);
        var ofx = string.Join("", [
            "<OFX><BANKTRANLIST>",
            Tx("invoice", "Pagamento de fatura Cartao Principal", -100m),
            Tx("installment", "Loja parcela 1/3", -30m),
            Tx("rule", "Regra Forte", -10m),
            Tx("recurring", "Streaming mensal", -20m),
            Tx("unknown", "Compra sem regra", -7m),
            "</BANKTRANLIST></OFX>"]);

        var result = await service.PreviewAsync(new StatementImportRequest(1, bank.Id, "extrato.ofx", Encoding.UTF8.GetBytes(ofx)), default);
        var items = result.Batch.Items.ToDictionary(x => x.ExternalId!);

        Assert.Equal(ReportingKinds.InvoicePayment, items["invoice"].ReportingKind);
        Assert.Equal(card.Id, items["invoice"].TargetAccountId);
        Assert.Equal(ImportItemStatuses.NeedsReview, items["installment"].Status);
        Assert.Equal(1, items["installment"].InstallmentNumber);
        Assert.Equal(3, items["installment"].TotalInstallments);
        Assert.Equal(ImportItemStatuses.Ready, items["rule"].Status);
        Assert.Equal(category.Id, items["rule"].CategoryId);
        Assert.Equal(ImportItemStatuses.Ready, items["recurring"].Status);
        Assert.Equal(category.Id, items["recurring"].CategoryId);
        Assert.Equal(ImportItemStatuses.NeedsReview, items["unknown"].Status);
    }

    [Fact]
    public async Task InvalidOfx_DoesNotLeaveOrphanBatch()
    {
        var (db, finance) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 1, Name = "Nubank", Type = "Checking" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var service = new StatementImportService(db, finance);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.PreviewAsync(new StatementImportRequest(1, account.Id, "broken.ofx", Encoding.UTF8.GetBytes("<OFX/>")), default));
        Assert.Empty(db.ImportBatches);
        Assert.Empty(db.ImportedStatementItems);
    }

    [Fact]
    public async Task GmailClient_RequestsFullRestrictedPayloadAndFindsNestedOfxOnly()
    {
        var handler = new StubGmailHandler();
        var options = new GmailIntegrationOptions { ClientId = "client", ClientSecret = "secret", RedirectUri = "https://example.test/callback" };
        var client = new GoogleGmailClient(new HttpClient(handler), options);

        var attachments = await client.FindOfxAttachmentsAsync("access", "has:attachment", default);

        Assert.Equal(2, attachments.Count);
        Assert.All(attachments, x => Assert.EndsWith(".ofx", x.FileName, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attachments, x => x.FileName == "extrato.OFX" && x.AttachmentId == "nested-1");
        Assert.Contains(attachments, x => x.FileName == "second.ofx" && x.AttachmentId == "nested-2");
        Assert.DoesNotContain(handler.Requests, x => x.Contains("format=metadata", StringComparison.OrdinalIgnoreCase));
        var detail = Assert.Single(handler.Requests, x => x.Contains("messages/msg-1", StringComparison.Ordinal));
        Assert.Contains("format=full", detail, StringComparison.Ordinal);
        Assert.Contains("fields=", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", detail, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Ofx(string fitId, string memo, decimal amount) => Encoding.UTF8.GetBytes($"<OFX><BANKTRANLIST>{Tx(fitId, memo, amount)}</BANKTRANLIST></OFX>");
    private static string Tx(string fitId, string memo, decimal amount) => $"<STMTTRN><TRNAMT>{amount.ToString(CultureInfo.InvariantCulture)}</TRNAMT><DTPOSTED>20260917<FITID>{fitId}</FITID><MEMO>{memo}</MEMO></STMTTRN>";

    private sealed class StubGmailHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            var body = request.RequestUri.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal)
                ? "{\"messages\":[{\"id\":\"msg-1\"}]}"
                : "{\"id\":\"msg-1\",\"payload\":{\"headers\":[{\"name\":\"Date\",\"value\":\"Wed, 17 Sep 2026 10:00:00 +0000\"},{\"name\":\"Subject\",\"value\":\"Extratos\"}],\"parts\":[{\"mimeType\":\"multipart/mixed\",\"parts\":[{\"mimeType\":\"text/plain\",\"filename\":\"\",\"body\":{}},{\"mimeType\":\"application/octet-stream\",\"filename\":\"extrato.OFX\",\"body\":{\"attachmentId\":\"nested-1\",\"size\":123}},{\"mimeType\":\"application/pdf\",\"filename\":\"fatura.pdf\",\"body\":{\"attachmentId\":\"pdf-1\"}},{\"mimeType\":\"application/octet-stream\",\"filename\":\"missing.ofx\",\"body\":{}}]},{\"mimeType\":\"application/octet-stream\",\"filename\":\"second.ofx\",\"body\":{\"attachmentId\":\"nested-2\",\"size\":456}}]}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
