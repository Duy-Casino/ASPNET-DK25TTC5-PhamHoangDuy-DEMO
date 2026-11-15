BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115103532_CheckoutPoints'
)
BEGIN
    ALTER TABLE [Orders] DROP CONSTRAINT [CK_Orders_Math];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115103532_CheckoutPoints'
)
BEGIN
    ALTER TABLE [Orders] ADD [PointsDiscountAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115103532_CheckoutPoints'
)
BEGIN
    ALTER TABLE [Orders] ADD [PointsUsed] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115103532_CheckoutPoints'
)
BEGIN
    EXEC(N'ALTER TABLE [Orders] ADD CONSTRAINT [CK_Orders_Math] CHECK ([TotalAmount] = [SubTotal] - [DiscountAmount] - [PointsDiscountAmount])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115103532_CheckoutPoints'
)
BEGIN
    EXEC(N'ALTER TABLE [Orders] ADD CONSTRAINT [CK_Orders_Points] CHECK ([PointsUsed] >= 0 AND [PointsDiscountAmount] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115103532_CheckoutPoints'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261115103532_CheckoutPoints', N'10.0.14');
END;

COMMIT;
GO

