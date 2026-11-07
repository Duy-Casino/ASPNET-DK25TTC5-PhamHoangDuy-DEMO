BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    ALTER TABLE [Orders] ADD [RefundReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    ALTER TABLE [Orders] ADD [RefundedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    ALTER TABLE [Coupons] ADD [RestrictedToUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE TABLE [CourseShares] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [Token] nvarchar(32) NOT NULL,
        [Channel] int NOT NULL,
        [CreatorKey] nvarchar(64) NULL,
        [OpenCount] int NOT NULL,
        [FirstOpenedAt] datetime2 NULL,
        [RewardedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CourseShares] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseShares_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_CourseShares_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE TABLE [DailyCheckIns] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CheckInDate] date NOT NULL,
        [StreakCount] int NOT NULL,
        [PointsAwarded] int NOT NULL,
        [StreakBonusAwarded] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DailyCheckIns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DailyCheckIns_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE TABLE [PointRedemptions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Points] int NOT NULL,
        [VoucherValue] decimal(18,2) NOT NULL,
        [CouponId] uniqueidentifier NOT NULL,
        [CouponCode] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PointRedemptions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PointRedemptions_Points] CHECK ([Points] > 0),
        CONSTRAINT [FK_PointRedemptions_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]),
        CONSTRAINT [FK_PointRedemptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE TABLE [PointRules] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(50) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Points] int NOT NULL,
        [AmountUnit] decimal(18,2) NULL,
        [Threshold] int NULL,
        [IsActive] bit NOT NULL,
        [IsSystem] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PointRules] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PointRules_Points] CHECK ([Points] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE TABLE [PointTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Points] int NOT NULL,
        [BalanceAfter] int NOT NULL,
        [Type] int NOT NULL,
        [RuleKey] nvarchar(50) NULL,
        [ReferenceKey] nvarchar(120) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PointTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PointTransactions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE TABLE [ReferralCodes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Code] nvarchar(16) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ReferralCodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReferralCodes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE TABLE [Referrals] (
        [Id] uniqueidentifier NOT NULL,
        [ReferrerId] uniqueidentifier NOT NULL,
        [ReferredUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RewardedAt] datetime2 NULL,
        [RewardOrderId] uniqueidentifier NULL,
        CONSTRAINT [PK_Referrals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Referrals_Users_ReferredUserId] FOREIGN KEY ([ReferredUserId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_Referrals_Users_ReferrerId] FOREIGN KEY ([ReferrerId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE TABLE [UserPoints] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Balance] int NOT NULL,
        [TotalEarned] int NOT NULL,
        [TotalSpent] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserPoints] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_UserPoints_Balance] CHECK ([Balance] >= 0),
        CONSTRAINT [CK_UserPoints_Totals] CHECK ([TotalEarned] >= 0 AND [TotalSpent] >= 0),
        CONSTRAINT [FK_UserPoints_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE TABLE [CourseShareOpens] (
        [Id] uniqueidentifier NOT NULL,
        [CourseShareId] uniqueidentifier NOT NULL,
        [VisitorKey] nvarchar(64) NOT NULL,
        [VisitorUserId] uniqueidentifier NULL,
        [IsValid] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CourseShareOpens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseShareOpens_CourseShares_CourseShareId] FOREIGN KEY ([CourseShareId]) REFERENCES [CourseShares] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE INDEX [IX_Coupons_RestrictedToUserId] ON [Coupons] ([RestrictedToUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CourseShareOpens_CourseShareId_VisitorKey] ON [CourseShareOpens] ([CourseShareId], [VisitorKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE INDEX [IX_CourseShares_CourseId_CreatedAt] ON [CourseShares] ([CourseId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CourseShares_Token] ON [CourseShares] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE INDEX [IX_CourseShares_UserId_CreatedAt] ON [CourseShares] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DailyCheckIns_UserId_CheckInDate] ON [DailyCheckIns] ([UserId], [CheckInDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PointRedemptions_CouponId] ON [PointRedemptions] ([CouponId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE INDEX [IX_PointRedemptions_UserId_CreatedAt] ON [PointRedemptions] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PointRules_Key] ON [PointRules] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE INDEX [IX_PointTransactions_CreatedAt] ON [PointTransactions] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE INDEX [IX_PointTransactions_RuleKey_CreatedAt] ON [PointTransactions] ([RuleKey], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE INDEX [IX_PointTransactions_UserId_CreatedAt] ON [PointTransactions] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PointTransactions_UserId_ReferenceKey] ON [PointTransactions] ([UserId], [ReferenceKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReferralCodes_Code] ON [ReferralCodes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReferralCodes_UserId] ON [ReferralCodes] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Referrals_ReferredUserId] ON [Referrals] ([ReferredUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE INDEX [IX_Referrals_ReferrerId] ON [Referrals] ([ReferrerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE INDEX [IX_UserPoints_Balance] ON [UserPoints] ([Balance]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPoints_UserId] ON [UserPoints] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107150533_RewardPoints'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107150533_RewardPoints', N'10.0.14');
END;

COMMIT;
GO

