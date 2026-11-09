# setup: Cài đặt và chạy lại hệ thống

Thư mục này chứa những gì cần để cài đặt EduViet trên một máy mới và tái tạo đúng dữ liệu thử đã dùng khi báo cáo.

| Thành phần | Nội dung |
| --- | --- |
| [`run-demo.ps1`](run-demo.ps1) | Script PowerShell: kiểm tra công cụ, đặt bí mật lần đầu, cài gói npm, mở API và web trong hai cửa sổ |
| [`database/`](database/) | Script SQL Server: `schema.sql` (toàn bộ CSDL) và script migration từng bước, idempotent |
| [`test-data/`](test-data/) | Mô tả dữ liệu thử: tài khoản, khóa học, mã giảm giá, và cách tạo lại dữ liệu khớp với kết quả báo cáo |

Ứng dụng là website (API + giao diện web), không có tệp cài đặt `.exe`. Bộ cài .NET SDK, Node.js và SQL Server không lưu trong repository vì dung lượng lớn và có giấy phép riêng. Liên kết tải chính thức nằm ở [`../soft/`](../soft/README.md).

## 1. Sơ đồ triển khai

### 1.1. Chạy thử trên một máy (chế độ demo)

```
┌──────────────────────────── Máy tính Windows / macOS / Linux ────────────────────────────┐
│                                                                                          │
│   Trình duyệt ──http://localhost:5173──> Vite dev server (React)                         │
│                                             │  proxy /api, /uploads                      │
│                                             ▼                                            │
│                                  ASP.NET Core Web API  (http://localhost:5080)           │
│                                             │  EF Core                                   │
│                                             ▼                                            │
│                                  SQLite: eduviet-demo.db  (tự tạo + dữ liệu mẫu)         │
│                                  App_Data/storage         (video, tài liệu cục bộ)       │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

### 1.2. Triển khai thật (Docker Compose trên máy chủ)

```
Internet ──HTTPS──> Caddy / Cloudflare (TLS)
                         │ HTTP
                         ▼
                ┌─ web: Nginx :80 ──────────────┐
                │   /          → React (dist)   │
                │   /api/*     → api:8080 ──────┼──> sqlserver:1433 (SQL Server 2022, không mở ra ngoài)
                │   /uploads/* → api:8080       │
                └───────────────────────────────┘
                api ──> VNPay / MoMo (IPN), Cloudinary (tệp), SMTP (email)
```

Tệp cấu hình Docker nằm ở [`../docker/`](../docker/README.md). Các phương án IIS và Azure: [`thesis/html/11-trien-khai.md`](../thesis/html/11-trien-khai.md).

## 2. Cài đặt nhanh (chế độ demo, không cần SQL Server)

1. Cài **.NET 10 SDK** và **Node.js 22+** (liên kết trong [`soft/`](../soft/README.md)).
2. Mở PowerShell tại thư mục gốc repository và chạy:
   ```powershell
   powershell -ExecutionPolicy Bypass -File setup\run-demo.ps1
   ```
   Lần đầu, script hỏi **mật khẩu demo** (ít nhất 8 ký tự, có chữ hoa, chữ thường và số) rồi lưu bằng User Secrets. Khóa JWT được sinh ngẫu nhiên.
3. Mở <http://localhost:5173>. Đăng nhập bằng tài khoản trong [`test-data/`](test-data/README.md) với mật khẩu demo vừa đặt.

Muốn tạo lại dữ liệu mẫu từ đầu: đóng cửa sổ API, xóa `src/backend/src/OnlineLearning.API/eduviet-demo.db`, rồi chạy lại script. Hoặc chạy `run-demo.ps1 -ResetData`.

Làm thủ công từng bước: xem [README gốc, mục 3](../README.md#3-cài-đặt-và-chạy-thử).

## 3. Cài đặt với SQL Server

Chọn một trong hai cách tạo CSDL `OnlineLearningDb`:

- **Bằng EF Core migration** (cần `dotnet-ef`):
  ```powershell
  cd src\backend
  dotnet ef database update -p src\OnlineLearning.Infrastructure -s src\OnlineLearning.API
  ```
- **Bằng script SQL** (cho DBA, không cần .NET): mở SQL Server Management Studio, tạo database `OnlineLearningDb`, rồi chạy lần lượt:
  1. `database/migration-InitialCreate.sql`
  2. `database/migration-InstructorProfileAndMessaging.sql`
  3. `database/migration-RewardPoints.sql`

  Các script đều idempotent, chạy lại không gây lỗi. `database/schema.sql` là bản tham chiếu đầy đủ, dùng để đọc hoặc vẽ ERD.

Sau đó đặt chuỗi kết nối và chạy API:

```powershell
cd src\backend\src\OnlineLearning.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=OnlineLearningDb;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Seed:RunOnStartup" "true"    # chỉ khi muốn nạp dữ liệu mẫu
dotnet run
```

## 4. Kiểm tra sau khi cài

| Kiểm tra | Kết quả mong đợi |
| --- | --- |
| <http://localhost:5080/api/health> | `{"status":"ok", ...}` |
| <http://localhost:5080/swagger> | Trang Swagger liệt kê các endpoint |
| <http://localhost:5173> | Trang chủ có danh sách khóa học |
| `cd src\backend; dotnet test` | 43/43 test đạt |
| `cd src\frontend\online-learning-client; $env:E2E_CHANNEL="chrome"; npm run test:e2e` | 25/25 kịch bản đạt |

## 5. Lỗi thường gặp

| Hiện tượng | Cách xử lý |
| --- | --- |
| Web báo "Không tải được dữ liệu" | API chưa chạy hoặc đã dừng. Kiểm tra cửa sổ API và <http://localhost:5080/api/health> |
| API báo lỗi thiếu cột/bảng sau khi kéo code mới | DB demo cũ chưa có schema mới. Xóa `eduviet-demo.db` rồi chạy lại |
| `dotnet test` hoặc build báo tệp DLL đang bị khóa | Đóng API demo trước khi build/test |
| Cổng 5080 hoặc 5173 đã bị chiếm | Đóng chương trình đang dùng cổng, hoặc đổi cổng trong `launchSettings.json` / `vite.config.ts` |
