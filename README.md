# EduViet — Website bán khóa học trực tuyến

ASP.NET Core 10 Web API · EF Core · SQL Server · ASP.NET Core Identity + JWT · React + TypeScript + Vite + Tailwind CSS v4 · VNPay/MoMo Sandbox · Cloudinary · QuestPDF · Playwright.

Nền tảng bán và học khóa học trực tuyến: 4 vai trò (khách, học viên, giảng viên, admin), giỏ hàng + mã giảm giá, thanh toán VNPay/MoMo
(kèm kênh thử nghiệm không thu tiền), trình học video có lưu tiến độ, bài kiểm tra trắc nghiệm, chứng chỉ PDF có mã QR và trang xác minh công khai,
dashboard có biểu đồ cho cả 3 vai trò.

## Bắt đầu nhanh (không cần SQL Server)

```powershell
# Yêu cầu: .NET 10 SDK, Node.js 22+
cd backend/src/OnlineLearning.API
dotnet user-secrets set "Jwt:SecretKey" "<chuỗi ngẫu nhiên ≥ 32 ký tự>"
dotnet user-secrets set "Seed:DemoPassword" "<mật khẩu demo, có hoa/thường/số>"
dotnet run --launch-profile demo-sqlite          # API: http://localhost:5080

cd ../../../frontend/online-learning-client
npm install
npm run dev                                      # Web: http://localhost:5173
```

Đăng nhập `admin@eduviet.local`, `giangvien@eduviet.local` hoặc `hocvien@eduviet.local` với mật khẩu demo vừa đặt.
Chạy với SQL Server, chạy test và các chi tiết khác: xem [docs/04](docs/04-cai-dat-moi-truong.md). Triển khai thật: [docs/11](docs/11-trien-khai.md).

## Cấu trúc

```
backend/    ASP.NET Core 10 — Domain, Application, Infrastructure, API (Clean Architecture) + tests/
frontend/   online-learning-client — React 19 + TypeScript + Vite + Tailwind CSS v4 + e2e/ (Playwright)
database/   schema.sql + script migration
docs/       Phân tích, thiết kế, hướng dẫn, báo cáo kiểm thử
docker-compose.yml, .env.example, .github/workflows/ci.yml
```

## Tài liệu

| # | Tài liệu |
| --- | --- |
| 01 | [Phân tích yêu cầu, quy tắc nghiệp vụ, Use Case](docs/01-phan-tich-yeu-cau.md) |
| 02 | [Kiến trúc hệ thống & bảo mật](docs/02-kien-truc.md) |
| 03 | [Thiết kế database & ERD](docs/03-thiet-ke-database.md) · [schema.sql](database/schema.sql) |
| 04 | [Cài đặt môi trường & chạy dự án](docs/04-cai-dat-moi-truong.md) |
| 05 | [Design system](docs/05-design-system.md) |
| 06 | [Backend API, quy tắc nghiệp vụ trong code, kiểm thử, dữ liệu mẫu](docs/06-backend-api.md) |
| 07 | [Frontend: chạy, các trang, xác thực phía client, thiết kế](docs/07-frontend.md) |
| 08 | [Học trực tuyến, bài kiểm tra, dashboard](docs/08-hoc-tap-va-dashboard.md) |
| 09 | [Thanh toán VNPay/MoMo/kênh test, lưu trữ Cloudinary](docs/09-thanh-toan-va-luu-tru.md) |
| 10 | [Chứng chỉ PDF & trang xác minh](docs/10-chung-chi.md) |
| 11 | [Triển khai: Docker Compose, IIS, Azure + checklist production](docs/11-trien-khai.md) |
| 12 | [Báo cáo kiểm thử](docs/12-bao-cao-kiem-thu.md) |

## Lộ trình

| Giai đoạn | Nội dung | Trạng thái |
| --- | --- | --- |
| 1 | Phân tích, kiến trúc, database, cấu trúc Solution, môi trường | ✅ Hoàn tất |
| 2 | Backend: Auth/JWT/refresh, Courses, Categories, Cart, Orders, Coupons, Reviews, Admin API, seed khóa học mẫu | ✅ Build sạch, 16/16 test tích hợp đạt (chưa chạy trên SQL Server thật) |
| 3 | Frontend: layout, trang chủ, danh sách/chi tiết khóa học, auth, giỏ hàng, thanh toán, đơn hàng, hồ sơ | ✅ Đã chạy thử end-to-end với API demo |
| 4 | Học trực tuyến, tiến độ, Quiz, Dashboard 3 vai trò + biểu đồ | ✅ 20/20 test backend, đã chạy thử trên trình duyệt |
| 5 | Thanh toán VNPay/MoMo Sandbox (IPN, idempotency), kênh test, Cloudinary | ✅ 28/28 test backend, đã chạy thử kênh test và tải ảnh trên trình duyệt |
| 6 | Chứng chỉ PDF + trang xác minh | ✅ 30/30 test backend, đã cấp và xác minh chứng chỉ trên trình duyệt |
| 7 | Kiểm thử (xUnit, Playwright E2E), hướng dẫn triển khai production | ✅ 30/30 test backend, 18/18 E2E; Docker/CI đã viết, chưa chạy |

## Checklist Giai đoạn 1

**Tài liệu**
- [x] Đặc tả 4 vai trò, 8 nhóm chức năng có mã FR, 12 quy tắc nghiệp vụ BR
- [x] Use Case Diagram, sequence thanh toán, state khóa học, flow cấp chứng chỉ
- [x] ERD 23 bảng nghiệp vụ + bảng Identity + RefreshTokens, từ điển dữ liệu
- [x] Script SQL Server có PK, FK, unique, index, check constraint

**Mã nguồn**
- [x] Solution 4 project Clean Architecture (`Domain`, `Application`, `Infrastructure`, `API`)
- [x] Domain entities + enums, AppDbContext + Fluent API (cascade chỉ trong aggregate, unique chống mua trùng / xử lý IPN lặp)
- [x] Seeder: vai trò, 3 tài khoản demo (mật khẩu từ User Secrets), 8 danh mục
- [x] `/api/health` kiểm tra kết nối DB, Swagger
- [x] Frontend Vite + React + TS + Tailwind v4, cấu trúc thư mục yêu cầu, design tokens, Vite proxy `/api`
- [x] `npm run build` thành công

**Bạn cần kiểm tra trên máy** (máy hiện tại chưa có .NET SDK và SQL Server nên chưa chạy được)
- [ ] Cài .NET 10 SDK, SQL Server, `dotnet-ef` (xem tài liệu 04)
- [ ] `dotnet build` không lỗi
- [ ] `dotnet ef migrations add InitialCreate` + `dotnet ef database update` tạo đủ bảng trong `OnlineLearningDb`
- [ ] `dotnet run` → Swagger mở được, `/api/health` trả `status: ok`
- [ ] Bảng `Roles` có 3 vai trò, `Users` có 3 tài khoản demo, `Categories` có 8 danh mục
- [ ] `npm run dev` → ô "Kết nối Backend" báo sẵn sàng

## Checklist Giai đoạn 2

**Đã hoàn thành và kiểm chứng trên máy phát triển**
- [x] Auth: đăng ký, đăng nhập, JWT 15 phút, refresh token xoay vòng trong cookie HttpOnly (lưu hash SHA-256), phát hiện tái sử dụng, đăng xuất, quên/đặt lại mật khẩu, xác nhận email, hồ sơ, đổi mật khẩu, khóa sau 5 lần sai
- [x] Danh mục 2 cấp; tìm kiếm/lọc/sắp xếp/phân trang khóa học; chi tiết khóa học chỉ lộ video preview
- [x] Giảng viên: CRUD khóa học, chương, bài học, tài liệu, sắp xếp; gửi duyệt; khóa chỉnh sửa khi Pending/Published; xóa mềm
- [x] Admin: duyệt/từ chối/gỡ khóa học, nổi bật, danh mục, mã giảm giá, đơn hàng, người dùng/vai trò/khóa tài khoản, tổng quan 12 tháng
- [x] Giỏ hàng, báo giá, đơn hàng; giá tính hoàn toàn ở server; coupon; đơn 0đ cấp quyền ngay; chặn mua trùng; đơn quá hạn tự Expired
- [x] Đánh giá (chỉ người đã mua, 1 lần), giảng viên phản hồi, admin ẩn; thông báo
- [x] Rate limit đăng nhập, ProblemDetails tiếng Việt, header bảo mật, lọc HTML chống XSS
- [x] Migration `InitialCreate` (29 bảng) + script SQL; dữ liệu mẫu 10 khóa học, ~40 đơn hàng, đánh giá, 3 mã giảm giá
- [x] `dotnet build` 0 lỗi 0 cảnh báo; `dotnet test` 16/16 đạt; Swagger sinh 76 endpoint

**Bạn cần kiểm tra trên máy có SQL Server**
- [ ] `dotnet ef database update` thành công
- [ ] `dotnet run` → seed chạy, Swagger gọi được `/api/home`, đăng nhập `admin@eduviet.local`
- [ ] Dùng nút Authorize trong Swagger với `accessToken` để thử API Admin/Giảng viên

Khi các mục trên đạt, chuyển sang **Giai đoạn 3 — Frontend**.

## Checklist Giai đoạn 3

**Đã hoàn thành và kiểm chứng trên trình duyệt (API demo SQLite + dữ liệu mẫu)**
- [x] Trang chủ: hero chứng chỉ có tương tác (gõ tên → in lên chứng chỉ + mã xác minh), lĩnh vực, nổi bật, bán chạy, phiếu thống kê, mới xuất bản, giảng viên, đánh giá, lời mời đăng ký
- [x] Danh sách khóa học: tìm kiếm, lọc lĩnh vực/cấp độ/giá/đánh giá, sắp xếp, phân trang, empty state; bộ lọc dạng sheet trên điện thoại
- [x] Chi tiết khóa học: thẻ mua hàng dính, xem thử video, đề cương, đánh giá + phân bố sao, viết đánh giá khi đã sở hữu
- [x] Đăng ký (validation client + server), đăng nhập, quay lại trang trước, quên/đặt lại mật khẩu, xác nhận email, khôi phục phiên sau khi tải lại trang
- [x] Giỏ hàng, thanh toán với báo giá từ server, mã giảm giá sai/đúng, đơn 0đ hoàn tất, đơn chờ thanh toán có đếm ngược, hủy đơn, lịch sử mua hàng
- [x] Hồ sơ, đổi mật khẩu; trang 404; loading skeleton, lỗi + thử lại, toast
- [x] `npm run build` thành công; taste-skill inspect trang chủ 0 FAIL ở 1440/768/390px; backend vẫn 16/16 test
- [x] Lỗi phát hiện và đã sửa khi chạy thử: giờ hiển thị lệch 7 tiếng (API trả DateTime thiếu "Z"), tiền "249,000đ" trong thông báo mã giảm giá, trang thanh toán tự quay về giỏ sau khi đặt hàng, thông báo mã sai bị xóa ngay

**Để lại cho giai đoạn sau**
- [ ] Trình học, tiến độ, quiz, dashboard (Giai đoạn 4) — hiện trang khóa học chỉ báo "Bạn đã sở hữu khóa học này"
- [ ] Nút "Thanh toán qua VNPay" đang tắt (Giai đoạn 5); tải ảnh đại diện lên Cloudinary (Giai đoạn 5)
- [ ] Trang xác minh chứng chỉ (Giai đoạn 6)

## Checklist Giai đoạn 4

**Đã hoàn thành và kiểm chứng (20/20 test backend + chạy thử trên trình duyệt với API demo)**
- [x] Trình học: tiếp tục bài gần nhất, video nhớ vị trí và tự hoàn thành khi xem hết, đánh dấu hoàn thành, tài liệu, bài trước/sau, đề cương có trạng thái, giao diện điện thoại
- [x] Bảo vệ nội dung: bài chưa mua trả 403 (trừ bài học thử); giảng viên/admin ở chế độ xem trước
- [x] Tiến độ theo bài bắt buộc; khóa học "Hoàn thành" khi đủ bài + đạt bài kiểm tra bắt buộc (sẵn sàng cho cấp chứng chỉ ở Giai đoạn 6)
- [x] Bài kiểm tra: không lộ đáp án trước khi nộp, chấm ở server, giới hạn lượt, giới hạn thời gian + tự nộp, nộp lặp không chấm lại, đáp án chi tiết + giải thích, lịch sử
- [x] Giảng viên soạn quiz (một/nhiều đáp án, đúng-sai), khóa câu hỏi khi đã có người làm
- [x] Dashboard học viên: số liệu, tiếp tục học, khóa học theo trạng thái, kết quả kiểm tra, chứng chỉ, lịch sử thanh toán
- [x] Dashboard giảng viên: doanh thu/lượt đăng ký 12 tháng, thống kê theo khóa, trang soạn khóa học (thông tin, chương, bài, tài liệu, sắp xếp, bài kiểm tra, gửi duyệt), học viên, trả lời đánh giá
- [x] Dashboard admin: tổng quan + biểu đồ, duyệt/từ chối/gỡ/nổi bật khóa học, phân quyền, khóa tài khoản, danh mục, đơn hàng, mã giảm giá

**Để lại cho giai đoạn sau**
- [ ] VNPay/MoMo Sandbox, tải ảnh/video/tài liệu lên Cloudinary, URL video có chữ ký (Giai đoạn 5)
- [ ] Sinh chứng chỉ PDF + trang xác minh công khai (Giai đoạn 6)
- [ ] Test E2E Playwright, hướng dẫn triển khai production (Giai đoạn 7)

## Checklist Giai đoạn 5

**Đã hoàn thành và kiểm chứng**
- [x] Cổng VNPay (HMAC-SHA512) và MoMo (HMAC-SHA256) sandbox: tạo giao dịch, IPN, trang kết quả
- [x] Kênh thanh toán thử nghiệm: tự động thành công qua IPN có chữ ký, hiện thông báo "Hiện tại đang trong giai đoạn test kênh thanh toán, chưa thu tiền thật"
- [x] Xác thực chữ ký + số tiền, idempotency, không cấp quyền qua redirect, xử lý thất bại và thanh toán hai lần
- [x] Chọn phương thức khi thanh toán, thanh toán lại đơn chờ, trang kết quả chờ IPN, Admin tra cứu giao dịch
- [x] Tải ảnh / video / tài liệu (Cloudinary hoặc lưu cục bộ), kiểm tra định dạng + dung lượng + magic bytes
- [x] Video và tài liệu trả phí phát qua URL có chữ ký (hết hạn khi lưu cục bộ), chỉ cấp cho người có quyền
- [x] Sửa: VNPay yêu cầu đúng tên trường `RspCode` (ASP.NET mặc định trả `rspCode`); SMTP mặc định để trống để không lỗi khi chưa cấu hình

**Bạn cần làm khi dùng cổng thật**
- [ ] Đăng ký sandbox VNPay/MoMo, đặt khóa bằng User Secrets, mở ngrok cho IPN (xem tài liệu 09)
- [ ] Đặt khóa Cloudinary nếu muốn lưu trên CDN
- [ ] Tắt kênh thử nghiệm ở production

## Checklist Giai đoạn 6

**Đã hoàn thành và kiểm chứng**
- [x] Tự cấp chứng chỉ khi đủ 100% bài bắt buộc + đạt bài kiểm tra bắt buộc; một chứng chỉ / lượt ghi danh; mã `EDV-YYYY-XXXXXXXX` duy nhất
- [x] PDF (QuestPDF) khổ A4 ngang, đủ dấu tiếng Việt, mã QR + đường dẫn xác minh; dấu "ĐÃ THU HỒI" khi bị thu hồi
- [x] Trang xác minh công khai `/xac-minh/:mã` + tải PDF bản gốc; liên kết trên menu và chân trang
- [x] Học viên: tải PDF, mở trang xác minh, sao chép liên kết; trình học báo nhận chứng chỉ
- [x] Admin: tra cứu, tải, thu hồi có lý do, khôi phục, cấp bù chứng chỉ còn thiếu

**Lưu ý triển khai**
- [ ] Máy chủ Linux cần cài font có dấu tiếng Việt (`fonts-dejavu-core` hoặc `fonts-noto-core`), hoặc đặt `.ttf` vào thư mục `Fonts/`
- [ ] Đặt `App:ClientUrl` đúng tên miền thật để QR trên chứng chỉ trỏ về trang xác minh thật
