# Báo cáo tiến độ số 01

| | |
| --- | --- |
| Thời gian | 05/11/2026 |
| Sinh viên | Phạm Hoàng Duy, MSSV 170125375, lớp DK25TTC5 |
| Commit | `bd42410`, `5ef2f10`, `ce98a70` |

## 1. Mục tiêu

Hoàn thành phần lõi của website bán khóa học: phân tích, thiết kế, backend, frontend, học trực tuyến, thanh toán, chứng chỉ và kiểm thử.

## 2. Công việc đã thực hiện

| Giai đoạn | Nội dung |
| --- | --- |
| 1. Phân tích, thiết kế | Đặc tả 4 vai trò, nhóm chức năng (FR) và quy tắc nghiệp vụ (BR); Use Case, sequence thanh toán; ERD 23 bảng nghiệp vụ; script SQL Server; Solution 4 project theo Clean Architecture |
| 2. Backend | Auth (JWT 15 phút, refresh token xoay vòng, khóa sau 5 lần sai), khóa học, chương, bài; kiểm duyệt khóa học; giỏ hàng, đơn hàng, mã giảm giá (giá tính ở server); đánh giá; API quản trị; dữ liệu mẫu |
| 3. Frontend | React 19 + TypeScript + Tailwind v4: trang chủ, danh sách, chi tiết khóa học, đăng ký/đăng nhập, giỏ hàng, thanh toán, đơn hàng, hồ sơ; responsive |
| 4. Học trực tuyến | Trình học video lưu tiến độ, bài kiểm tra chấm ở server, dashboard 3 vai trò có biểu đồ |
| 5. Thanh toán, lưu trữ | VNPay/MoMo sandbox (IPN có kiểm tra chữ ký, số tiền, idempotency), kênh thử nghiệm không thu tiền; Cloudinary hoặc lưu cục bộ, URL có chữ ký |
| 6. Chứng chỉ | PDF A4 có QR, mã duy nhất, trang xác minh công khai, thu hồi/khôi phục |
| 7. Kiểm thử, triển khai | xUnit + WebApplicationFactory, Playwright E2E; Dockerfile, docker-compose, CI GitHub Actions; hướng dẫn triển khai |

Sau đó trong ngày: ẩn nút "Đăng ký miễn phí" khi đã đăng nhập; làm lại trang chủ theo kiểu sàn khóa học nền sáng (hướng E, chọn từ các bản demo).

## 3. Kết quả

- Backend: **30/30** test tích hợp đạt. E2E: **18/18** kịch bản đạt.
- Đã chạy thử trọn luồng trên trình duyệt với API chế độ demo (SQLite): mua khóa học, học, làm bài kiểm tra, nhận và xác minh chứng chỉ.
- Đưa mã nguồn lên GitHub.

## 4. Khó khăn và cách xử lý

| Khó khăn | Cách xử lý |
| --- | --- |
| Máy phát triển không có SQL Server | Thêm chế độ demo dùng SQLite (`--launch-profile demo-sqlite`), giữ migration SQL Server cho môi trường thật |
| Giờ hiển thị lệch 7 tiếng | API trả DateTime thiếu "Z"; chuẩn hóa về UTC |
| VNPay yêu cầu trường `RspCode` nhưng API trả `rspCode` | Đặt tên thuộc tính JSON tường minh; có test |
| QuestPDF không tìm thấy font, PDF lỗi 500 | Nạp font tiếng Việt tường minh; Dockerfile cài sẵn font |
| Rate limit đăng nhập chặn kiểm thử tự động | Thêm cấu hình `RateLimiting:Enabled`, chỉ tắt khi chạy E2E |

## 5. Kế hoạch tiếp theo

- Cải thiện giao diện cho hiện đại, thu hút hơn.
- Bổ sung trang chính sách, thông báo, SEO.
