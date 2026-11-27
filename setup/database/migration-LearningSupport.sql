BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE TABLE [LearningActionLogs] (
        [Id] uniqueidentifier NOT NULL,
        [LearnerId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [EnrollmentId] uniqueidentifier NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [Action] varchar(50) NOT NULL,
        [Detail] nvarchar(500) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LearningActionLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE TABLE [LearningSupportRequests] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [QuizId] uniqueidentifier NULL,
        [LessonId] uniqueidentifier NULL,
        [Type] nvarchar(30) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Open',
        [Response] nvarchar(1000) NULL,
        [ResolvedById] uniqueidentifier NULL,
        [ResolvedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_LearningSupportRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningSupportRequests_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_LearningSupportRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE TABLE [QuizAttemptGrants] (
        [Id] uniqueidentifier NOT NULL,
        [QuizId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ExtraAttempts] int NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [GrantedById] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_QuizAttemptGrants] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_QuizAttemptGrants_Extra] CHECK ([ExtraAttempts] BETWEEN 1 AND 10),
        CONSTRAINT [FK_QuizAttemptGrants_Quizzes_QuizId] FOREIGN KEY ([QuizId]) REFERENCES [Quizzes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QuizAttemptGrants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE INDEX [IX_LearningActionLogs_CreatedAt] ON [LearningActionLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE INDEX [IX_LearningActionLogs_LearnerId_CourseId_CreatedAt] ON [LearningActionLogs] ([LearnerId], [CourseId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE INDEX [IX_LearningSupportRequests_CourseId] ON [LearningSupportRequests] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE INDEX [IX_LearningSupportRequests_Status_CreatedAt] ON [LearningSupportRequests] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE INDEX [IX_LearningSupportRequests_UserId_CourseId] ON [LearningSupportRequests] ([UserId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE INDEX [IX_QuizAttemptGrants_QuizId_UserId] ON [QuizAttemptGrants] ([QuizId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    CREATE INDEX [IX_QuizAttemptGrants_UserId] ON [QuizAttemptGrants] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127041117_LearningSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261127041117_LearningSupport', N'10.0.15');
END;

COMMIT;
GO

