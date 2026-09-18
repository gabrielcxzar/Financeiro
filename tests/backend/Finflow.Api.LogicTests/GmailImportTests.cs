namespace Finflow.Api.LogicTests;

public sealed class GmailImportTests
{
    [Fact]
    public void RefreshTokenProtector_DoesNotStorePlaintextAndRoundTrips()
    {
        var options = new GmailIntegrationOptions { Enabled = true, TokenEncryptionKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("01234567890123456789012345678901")) };
        var protector = new AesGcmGmailTokenProtector(options);
        var encrypted = protector.Protect("refresh-token-value");

        Assert.DoesNotContain("refresh-token-value", encrypted);
        Assert.Equal("refresh-token-value", protector.Unprotect(encrypted));
    }

    [Fact]
    public async Task GmailOfxImport_CreatesPreviewOnlyAndDeduplicatesFileHash()
    {
        var (db, _) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 1, Name = "Nubank", Type = "Checking" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var service = new StatementImportService(db);
        var ofx = Encoding.UTF8.GetBytes("<OFX><BANKTRANLIST><STMTTRN><TRNAMT>-12.50</TRNAMT><DTPOSTED>20260917</DTPOSTED><FITID>abc-1</FITID><MEMO>Compra teste</MEMO></STMTTRN></BANKTRANLIST></OFX>");

        var first = await service.ImportOfxAsync(1, account.Id, "extrato.ofx", ofx, "gmail", default);
        var second = await service.ImportOfxAsync(1, account.Id, "extrato.ofx", ofx, "gmail", default);

        Assert.NotEqual(0, first.BatchId);
        Assert.Equal(1, first.ReviewCount);
        Assert.Equal(0, second.BatchId);
        Assert.Empty(db.Transactions);
    }

    [Fact]
    public async Task GmailOfxImport_RejectsAccountFromAnotherUser()
    {
        var (db, _) = TestContextFactory.Create();
        await using var context = db;
        var account = new Account { UserId = 2, Name = "Outra conta", Type = "Checking" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var service = new StatementImportService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ImportOfxAsync(1, account.Id, "extrato.ofx", Encoding.UTF8.GetBytes("<OFX/>"), "gmail", default));
    }
}
