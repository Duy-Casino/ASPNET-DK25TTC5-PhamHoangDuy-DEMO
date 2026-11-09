# EduViet: Website bán khóa học trực tuyến

## Thông tin sinh viên

| | |
| --- | --- |
| Họ và tên | **Phạm Hoàng Duy** |
| Mã sinh viên | 170125375 |
| Lớp | DK25TTC5 |
| Email | duyph250994@tvu-onschool.edu.vn |
| Số điện thoại | 0327607672 |
| Dự án | Website bán khóa học trực tuyến |
| Repository | <https://github.com/Duy-Casino/ASPNET-DK25TTC5-PhamHoangDuy-DEMO> |

Cập nhật lần cuối: **09/11/2026**. Nhật ký thay đổi xem mục [9](#9-nhật-ký-tiến-độ).

EduViet là website mua và học khóa học trực tuyến, giao diện tiếng Việt, với bốn vai trò: khách, học viên, giảng viên và quản trị viên.

- Người học tìm khóa học, học thử, mua bằng VNPay/MoMo, học video có lưu tiến độ, làm bài kiểm tra, nhận chứng chỉ PDF có mã xác minh và tích điểm thưởng.
- Giảng viên soạn khóa học, quản lý hồ sơ giảng dạy và trả lời câu hỏi của học viên.
- Quản trị viên duyệt khóa học, quản lý người dùng, đơn hàng, mã giảm giá, chứng chỉ và điểm thưởng.

**Công nghệ:** ASP.NET Core 10 Web API · EF Core · SQL Server (SQLite cho chế độ demo) · ASP.NET Core Identity + JWT · React 19 + TypeScript + Vite + Tailwind CSS v4 · VNPay/MoMo Sandbox · Cloudinary · QuestPDF · xUnit · Playwright.

---

## Mục lục

1. [Chức năng đã thực hiện](#1-chức-năng-đã-thực-hiện)
2. [Kiến trúc và công nghệ](#2-kiến-trúc-và-công-nghệ)
3. [Cài đặt và chạy thử](#3-cài-đặt-và-chạy-thử)
4. [Tài khoản và dữ liệu mẫu](#4-tài-khoản-và-dữ-liệu-mẫu)
5. [Hướng dẫn sử dụng nhanh](#5-hướng-dẫn-sử-dụng-nhanh)
6. [Kiểm thử](#6-kiểm-thử)
7. [Cấu trúc thư mục](#7-cấu-trúc-thư-mục)
8. [Tài liệu chi tiết](#8-tài-liệu-chi-tiết)
9. [Nhật ký tiến độ](#9-nhật-ký-tiến-độ)
10. [Hạn chế và việc còn lại](#10-hạn-chế-và-việc-còn-lại)

---

## 1. Chức năng đã thực hiện

| Nhóm | Chức năng |
| --- | --- |
| **Tài khoản** | Đăng ký, đăng nhập, JWT 15 phút + refresh token xoay vòng trong cookie HttpOnly, phát hiện dùng lại token, quên/đặt lại mật khẩu, xác nhận email, hồ sơ, đổi mật khẩu, khóa sau 5 lần sai |
| **Khóa học** | Danh mục 2 cấp; tìm kiếm, lọc theo lĩnh vực, cấp độ, giá, đánh giá; sắp xếp, phân trang; trang chi tiết có đề cương, video học thử, đánh giá và phân bố sao |
| **Mua hàng** | Giỏ hàng, báo giá tính ở server (chống sửa giá), mã giảm giá, đơn 0đ, chặn mua trùng, đơn quá hạn tự hủy, lịch sử đơn hàng |
| **Thanh toán** | VNPay (HMAC-SHA512) và MoMo (HMAC-SHA256) sandbox. Kiểm tra chữ ký và số tiền ở IPN, idempotency, chỉ cấp quyền học qua IPN chứ không qua trang redirect. Có thêm kênh thử nghiệm không thu tiền thật |
| **Học trực tuyến** | Trình học video nhớ vị trí, tự hoàn thành bài, tài liệu đính kèm, tiến độ theo bài bắt buộc; bài chưa mua bị chặn (403) |
| **Bài kiểm tra** | Trắc nghiệm một hoặc nhiều đáp án, đúng/sai; chấm ở server; giới hạn lượt và thời gian; xem đáp án và giải thích sau khi nộp |
| **Chứng chỉ** | Tự cấp khi hoàn thành khóa; PDF A4 có mã QR, mã `EDV-YYYY-XXXXXXXX`; trang xác minh công khai `/xac-minh/:mã`; Admin thu hồi, khôi phục, cấp bù |
| **Giảng viên** | Soạn khóa học, chương, bài, tài liệu, bài kiểm tra; gửi duyệt. Hồ sơ giảng dạy gồm kinh nghiệm, học vấn, chứng nhận, chuyên môn, ảnh đại diện; huy hiệu "Đã xác minh" do Admin cấp; thống kê học viên và đánh giá |
| **Trao đổi** | Học viên nhắn tin hỏi giảng viên về khóa học, trạng thái đã đọc/chưa đọc, thông báo trên chuông |
| **Điểm thưởng** | Tích điểm khi mua khóa (10 điểm/100.000đ), hoàn thành khóa, điểm danh hằng ngày, chuỗi 7 ngày, chia sẻ khóa học, giới thiệu bạn bè. Đổi điểm lấy mã giảm giá cá nhân. Admin cấu hình quy tắc, điều chỉnh điểm có lý do, xem thống kê, xuất CSV. Hoàn tiền thì thu hồi điểm |
| **Dashboard** | Học viên: tiến độ, kết quả, chứng chỉ, thanh toán. Giảng viên: doanh thu, lượt đăng ký 12 tháng. Admin: tổng quan, biểu đồ, duyệt khóa học, phân quyền, đơn hàng, giao dịch |
| **Nội dung & SEO** | Trang Giới thiệu, Liên hệ, chính sách (điều khoản, bảo mật, hoàn tiền); thẻ Open Graph, canonical, JSON-LD (Course, Person); `sitemap.xml`, `robots.txt`; giao diện responsive cho điện thoại |
| **Bảo mật** | Rate limit, ProblemDetails tiếng Việt, header bảo mật, lọc HTML chống XSS, kiểm tra magic bytes khi tải tệp, URL video có chữ ký, phân quyền theo vai trò |

## 2. Kiến trúc và công nghệ

```
React SPA (Vite, cổng 5173) ──/api──> ASP.NET Core Web API (cổng 5080) ──EF Core──> SQL Server / SQLite
                                              │
                                              ├── VNPay / MoMo (thanh toán, IPN)
                                              ├── Cloudinary hoặc lưu cục bộ (ảnh, video, tài liệu)
                                              └── QuestPDF (chứng chỉ PDF)
```

Backend theo **Clean Architecture**, gồm bốn project:

| Project | Vai trò |
| --- | --- |
| `OnlineLearning.Domain` | Entity, enum, hằng số nghiệp vụ. Không phụ thuộc thư viện ngoài |
| `OnlineLearning.Application` | Service nghiệp vụ, DTO, FluentValidation, kiểu `Result<T>` |
| `OnlineLearning.Infrastructure` | EF Core DbContext, cấu hình Fluent API, migration, cổng thanh toán, lưu trữ tệp, email, PDF |
| `OnlineLearning.API` | Controller, xác thực JWT, phân quyền, rate limit, Swagger |

Frontend: React 19 + TypeScript, React Router, Tailwind CSS v4 (design token trong `src/index.css`), Recharts cho biểu đồ.

Chi tiết: [tài liệu 02 Kiến trúc](thesis/html/02-kien-truc.md) · [tài liệu 03 Database](thesis/html/03-thiet-ke-database.md).

## 3. Cài đặt và chạy thử

### 3.1. Yêu cầu

| Công cụ | Phiên bản | Bắt buộc |
| --- | --- | --- |
| .NET SDK | 10.0 | Có |
| Node.js | 22 trở lên | Có |
| SQL Server | 2022 (Developer/Express) hoặc LocalDB | Chỉ khi chạy cách B |
| `dotnet-ef` | 10.x | Chỉ khi chạy cách B |

```powershell
winget install Microsoft.DotNet.SDK.10
winget install OpenJS.NodeJS.LTS
dotnet tool install --global dotnet-ef
```

### Cách nhanh nhất: một lệnh

```powershell
git clone https://github.com/Duy-Casino/ASPNET-DK25TTC5-PhamHoangDuy-DEMO.git
cd ASPNET-DK25TTC5-PhamHoangDuy-DEMO
powershell -ExecutionPolicy Bypass -File setup\run-demo.ps1
```

Script kiểm tra công cụ, hỏi mật khẩu demo ở lần đầu, cài gói npm rồi mở API và web trong hai cửa sổ. Sơ đồ triển khai, cài với SQL Server và xử lý lỗi thường gặp: [setup/README.md](setup/README.md). Các bước 3.2–3.4 dưới đây là cách làm thủ công.

### 3.2. Lấy mã nguồn và cấu hình bí mật (một lần)

```powershell
git clone https://github.com/Duy-Casino/ASPNET-DK25TTC5-PhamHoangDuy-DEMO.git
cd ASPNET-DK25TTC5-PhamHoangDuy-DEMO\src\backend\src\OnlineLearning.API
dotnet user-secrets set "Jwt:SecretKey" "<chuỗi ngẫu nhiên ≥ 32 ký tự>"
dotnet user-secrets set "Seed:DemoPassword" "<mật khẩu demo, ≥ 8 ký tự, có hoa/thường/số>"
```

Bí mật được lưu bằng User Secrets, không nằm trong mã nguồn. Sinh chuỗi ngẫu nhiên trong PowerShell:
`[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))`

### 3.3. Cách A: chế độ demo, không cần SQL Server (khuyên dùng để xem thử)

```powershell
# Terminal 1: API tại http://localhost:5080 (Swagger: /swagger)
cd src\backend\src\OnlineLearning.API
dotnet run --launch-profile demo-sqlite

# Terminal 2: Web tại http://localhost:5173
cd src\frontend\online-learning-client
npm install
npm run dev
```

Lần chạy đầu, API tự tạo `eduviet-demo.db` cùng dữ liệu mẫu. Mở <http://localhost:5173>.

> Chế độ demo tạo DB bằng `EnsureCreated` nên **không tự cập nhật khi schema đổi**. Sau khi kéo code có migration mới, hãy dừng API, xóa hoặc đổi tên `eduviet-demo.db`, rồi chạy lại.

### 3.4. Cách B: SQL Server

1. Sửa `ConnectionStrings:DefaultConnection` (hoặc đặt bằng User Secrets) trỏ tới SQL Server của bạn.
2. Tạo database bằng migration:
   ```powershell
   cd src\backend
   dotnet ef database update -p src\OnlineLearning.Infrastructure -s src\OnlineLearning.API
   ```
   Có thể chạy tay các script trong thư mục `setup/database/` (xem [setup/README.md](setup/README.md#3-cài-đặt-với-sql-server)) thay cho bước này.
3. Chạy API bằng `dotnet run` trong `src\OnlineLearning.API`, rồi chạy frontend như cách A.

Hướng dẫn đầy đủ, kể cả cài .NET không cần quyền Admin: [tài liệu 04](thesis/html/04-cai-dat-moi-truong.md). Triển khai lên máy chủ thật (Docker Compose, IIS, Azure) kèm checklist: [tài liệu 11](thesis/html/11-trien-khai.md).

## 4. Tài khoản và dữ liệu mẫu

Dữ liệu mẫu có 10 khóa học, khoảng 40 đơn hàng, đánh giá, 3 mã giảm giá, 3 giảng viên (ảnh đại diện minh họa, người không có thật) và quy tắc điểm thưởng mặc định.

| Vai trò | Email | Mật khẩu |
| --- | --- | --- |
| Quản trị viên | `admin@eduviet.local` | Giá trị `Seed:DemoPassword` đã đặt ở bước 3.2 |
| Giảng viên | `giangvien@eduviet.local` | như trên |
| Học viên | `hocvien@eduviet.local` | như trên |

Thanh toán thử: ở trang thanh toán, chọn **Kênh thử nghiệm**. Đơn được xác nhận tự động và không thu tiền thật.

## 5. Hướng dẫn sử dụng nhanh

| Vai trò | Đường dẫn | Việc có thể thử |
| --- | --- | --- |
| Khách | `/`, `/khoa-hoc`, `/khoa-hoc/:slug`, `/giang-vien/:id`, `/xac-minh/:mã` | Tìm và lọc khóa học, xem học thử, xem hồ sơ giảng viên, xác minh chứng chỉ |
| Học viên | `/hoc-vien`, `/hoc/:slug`, `/gio-hang`, `/tin-nhan`, `/hoc-vien/diem-thuong` | Mua khóa học, học, làm bài kiểm tra, nhận chứng chỉ, nhắn giảng viên, điểm danh, đổi điểm |
| Giảng viên | `/giang-day`, `/giang-day/ho-so` | Tạo khóa học, gửi duyệt, sửa hồ sơ giảng dạy, trả lời tin nhắn và đánh giá |
| Quản trị viên | `/quan-tri`, `/quan-tri/diem-thuong` | Duyệt khóa học, quản lý người dùng, đơn hàng, hoàn tiền, mã giảm giá, chứng chỉ, điểm thưởng |

Luồng nên thử: đăng ký học viên → thêm khóa vào giỏ → thanh toán qua kênh thử nghiệm → vào học → hoàn thành bài và bài kiểm tra → tải chứng chỉ → mở trang xác minh. Mỗi bước cũng cộng điểm thưởng tương ứng.

## 6. Kiểm thử

| Tầng | Công cụ | Kết quả gần nhất |
| --- | --- | --- |
| Backend tích hợp | xUnit + WebApplicationFactory + SQLite in-memory | **43/43 đạt** |
| End-to-end | Playwright (Chrome), 18 kịch bản desktop + 7 kịch bản điện thoại | **25/25 đạt** |
| Build | `dotnet build`, `npm run build` | Đạt |

```powershell
# Backend (dừng API demo trước để tránh khóa DLL)
cd src\backend
dotnet test

# E2E: tự chạy API trên cổng 5081 và Vite trên cổng 5174, dùng DB mới mỗi lần
cd src\frontend\online-learning-client
npx playwright install chrome
$env:E2E_CHANNEL="chrome"; npx playwright test
```

Danh sách kịch bản và các lỗi phát hiện nhờ kiểm thử: [tài liệu 12](thesis/html/12-bao-cao-kiem-thu.md).

## 7. Cấu trúc thư mục

| Thư mục | Nội dung |
| --- | --- |
| [`setup/`](setup/README.md) | Cài đặt: script `run-demo.ps1`, script SQL Server (`database/`), mô tả dữ liệu thử khớp kết quả báo cáo (`test-data/`), sơ đồ triển khai |
| [`src/`](src/) | Mã nguồn và dữ liệu thử nghiệm: `backend/` (ASP.NET Core, kèm test xUnit), `frontend/` (React, kèm kịch bản E2E) |
| [`progress-report/`](progress-report/README.md) | Báo cáo tiến độ theo tuần (tính từ 05/11/2026), mẫu báo cáo tuần, báo cáo chi tiết theo đợt (`theo-dot/`) |
| [`thesis/`](thesis/README.md) | Tài liệu đồ án: `doc/` (Word), `pdf/`, `html/` (tài liệu kỹ thuật 01–14), `abs/` (slide, video), `refs/` (tài liệu tham khảo) |
| [`soft/`](soft/README.md) | Phần mềm sử dụng và liên kết tải |
| [`docker/`](docker/README.md) | Triển khai Docker: `docker-compose.yml`, `.env.example` |
| `.github/workflows/` | CI: build, test backend, build frontend, chạy E2E |

```
src/
├── backend/
│   ├── OnlineLearning.sln
│   ├── src/OnlineLearning.Domain/          Entity, enum, hằng số
│   ├── src/OnlineLearning.Application/     Service nghiệp vụ theo tính năng (Features/*), DTO, validator
│   ├── src/OnlineLearning.Infrastructure/  DbContext, cấu hình EF, Migrations/, Seed/ (dữ liệu mẫu), thanh toán, lưu trữ, PDF
│   ├── src/OnlineLearning.API/             Controllers/, Program.cs, appsettings, launch profile demo-sqlite
│   └── tests/OnlineLearning.IntegrationTests/
└── frontend/online-learning-client/
    ├── src/pages/        Trang theo vai trò: auth, checkout, learn, student, instructor, admin, messages, info
    ├── src/components/   Component dùng chung (ui, layout, dashboard, rewards…)
    ├── src/services/     Gọi API
    ├── e2e/              Kịch bản Playwright
    └── public/           Ảnh tĩnh, robots.txt, ảnh đại diện giảng viên demo
```

## 8. Tài liệu chi tiết

Bản gộp đọc offline: [thesis/html/tai-lieu-ky-thuat.html](thesis/html/tai-lieu-ky-thuat.html) · [thesis/pdf/tai-lieu-ky-thuat.pdf](thesis/pdf/tai-lieu-ky-thuat.pdf). Báo cáo tiến độ: [progress-report/](progress-report/README.md).

| # | Tài liệu |
| --- | --- |
| 01 | [Phân tích yêu cầu, quy tắc nghiệp vụ, Use Case](thesis/html/01-phan-tich-yeu-cau.md) |
| 02 | [Kiến trúc hệ thống và bảo mật](thesis/html/02-kien-truc.md) |
| 03 | [Thiết kế database, ERD](thesis/html/03-thiet-ke-database.md) · [schema.sql](setup/database/schema.sql) |
| 04 | [Cài đặt môi trường, chạy dự án](thesis/html/04-cai-dat-moi-truong.md) |
| 05 | [Design system (phiên bản đầu)](thesis/html/05-design-system.md) |
| 06 | [Backend API, dữ liệu mẫu](thesis/html/06-backend-api.md) |
| 07 | [Frontend: các trang, xác thực phía client](thesis/html/07-frontend.md) |
| 08 | [Học trực tuyến, bài kiểm tra, dashboard](thesis/html/08-hoc-tap-va-dashboard.md) |
| 09 | [Thanh toán VNPay/MoMo/kênh thử nghiệm, lưu trữ Cloudinary](thesis/html/09-thanh-toan-va-luu-tru.md) |
| 10 | [Chứng chỉ PDF, trang xác minh](thesis/html/10-chung-chi.md) |
| 11 | [Triển khai: Docker Compose, IIS, Azure, checklist production](thesis/html/11-trien-khai.md) |
| 12 | [Báo cáo kiểm thử](thesis/html/12-bao-cao-kiem-thu.md) |
| 13 | [Design system giao diện hiện tại](thesis/html/13-design-system.md) |
| 14 | [Điểm thưởng: quy tắc, thiết kế, API](thesis/html/14-diem-thuong.md) |

## 9. Nhật ký tiến độ

Mỗi khi hoàn thành một tính năng, thêm một dòng vào bảng này và cập nhật mục 1, 6, 8 nếu có thay đổi. Báo cáo theo tuần và theo đợt nằm trong [progress-report/](progress-report/README.md); tuần 1 bắt đầu ngày 05/11/2026.

| Ngày | Nội dung | Kết quả kiểm thử |
| --- | --- | --- |
| 05/11/2026 | Giai đoạn 1–7: phân tích, thiết kế DB, backend (Auth, khóa học, giỏ hàng, đơn hàng, mã giảm giá, đánh giá, Admin), frontend, học trực tuyến, bài kiểm tra, dashboard, thanh toán VNPay/MoMo, chứng chỉ PDF, kiểm thử, hướng dẫn triển khai | 30/30 backend, 18/18 E2E |
| 05/11/2026 | Trang chủ dạng sàn khóa học nền sáng; ẩn nút đăng ký khi đã đăng nhập | 18/18 E2E |
| 06/11/2026 | Thiết kế lại giao diện theo phong cách nền tảng công nghệ, giữ thương hiệu EduViet ([tài liệu 13](thesis/html/13-design-system.md)) | 18/18 E2E |
| 06/11/2026 | Trang chính sách (điều khoản hoàn tiền: 7 ngày, học tối đa 20%), chuông thông báo, SEO (Open Graph, JSON-LD, sitemap), tối ưu ảnh và gói JS | 31/31 backend, 19/19 E2E |
| 07/11/2026 | Tính năng giảng viên: hồ sơ giảng dạy chi tiết, xác minh, nhắn tin với học viên (migration `InstructorProfileAndMessaging`) | 35/35 backend, 23/23 E2E |
| 07/11/2026 | Hình minh họa ba lợi ích trên trang chủ; ảnh đại diện giảng viên demo | 36/36 backend, 23/23 E2E |
| 07/11/2026 | Điểm thưởng: tích điểm, điểm danh, chia sẻ, giới thiệu, đổi điểm, hoàn tiền thu hồi điểm, trang Admin (migration `RewardPoints`, [tài liệu 14](thesis/html/14-diem-thuong.md)) | 43/43 backend, 25/25 E2E |
| 09/11/2026 | Viết lại README theo trạng thái hiện tại của đồ án; thêm thông tin sinh viên | |
| 09/11/2026 | Tổ chức lại repository theo quy định: `setup/`, `src/`, `progress-report/`, `thesis/`, `soft/`, `docker/`; script cài đặt; báo cáo tiến độ 01–04; tài liệu kỹ thuật bản HTML/PDF | 43/43 backend, 25/25 E2E |
| 09/11/2026 | Báo cáo tiến độ theo tuần: lịch báo cáo, báo cáo tuần 01 (05/11–11/11/2026) gồm công việc theo ngày và 10 điều chỉnh trong tuần, mẫu báo cáo tuần | |

## 10. Hạn chế và việc còn lại

**Chưa kiểm thử được trên máy phát triển**
- Chạy trên SQL Server thật. Migration đã sinh và đã viết script, nhưng hiện mới chạy trên SQLite.
- Cổng VNPay/MoMo sandbox thật: cần khóa merchant và URL công khai (ngrok) để nhận IPN. Thuật toán ký đã test bằng khóa giả.
- Cloudinary thật: cần khóa. Đã test với lưu trữ cục bộ.
- Docker Compose: chưa build vì máy không có Docker.

**Cần làm trước khi đưa vào sử dụng thật**
- Thay email, điện thoại, địa chỉ mẫu trong `src/frontend/online-learning-client/src/config/site.ts`.
- Tắt kênh thanh toán thử nghiệm (`Payments:TestGateway:Enabled=false`).
- Thay ảnh đại diện giảng viên minh họa bằng ảnh thật.
- Xem checklist đầy đủ ở [tài liệu 11](thesis/html/11-trien-khai.md#checklist-trước-khi-mở-cho-người-dùng).
