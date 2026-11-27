BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    CREATE TABLE [AdminAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [ActorName] nvarchar(100) NOT NULL,
        [Action] varchar(100) NOT NULL,
        [Summary] nvarchar(300) NOT NULL,
        [HttpMethod] varchar(10) NOT NULL,
        [Path] nvarchar(300) NOT NULL,
        [Payload] nvarchar(2000) NULL,
        [StatusCode] int NOT NULL,
        [Succeeded] bit NOT NULL,
        [IpAddress] varchar(64) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminAuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    CREATE TABLE [Announcements] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [Link] nvarchar(300) NULL,
        [Audience] nvarchar(20) NOT NULL,
        [CourseId] uniqueidentifier NULL,
        [SendEmail] bit NOT NULL,
        [RecipientCount] int NOT NULL,
        [EmailCount] int NOT NULL,
        [CreatedById] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Announcements] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    CREATE TABLE [InstructorApplications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Headline] nvarchar(200) NOT NULL,
        [Bio] nvarchar(2000) NOT NULL,
        [Expertise] nvarchar(500) NOT NULL,
        [ProposedTopic] nvarchar(500) NOT NULL,
        [PortfolioUrl] nvarchar(500) NULL,
        [YearsOfExperience] int NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [AdminNote] nvarchar(500) NULL,
        [ProcessedAt] datetime2 NULL,
        [ProcessedById] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_InstructorApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InstructorApplications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    CREATE TABLE [SystemSettings] (
        [Key] varchar(100) NOT NULL,
        [Value] nvarchar(500) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Key])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    CREATE INDEX [IX_AdminAuditLogs_Action_CreatedAt] ON [AdminAuditLogs] ([Action], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    CREATE INDEX [IX_AdminAuditLogs_ActorId_CreatedAt] ON [AdminAuditLogs] ([ActorId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    CREATE INDEX [IX_AdminAuditLogs_CreatedAt] ON [AdminAuditLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    CREATE INDEX [IX_Announcements_CreatedAt] ON [Announcements] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    CREATE INDEX [IX_InstructorApplications_Status_CreatedAt] ON [InstructorApplications] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InstructorApplications_UserId_Pending] ON [InstructorApplications] ([UserId]) WHERE [Status] = ''Pending''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127072444_AdminTools'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261127072444_AdminTools', N'10.0.15');
END;

COMMIT;
GO

