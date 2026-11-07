# 12 — Báo cáo kiểm thử (Giai đoạn 7)

| Tầng | Công cụ | Số kịch bản | Kết quả |
| --- | --- | --- | --- |
| Backend tích hợp | xUnit + WebApplicationFactory + SQLite in-memory | 35 | **35/35 đạt** |
| End-to-end | Playwright (Chrome) + API demo + Vite | 23 (16 desktop, 7 điện thoại) | **23/23 đạt** |
| Giao diện | taste-skill `inspect.mjs` (1440 / 768 / 390 px) | Trang chủ, chi tiết khóa, xác minh | Trang chủ 0 FAIL; các trang khác đã sửa lỗi tương phản/bố cục |
| Thủ công trên trình duyệt | Claude Browser | Mọi luồng chính của 3 vai trò | Đạt |

## 1. Backend (`backend/tests`)

| Nhóm | Kịch bản chính |
| --- | --- |
| Danh mục & khóa học | Trang chủ; sitemap.xml chỉ chứa khóa đã xuất bản; tìm kiếm/lọc/sắp xếp; ẩn khóa Pending; chỉ lộ video bài học thử |
| Tài khoản | Đăng ký, refresh token xoay vòng, phát hiện dùng lại token cũ → thu hồi mọi phiên; đăng xuất; sai mật khẩu; validation; đổi mật khẩu; phân quyền 401/403 |
| Mua hàng | Giỏ hàng; báo giá; chống sửa giá; mã giảm giá (sai, tối thiểu, 100%); đơn 0đ; chặn mua trùng; hủy đơn; đánh giá một lần |
| Giảng viên/Admin | Tạo khóa → gửi duyệt → từ chối → duyệt; khóa chỉnh sửa khi chờ duyệt; giảng viên không thấy khóa của nhau; vai trò; khóa tài khoản; mã giảm giá; danh mục |
| Học tập & quiz | Nội dung chặn khi chưa mua; tiến độ; tiếp tục học; giới hạn lượt; nộp lặp; hoàn thành khóa; người khác không nộp hộ được |
| Thanh toán | Kênh test qua IPN có chữ ký + chống phát lại; VNPay sai chữ ký (97) / sai số tiền (04) / không tồn tại (01) / hợp lệ (00) / lặp (02) / thất bại; trang return không cấp quyền; MoMo đúng/sai chữ ký; Admin xem giao dịch |
| Tệp | Ảnh hợp lệ / giả định dạng / đuôi cấm; học viên không tải video được; video riêng tư chỉ phát qua URL có chữ ký |
| Giảng viên & tin nhắn | Hồ sơ công khai (kinh nghiệm, xác minh, thống kê); giảng viên sửa hồ sơ + validation (URL https, năm, gộp chuyên môn trùng); học viên không gọi được API hồ sơ; nhắn tin: không tự nhắn, chỉ nhắn giảng viên, chưa đọc/đã đọc, thông báo, người ngoài không đọc được, cùng một cuộc trao đổi; Admin chỉ xác minh được giảng viên |
| Chứng chỉ | Cấp đúng một lần; PDF hợp lệ; người khác không tải được; xác minh công khai; thu hồi/khôi phục; cấp bù |

## 2. End-to-end (`frontend/online-learning-client/e2e`)

| Tệp | Kịch bản |
| --- | --- |
| `public.spec.ts` | Trang chủ: đủ các section, tab lĩnh vực đổi nội dung, menu "Khóa học" trên header lọc đúng, ô xác minh chứng chỉ; trang Giới thiệu/Liên hệ/Chính sách, canonical, JSON-LD Course, robots.txt, sitemap.xml; mở ô tìm kiếm → tìm kiếm → chi tiết; lọc + sắp xếp lưu trên URL; mua khi chưa đăng nhập → chuyển tới đăng nhập; xác minh mã không tồn tại; `/quan-tri` yêu cầu đăng nhập |
| `student.spec.ts` | Đăng ký → thêm giỏ → mã sai/đúng → kênh test (có thông báo "chưa thu tiền thật") → kết quả thành công → vào học (có video). Đăng ký khóa miễn phí → học hết bài → làm quiz đạt → nhận mã chứng chỉ → tải PDF → chuông thông báo có "Bạn đã nhận chứng chỉ" → người lạ xác minh hợp lệ. Dashboard học viên mẫu → "Học tiếp" |
| `staff.spec.ts` | Giảng viên: biểu đồ tổng quan, tạo khóa, chương, bài, bài kiểm tra, gửi duyệt bị chặn vì thiếu thông tin. Admin: duyệt khóa chờ duyệt → khóa xuất hiện công khai; trang giao dịch. Học viên bị chặn khỏi `/quan-tri` và `/giang-day` |
| `instructor.spec.ts` | Hồ sơ giảng viên có huy hiệu xác minh, kinh nghiệm, chứng nhận, JSON-LD Person; khách bấm nhắn tin → đăng nhập; học viên hỏi về khóa học → giảng viên nhận thông báo, trả lời → học viên thấy; giảng viên sửa hồ sơ (lỗi báo đúng ô, trang công khai cập nhật); Admin xác minh → huy hiệu hiện trên trang khóa học |
| `mobile.spec.ts` | Không tràn ngang trên 6 trang; menu điện thoại; bộ lọc dạng sheet |

Mỗi lần chạy E2E dùng một DB SQLite mới với dữ liệu mẫu. API chạy trên cổng 5081, Vite trên cổng 5174, nên không ảnh hưởng server đang dùng để phát triển.

## 3. Lỗi phát hiện nhờ kiểm thử (đã sửa)

| Giai đoạn | Lỗi | Phát hiện bởi |
| --- | --- | --- |
| 3 | Giờ hiển thị lệch 7 tiếng (DateTime thiếu "Z") | Chạy thử trên trình duyệt |
| 3 | Trang thanh toán tự quay về giỏ hàng sau khi đặt; thông báo mã giảm giá sai bị xóa ngay; tiền định dạng "249,000đ" | Chạy thử trên trình duyệt |
| 4 | Nộp bài xong không hiện kết quả; thẻ `<a>` lồng nhau; số liệu tràn ô | Chạy thử trên trình duyệt |
| 5 | VNPay yêu cầu trường `RspCode` nhưng API trả `rspCode` | Test tích hợp |
| 5 | SMTP mặc định trỏ `localhost` làm "quên mật khẩu" lỗi 500 | Rà soát cấu hình |
| 6 | QuestPDF không thấy font hệ thống → PDF lỗi 500 | Test tích hợp |
| 7 | Rate limit đăng nhập chặn kiểm thử tự động → thêm `RateLimiting:Enabled` (chỉ tắt khi chạy E2E) | E2E |

Các lỗi E2E ở lần chạy đầu đều nằm trong kịch bản test (bộ chọn trùng phần tử, đếm trước khi dữ liệu tải xong), không phải lỗi ứng dụng.

## 4. Chưa kiểm thử được

- Chạy trên **SQL Server thật**: máy phát triển không có SQL Server. Migration đã được sinh và CI kiểm tra model không lệch migration, nhưng chưa áp dụng vào SQL Server.
- **VNPay/MoMo sandbox thật**: cần khóa merchant và URL công khai (ngrok). Thuật toán ký đã được test với khóa giả theo đặc tả.
- **Cloudinary thật**: cần khóa. Đã test đầy đủ với lưu trữ cục bộ cùng giao diện `IFileStorage`.
- **Docker Compose**: chưa build được vì máy không có Docker.
- **CI GitHub Actions**: đã viết workflow nhưng chưa chạy, vì thư mục chưa phải repo git và chưa có remote.
