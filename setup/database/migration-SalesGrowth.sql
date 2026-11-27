BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    ALTER TABLE [Orders] ADD [BundleId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    ALTER TABLE [Orders] ADD [IsGift] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    ALTER TABLE [Courses] ADD [DiscountEndsAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    ALTER TABLE [Carts] ADD [ReminderSentAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE TABLE [Bundles] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Slug] varchar(220) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [ThumbnailUrl] nvarchar(500) NULL,
        [Price] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Bundles] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Bundles_Price] CHECK ([Price] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE TABLE [CourseGifts] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [SenderId] uniqueidentifier NOT NULL,
        [RecipientEmail] nvarchar(256) NOT NULL,
        [RecipientName] nvarchar(100) NOT NULL,
        [Message] nvarchar(500) NULL,
        [Code] varchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [SentAt] datetime2 NULL,
        [RedeemedAt] datetime2 NULL,
        [RedeemedById] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CourseGifts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseGifts_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]),
        CONSTRAINT [FK_CourseGifts_Users_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE TABLE [WishlistItems] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WishlistItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WishlistItems_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_WishlistItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE TABLE [BundleCourses] (
        [Id] uniqueidentifier NOT NULL,
        [BundleId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_BundleCourses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BundleCourses_Bundles_BundleId] FOREIGN KEY ([BundleId]) REFERENCES [Bundles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BundleCourses_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE INDEX [IX_Orders_BundleId] ON [Orders] ([BundleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BundleCourses_BundleId_CourseId] ON [BundleCourses] ([BundleId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE INDEX [IX_BundleCourses_CourseId] ON [BundleCourses] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE INDEX [IX_Bundles_IsActive] ON [Bundles] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bundles_Slug] ON [Bundles] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CourseGifts_Code] ON [CourseGifts] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CourseGifts_OrderId] ON [CourseGifts] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE INDEX [IX_CourseGifts_RecipientEmail] ON [CourseGifts] ([RecipientEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE INDEX [IX_CourseGifts_SenderId] ON [CourseGifts] ([SenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE INDEX [IX_WishlistItems_CourseId] ON [WishlistItems] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WishlistItems_UserId_CourseId] ON [WishlistItems] ([UserId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_Bundles_BundleId] FOREIGN KEY ([BundleId]) REFERENCES [Bundles] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127015316_SalesGrowth'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261127015316_SalesGrowth', N'10.0.15');
END;

COMMIT;
GO

