# docker: Triển khai bằng Docker Compose

| Tệp | Nội dung |
| --- | --- |
| `docker-compose.yml` | 3 dịch vụ: `sqlserver` (SQL Server 2022), `api` (ASP.NET Core), `web` (Nginx phục vụ React và chuyển tiếp `/api`) |
| `.env.example` | Mẫu biến môi trường. Sao chép thành `.env` rồi điền giá trị thật. **Không commit `.env`** |
| `../src/backend/Dockerfile` | Ảnh API: build .NET 10, cài font tiếng Việt cho chứng chỉ PDF |
| `../src/frontend/online-learning-client/Dockerfile`, `nginx.conf` | Ảnh web: build Vite, phục vụ bằng Nginx |

Hai Dockerfile nằm cạnh mã nguồn vì build context của chúng là thư mục mã nguồn tương ứng.

## Chạy

```bash
cd docker
cp .env.example .env
nano .env        # PUBLIC_URL, SQL_SA_PASSWORD, JWT_SECRET, ADMIN_EMAIL/ADMIN_PASSWORD, khóa VNPay/MoMo/Cloudinary/SMTP
docker compose up -d --build
docker compose logs -f api      # chờ dòng "Application started"
curl http://localhost/api/health
```

API tự áp dụng migration và tạo tài khoản Admin từ `ADMIN_EMAIL`/`ADMIN_PASSWORD`. Sau lần chạy đầu, xóa `ADMIN_PASSWORD` khỏi `.env`.

Sơ đồ triển khai: [`setup/README.md`](../setup/README.md#12-triển-khai-thật-docker-compose-trên-máy-chủ). HTTPS, sao lưu và checklist production: [`thesis/html/11-trien-khai.md`](../thesis/html/11-trien-khai.md).

> **Trạng thái:** các tệp Docker đã viết nhưng **chưa build thử**, vì máy phát triển không có Docker. Hãy chạy `docker compose build` trên máy có Docker trước khi dùng thật.
