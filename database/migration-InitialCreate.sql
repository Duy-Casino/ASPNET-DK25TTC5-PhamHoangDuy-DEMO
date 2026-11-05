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

CREATE TABLE [Roles] (
    [Id] uniqueidentifier NOT NULL,
    [Description] nvarchar(256) NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);

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

CREATE TABLE [RoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Carts] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Carts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

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

CREATE TABLE [UserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [CartItems] (
    [Id] uniqueidentifier NOT NULL,
    [CartId] uniqueidentifier NOT NULL,
    [CourseId] uniqueidentifier NOT NULL,
    [AddedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CartItems_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id])
);

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

CREATE TABLE [AnswerOptions] (
    [Id] uniqueidentifier NOT NULL,
    [QuestionId] uniqueidentifier NOT NULL,
    [Content] nvarchar(1000) NOT NULL,
    [IsCorrect] bit NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_AnswerOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AnswerOptions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE CASCADE
);

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

CREATE INDEX [IX_AnswerOptions_QuestionId] ON [AnswerOptions] ([QuestionId]);

CREATE UNIQUE INDEX [IX_CartItems_CartId_CourseId] ON [CartItems] ([CartId], [CourseId]);

CREATE INDEX [IX_CartItems_CourseId] ON [CartItems] ([CourseId]);

CREATE UNIQUE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]);

CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);

CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);

CREATE UNIQUE INDEX [IX_Certificates_CertificateCode] ON [Certificates] ([CertificateCode]);

CREATE INDEX [IX_Certificates_CourseId] ON [Certificates] ([CourseId]);

CREATE UNIQUE INDEX [IX_Certificates_EnrollmentId] ON [Certificates] ([EnrollmentId]);

CREATE UNIQUE INDEX [IX_Certificates_UserId_CourseId] ON [Certificates] ([UserId], [CourseId]);

CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);

CREATE INDEX [IX_Coupons_CourseId] ON [Coupons] ([CourseId]);

CREATE INDEX [IX_Courses_CategoryId] ON [Courses] ([CategoryId]);

CREATE INDEX [IX_Courses_EnrollmentCount] ON [Courses] ([EnrollmentCount]);

CREATE INDEX [IX_Courses_InstructorId] ON [Courses] ([InstructorId]);

CREATE UNIQUE INDEX [IX_Courses_Slug] ON [Courses] ([Slug]);

CREATE INDEX [IX_Courses_Status_PublishedAt] ON [Courses] ([Status], [PublishedAt]);

CREATE INDEX [IX_CourseSections_CourseId_SortOrder] ON [CourseSections] ([CourseId], [SortOrder]);

CREATE INDEX [IX_Enrollments_CourseId] ON [Enrollments] ([CourseId]);

CREATE INDEX [IX_Enrollments_LastLessonId] ON [Enrollments] ([LastLessonId]);

CREATE INDEX [IX_Enrollments_OrderId] ON [Enrollments] ([OrderId]);

CREATE UNIQUE INDEX [IX_Enrollments_UserId_CourseId] ON [Enrollments] ([UserId], [CourseId]);

CREATE UNIQUE INDEX [IX_LessonProgress_EnrollmentId_LessonId] ON [LessonProgress] ([EnrollmentId], [LessonId]);

CREATE INDEX [IX_LessonProgress_LessonId] ON [LessonProgress] ([LessonId]);

CREATE INDEX [IX_LessonResources_LessonId] ON [LessonResources] ([LessonId]);

CREATE INDEX [IX_Lessons_SectionId_SortOrder] ON [Lessons] ([SectionId], [SortOrder]);

CREATE INDEX [IX_Notifications_UserId_IsRead_CreatedAt] ON [Notifications] ([UserId], [IsRead], [CreatedAt]);

CREATE INDEX [IX_OrderItems_CourseId] ON [OrderItems] ([CourseId]);

CREATE INDEX [IX_OrderItems_InstructorId] ON [OrderItems] ([InstructorId]);

CREATE UNIQUE INDEX [IX_OrderItems_OrderId_CourseId] ON [OrderItems] ([OrderId], [CourseId]);

CREATE INDEX [IX_Orders_CouponId] ON [Orders] ([CouponId]);

CREATE UNIQUE INDEX [IX_Orders_OrderCode] ON [Orders] ([OrderCode]);

CREATE INDEX [IX_Orders_Status] ON [Orders] ([Status]);

CREATE INDEX [IX_Orders_UserId_CreatedAt] ON [Orders] ([UserId], [CreatedAt]);

CREATE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);

CREATE UNIQUE INDEX [IX_Payments_Provider_ProviderTransactionId] ON [Payments] ([Provider], [ProviderTransactionId]) WHERE [ProviderTransactionId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_Payments_TransactionRef] ON [Payments] ([TransactionRef]);

CREATE INDEX [IX_Questions_QuizId] ON [Questions] ([QuizId]);

CREATE INDEX [IX_QuizAnswers_AnswerOptionId] ON [QuizAnswers] ([AnswerOptionId]);

CREATE UNIQUE INDEX [IX_QuizAnswers_AttemptId_QuestionId_AnswerOptionId] ON [QuizAnswers] ([AttemptId], [QuestionId], [AnswerOptionId]);

CREATE INDEX [IX_QuizAnswers_QuestionId] ON [QuizAnswers] ([QuestionId]);

CREATE UNIQUE INDEX [IX_QuizAttempts_QuizId_UserId_AttemptNumber] ON [QuizAttempts] ([QuizId], [UserId], [AttemptNumber]);

CREATE INDEX [IX_QuizAttempts_UserId] ON [QuizAttempts] ([UserId]);

CREATE INDEX [IX_Quizzes_CourseId] ON [Quizzes] ([CourseId]);

CREATE INDEX [IX_Quizzes_SectionId] ON [Quizzes] ([SectionId]);

CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);

CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);

CREATE INDEX [IX_Reviews_CourseId_CreatedAt] ON [Reviews] ([CourseId], [CreatedAt]);

CREATE UNIQUE INDEX [IX_Reviews_CourseId_UserId] ON [Reviews] ([CourseId], [UserId]);

CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);

CREATE INDEX [IX_RoleClaims_RoleId] ON [RoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_UserClaims_UserId] ON [UserClaims] ([UserId]);

CREATE INDEX [IX_UserLogins_UserId] ON [UserLogins] ([UserId]);

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261104075033_InitialCreate', N'10.0.14');

COMMIT;
GO

