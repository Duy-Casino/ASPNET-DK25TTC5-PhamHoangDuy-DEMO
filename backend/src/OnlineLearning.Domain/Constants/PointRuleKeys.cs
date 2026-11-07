namespace OnlineLearning.Domain.Constants;

/// <summary>Khóa các quy tắc điểm hệ thống (gắn với code, Admin chỉ chỉnh số điểm và bật/tắt).</summary>
public static class PointRuleKeys
{
    public const string Purchase = "purchase";
    public const string CourseCompletion = "course_completion";
    public const string DailyCheckIn = "daily_checkin";
    public const string CheckInStreak = "checkin_streak";
    public const string CourseShare = "course_share";
    public const string Referral = "referral";
    public const string RedeemVoucher = "redeem_voucher";

    public static readonly string[] System =
        [Purchase, CourseCompletion, DailyCheckIn, CheckInStreak, CourseShare, Referral, RedeemVoucher];
}
