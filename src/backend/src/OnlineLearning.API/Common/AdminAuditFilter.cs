using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;

namespace OnlineLearning.API.Common;

/// <summary>
/// Ghi nhật ký mọi thao tác thay đổi dữ liệu trong khu vực /api/admin (POST/PUT/PATCH/DELETE), kể cả thao tác thất bại:
/// ai làm, làm gì, dữ liệu gửi lên, kết quả. Tự áp dụng cho endpoint Admin mới mà không cần sửa từng service.
/// </summary>
public sealed class AdminAuditFilter(ILogger<AdminAuditFilter> logger) : IAsyncActionFilter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All) };

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin.SetRoles"] = "Đổi vai trò người dùng", ["Admin.SetActive"] = "Khóa/mở khóa tài khoản", ["Admin.SetInstructorVerified"] = "Xác minh giảng viên",
        ["Admin.Approve"] = "Duyệt khóa học", ["Admin.Reject"] = "Từ chối khóa học", ["Admin.Unpublish"] = "Gỡ xuất bản khóa học", ["Admin.SetFeatured"] = "Đặt khóa học nổi bật",
        ["Admin.CreateCategory"] = "Tạo danh mục", ["Admin.UpdateCategory"] = "Sửa danh mục", ["Admin.DeleteCategory"] = "Xóa danh mục",
        ["Admin.CreateCoupon"] = "Tạo mã giảm giá", ["Admin.UpdateCoupon"] = "Sửa mã giảm giá", ["Admin.DeleteCoupon"] = "Xóa mã giảm giá",
        ["Admin.SetReviewHidden"] = "Ẩn/hiện đánh giá",
        ["AdminRewards.Refund"] = "Hoàn tiền đơn hàng", ["AdminRewards.Adjust"] = "Điều chỉnh điểm thưởng", ["AdminRewards.CreateRule"] = "Tạo quy tắc điểm",
        ["AdminRewards.UpdateRule"] = "Sửa quy tắc điểm", ["AdminRewards.DeleteRule"] = "Xóa quy tắc điểm",
        ["AdminRevenue.MarkPaid"] = "Xác nhận đã chi trả giảng viên", ["AdminRevenue.RejectPayout"] = "Từ chối yêu cầu rút tiền",
        ["AdminRevenue.Approve"] = "Duyệt yêu cầu hoàn tiền", ["AdminRevenue.Reject"] = "Từ chối yêu cầu hoàn tiền",
        ["AdminCertificates.Revoke"] = "Thu hồi chứng chỉ", ["AdminCertificates.Restore"] = "Khôi phục chứng chỉ", ["AdminCertificates.Backfill"] = "Cấp bù chứng chỉ",
        ["AdminBundles.Create"] = "Tạo combo", ["AdminBundles.Update"] = "Sửa combo", ["AdminBundles.Delete"] = "Xóa combo",
        ["AdminLearning.GrantAttempts"] = "Cấp thêm lượt kiểm tra", ["AdminLearning.SetLesson"] = "Sửa hoàn thành bài học", ["AdminLearning.Reset"] = "Đặt lại tiến độ",
        ["AdminLearning.Recalculate"] = "Tính lại tiến độ", ["AdminLearning.Revoke"] = "Thu hồi quyền học", ["AdminLearning.Restore"] = "Khôi phục quyền học",
        ["AdminLearning.ManualEnroll"] = "Cấp quyền học thủ công", ["AdminLearning.Respond"] = "Phản hồi yêu cầu hỗ trợ",
        ["AdminTools.ApproveApplication"] = "Duyệt hồ sơ giảng viên", ["AdminTools.RejectApplication"] = "Từ chối hồ sơ giảng viên",
        ["AdminTools.SendAnnouncement"] = "Gửi thông báo hàng loạt", ["AdminTools.SaveSettings"] = "Lưu cài đặt hệ thống", ["AdminTools.RunJob"] = "Chạy công việc nền",
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var method = http.Request.Method;
        var isWrite = !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method);
        var isAdminArea = http.Request.Path.StartsWithSegments("/api/admin");
        if (!isWrite || !isAdminArea || http.User.Identity?.IsAuthenticated != true || !http.User.IsInRole(Roles.Admin))
        {
            await next();
            return;
        }

        var executed = await next();
        try
        {
            var status = executed.Exception is not null && !executed.ExceptionHandled ? 500
                : executed.Result switch
                {
                    ObjectResult o => o.StatusCode ?? 200,
                    StatusCodeResult s => s.StatusCode,
                    FileResult => 200,
                    _ => http.Response.StatusCode,
                };
            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var action = descriptor is null ? method : $"{descriptor.ControllerName}.{descriptor.ActionName}";
            var args = context.ActionArguments.Where(a => a.Value is not CancellationToken).ToDictionary(a => a.Key, a => a.Value);
            var payload = args.Count == 0 ? null : JsonSerializer.Serialize(args, Json);
            if (payload?.Length > 2000) payload = payload[..2000];

            var db = http.RequestServices.GetRequiredService<IAppDbContext>();
            db.ChangeTracker.Clear();   // không lưu kèm thay đổi dở dang của thao tác thất bại
            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                ActorId = Guid.TryParse(http.User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty,
                ActorName = http.User.FindFirst("name")?.Value ?? http.User.FindFirst("email")?.Value ?? "Admin",
                Action = action, Summary = Labels.GetValueOrDefault(action, $"{method} {http.Request.Path}"),
                HttpMethod = method, Path = http.Request.Path.Value?[..Math.Min(300, http.Request.Path.Value.Length)] ?? "",
                Payload = payload, StatusCode = status, Succeeded = status is >= 200 and < 300,
                IpAddress = http.Connection.RemoteIpAddress?.ToString(), CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(http.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không ghi được nhật ký Admin cho {Path}", http.Request.Path);
        }
    }
}
