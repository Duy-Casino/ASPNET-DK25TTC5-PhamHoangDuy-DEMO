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

public enum NotificationType { System, Order, Course, Certificate, Review, Message, Points }

/// <summary>Loại giao dịch điểm.</summary>
/// <summary>Restore: hoàn lại điểm đã dùng khi thanh toán (đơn bị hủy, hết hạn hoặc hoàn tiền).</summary>
public enum PointTransactionType { Earn, Spend, Revoke, Adjust, Restore }

public enum ShareChannel { Facebook, Zalo, CopyLink, Other }

/// <summary>Loại mục trong hồ sơ giảng viên.</summary>
public enum ExperienceType { Work, Education, Certification }

/// <summary>Dòng sổ thu nhập giảng viên: bán khóa học (+) hoặc hoàn tiền (−).</summary>
public enum EarningType { Sale, Refund }

/// <summary>Trạng thái yêu cầu rút tiền của giảng viên.</summary>
public enum PayoutStatus { Requested, Paid, Rejected, Cancelled }

/// <summary>Trạng thái yêu cầu hoàn tiền của học viên.</summary>
public enum RefundRequestStatus { Pending, Approved, Rejected }

/// <summary>Trạng thái quà tặng: chờ thanh toán → đã gửi → đã nhận; hủy khi đơn bị hủy/hoàn tiền trước khi nhận.</summary>
public enum GiftStatus { Pending, Sent, Redeemed, Cancelled }

/// <summary>Loại yêu cầu hỗ trợ học tập.</summary>
public enum SupportRequestType { QuizRetake, VideoIssue, ProgressIssue, CertificateIssue, Other }

public enum SupportRequestStatus { Open, Resolved, Rejected }

public enum InstructorApplicationStatus { Pending, Approved, Rejected }

/// <summary>Nhóm nhận thông báo hàng loạt.</summary>
public enum AnnouncementAudience { Everyone, Students, Instructors, CourseStudents }
