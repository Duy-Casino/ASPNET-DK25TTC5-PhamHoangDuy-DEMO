/* =====================================================================
   EduViet — Online Learning Platform
   SQL Server schema (tham chiếu). Nguồn chính thức là EF Core Migrations
   trong src/backend/src/OnlineLearning.Infrastructure/Persistence/Migrations.
   Chạy: sqlcmd -S localhost -E -i schema.sql
   ===================================================================== */
IF DB_ID(N'OnlineLearningDb') IS NULL CREATE DATABASE OnlineLearningDb COLLATE Vietnamese_CI_AS;
GO
USE OnlineLearningDb;
GO

/* ---------- Identity ---------- */
CREATE TABLE Users (
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserName             NVARCHAR(256) NULL,
    NormalizedUserName   NVARCHAR(256) NULL,
    Email                NVARCHAR(256) NULL,
    NormalizedEmail      NVARCHAR(256) NULL,
    EmailConfirmed       BIT NOT NULL DEFAULT 0,
    PasswordHash         NVARCHAR(MAX) NULL,
    SecurityStamp        NVARCHAR(MAX) NULL,
    ConcurrencyStamp     NVARCHAR(MAX) NULL,
    PhoneNumber          NVARCHAR(20) NULL,
    PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
    TwoFactorEnabled     BIT NOT NULL DEFAULT 0,
    LockoutEnd           DATETIMEOFFSET NULL,
    LockoutEnabled       BIT NOT NULL DEFAULT 1,
    AccessFailedCount    INT NOT NULL DEFAULT 0,
    FullName             NVARCHAR(150) NOT NULL,
    AvatarUrl            NVARCHAR(500) NULL,
    Headline             NVARCHAR(200) NULL,
    Bio                  NVARCHAR(MAX) NULL,
    IsActive             BIT NOT NULL DEFAULT 1,
    CreatedAt            DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt            DATETIME2 NULL
);
CREATE UNIQUE INDEX UX_Users_NormalizedUserName ON Users(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
CREATE INDEX IX_Users_NormalizedEmail ON Users(NormalizedEmail);

CREATE TABLE Roles (
    Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Roles PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Name             NVARCHAR(256) NULL,
    NormalizedName   NVARCHAR(256) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL,
    Description      NVARCHAR(256) NULL
);
CREATE UNIQUE INDEX UX_Roles_NormalizedName ON Roles(NormalizedName) WHERE NormalizedName IS NOT NULL;

CREATE TABLE UserRoles (
    UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UserRoles_Users REFERENCES Users(Id) ON DELETE CASCADE,
    RoleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UserRoles_Roles REFERENCES Roles(Id) ON DELETE CASCADE,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId)
);
CREATE TABLE UserClaims (
    Id INT IDENTITY CONSTRAINT PK_UserClaims PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    ClaimType NVARCHAR(MAX) NULL, ClaimValue NVARCHAR(MAX) NULL
);
CREATE TABLE UserLogins (
    LoginProvider NVARCHAR(450) NOT NULL, ProviderKey NVARCHAR(450) NOT NULL,
    ProviderDisplayName NVARCHAR(MAX) NULL,
    UserId UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT PK_UserLogins PRIMARY KEY (LoginProvider, ProviderKey)
);
CREATE TABLE UserTokens (
    UserId UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    LoginProvider NVARCHAR(450) NOT NULL, Name NVARCHAR(450) NOT NULL, Value NVARCHAR(MAX) NULL,
    CONSTRAINT PK_UserTokens PRIMARY KEY (UserId, LoginProvider, Name)
);
CREATE TABLE RoleClaims (
    Id INT IDENTITY CONSTRAINT PK_RoleClaims PRIMARY KEY,
    RoleId UNIQUEIDENTIFIER NOT NULL REFERENCES Roles(Id) ON DELETE CASCADE,
    ClaimType NVARCHAR(MAX) NULL, ClaimValue NVARCHAR(MAX) NULL
);

CREATE TABLE RefreshTokens (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_RefreshTokens_Users REFERENCES Users(Id) ON DELETE CASCADE,
    TokenHash           NVARCHAR(128) NOT NULL,
    ExpiresAt           DATETIME2 NOT NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByIp         NVARCHAR(64) NULL,
    RevokedAt           DATETIME2 NULL,
    RevokedByIp         NVARCHAR(64) NULL,
    ReplacedByTokenHash NVARCHAR(128) NULL
);
CREATE UNIQUE INDEX UX_RefreshTokens_TokenHash ON RefreshTokens(TokenHash);
CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);

/* ---------- Catalog ---------- */
CREATE TABLE Categories (
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Categories PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ParentId     UNIQUEIDENTIFIER NULL CONSTRAINT FK_Categories_Parent REFERENCES Categories(Id),
    Name         NVARCHAR(100) NOT NULL,
    Slug         NVARCHAR(120) NOT NULL,
    Description  NVARCHAR(500) NULL,
    IconUrl      NVARCHAR(500) NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsActive     BIT NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2 NULL
);
CREATE UNIQUE INDEX UX_Categories_Slug ON Categories(Slug);
CREATE INDEX IX_Categories_ParentId ON Categories(ParentId);

CREATE TABLE Courses (
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Courses PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    InstructorId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Courses_Instructor REFERENCES Users(Id),
    CategoryId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Courses_Categories REFERENCES Categories(Id),
    Title                NVARCHAR(200) NOT NULL,
    Slug                 NVARCHAR(220) NOT NULL,
    ShortDescription     NVARCHAR(500) NULL,
    Description          NVARCHAR(MAX) NULL,
    Requirements         NVARCHAR(MAX) NULL,
    Outcomes             NVARCHAR(MAX) NULL,
    ThumbnailUrl         NVARCHAR(500) NULL,
    PreviewVideoUrl      NVARCHAR(500) NULL,
    Language             NVARCHAR(10) NOT NULL DEFAULT N'vi',
    Level                NVARCHAR(20) NOT NULL CONSTRAINT CK_Courses_Level CHECK (Level IN (N'Beginner', N'Intermediate', N'Advanced')),
    Price                DECIMAL(18,2) NOT NULL CONSTRAINT CK_Courses_Price CHECK (Price >= 0),
    DiscountPrice        DECIMAL(18,2) NULL,
    Status               NVARCHAR(20) NOT NULL DEFAULT N'Draft' CONSTRAINT CK_Courses_Status CHECK (Status IN (N'Draft', N'Pending', N'Published', N'Rejected')),
    RejectionReason      NVARCHAR(1000) NULL,
    IsFeatured           BIT NOT NULL DEFAULT 0,
    SubmittedAt          DATETIME2 NULL,
    PublishedAt          DATETIME2 NULL,
    TotalDurationSeconds INT NOT NULL DEFAULT 0,
    TotalLessons         INT NOT NULL DEFAULT 0,
    EnrollmentCount      INT NOT NULL DEFAULT 0,
    ReviewCount          INT NOT NULL DEFAULT 0,
    AverageRating        DECIMAL(3,2) NOT NULL DEFAULT 0,
    IsDeleted            BIT NOT NULL DEFAULT 0,
    CreatedAt            DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt            DATETIME2 NULL,
    RowVersion           ROWVERSION,
    CONSTRAINT CK_Courses_DiscountPrice CHECK (DiscountPrice IS NULL OR (DiscountPrice >= 0 AND DiscountPrice <= Price))
);
CREATE UNIQUE INDEX UX_Courses_Slug ON Courses(Slug);
CREATE INDEX IX_Courses_InstructorId ON Courses(InstructorId);
CREATE INDEX IX_Courses_CategoryId ON Courses(CategoryId);
CREATE INDEX IX_Courses_Status_PublishedAt ON Courses(Status, PublishedAt);

CREATE TABLE CourseSections (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CourseSections PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CourseId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CourseSections_Courses REFERENCES Courses(Id) ON DELETE CASCADE,
    Title       NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    SortOrder   INT NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2 NULL
);
CREATE INDEX IX_CourseSections_CourseId_SortOrder ON CourseSections(CourseId, SortOrder);

CREATE TABLE Lessons (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Lessons PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    SectionId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Lessons_CourseSections REFERENCES CourseSections(Id) ON DELETE CASCADE,
    Title           NVARCHAR(200) NOT NULL,
    Type            NVARCHAR(20) NOT NULL CONSTRAINT CK_Lessons_Type CHECK (Type IN (N'Video', N'Article')),
    Content         NVARCHAR(MAX) NULL,
    VideoUrl        NVARCHAR(500) NULL,
    VideoPublicId   NVARCHAR(300) NULL,
    DurationSeconds INT NOT NULL DEFAULT 0 CONSTRAINT CK_Lessons_Duration CHECK (DurationSeconds >= 0),
    SortOrder       INT NOT NULL DEFAULT 0,
    IsPreview       BIT NOT NULL DEFAULT 0,
    IsRequired      BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2 NULL
);
CREATE INDEX IX_Lessons_SectionId_SortOrder ON Lessons(SectionId, SortOrder);

CREATE TABLE LessonResources (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_LessonResources PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    LessonId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_LessonResources_Lessons REFERENCES Lessons(Id) ON DELETE CASCADE,
    FileName    NVARCHAR(255) NOT NULL,
    FileUrl     NVARCHAR(500) NOT NULL,
    PublicId    NVARCHAR(300) NULL,
    ContentType NVARCHAR(100) NULL,
    FileSize    BIGINT NOT NULL DEFAULT 0 CONSTRAINT CK_LessonResources_FileSize CHECK (FileSize >= 0),
    CreatedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2 NULL
);
CREATE INDEX IX_LessonResources_LessonId ON LessonResources(LessonId);

/* ---------- Commerce ---------- */
CREATE TABLE Coupons (
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Coupons PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Code              NVARCHAR(50) NOT NULL,
    Description       NVARCHAR(500) NULL,
    DiscountType      NVARCHAR(20) NOT NULL CONSTRAINT CK_Coupons_Type CHECK (DiscountType IN (N'Percentage', N'FixedAmount')),
    DiscountValue     DECIMAL(18,2) NOT NULL CONSTRAINT CK_Coupons_Value CHECK (DiscountValue > 0),
    MaxDiscountAmount DECIMAL(18,2) NULL,
    MinOrderAmount    DECIMAL(18,2) NOT NULL DEFAULT 0,
    UsageLimit        INT NULL,
    UsedCount         INT NOT NULL DEFAULT 0,
    PerUserLimit      INT NOT NULL DEFAULT 1,
    CourseId          UNIQUEIDENTIFIER NULL CONSTRAINT FK_Coupons_Courses REFERENCES Courses(Id),
    StartsAt          DATETIME2 NOT NULL,
    ExpiresAt         DATETIME2 NOT NULL,
    IsActive          BIT NOT NULL DEFAULT 1,
    CreatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2 NULL,
    RowVersion        ROWVERSION,
    CONSTRAINT CK_Coupons_Dates CHECK (ExpiresAt > StartsAt),
    CONSTRAINT CK_Coupons_Percentage CHECK (DiscountType <> N'Percentage' OR DiscountValue <= 100)
);
CREATE UNIQUE INDEX UX_Coupons_Code ON Coupons(Code);

CREATE TABLE Carts (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Carts PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Carts_Users REFERENCES Users(Id) ON DELETE CASCADE,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NULL
);
CREATE UNIQUE INDEX UX_Carts_UserId ON Carts(UserId);

CREATE TABLE CartItems (
    Id       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CartItems PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CartId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CartItems_Carts REFERENCES Carts(Id) ON DELETE CASCADE,
    CourseId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CartItems_Courses REFERENCES Courses(Id),
    AddedAt  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_CartItems_CartId_CourseId ON CartItems(CartId, CourseId);

CREATE TABLE Orders (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Orders PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    OrderCode      VARCHAR(20) NOT NULL,
    UserId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Orders_Users REFERENCES Users(Id),
    CouponId       UNIQUEIDENTIFIER NULL CONSTRAINT FK_Orders_Coupons REFERENCES Coupons(Id),
    CouponCode     NVARCHAR(50) NULL,
    SubTotal       DECIMAL(18,2) NOT NULL CONSTRAINT CK_Orders_SubTotal CHECK (SubTotal >= 0),
    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0 CONSTRAINT CK_Orders_Discount CHECK (DiscountAmount >= 0),
    TotalAmount    DECIMAL(18,2) NOT NULL CONSTRAINT CK_Orders_Total CHECK (TotalAmount >= 0),
    Status         NVARCHAR(20) NOT NULL DEFAULT N'Pending' CONSTRAINT CK_Orders_Status CHECK (Status IN (N'Pending', N'Paid', N'Cancelled', N'Expired', N'Refunded')),
    Note           NVARCHAR(500) NULL,
    ExpiresAt      DATETIME2 NOT NULL,
    PaidAt         DATETIME2 NULL,
    CancelledAt    DATETIME2 NULL,
    CreatedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2 NULL,
    RowVersion     ROWVERSION,
    CONSTRAINT CK_Orders_Math CHECK (TotalAmount = SubTotal - DiscountAmount)
);
CREATE UNIQUE INDEX UX_Orders_OrderCode ON Orders(OrderCode);
CREATE INDEX IX_Orders_UserId_CreatedAt ON Orders(UserId, CreatedAt);
CREATE INDEX IX_Orders_Status ON Orders(Status);

CREATE TABLE OrderItems (
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    OrderId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_OrderItems_Orders REFERENCES Orders(Id) ON DELETE CASCADE,
    CourseId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_OrderItems_Courses REFERENCES Courses(Id),
    CourseTitle       NVARCHAR(200) NOT NULL,
    InstructorId      UNIQUEIDENTIFIER NOT NULL,
    OriginalPrice     DECIMAL(18,2) NOT NULL,
    Price             DECIMAL(18,2) NOT NULL,
    DiscountAllocated DECIMAL(18,2) NOT NULL DEFAULT 0,
    FinalPrice        DECIMAL(18,2) NOT NULL
);
CREATE UNIQUE INDEX UX_OrderItems_OrderId_CourseId ON OrderItems(OrderId, CourseId);
CREATE INDEX IX_OrderItems_CourseId ON OrderItems(CourseId);
CREATE INDEX IX_OrderItems_InstructorId ON OrderItems(InstructorId);

CREATE TABLE Payments (
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Payments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    OrderId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Payments_Orders REFERENCES Orders(Id),
    Provider              NVARCHAR(20) NOT NULL CONSTRAINT CK_Payments_Provider CHECK (Provider IN (N'VNPay', N'MoMo', N'Free', N'Test')),
    Amount                DECIMAL(18,2) NOT NULL,
    Status                NVARCHAR(20) NOT NULL DEFAULT N'Pending' CONSTRAINT CK_Payments_Status CHECK (Status IN (N'Pending', N'Success', N'Failed')),
    TransactionRef        VARCHAR(50) NOT NULL,
    ProviderTransactionId VARCHAR(100) NULL,
    BankCode              NVARCHAR(20) NULL,
    ResponseCode          NVARCHAR(20) NULL,
    RawResponse           NVARCHAR(MAX) NULL,
    PaidAt                DATETIME2 NULL,
    CreatedAt             DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt             DATETIME2 NULL
);
CREATE UNIQUE INDEX UX_Payments_TransactionRef ON Payments(TransactionRef);
CREATE UNIQUE INDEX UX_Payments_Provider_ProviderTxn ON Payments(Provider, ProviderTransactionId) WHERE ProviderTransactionId IS NOT NULL;
CREATE INDEX IX_Payments_OrderId ON Payments(OrderId);

/* ---------- Learning ---------- */
CREATE TABLE Enrollments (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Enrollments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Enrollments_Users REFERENCES Users(Id),
    CourseId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Enrollments_Courses REFERENCES Courses(Id),
    OrderId         UNIQUEIDENTIFIER NULL CONSTRAINT FK_Enrollments_Orders REFERENCES Orders(Id),
    LastLessonId    UNIQUEIDENTIFIER NULL CONSTRAINT FK_Enrollments_LastLesson REFERENCES Lessons(Id),
    Status          NVARCHAR(20) NOT NULL DEFAULT N'Active' CONSTRAINT CK_Enrollments_Status CHECK (Status IN (N'Active', N'Completed', N'Revoked')),
    ProgressPercent DECIMAL(5,2) NOT NULL DEFAULT 0 CONSTRAINT CK_Enrollments_Progress CHECK (ProgressPercent BETWEEN 0 AND 100),
    EnrolledAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastAccessedAt  DATETIME2 NULL,
    CompletedAt     DATETIME2 NULL
);
CREATE UNIQUE INDEX UX_Enrollments_UserId_CourseId ON Enrollments(UserId, CourseId);
CREATE INDEX IX_Enrollments_CourseId ON Enrollments(CourseId);

CREATE TABLE LessonProgress (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_LessonProgress PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    EnrollmentId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_LessonProgress_Enrollments REFERENCES Enrollments(Id) ON DELETE CASCADE,
    LessonId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_LessonProgress_Lessons REFERENCES Lessons(Id),
    IsCompleted         BIT NOT NULL DEFAULT 0,
    CompletedAt         DATETIME2 NULL,
    LastPositionSeconds INT NOT NULL DEFAULT 0,
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_LessonProgress_Enrollment_Lesson ON LessonProgress(EnrollmentId, LessonId);

/* ---------- Assessment ---------- */
CREATE TABLE Quizzes (
    Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Quizzes PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CourseId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Quizzes_Courses REFERENCES Courses(Id) ON DELETE CASCADE,
    SectionId        UNIQUEIDENTIFIER NULL CONSTRAINT FK_Quizzes_CourseSections REFERENCES CourseSections(Id),
    Title            NVARCHAR(200) NOT NULL,
    Description      NVARCHAR(1000) NULL,
    PassingScore     DECIMAL(5,2) NOT NULL DEFAULT 70 CONSTRAINT CK_Quizzes_Passing CHECK (PassingScore BETWEEN 0 AND 100),
    MaxAttempts      INT NULL CONSTRAINT CK_Quizzes_MaxAttempts CHECK (MaxAttempts IS NULL OR MaxAttempts > 0),
    TimeLimitMinutes INT NULL,
    IsRequired       BIT NOT NULL DEFAULT 1,
    IsFinalExam      BIT NOT NULL DEFAULT 0,
    ShuffleQuestions BIT NOT NULL DEFAULT 0,
    SortOrder        INT NOT NULL DEFAULT 0,
    CreatedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2 NULL
);
CREATE INDEX IX_Quizzes_CourseId ON Quizzes(CourseId);
CREATE INDEX IX_Quizzes_SectionId ON Quizzes(SectionId);

CREATE TABLE Questions (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Questions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    QuizId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Questions_Quizzes REFERENCES Quizzes(Id) ON DELETE CASCADE,
    Content     NVARCHAR(2000) NOT NULL,
    Type        NVARCHAR(20) NOT NULL CONSTRAINT CK_Questions_Type CHECK (Type IN (N'SingleChoice', N'MultipleChoice', N'TrueFalse')),
    Points      DECIMAL(5,2) NOT NULL DEFAULT 1 CONSTRAINT CK_Questions_Points CHECK (Points > 0),
    Explanation NVARCHAR(2000) NULL,
    SortOrder   INT NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2 NULL
);
CREATE INDEX IX_Questions_QuizId ON Questions(QuizId);

CREATE TABLE AnswerOptions (
    Id         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AnswerOptions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    QuestionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AnswerOptions_Questions REFERENCES Questions(Id) ON DELETE CASCADE,
    Content    NVARCHAR(1000) NOT NULL,
    IsCorrect  BIT NOT NULL DEFAULT 0,
    SortOrder  INT NOT NULL DEFAULT 0
);
CREATE INDEX IX_AnswerOptions_QuestionId ON AnswerOptions(QuestionId);

CREATE TABLE QuizAttempts (
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_QuizAttempts PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    QuizId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_QuizAttempts_Quizzes REFERENCES Quizzes(Id),
    UserId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_QuizAttempts_Users REFERENCES Users(Id),
    AttemptNumber INT NOT NULL,
    StartedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    SubmittedAt   DATETIME2 NULL,
    Score         DECIMAL(7,2) NOT NULL DEFAULT 0,
    MaxScore      DECIMAL(7,2) NOT NULL DEFAULT 0,
    ScorePercent  DECIMAL(5,2) NOT NULL DEFAULT 0,
    IsPassed      BIT NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX UX_QuizAttempts_Quiz_User_Number ON QuizAttempts(QuizId, UserId, AttemptNumber);
CREATE INDEX IX_QuizAttempts_UserId ON QuizAttempts(UserId);

CREATE TABLE QuizAnswers (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_QuizAnswers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AttemptId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_QuizAnswers_Attempts REFERENCES QuizAttempts(Id) ON DELETE CASCADE,
    QuestionId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_QuizAnswers_Questions REFERENCES Questions(Id),
    AnswerOptionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_QuizAnswers_Options REFERENCES AnswerOptions(Id),
    IsCorrect      BIT NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX UX_QuizAnswers_Attempt_Question_Option ON QuizAnswers(AttemptId, QuestionId, AnswerOptionId);

/* ---------- Certificates, Reviews, Notifications ---------- */
CREATE TABLE Certificates (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Certificates PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CertificateCode VARCHAR(30) NOT NULL,
    UserId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Certificates_Users REFERENCES Users(Id),
    CourseId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Certificates_Courses REFERENCES Courses(Id),
    EnrollmentId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Certificates_Enrollments REFERENCES Enrollments(Id),
    StudentName     NVARCHAR(150) NOT NULL,
    CourseTitle     NVARCHAR(200) NOT NULL,
    InstructorName  NVARCHAR(150) NOT NULL,
    FinalScore      DECIMAL(5,2) NULL,
    PdfUrl          NVARCHAR(500) NULL,
    IssuedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    IsRevoked       BIT NOT NULL DEFAULT 0,
    RevokedAt       DATETIME2 NULL,
    RevokedReason   NVARCHAR(500) NULL
);
CREATE UNIQUE INDEX UX_Certificates_Code ON Certificates(CertificateCode);
CREATE UNIQUE INDEX UX_Certificates_User_Course ON Certificates(UserId, CourseId);
CREATE UNIQUE INDEX UX_Certificates_EnrollmentId ON Certificates(EnrollmentId);

CREATE TABLE Reviews (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Reviews PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CourseId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Reviews_Courses REFERENCES Courses(Id),
    UserId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Reviews_Users REFERENCES Users(Id),
    Rating          TINYINT NOT NULL CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5),
    Comment         NVARCHAR(2000) NULL,
    IsHidden        BIT NOT NULL DEFAULT 0,
    InstructorReply NVARCHAR(2000) NULL,
    RepliedAt       DATETIME2 NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2 NULL
);
CREATE UNIQUE INDEX UX_Reviews_Course_User ON Reviews(CourseId, UserId);
CREATE INDEX IX_Reviews_CourseId_CreatedAt ON Reviews(CourseId, CreatedAt);

CREATE TABLE Notifications (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Notifications_Users REFERENCES Users(Id) ON DELETE CASCADE,
    Title     NVARCHAR(200) NOT NULL,
    Message   NVARCHAR(1000) NOT NULL,
    Type      NVARCHAR(20) NOT NULL CONSTRAINT CK_Notifications_Type CHECK (Type IN (N'System', N'Order', N'Course', N'Certificate', N'Review')),
    Link      NVARCHAR(500) NULL,
    IsRead    BIT NOT NULL DEFAULT 0,
    ReadAt    DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Notifications_User_IsRead_CreatedAt ON Notifications(UserId, IsRead, CreatedAt);
GO

/* ---------- Dữ liệu vai trò ---------- */
INSERT INTO Roles (Id, Name, NormalizedName, ConcurrencyStamp, Description) VALUES
 ('A1B2C3D4-0000-0000-0000-000000000001', N'Admin',      N'ADMIN',      NEWID(), N'Quản trị hệ thống'),
 ('A1B2C3D4-0000-0000-0000-000000000002', N'Instructor', N'INSTRUCTOR', NEWID(), N'Giảng viên'),
 ('A1B2C3D4-0000-0000-0000-000000000003', N'Student',    N'STUDENT',    NEWID(), N'Học viên');
GO
