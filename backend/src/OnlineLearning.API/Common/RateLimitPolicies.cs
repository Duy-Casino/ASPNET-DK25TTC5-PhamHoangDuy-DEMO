namespace OnlineLearning.API.Common;

public static class RateLimitPolicies
{
    /// <summary>Đăng nhập/đăng ký/quên mật khẩu: 10 yêu cầu / phút / IP.</summary>
    public const string Auth = "auth";

    /// <summary>Tạo giao dịch và IPN: 60 yêu cầu / phút / IP.</summary>
    public const string Payment = "payment";
}
