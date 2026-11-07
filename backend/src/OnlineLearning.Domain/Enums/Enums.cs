namespace OnlineLearning.Domain.Enums;

// Tất cả enum được lưu dạng chuỗi (nvarchar) trong SQL Server — xem AppDbContext.ConfigureConventions.

public enum CourseLevel { Beginner, Intermediate, Advanced }

public enum CourseStatus { Draft, Pending, Published, Rejected }

public enum LessonType { Video, Article }

public enum EnrollmentStatus { Active, Completed, Revoked }

public enum OrderStatus { Pending, Paid, Cancelled, Expired, Refunded }

public enum PaymentProvider { VNPay, MoMo, Free, Test }

public enum PaymentStatus { Pending, Success, Failed }

public enum DiscountType { Percentage, FixedAmount }

public enum QuestionType { SingleChoice, MultipleChoice, TrueFalse }

public enum NotificationType { System, Order, Course, Certificate, Review, Message }

/// <summary>Loại mục trong hồ sơ giảng viên.</summary>
public enum ExperienceType { Work, Education, Certification }
