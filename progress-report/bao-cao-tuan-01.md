# Báo cáo tiến độ Tuần 01

| | |
| --- | --- |
| Đồ án | Website bán khóa học trực tuyến (EduViet) |
| Sinh viên | Phạm Hoàng Duy, MSSV 170125375, lớp DK25TTC5 |
| Thời gian | **Thứ Năm 05/11/2026 – Thứ Tư 11/11/2026** (tuần 1, tính từ ngày bắt đầu dự án 05/11/2026) |
| Số liệu cập nhật đến | 09/11/2026 |
| Commit trong tuần | 11 commit, từ `bd42410` đến `ff12fba` |

## 1. Tóm tắt tuần

- Hoàn thành toàn bộ chức năng lõi: tài khoản, khóa học, giỏ hàng, thanh toán VNPay/MoMo, học trực tuyến, bài kiểm tra, chứng chỉ PDF, dashboard 3 vai trò.
- Bổ sung 3 nhóm tính năng mở rộng: trang chính sách và SEO, tính năng giảng viên kèm nhắn tin, điểm thưởng.
- Giao diện điều chỉnh 2 lần theo góp ý.
- Tổ chức repository theo quy định đồ án.
- Kiểm thử cuối tuần: **backend 43/43, E2E 25/25** đạt.

| Chỉ số | Đầu tuần (05/11, commit đầu) | Cuối kỳ báo cáo (09/11) |
| --- | --- | --- |
| Test backend | 30 | 43 |
| Kịch bản E2E | 18 | 25 |
| Migration CSDL | 1 (`InitialCreate`) | 3 (thêm `InstructorProfileAndMessaging`, `RewardPoints`) |
| Tài liệu kỹ thuật | 12 | 14 (thêm design system mới, điểm thưởng) |

## 2. Công việc theo ngày

| Ngày | Commit | Nội dung | Quy mô thay đổi | Kiểm thử |
| --- | --- | --- | --- | --- |
| Thứ Năm 05/11 | `bd42410` 09:29 | Phiên bản đầu, giai đoạn 1–7: phân tích, thiết kế CSDL, backend Clean Architecture, frontend React, học trực tuyến, quiz, thanh toán, chứng chỉ, dashboard, Docker, CI, tài liệu | 178 tệp, +27.470 dòng | 30/30 backend, 18/18 E2E |
| | `5ef2f10` 13:27 | Sửa lỗi: ẩn nút "Đăng ký miễn phí" khi đã đăng nhập (bấm không có tác dụng) | 1 tệp | |
| | `ce98a70` 16:49 | **Điều chỉnh giao diện lần 1:** trang chủ kiểu sàn khóa học nền sáng (hướng E, chọn từ các bản demo) | 6 tệp, +428 / −378 | 18/18 E2E |
| Thứ Sáu 06/11 | `f1da8d0` 12:33 | **Điều chỉnh giao diện lần 2:** thiết kế lại theo phong cách nền tảng công nghệ, giữ thương hiệu EduViet; design system mới | 35 tệp, +1.045 / −624 | 18/18 E2E |
| | `eb08703` 13:00 | Trang Giới thiệu, Liên hệ, chính sách; chuông thông báo; SEO (Open Graph, JSON-LD, sitemap); tối ưu ảnh và gói JS | 35 tệp, +948 / −87 | 31/31 backend, 19/19 E2E |
| Thứ Bảy 07/11 | `86e19f8` 20:57 | Tính năng giảng viên: hồ sơ giảng dạy, xác minh, nhắn tin học viên–giảng viên | 42 tệp, +4.953 / −86 | 35/35 backend, 23/23 E2E |
| | `85bd72d` 21:42 | Hình minh họa ba lợi ích trên trang chủ; ảnh đại diện giảng viên ảo | 9 tệp, +269 / −38 | 36/36 backend, 23/23 E2E |
| | `59a4c9c` 22:26 | Tính năng điểm thưởng: tích điểm, điểm danh, chia sẻ, giới thiệu, đổi điểm, hoàn tiền thu hồi điểm, trang Admin | 40 tệp, +6.740 / −15 | 43/43 backend, 25/25 E2E |
| Chủ Nhật 08/11 | | Không có commit | | |
| Thứ Hai 09/11 | `50e9a94` 11:37 | Viết lại README theo trạng thái đồ án | 1 tệp | |
| | `1bc9bcb` 11:40 | Thêm thông tin sinh viên vào README | 1 tệp | |
| | `ff12fba` 11:51 | Tổ chức lại repository: `setup/`, `src/`, `progress-report/`, `thesis/`, `soft/`, `docker/` | 237 tệp (chủ yếu đổi chỗ) | 43/43 backend, 25/25 E2E |
| | | Mời cộng tác viên theo dõi tiến độ trên GitHub | | |
| Thứ Ba 10/11 – Thứ Tư 11/11 | | Chưa đến tại thời điểm lập báo cáo | | |

## 3. Các điều chỉnh trong tuần

| # | Điều chỉnh | Lý do | Kết quả |
| --- | --- | --- | --- |
| 1 | Trang chủ đổi từ bản đầu sang kiểu sàn khóa học nền sáng (hướng E) | Giao diện ban đầu đơn giản, chưa thu hút người học | Chọn hướng E từ các bản demo, commit `ce98a70` |
| 2 | Thiết kế lại toàn bộ giao diện theo phong cách nền tảng công nghệ | Muốn giao diện hiện đại hơn; tham khảo bố cục trang khác nhưng **không sao chép** logo, nội dung, nhận diện thương hiệu | Design system mới (`thesis/html/13-design-system.md`), đồng bộ mọi trang, commit `f1da8d0` |
| 3 | Bổ sung nhóm "tăng niềm tin người mua" và SEO, ngoài kế hoạch ban đầu | Kết quả tự đánh giá website: thiếu trang chính sách, thông báo, SEO | Commit `eb08703` |
| 4 | Chốt điều khoản hoàn tiền: yêu cầu trong 7 ngày, đã học tối đa 20%, phản hồi trong 7 ngày làm việc | Cần quy tắc rõ ràng trước khi công bố trang chính sách | Đưa vào `src/config/site.ts` và trang Chính sách hoàn tiền |
| 5 | Thông tin liên hệ doanh nghiệp tạm dùng bản mẫu | Chưa có thông tin thật | Đánh dấu "CẦN THAY", thay trước khi triển khai |
| 6 | Mở rộng phạm vi: tính năng giảng viên và nhắn tin | Tăng uy tín giảng viên, giúp học viên hỏi trực tiếp trước khi mua | Thêm migration `InstructorProfileAndMessaging` |
| 7 | Ảnh đại diện giảng viên: dùng ảnh minh họa CC0 thay cho ảnh tạo bằng AI | Dịch vụ tạo ảnh AI hết lượt; yêu cầu dùng người ảo, không có thật | Ảnh tự lưu trong `public/avatars/demo` |
| 8 | Mở rộng phạm vi: tính năng điểm thưởng | Giữ chân người học, khuyến khích giới thiệu | Thêm migration `RewardPoints` và trang quản trị riêng |
| 9 | Không đưa DB demo lên GitHub | Ban đầu định đưa lên, sau đó quyết định không đưa, để tránh lộ dữ liệu và làm nặng repository | DB demo được loại trừ bằng `.gitignore`; dữ liệu thử sinh bằng code (`setup/test-data/`) |
| 10 | Đổi cấu trúc thư mục repository | Theo quy định tổ chức repository của đồ án | Commit `ff12fba`; mọi đường dẫn trong CI, Docker, tài liệu đã cập nhật; test vẫn đạt |

## 4. Khó khăn và cách xử lý

| Khó khăn | Cách xử lý |
| --- | --- |
| Máy phát triển không có SQL Server và Docker | Chế độ demo SQLite để phát triển và chạy E2E; migration và script SQL Server giữ cho môi trường thật |
| Lỗi giờ lệch 7 tiếng, định dạng tiền sai, trùng phần tử trong kịch bản E2E | Phát hiện nhờ chạy thử và kiểm thử, đã sửa (chi tiết trong `thesis/html/12-bao-cao-kiem-thu.md`) |
| Chống gian lận điểm thưởng (cộng trùng, chia sẻ ảo, gửi đồng thời) | Xử lý hoàn toàn ở backend trong transaction; unique index chống cộng trùng; chỉ thưởng chia sẻ khi người khác mở |
| .NET bị crash khi build/test (lỗi telemetry, `bin/obj` cũ sau khi đổi thư mục) | Tắt telemetry .NET CLI; xóa `bin/obj` rồi build lại |

## 5. Đánh giá so với kế hoạch

- **Đã xong:** toàn bộ chức năng trong phạm vi ban đầu (giai đoạn 1–7), cộng 3 nhóm mở rộng.
- **Chưa kiểm thử được:** SQL Server thật, cổng VNPay/MoMo sandbox thật, Cloudinary thật, build Docker.
- **Chưa có:** báo cáo Word, slide, video demo.

## 6. Kế hoạch Tuần 02 (12/11/2026 – 18/11/2026)

| Việc | Đầu ra |
| --- | --- |
| Viết báo cáo đồ án theo mẫu khoa | `thesis/doc/*.docx`, bản PDF trong `thesis/pdf/` |
| Thử chạy trên SQL Server thật bằng migration hoặc script `setup/database/` | Ghi kết quả vào `thesis/html/12-bao-cao-kiem-thu.md` |
| Kiểm tra kết quả CI GitHub Actions sau khi đổi cấu trúc thư mục | CI đạt |
| Chuẩn bị slide và quay video demo các luồng chính | `thesis/abs/` |
| Cập nhật README và báo cáo tuần | `progress-report/bao-cao-tuan-02.md` |
