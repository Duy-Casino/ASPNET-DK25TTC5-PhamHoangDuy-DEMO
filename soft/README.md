# soft: Phần mềm sử dụng trong đồ án

Bộ cài không lưu trong repository vì dung lượng lớn và có giấy phép riêng. Tải bản chính thức theo liên kết dưới đây.

## Bắt buộc để chạy

| Phần mềm | Phiên bản đã dùng | Tải về | Ghi chú |
| --- | --- | --- | --- |
| .NET SDK | 10.0 | <https://dotnet.microsoft.com/download/dotnet/10.0> | Hoặc `winget install Microsoft.DotNet.SDK.10` |
| Node.js | 22 LTS trở lên (đã dùng 24) | <https://nodejs.org/> | Hoặc `winget install OpenJS.NodeJS.LTS` |
| Git | Bất kỳ | <https://git-scm.com/downloads> | Để clone repository |

## Khi chạy với SQL Server

| Phần mềm | Phiên bản | Tải về |
| --- | --- | --- |
| SQL Server | 2022 Developer hoặc Express | <https://www.microsoft.com/sql-server/sql-server-downloads> |
| SQL Server Management Studio | 20+ | <https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms> |
| dotnet-ef | 10.x | `dotnet tool install --global dotnet-ef` |

## Công cụ phát triển và kiểm thử

| Phần mềm | Dùng để |
| --- | --- |
| Visual Studio 2022 / VS Code | Soạn mã nguồn |
| Google Chrome | Chạy kiểm thử E2E (`E2E_CHANNEL=chrome`) |
| Playwright | Kiểm thử E2E; cài cùng `npm install`, trình duyệt: `npx playwright install chrome` |
| Docker Desktop | Triển khai bằng `docker/` (tùy chọn) |
| ngrok | Mở URL công khai để nhận IPN từ VNPay/MoMo sandbox (tùy chọn) |

## Dịch vụ bên ngoài (tùy chọn)

| Dịch vụ | Dùng để | Đăng ký |
| --- | --- | --- |
| VNPay Sandbox | Thanh toán thử | <https://sandbox.vnpayment.vn/devreg/> |
| MoMo Developers | Thanh toán thử | <https://developers.momo.vn/> |
| Cloudinary | Lưu ảnh, video trên CDN | <https://cloudinary.com/> |

Không có các khóa trên, hệ thống vẫn chạy đầy đủ với **kênh thanh toán thử nghiệm** và **lưu trữ tệp cục bộ**.
