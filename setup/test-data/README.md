# Dữ liệu thử

Dữ liệu thử **được sinh bằng code**, không lưu dưới dạng tệp DB trong repository. Nhờ vậy, mỗi lần chạy đều ra cùng một bộ dữ liệu, khớp với kết quả đã báo cáo, và không có dữ liệu cá nhân thật nào bị đưa lên GitHub.

| Nguồn | Vai trò |
| --- | --- |
| `src/backend/src/OnlineLearning.Infrastructure/Persistence/Seed/SampleData.cs` | Danh sách khóa học, chương, bài, giảng viên, học viên mẫu |
| `src/backend/src/OnlineLearning.Infrastructure/Persistence/Seed/DbSeeder.cs` | Nạp vai trò, danh mục, tài khoản, đơn hàng, đánh giá, mã giảm giá, hồ sơ giảng viên, tin nhắn, quy tắc điểm thưởng |
| `src/backend/tests/OnlineLearning.IntegrationTests/` | Dữ liệu riêng cho 43 test backend (SQLite in-memory, tạo mới mỗi test) |
| `src/frontend/online-learning-client/e2e/` | 25 kịch bản E2E, mỗi lần chạy dùng một DB SQLite mới với dữ liệu mẫu ở trên |

Dữ liệu mẫu được nạp khi chạy chế độ demo (`--launch-profile demo-sqlite`) hoặc khi bật `Seed:RunOnStartup=true`.

## Tài khoản

Mật khẩu của mọi tài khoản demo là giá trị `Seed:DemoPassword` bạn đặt lúc cài (script `setup/run-demo.ps1` sẽ hỏi). Khi chạy E2E, mật khẩu được đặt trong `playwright.config.ts`.

| Vai trò | Email |
| --- | --- |
| Quản trị viên | `admin@eduviet.local` |
| Giảng viên | `giangvien@eduviet.local`, `lethu.design@eduviet.local`, `phamquang.data@eduviet.local` |
| Học viên | `hocvien@eduviet.local` (đã mua và học sẵn một số khóa), `hoangnam@eduviet.local`, `ngoclan@eduviet.local`, `minhtuan@eduviet.local`, `baoanh@eduviet.local` |

Giảng viên và học viên mẫu là **người ảo, không có thật**. Ảnh đại diện giảng viên là ảnh minh họa giấy phép CC0.

## Khóa học mẫu

| Khóa học | Lĩnh vực | Giá gốc | Giá bán |
| --- | --- | --- | --- |
| Lập trình C# từ cơ bản đến nâng cao | Lập trình | 899.000đ | 499.000đ |
| ASP.NET Core Web API thực chiến | Lập trình | 1.290.000đ | 790.000đ |
| ReactJS và TypeScript cho người mới | Lập trình | 990.000đ | 990.000đ |
| SQL Server cho lập trình viên | Lập trình | 690.000đ | 390.000đ |
| Thiết kế UI/UX với Figma | Thiết kế | 1.190.000đ | 690.000đ |
| Photoshop cơ bản cho người đi làm | Thiết kế | 590.000đ | 299.000đ |
| Excel từ cơ bản đến nâng cao | Tin học văn phòng | 499.000đ | 249.000đ |
| Power BI phân tích dữ liệu kinh doanh | Dữ liệu & AI | 1.490.000đ | 990.000đ |
| Kỹ năng thuyết trình chuyên nghiệp | Kỹ năng mềm | Miễn phí | Miễn phí |
| Digital Marketing tổng quan | Marketing | 790.000đ | 450.000đ |

Kèm theo: khoảng 40 đơn hàng trong 12 tháng (để biểu đồ doanh thu có số liệu), đánh giá, bài kiểm tra, hồ sơ giảng viên và tin nhắn mẫu.

## Mã giảm giá

| Mã | Nội dung |
| --- | --- |
| `CHAOBANMOI` | Giảm 10%, tối đa 100.000đ, mỗi người dùng một lần |
| `GIAM50K` | Giảm 50.000đ cho đơn từ 300.000đ |
| `MIENPHI100` | Giảm 100%, dùng để thử đơn 0đ không qua cổng thanh toán |

## Thanh toán thử

Ở trang thanh toán, chọn **Kênh thử nghiệm**. Hệ thống giả lập IPN có chữ ký và xác nhận đơn ngay, **không thu tiền thật**. Kênh này phải tắt ở môi trường thật (`Payments:TestGateway:Enabled=false`).

## Kết quả kiểm thử tương ứng

| Ngày | Backend | E2E |
| --- | --- | --- |
| 09/11/2026 | 43/43 đạt | 25/25 đạt |

Chi tiết kịch bản: [`thesis/html/12-bao-cao-kiem-thu.md`](../../thesis/html/12-bao-cao-kiem-thu.md).
