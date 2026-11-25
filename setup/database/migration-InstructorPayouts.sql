BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE TABLE [InstructorEarnings] (
        [Id] uniqueidentifier NOT NULL,
        [InstructorId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [OrderItemId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [OrderCode] varchar(20) NOT NULL,
        [CourseTitle] nvarchar(200) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [GrossAmount] decimal(18,2) NOT NULL,
        [SharePercent] decimal(18,2) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [AvailableAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_InstructorEarnings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InstructorEarnings_Share] CHECK ([SharePercent] >= 0 AND [SharePercent] <= 100),
        CONSTRAINT [CK_InstructorEarnings_Sign] CHECK (([Type] = 'Sale' AND [Amount] >= 0 AND [GrossAmount] >= 0) OR ([Type] = 'Refund' AND [Amount] <= 0 AND [GrossAmount] <= 0)),
        CONSTRAINT [FK_InstructorEarnings_OrderItems_OrderItemId] FOREIGN KEY ([OrderItemId]) REFERENCES [OrderItems] ([Id]),
        CONSTRAINT [FK_InstructorEarnings_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]),
        CONSTRAINT [FK_InstructorEarnings_Users_InstructorId] FOREIGN KEY ([InstructorId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE TABLE [InstructorPayouts] (
        [Id] uniqueidentifier NOT NULL,
        [InstructorId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Requested',
        [BankName] nvarchar(100) NOT NULL,
        [BankAccountNumber] varchar(30) NOT NULL,
        [BankAccountName] nvarchar(100) NOT NULL,
        [Note] nvarchar(500) NULL,
        [TransferReference] nvarchar(100) NULL,
        [AdminNote] nvarchar(500) NULL,
        [ProcessedAt] datetime2 NULL,
        [ProcessedById] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_InstructorPayouts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InstructorPayouts_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_InstructorPayouts_Users_InstructorId] FOREIGN KEY ([InstructorId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE TABLE [RefundRequests] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [AdminNote] nvarchar(500) NULL,
        [ProcessedAt] datetime2 NULL,
        [ProcessedById] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_RefundRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefundRequests_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]),
        CONSTRAINT [FK_RefundRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE INDEX [IX_InstructorEarnings_InstructorId_AvailableAt] ON [InstructorEarnings] ([InstructorId], [AvailableAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE INDEX [IX_InstructorEarnings_OrderId] ON [InstructorEarnings] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InstructorEarnings_OrderItemId_Type] ON [InstructorEarnings] ([OrderItemId], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE INDEX [IX_InstructorPayouts_InstructorId_CreatedAt] ON [InstructorPayouts] ([InstructorId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InstructorPayouts_InstructorId_Open] ON [InstructorPayouts] ([InstructorId]) WHERE [Status] = ''Requested''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE INDEX [IX_InstructorPayouts_Status_CreatedAt] ON [InstructorPayouts] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RefundRequests_OrderId_Pending] ON [RefundRequests] ([OrderId]) WHERE [Status] = ''Pending''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE INDEX [IX_RefundRequests_Status_CreatedAt] ON [RefundRequests] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    CREATE INDEX [IX_RefundRequests_UserId] ON [RefundRequests] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125032357_InstructorPayouts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261125032357_InstructorPayouts', N'10.0.14');
END;

COMMIT;
GO

