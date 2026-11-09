# 06 — Backend API (Giai đoạn 2)

Swagger: `https://localhost:7080/swagger` (Development). Mọi lỗi trả về `ProblemDetails` (`title` là thông báo tiếng Việt, `code` là mã lỗi, `errors` cho lỗi validation). Enum trả về dạng chuỗi (`"Published"`, `"Paid"`…).

## 1. Cấu trúc mã nguồn

```
src/OnlineLearning.Application/
  Common/        Result, PagedQuery, Options, Helpers (Slug, mã đơn, lọc HTML), Interfaces
  Features/
    Auth/            AuthService (đăng ký, đăng nhập, refresh xoay vòng, quên/đặt lại mật khẩu, hồ sơ)
    Categories/      CategoryService (cây 2 cấp)
    Courses/         CatalogService (công khai), InstructorCourseService, CourseModerationService
    Orders/          CartService, PricingService, OrderService, OrderFulfillmentService, CouponService, ReviewService
    Notifications/   NotificationService
    Admin/           AdminService (tổng quan, người dùng, vai trò)
src/OnlineLearning.Infrastructure/
  Identity/      JwtTokenService, VietnameseIdentityErrorDescriber
  Email/         SmtpEmailSender (không cấu hình Host → chỉ ghi log)
  BackgroundJobs/OrderExpiryService (5 phút/lần chuyển đơn quá hạn sang Expired)
  Persistence/   AppDbContext, Configurations, Migrations/InitialCreate, Seed/
src/OnlineLearning.API/
  Common/        ApiControllerBase (Result → HTTP), CurrentUser, ValidationFilter, GlobalExceptionHandler
  Controllers/   Auth, Account, Home, Categories, Courses, Instructors, Instructor, Cart, Orders, Notifications, Admin
tests/OnlineLearning.IntegrationTests/  16 kịch bản end-to-end trên SQLite in-memory
```

## 2. Danh sách endpoint

### Công khai
| Method | Đường dẫn | Mô tả |
| --- | --- | --- |
| GET | `/api/home` | Nổi bật, mới nhất, bán chạy, danh mục, giảng viên nổi bật, đánh giá tiêu biểu, thống kê |
| GET | `/api/categories` · `/api/categories/{slug}` | Cây danh mục |
| GET | `/api/courses?keyword=&category=&level=&minPrice=&maxPrice=&minRating=&free=&sort=&page=&pageSize=` | Tìm kiếm; `sort`: `newest`, `popular`, `rating`, `price_asc`, `price_desc` |
| GET | `/api/courses/{slug}` | Chi tiết + đề cương (chỉ lộ URL video của bài preview), `isEnrolled`, `isInCart`, phân bố sao |
| GET | `/api/courses/{courseId}/reviews` | Đánh giá (phân trang) |
| GET | `/api/instructors/featured` · `/api/instructors/{id}` | Giảng viên |

### Tài khoản
| Method | Đường dẫn | Ghi chú |
| --- | --- | --- |
| POST | `/api/auth/register` · `/api/auth/login` | Trả `accessToken` (15 phút) + đặt cookie `eduviet_rt` HttpOnly (7 ngày) |
| POST | `/api/auth/refresh` | Xoay vòng refresh token; dùng lại token cũ → thu hồi mọi phiên |
| POST | `/api/auth/logout` | Thu hồi refresh token |
| POST | `/api/auth/forgot-password` · `/api/auth/reset-password` · `/api/auth/confirm-email` | Luôn 204 với forgot-password |
| GET | `/api/auth/me` | Người dùng hiện tại |
| GET/PUT | `/api/account/profile` | Hồ sơ |
| POST | `/api/account/change-password` | Đăng xuất các thiết bị khác |

### Học viên (cần đăng nhập)
| Method | Đường dẫn | Ghi chú |
| --- | --- | --- |
| GET/DELETE | `/api/cart` | Giỏ hàng (tự loại khóa đã mua/ngừng bán) |
| POST/DELETE | `/api/cart/items` · `/api/cart/items/{courseId}` | |
| POST | `/api/orders/quote` | `{ courseIds, couponCode }` → giá do server tính |
| POST | `/api/orders` | `{ courseIds | fromCart, couponCode, note }` — **không có trường giá**. Tổng 0đ → Paid ngay |
| GET | `/api/orders` · `/api/orders/{id}` | Lịch sử mua hàng |
| POST | `/api/orders/{id}/cancel` | Hủy đơn Pending |
| GET/POST/PUT/DELETE | `/api/courses/{courseId}/reviews[/me]` | Đánh giá (cần sở hữu khóa học) |
| GET/POST | `/api/notifications`, `/unread-count`, `/{id}/read`, `/read-all` | Thông báo |

### Giảng viên (`Instructor` hoặc `Admin`)
| Method | Đường dẫn |
| --- | --- |
| GET/POST | `/api/instructor/courses` |
| GET/PUT/DELETE | `/api/instructor/courses/{id}` |
| POST | `/api/instructor/courses/{id}/submit` · `/unpublish` |
| POST · PUT | `/api/instructor/courses/{courseId}/sections` · `/sections/order` |
| PUT/DELETE | `/api/instructor/sections/{id}` |
| POST · PUT | `/api/instructor/sections/{id}/lessons` · `/lessons/order` |
| PUT/DELETE | `/api/instructor/lessons/{id}` |
| POST · DELETE | `/api/instructor/lessons/{id}/resources` · `/api/instructor/resources/{id}` |
| GET · POST | `/api/instructor/reviews` · `/api/instructor/reviews/{id}/reply` |

### Admin
| Method | Đường dẫn |
| --- | --- |
| GET | `/api/admin/overview` (doanh thu, đơn, người dùng 12 tháng) |
| GET · PUT · PUT | `/api/admin/users` · `/users/{id}/roles` · `/users/{id}/active` |
| GET · POST | `/api/admin/courses?status=Pending` · `/courses/{id}/approve` · `/reject` · `/unpublish` |
| PUT | `/api/admin/courses/{id}/featured` |
| GET/POST/PUT/DELETE | `/api/admin/categories[/{id}]` |
| GET/POST/PUT/DELETE | `/api/admin/coupons[/{id}]` |
| GET | `/api/admin/orders` · `/api/admin/orders/{id}` |
| PUT | `/api/admin/reviews/{id}/hidden` |

## 3. Quy tắc nghiệp vụ đã thực thi trong code

| Quy tắc | Nơi thực thi |
| --- | --- |
| BR-01 giá thực = giá KM nếu nhỏ hơn giá gốc | `Course.EffectivePrice`, `CatalogService.ToSummary` |
| BR-02 không mua khóa đã sở hữu / của chính mình / chưa xuất bản | `PricingService.CalculateAsync`, `CartService.AddAsync` |
| BR-03 không ghi danh trùng | Unique `(UserId, CourseId)` + kiểm tra trong `OrderFulfillmentService` |
| BR-04 giá ảnh chụp | `OrderItem.Price/FinalPrice` |
| BR-05 coupon %/cố định, trần, tối thiểu, phạm vi khóa, lượt/người | `Coupon.CalculateDiscount`, `PricingService` (phân bổ giảm giá theo tỷ lệ) |
| BR-06/07 idempotent, khớp số tiền | `OrderFulfillmentService.MarkPaidAsync` (transaction; đơn đã Paid → bỏ qua; sai số tiền → từ chối) |
| BR-09 đánh giá cần sở hữu, 1 lần | `ReviewService.CreateAsync` + unique index |
| BR-11 xóa mềm khi có lịch sử | `InstructorCourseService.DeleteAsync` |
| Nội dung chỉ sửa khi Draft/Rejected | `Course.IsEditable` |
| XSS | `ContentSanitizer` (HtmlSanitizer) cho mô tả khóa học và bài viết |
| Lượt dùng coupon chỉ tính khi đơn **Paid** | Đơn hết hạn/hủy không tốn lượt |

`MarkPaidAsync` là điểm duy nhất cấp quyền học. Giai đoạn 5 (VNPay/MoMo) chỉ gọi hàm này sau khi xác thực chữ ký IPN.

## 4. Kiểm thử

```powershell
cd src\backend
dotnet test
```

16 kịch bản chạy toàn bộ pipeline thật (controller → service → EF Core) trên SQLite in-memory với dữ liệu mẫu:
trang chủ, tìm kiếm/lọc/sắp xếp, ẩn video không preview, ẩn khóa Pending; đăng ký, refresh xoay vòng, phát hiện tái sử dụng token,
đăng xuất, sai mật khẩu, validation, đổi mật khẩu, phân quyền 401/403; giỏ hàng, báo giá, chống sửa giá, mã giảm giá, đơn 0đ cấp quyền,
chặn mua trùng, đánh giá một lần; giảng viên tạo khóa → gửi duyệt → bị từ chối → duyệt, khóa chỉnh sửa khi Pending, cô lập giữa giảng viên;
admin tổng quan, vai trò, khóa tài khoản, mã giảm giá, danh mục.

## 5. Dữ liệu mẫu

| Loại | Nội dung |
| --- | --- |
| Tài khoản | `admin@eduviet.local`; giảng viên `giangvien@`, `lethu.design@`, `phamquang.data@`; học viên `hocvien@`, `hoangnam@`, `ngoclan@`, `minhtuan@`, `baoanh@`, `ducthinh@` (đuôi `@eduviet.local`), mật khẩu = `Seed:DemoPassword` |
| Khóa học | 10 khóa Published (1 khóa miễn phí) + 1 khóa Pending để demo kiểm duyệt; ảnh từ picsum.photos, video mẫu công khai |
| Đơn hàng | ~40 đơn Paid rải 10 tháng (có dữ liệu cho biểu đồ doanh thu), kèm đánh giá và phản hồi |
| Mã giảm giá | `CHAOBANMOI` (10%, tối đa 100k), `GIAM50K` (đơn từ 300k), `MIENPHI100` (100% — demo đơn 0đ) |

Ảnh, video và tài liệu mẫu là placeholder; sẽ thay bằng Cloudinary ở Giai đoạn 5.
