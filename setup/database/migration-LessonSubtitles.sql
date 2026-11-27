BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127104656_LessonSubtitles'
)
BEGIN
    ALTER TABLE [Lessons] ADD [SubtitlePublicId] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127104656_LessonSubtitles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261127104656_LessonSubtitles', N'10.0.15');
END;

COMMIT;
GO

