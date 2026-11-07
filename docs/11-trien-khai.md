# 11 — Triển khai lên môi trường thực tế

Ba phương án, chọn một:

| Phương án | Phù hợp | Thành phần |
| --- | --- | --- |
| **A. Docker Compose trên VPS Linux** (khuyến nghị) | Một máy chủ 2 vCPU / 4 GB RAM trở lên | SQL Server 2022 container, API, Nginx (frontend + reverse proxy) |
| **B. Windows Server + IIS** | Hạ tầng Windows có sẵn | IIS + ASP.NET Core Hosting Bundle, SQL Server |
| **C. Azure** | Không muốn tự quản máy chủ | App Service (API), Static Web Apps (frontend), Azure SQL |

> Dockerfile và docker-compose đã viết nhưng **chưa build thử** trên máy phát triển vì máy này không có Docker. Hãy chạy `docker compose build` ở môi trường có Docker trước khi đưa lên production.

## Phương án A — Docker Compose

```
Internet ──HTTPS──> Caddy/Cloudflare (TLS) ──HTTP──> web (Nginx :80)
                                                    ├── /           → React (dist)
                                                    ├── /api/*      → api:8080
                                                    └── /uploads/*  → api:8080
                                       api ──> sqlserver:1433 (không mở ra ngoài)
```

1. **Chuẩn bị máy chủ** (Ubuntu 24.04): cài Docker Engine + Compose plugin, mở cổng 80/443, trỏ DNS `eduviet.example.com` về máy chủ.
2. **Lấy mã nguồn và cấu hình:**
   ```bash
   git clone <repo> eduviet && cd eduviet/khoa-hoc-online
   cp .env.example .env
   nano .env   # PUBLIC_URL, SQL_SA_PASSWORD, JWT_SECRET, ADMIN_EMAIL/ADMIN_PASSWORD, khóa VNPay/MoMo/Cloudinary/SMTP
   ```
3. **Chạy:**
   ```bash
   docker compose up -d --build
   docker compose logs -f api   # chờ dòng "Application started"
   curl http://localhost/api/health
   ```
   API tự áp dụng migration (`Database__MigrateOnStartup=true`), tạo vai trò, danh mục mặc định và tài khoản Admin từ `ADMIN_EMAIL`/`ADMIN_PASSWORD`. Sau lần chạy đầu, xóa `ADMIN_PASSWORD` khỏi `.env`.
4. **HTTPS:** đặt Caddy trước Nginx (tự xin chứng chỉ Let's Encrypt):
   ```
   eduviet.example.com {
       reverse_proxy localhost:8081
       header_up X-Forwarded-Proto https
   }
   ```
   Khi đó đặt `HTTP_PORT=8081` và `HTTPS_REDIRECT=true` trong `.env`. Dùng Cloudflare cũng được, với chế độ SSL "Full".
5. **Cập nhật phiên bản:** `git pull && docker compose up -d --build`. Migration mới được áp dụng tự động.
6. **Sao lưu:**
   ```bash
   docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SQL_SA_PASSWORD" \
     -Q "BACKUP DATABASE OnlineLearningDb TO DISK='/var/opt/mssql/backup/eduviet.bak' WITH INIT"
   ```
   Sao lưu thêm volume `apidata` (khóa Data Protection + tệp riêng tư) và `uploads`, trừ khi đã dùng Cloudinary.

## Phương án B — Windows Server + IIS

1. Cài **ASP.NET Core 10 Hosting Bundle**, **SQL Server 2022**, bật IIS và module **URL Rewrite**.
2. Build và publish:
   ```powershell
   cd khoa-hoc-online\backend
   dotnet publish src\OnlineLearning.API -c Release -o C:\inetpub\eduviet-api
   cd ..\frontend\online-learning-client
   npm ci; npm run build   # copy dist\* vào C:\inetpub\eduviet-web
   ```
3. Áp dụng migration bằng *migration bundle* (không cần cài SDK trên máy chủ):
   ```powershell
   dotnet ef migrations bundle -p src\OnlineLearning.Infrastructure -s src\OnlineLearning.API -o efbundle.exe
   .\efbundle.exe --connection "Server=...;Database=OnlineLearningDb;..."
   ```
4. Tạo site IIS cho frontend (thư mục `eduviet-web`). Thêm `web.config` dùng URL Rewrite để: chuyển `api/*` và `uploads/*` sang API (ứng dụng con `/api`, hoặc ARR proxy tới site API), và trả `index.html` cho mọi đường dẫn còn lại (SPA).
5. Cấu hình API bằng **biến môi trường** trong Application Pool, ví dụ `ConnectionStrings__DefaultConnection`, `Jwt__SecretKey`, `App__ClientUrl`, `VnPay__*`, `MoMo__*`, `Cloudinary__*`, `DataProtection__KeysPath=C:\eduviet-data\keys`, `Storage__LocalRoot=C:\eduviet-data\storage`. Cấp quyền ghi cho identity của Application Pool vào `C:\eduviet-data` và `wwwroot\uploads`.
6. Gắn chứng chỉ HTTPS cho site (win-acme để lấy Let's Encrypt). Để `Https__Redirect=true`.

## Phương án C — Azure

| Thành phần | Dịch vụ | Ghi chú |
| --- | --- | --- |
| API | App Service Linux (.NET 10) | Đặt cấu hình ở *Environment variables*; khóa bí mật để trong Key Vault |
| Frontend | Static Web Apps | Build `npm run build`, thư mục `dist`; khai báo route `/api/*` tới App Service (linked backend) |
| Database | Azure SQL Database (gói S0 trở lên) | Bật *Allow Azure services*; chạy migration bundle trong pipeline |
| Tệp | Cloudinary | Không lưu tệp trên đĩa App Service |
| Data Protection | Blob Storage | Hoặc `DataProtection__KeysPath=/home/keys` (thư mục `/home` của App Service là bền vững) |

App Service Linux chưa có sẵn font tiếng Việt. Muốn PDF chứng chỉ hiển thị đúng dấu, triển khai API dạng container (dùng `backend/Dockerfile`, đã cài font), hoặc đặt tệp `.ttf` vào thư mục `Fonts/`.

## Checklist trước khi mở cho người dùng

**Bảo mật**
- [ ] `ASPNETCORE_ENVIRONMENT=Production` (Swagger tự tắt)
- [ ] `Jwt:SecretKey` ngẫu nhiên ≥ 32 ký tự; không có bí mật nào trong `appsettings.json` hay git
- [ ] `Payments:TestGateway:Enabled=false`. **Bắt buộc**, nếu không khách có thể "thanh toán" mà không trả tiền
- [ ] HTTPS bắt buộc; `App:ClientUrl` và `Payments:ReturnUrl` dùng `https://` và tên miền thật
- [ ] Chỉ mở cổng 80/443; SQL Server và API không mở ra Internet (`ForwardedHeaders` đang tin mọi proxy, nên API chỉ được đứng sau Nginx)
- [ ] Mật khẩu SA mạnh; tốt nhất tạo user SQL riêng chỉ có quyền trên `OnlineLearningDb`
- [ ] Không bật `Seed:RunOnStartup` (seed này tạo tài khoản demo)

**Thanh toán**
- [ ] VNPay/MoMo đã chuyển sang khóa và URL **production**
- [ ] Đã khai báo URL IPN `https://<tên miền>/api/payments/vnpay/ipn` trong trang merchant VNPay; MoMo dùng `…/momo/ipn`
- [ ] Đã thử một giao dịch thật số tiền nhỏ và đối chiếu trang Admin → Giao dịch

**Cơ sở dữ liệu**
- [ ] Migration `InstructorProfileAndMessaging` (hồ sơ giảng viên, tin nhắn) được áp dụng tự động khi `Database__MigrateOnStartup=true`; DBA chạy tay thì dùng `database/migration-InstructorProfileAndMessaging.sql` (idempotent)

**Nội dung & SEO**
- [x] Điều khoản hoàn tiền đã xác nhận (06/11/2026): yêu cầu trong 7 ngày, đã học tối đa 20%, chưa cấp chứng chỉ; phản hồi trong 7 ngày làm việc
- [ ] Thay email, điện thoại, địa chỉ mẫu bằng thông tin thật trong `frontend/online-learning-client/src/config/site.ts` (mục "CẦN THAY")
- [ ] Nhờ người có chuyên môn pháp lý rà soát các trang `/chinh-sach/*`
- [ ] `PUBLIC_URL` đúng tên miền thật: docker-compose truyền vào `VITE_SITE_URL` khi build để thẻ Open Graph/canonical dùng URL tuyệt đối
- [ ] Sửa dòng `Sitemap:` trong `public/robots.txt` thành URL đầy đủ, rồi khai báo `https://<tên miền>/sitemap.xml` trong Google Search Console
- [ ] Thêm link mạng xã hội thật vào `site.social` (để trống thì footer không hiện)

**Vận hành**
- [ ] Đã cấu hình SMTP để email đặt lại mật khẩu gửi được
- [ ] Đã cấu hình Cloudinary (hoặc volume tệp được sao lưu)
- [ ] Khóa Data Protection lưu bền vững (`DataProtection:KeysPath` trỏ vào volume)
- [ ] Có lịch sao lưu DB hằng ngày và đã thử khôi phục
- [ ] Giám sát `/api/health` (UptimeRobot, Azure Monitor…); log được thu thập (`docker compose logs`, hoặc thêm Serilog sink)
- [ ] Có font tiếng Việt cho PDF (đã có sẵn trong `backend/Dockerfile`)
- [ ] Kiểm tra giấy phép QuestPDF: bản Community chỉ miễn phí cho tổ chức có doanh thu dưới 1 triệu USD/năm
