# 04 — Chạy dự án trên máy tính

Có hai cách chạy:

| Cách | Khi nào dùng | Cần cài |
| --- | --- | --- |
| **A. Chế độ demo (SQLite)** | Xem thử, phát triển giao diện, chạy E2E | .NET 10 SDK, Node.js 22+ |
| **B. SQL Server** | Giống môi trường thật, kiểm tra migration | Thêm SQL Server 2022 (Developer/Express) hoặc LocalDB |

## 1. Cài đặt công cụ (Windows)

```powershell
winget install Microsoft.DotNet.SDK.10
winget install OpenJS.NodeJS.LTS
dotnet tool install --global dotnet-ef
# Chỉ cho cách B:
winget install Microsoft.SQLServer.2022.Developer
```

Không có quyền Admin thì cài .NET vào thư mục người dùng:

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
.\dotnet-install.ps1 -Channel 10.0 -InstallDir "$env:USERPROFILE\.dotnet"
# Thêm %USERPROFILE%\.dotnet và %USERPROFILE%\.dotnet\tools vào PATH
```

Kiểm tra: `dotnet --list-sdks` (có 10.0.x), `node -v` (≥ 22).

## 2. Cấu hình bí mật (một lần)

Bí mật không nằm trong mã nguồn mà lưu bằng User Secrets:

```powershell
cd src\backend\src\OnlineLearning.API
dotnet user-secrets set "Jwt:SecretKey" "<chuỗi ngẫu nhiên ≥ 32 ký tự>"
dotnet user-secrets set "Seed:DemoPassword" "<mật khẩu demo, ≥8 ký tự, có hoa/thường/số>"
```

Sinh chuỗi ngẫu nhiên trong PowerShell: `[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))`.

## 3. Cách A — chế độ demo (không cần SQL Server)

```powershell
# Terminal 1: API (http://localhost:5080). Tự tạo eduviet-demo.db và dữ liệu mẫu.
cd src\backend\src\OnlineLearning.API
dotnet run --launch-profile demo-sqlite

# Terminal 2: Frontend (http://localhost:5173)
cd src\frontend\online-learning-client
npm install
npm run dev
```

Muốn tạo lại dữ liệu mẫu từ đầu: dừng API, xóa `eduviet-demo.db`, chạy lại.

> Chế độ demo tạo DB bằng `EnsureCreated` nên **không tự cập nhật khi schema đổi**. Sau khi kéo code có migration mới (ví dụ `InstructorProfileAndMessaging`), hãy dừng API rồi đổi tên/xóa `eduviet-demo.db` để DB được tạo lại kèm dữ liệu mẫu.

## 4. Cách B — SQL Server

```powershell
cd src\backend
# Nếu không dùng instance mặc định (localhost), đặt chuỗi kết nối:
dotnet user-secrets --project src\OnlineLearning.API set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=OnlineLearningDb;Trusted_Connection=True;TrustServerCertificate=True"
dotnet ef database update -p src\OnlineLearning.Infrastructure -s src\OnlineLearning.API
dotnet run --project src\OnlineLearning.API --launch-profile https
```

- Swagger: https://localhost:7080/swagger. Frontend vẫn gọi API qua http://localhost:5080.
- Nếu dùng LocalDB: `Server=(localdb)\MSSQLLocalDB;Database=OnlineLearningDb;Trusted_Connection=True;TrustServerCertificate=True`.
- DBA muốn chạy tay thì dùng script [`setup/database/migration-InitialCreate.sql`](../../setup/database/migration-InitialCreate.sql).

## 5. Tài khoản demo

Mật khẩu chung là giá trị `Seed:DemoPassword`.

| Email | Vai trò |
| --- | --- |
| `admin@eduviet.local` | Admin |
| `giangvien@eduviet.local`, `lethu.design@eduviet.local`, `phamquang.data@eduviet.local` | Giảng viên |
| `hocvien@eduviet.local`, `hoangnam@…`, `ngoclan@…`, `minhtuan@…`, `baoanh@…`, `ducthinh@…` | Học viên |

Mã giảm giá mẫu: `CHAOBANMOI`, `GIAM50K`, `MIENPHI100`. Thanh toán: chọn "Kênh thanh toán thử nghiệm" (tự thành công, không thu tiền).

## 6. Chạy kiểm thử

```powershell
# Backend: 30 test tích hợp (SQLite in-memory, không cần SQL Server)
cd src\backend
dotnet test

# E2E: Playwright tự khởi động API demo (cổng 5081) + Vite (cổng 5174) với DB mới
cd src\frontend\online-learning-client
$env:E2E_CHANNEL="chrome"   # dùng Chrome đã cài; bỏ dòng này nếu đã chạy: npx playwright install chromium
npm run test:e2e
npm run test:e2e:report     # xem báo cáo HTML
```

Phải dừng API demo đang chạy trước khi chạy E2E, vì lệnh `dotnet run` cần build lại dự án.

## 7. Sự cố thường gặp

| Lỗi | Cách xử lý |
| --- | --- |
| `Jwt:SecretKey phải dài ít nhất 32 ký tự` | Chưa đặt User Secrets (mục 2) |
| Frontend báo "Không kết nối được máy chủ" | API chưa chạy ở cổng 5080 |
| `A network-related or instance-specific error` | Sai tên SQL Server trong chuỗi kết nối, hoặc service SQL Server chưa chạy |
| `The certificate chain was issued by an authority that is not trusted` | Thêm `TrustServerCertificate=True` vào chuỗi kết nối |
| `dotnet ef` không nhận | Mở terminal mới, hoặc thêm `%USERPROFILE%\.dotnet\tools` vào PATH |
| Build báo tệp DLL đang bị khóa | Dừng tiến trình API đang chạy rồi build lại |
| PDF chứng chỉ mất dấu tiếng Việt (Linux) | Cài `fonts-dejavu-core` hoặc `fonts-noto-core` |
