BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    ALTER TABLE [Users] ADD [Expertise] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    ALTER TABLE [Users] ADD [FacebookUrl] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    ALTER TABLE [Users] ADD [InstructorVerifiedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    ALTER TABLE [Users] ADD [IsVerifiedInstructor] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    ALTER TABLE [Users] ADD [LinkedInUrl] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    ALTER TABLE [Users] ADD [WebsiteUrl] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    ALTER TABLE [Users] ADD [YearsOfExperience] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    ALTER TABLE [Users] ADD [YoutubeUrl] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE TABLE [Conversations] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [InstructorId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastMessageAt] datetime2 NOT NULL,
        [StudentLastReadAt] datetime2 NULL,
        [InstructorLastReadAt] datetime2 NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversations_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_Conversations_Users_InstructorId] FOREIGN KEY ([InstructorId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_Conversations_Users_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE TABLE [InstructorExperiences] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Organization] nvarchar(200) NULL,
        [StartYear] int NULL,
        [EndYear] int NULL,
        [Description] nvarchar(1000) NULL,
        [CredentialUrl] nvarchar(300) NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_InstructorExperiences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InstructorExperiences_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE TABLE [Messages] (
        [Id] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [SenderId] uniqueidentifier NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Messages_Users_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE INDEX [IX_Conversations_CourseId] ON [Conversations] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE INDEX [IX_Conversations_InstructorId_LastMessageAt] ON [Conversations] ([InstructorId], [LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Conversations_StudentId_InstructorId] ON [Conversations] ([StudentId], [InstructorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE INDEX [IX_Conversations_StudentId_LastMessageAt] ON [Conversations] ([StudentId], [LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE INDEX [IX_InstructorExperiences_UserId_SortOrder] ON [InstructorExperiences] ([UserId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE INDEX [IX_Messages_ConversationId_CreatedAt] ON [Messages] ([ConversationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    CREATE INDEX [IX_Messages_SenderId_CreatedAt] ON [Messages] ([SenderId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107131423_InstructorProfileAndMessaging'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107131423_InstructorProfileAndMessaging', N'10.0.14');
END;

COMMIT;
GO

