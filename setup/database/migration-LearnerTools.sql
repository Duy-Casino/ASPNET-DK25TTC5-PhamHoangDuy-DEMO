BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    ALTER TABLE [Users] ADD [ReminderEmailsDisabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    ALTER TABLE [Enrollments] ADD [StudyReminderSentAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE TABLE [LessonNotes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LessonId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [PositionSeconds] int NULL,
        [Content] nvarchar(2000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_LessonNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_LessonNotes_Position] CHECK ([PositionSeconds] IS NULL OR [PositionSeconds] >= 0),
        CONSTRAINT [FK_LessonNotes_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [Lessons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LessonNotes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE TABLE [LessonQuestions] (
        [Id] uniqueidentifier NOT NULL,
        [LessonId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Content] nvarchar(2000) NOT NULL,
        [IsResolved] bit NOT NULL,
        [LastActivityAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_LessonQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LessonQuestions_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [Lessons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LessonQuestions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE TABLE [LessonAnswers] (
        [Id] uniqueidentifier NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Content] nvarchar(2000) NOT NULL,
        [IsInstructor] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_LessonAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LessonAnswers_LessonQuestions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [LessonQuestions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LessonAnswers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE INDEX [IX_LessonAnswers_QuestionId_CreatedAt] ON [LessonAnswers] ([QuestionId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE INDEX [IX_LessonAnswers_UserId] ON [LessonAnswers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE INDEX [IX_LessonNotes_LessonId] ON [LessonNotes] ([LessonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE INDEX [IX_LessonNotes_UserId_CourseId] ON [LessonNotes] ([UserId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE INDEX [IX_LessonNotes_UserId_LessonId] ON [LessonNotes] ([UserId], [LessonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE INDEX [IX_LessonQuestions_CourseId_IsResolved] ON [LessonQuestions] ([CourseId], [IsResolved]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE INDEX [IX_LessonQuestions_LessonId_LastActivityAt] ON [LessonQuestions] ([LessonId], [LastActivityAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    CREATE INDEX [IX_LessonQuestions_UserId] ON [LessonQuestions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127034259_LearnerTools'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261127034259_LearnerTools', N'10.0.15');
END;

COMMIT;
GO

