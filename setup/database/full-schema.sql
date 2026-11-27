IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IconUrl] nvarchar(500) NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Categories_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Categories] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Description] nvarchar(256) NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [AvatarUrl] nvarchar(500) NULL,
        [Headline] nvarchar(200) NULL,
        [Bio] nvarchar(max) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [RoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Carts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Carts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Courses] (
        [Id] uniqueidentifier NOT NULL,
        [InstructorId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Slug] nvarchar(220) NOT NULL,
        [ShortDescription] nvarchar(500) NULL,
        [Description] nvarchar(max) NULL,
        [Requirements] nvarchar(max) NULL,
        [Outcomes] nvarchar(max) NULL,
        [ThumbnailUrl] nvarchar(500) NULL,
        [PreviewVideoUrl] nvarchar(500) NULL,
        [Language] nvarchar(10) NOT NULL DEFAULT N'vi',
        [Level] nvarchar(20) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [DiscountPrice] decimal(18,2) NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Draft',
        [RejectionReason] nvarchar(1000) NULL,
        [IsFeatured] bit NOT NULL,
        [SubmittedAt] datetime2 NULL,
        [PublishedAt] datetime2 NULL,
        [TotalDurationSeconds] int NOT NULL,
        [TotalLessons] int NOT NULL,
        [EnrollmentCount] int NOT NULL,
        [ReviewCount] int NOT NULL,
        [AverageRating] decimal(3,2) NOT NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Courses_DiscountPrice] CHECK ([DiscountPrice] IS NULL OR ([DiscountPrice] >= 0 AND [DiscountPrice] <= [Price])),
        CONSTRAINT [CK_Courses_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_Courses_Rating] CHECK ([AverageRating] BETWEEN 0 AND 5),
        CONSTRAINT [FK_Courses_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]),
        CONSTRAINT [FK_Courses_Users_InstructorId] FOREIGN KEY ([InstructorId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [Link] nvarchar(500) NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByIp] nvarchar(64) NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedByIp] nvarchar(64) NULL,
        [ReplacedByTokenHash] nvarchar(128) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [UserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [UserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [UserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [CartItems] (
        [Id] uniqueidentifier NOT NULL,
        [CartId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [AddedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CartItems_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Coupons] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DiscountType] nvarchar(20) NOT NULL,
        [DiscountValue] decimal(18,2) NOT NULL,
        [MaxDiscountAmount] decimal(18,2) NULL,
        [MinOrderAmount] decimal(18,2) NOT NULL,
        [UsageLimit] int NULL,
        [UsedCount] int NOT NULL,
        [PerUserLimit] int NOT NULL,
        [CourseId] uniqueidentifier NULL,
        [StartsAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Coupons] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Coupons_Dates] CHECK ([ExpiresAt] > [StartsAt]),
        CONSTRAINT [CK_Coupons_Percentage] CHECK ([DiscountType] <> 'Percentage' OR [DiscountValue] <= 100),
        CONSTRAINT [CK_Coupons_UsedCount] CHECK ([UsedCount] >= 0),
        CONSTRAINT [CK_Coupons_Value] CHECK ([DiscountValue] > 0),
        CONSTRAINT [FK_Coupons_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseSections] (
        [Id] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CourseSections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseSections_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Rating] tinyint NOT NULL,
        [Comment] nvarchar(2000) NULL,
        [IsHidden] bit NOT NULL,
        [InstructorReply] nvarchar(2000) NULL,
        [RepliedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Reviews_Rating] CHECK ([Rating] BETWEEN 1 AND 5),
        CONSTRAINT [FK_Reviews_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_Reviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] uniqueidentifier NOT NULL,
        [OrderCode] varchar(20) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NULL,
        [CouponCode] nvarchar(50) NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [Note] nvarchar(500) NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [PaidAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Orders_Discount] CHECK ([DiscountAmount] >= 0),
        CONSTRAINT [CK_Orders_Math] CHECK ([TotalAmount] = [SubTotal] - [DiscountAmount]),
        CONSTRAINT [CK_Orders_SubTotal] CHECK ([SubTotal] >= 0),
        CONSTRAINT [CK_Orders_Total] CHECK ([TotalAmount] >= 0),
        CONSTRAINT [FK_Orders_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]),
        CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Lessons] (
        [Id] uniqueidentifier NOT NULL,
        [SectionId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [Content] nvarchar(max) NULL,
        [VideoUrl] nvarchar(500) NULL,
        [VideoPublicId] nvarchar(300) NULL,
        [DurationSeconds] int NOT NULL,
        [SortOrder] int NOT NULL,
        [IsPreview] bit NOT NULL,
        [IsRequired] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Lessons] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Lessons_Duration] CHECK ([DurationSeconds] >= 0),
        CONSTRAINT [FK_Lessons_CourseSections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [CourseSections] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Quizzes] (
        [Id] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [SectionId] uniqueidentifier NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [PassingScore] decimal(5,2) NOT NULL,
        [MaxAttempts] int NULL,
        [TimeLimitMinutes] int NULL,
        [IsRequired] bit NOT NULL,
        [IsFinalExam] bit NOT NULL,
        [ShuffleQuestions] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Quizzes] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Quizzes_MaxAttempts] CHECK ([MaxAttempts] IS NULL OR [MaxAttempts] > 0),
        CONSTRAINT [CK_Quizzes_Passing] CHECK ([PassingScore] BETWEEN 0 AND 100),
        CONSTRAINT [FK_Quizzes_CourseSections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [CourseSections] ([Id]),
        CONSTRAINT [FK_Quizzes_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [CourseTitle] nvarchar(200) NOT NULL,
        [InstructorId] uniqueidentifier NOT NULL,
        [OriginalPrice] decimal(18,2) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [DiscountAllocated] decimal(18,2) NOT NULL,
        [FinalPrice] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Provider] nvarchar(20) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [TransactionRef] varchar(50) NOT NULL,
        [ProviderTransactionId] varchar(100) NULL,
        [BankCode] nvarchar(20) NULL,
        [ResponseCode] nvarchar(20) NULL,
        [RawResponse] nvarchar(max) NULL,
        [PaidAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Enrollments] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NULL,
        [LastLessonId] uniqueidentifier NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Active',
        [ProgressPercent] decimal(5,2) NOT NULL,
        [EnrolledAt] datetime2 NOT NULL,
        [LastAccessedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_Enrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Enrollments_Progress] CHECK ([ProgressPercent] BETWEEN 0 AND 100),
        CONSTRAINT [FK_Enrollments_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_Enrollments_Lessons_LastLessonId] FOREIGN KEY ([LastLessonId]) REFERENCES [Lessons] ([Id]),
        CONSTRAINT [FK_Enrollments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]),
        CONSTRAINT [FK_Enrollments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [LessonResources] (
        [Id] uniqueidentifier NOT NULL,
        [LessonId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FileUrl] nvarchar(500) NOT NULL,
        [PublicId] nvarchar(300) NULL,
        [ContentType] nvarchar(100) NULL,
        [FileSize] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_LessonResources] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_LessonResources_FileSize] CHECK ([FileSize] >= 0),
        CONSTRAINT [FK_LessonResources_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [Lessons] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Questions] (
        [Id] uniqueidentifier NOT NULL,
        [QuizId] uniqueidentifier NOT NULL,
        [Content] nvarchar(2000) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [Points] decimal(5,2) NOT NULL,
        [Explanation] nvarchar(2000) NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Questions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Questions_Points] CHECK ([Points] > 0),
        CONSTRAINT [FK_Questions_Quizzes_QuizId] FOREIGN KEY ([QuizId]) REFERENCES [Quizzes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [QuizAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [QuizId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [SubmittedAt] datetime2 NULL,
        [Score] decimal(7,2) NOT NULL,
        [MaxScore] decimal(7,2) NOT NULL,
        [ScorePercent] decimal(5,2) NOT NULL,
        [IsPassed] bit NOT NULL,
        CONSTRAINT [PK_QuizAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QuizAttempts_Quizzes_QuizId] FOREIGN KEY ([QuizId]) REFERENCES [Quizzes] ([Id]),
        CONSTRAINT [FK_QuizAttempts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [Certificates] (
        [Id] uniqueidentifier NOT NULL,
        [CertificateCode] varchar(30) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [EnrollmentId] uniqueidentifier NOT NULL,
        [StudentName] nvarchar(150) NOT NULL,
        [CourseTitle] nvarchar(200) NOT NULL,
        [InstructorName] nvarchar(150) NOT NULL,
        [FinalScore] decimal(5,2) NULL,
        [PdfUrl] nvarchar(500) NULL,
        [IssuedAt] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedReason] nvarchar(500) NULL,
        CONSTRAINT [PK_Certificates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Certificates_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_Certificates_Enrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [Enrollments] ([Id]),
        CONSTRAINT [FK_Certificates_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [LessonProgress] (
        [Id] uniqueidentifier NOT NULL,
        [EnrollmentId] uniqueidentifier NOT NULL,
        [LessonId] uniqueidentifier NOT NULL,
        [IsCompleted] bit NOT NULL,
        [CompletedAt] datetime2 NULL,
        [LastPositionSeconds] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LessonProgress] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LessonProgress_Enrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [Enrollments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LessonProgress_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [Lessons] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [AnswerOptions] (
        [Id] uniqueidentifier NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        [Content] nvarchar(1000) NOT NULL,
        [IsCorrect] bit NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_AnswerOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AnswerOptions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE TABLE [QuizAnswers] (
        [Id] uniqueidentifier NOT NULL,
        [AttemptId] uniqueidentifier NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        [AnswerOptionId] uniqueidentifier NOT NULL,
        [IsCorrect] bit NOT NULL,
        CONSTRAINT [PK_QuizAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QuizAnswers_AnswerOptions_AnswerOptionId] FOREIGN KEY ([AnswerOptionId]) REFERENCES [AnswerOptions] ([Id]),
        CONSTRAINT [FK_QuizAnswers_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]),
        CONSTRAINT [FK_QuizAnswers_QuizAttempts_AttemptId] FOREIGN KEY ([AttemptId]) REFERENCES [QuizAttempts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AnswerOptions_QuestionId] ON [AnswerOptions] ([QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CartItems_CartId_CourseId] ON [CartItems] ([CartId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CartItems_CourseId] ON [CartItems] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Certificates_CertificateCode] ON [Certificates] ([CertificateCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Certificates_CourseId] ON [Certificates] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Certificates_EnrollmentId] ON [Certificates] ([EnrollmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Certificates_UserId_CourseId] ON [Certificates] ([UserId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Coupons_CourseId] ON [Coupons] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_CategoryId] ON [Courses] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_EnrollmentCount] ON [Courses] ([EnrollmentCount]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_InstructorId] ON [Courses] ([InstructorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Courses_Slug] ON [Courses] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_Status_PublishedAt] ON [Courses] ([Status], [PublishedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseSections_CourseId_SortOrder] ON [CourseSections] ([CourseId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_CourseId] ON [Enrollments] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_LastLessonId] ON [Enrollments] ([LastLessonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_OrderId] ON [Enrollments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Enrollments_UserId_CourseId] ON [Enrollments] ([UserId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LessonProgress_EnrollmentId_LessonId] ON [LessonProgress] ([EnrollmentId], [LessonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LessonProgress_LessonId] ON [LessonProgress] ([LessonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LessonResources_LessonId] ON [LessonResources] ([LessonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Lessons_SectionId_SortOrder] ON [Lessons] ([SectionId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead_CreatedAt] ON [Notifications] ([UserId], [IsRead], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_CourseId] ON [OrderItems] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_InstructorId] ON [OrderItems] ([InstructorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrderItems_OrderId_CourseId] ON [OrderItems] ([OrderId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_CouponId] ON [Orders] ([CouponId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrderCode] ON [Orders] ([OrderCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_Status] ON [Orders] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId_CreatedAt] ON [Orders] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_Provider_ProviderTransactionId] ON [Payments] ([Provider], [ProviderTransactionId]) WHERE [ProviderTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_TransactionRef] ON [Payments] ([TransactionRef]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Questions_QuizId] ON [Questions] ([QuizId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_QuizAnswers_AnswerOptionId] ON [QuizAnswers] ([AnswerOptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuizAnswers_AttemptId_QuestionId_AnswerOptionId] ON [QuizAnswers] ([AttemptId], [QuestionId], [AnswerOptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_QuizAnswers_QuestionId] ON [QuizAnswers] ([QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuizAttempts_QuizId_UserId_AttemptNumber] ON [QuizAttempts] ([QuizId], [UserId], [AttemptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_QuizAttempts_UserId] ON [QuizAttempts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Quizzes_CourseId] ON [Quizzes] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Quizzes_SectionId] ON [Quizzes] ([SectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_CourseId_CreatedAt] ON [Reviews] ([CourseId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Reviews_CourseId_UserId] ON [Reviews] ([CourseId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RoleClaims_RoleId] ON [RoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserClaims_UserId] ON [UserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserLogins_UserId] ON [UserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104075033_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104075033_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

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
    VALUES (N'20261107131423_InstructorProfileAndMessaging', N'10.0.15');
END;

COMMIT;
GO

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
    VALUES (N'20261107150533_RewardPoints', N'10.0.15');
END;

COMMIT;
GO

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
    VALUES (N'20261115103532_CheckoutPoints', N'10.0.15');
END;

COMMIT;
GO

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
    VALUES (N'20261125032357_InstructorPayouts', N'10.0.15');
END;

COMMIT;
GO

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

