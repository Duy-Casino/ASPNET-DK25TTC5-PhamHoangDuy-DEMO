# Báo cáo tiến độ Tuần 04

| | |
| --- | --- |
| Đồ án | Website bán khóa học trực tuyến (EduViet) |
| Sinh viên | Phạm Hoàng Duy, MSSV 170125375, lớp DK25TTC5 |
| Thời gian | **Thứ Năm 26/11/2026 – Thứ Tư 02/12/2026** |
| Trạng thái | Đang thực hiện (cập nhật ngày 27/11/2026) |
| Commit trong tuần | `93b7c29` Tăng doanh số (27/11); tiện ích người học (27/11) |

## 1. Tóm tắt tuần

- Hoàn thành **nhóm 2: tăng doanh số** gồm 6 tính năng: yêu thích, gợi ý khóa học, flash sale, combo, mua tặng, email nhắc giỏ hàng.
- CSDL thêm 4 bảng (`WishlistItems`, `Bundles`, `BundleCourses`, `CourseGifts`) và 4 cột (`Courses.DiscountEndsAt`, `Carts.ReminderSentAt`, `Orders.BundleId`, `Orders.IsGift`); tổng **48 bảng**, migration thứ 6 `SalesGrowth` kèm script SQL Server.
- Rà soát trải nghiệm người học: phát hiện **video bài học mẫu không phát được** (máy chủ ngoài trả 403, trang chỉ hiện khung đen). Đã đóng gói video mẫu cùng Frontend và thêm thông báo lỗi kèm nút tải lại.
- Thêm **tiện ích người học**: hỏi đáp theo từng bài học, ghi chú gắn mốc thời gian video, email nhắc học khi bỏ dở, tùy chọn tắt email nhắc nhở. CSDL thêm 3 bảng (51 bảng), migration thứ 7 `LearnerTools`.
- Kiểm thử: **backend 65/65, E2E 29/29** đạt.

| Chỉ số | Cuối tuần 03 | Hiện tại (27/11) |
| --- | --- | --- |
| Test backend | 55 | 65 |
| Kịch bản E2E | 27 | 29 |
| Bảng dữ liệu | 44 | 51 |
| Migration CSDL | 5 | 7 (thêm `SalesGrowth`, `LearnerTools`) |

## 2. Công việc theo ngày

| Ngày | Nội dung | Kiểm thử |
| --- | --- | --- |
| Thứ Năm 26/11 | Không có thay đổi mã nguồn | |
| Thứ Sáu 27/11 | Nhóm 2 "tăng doanh số": yêu thích, gợi ý, flash sale, combo, mua tặng, nhắc giỏ hàng; 5 trang mới (`/combo`, `/combo/:slug`, `/qua-tang/:mã`, `/hoc-vien/yeu-thich`, `/quan-tri/combo`) | 61/61 backend, 28/28 E2E |

## 3. Chức năng mới

| Chức năng | Mô tả |
| --- | --- |
| Yêu thích | Nút trái tim trên mọi thẻ khóa học và trang chi tiết; trang "Khóa học yêu thích" thêm vào giỏ hoặc bỏ yêu thích. Chưa đăng nhập thì chuyển sang trang đăng nhập |
| Gợi ý khóa học | "Học viên cũng quan tâm": khóa học mà người đã mua khóa này cũng mua (tính từ dữ liệu ghi danh thật), thiếu thì bổ sung khóa cùng chủ đề được học nhiều. Không gợi ý khóa đã sở hữu, đã có trong giỏ hoặc của chính giảng viên. Có ở trang chi tiết và giỏ hàng |
| Flash sale | Giảng viên đặt giá khuyến mãi kèm thời điểm kết thúc (tối đa 90 ngày), cả khi khóa học đang bán, không cần duyệt lại. Thẻ khóa học có nhãn "Còn …", trang chi tiết có đồng hồ đếm ngược. Hết hạn thì máy chủ tự tính giá gốc khi báo giá và đặt hàng |
| Combo | Admin tạo combo 2–10 khóa đang xuất bản, giá thấp hơn tổng giá gốc. Giá combo được chia cho từng khóa theo tỷ lệ giá gốc nên mã giảm giá, điểm thưởng, thu nhập giảng viên, hoàn tiền vẫn tính đúng. Combo chỉ bán trọn bộ; combo đã có đơn không xóa được, chỉ ẩn |
| Mua tặng | Ở trang thanh toán chọn "Mua tặng người khác", nhập email, tên, lời nhắn. Người mua không được ghi danh. Thanh toán xong, người nhận nhận email có liên kết `/qua-tang/:mã`; phải đăng nhập bằng đúng email được tặng mới nhận được. Hoàn tiền đơn quà đã nhận thì thu hồi quyền học của người nhận |
| Nhắc giỏ hàng | Công việc nền chạy mỗi giờ: giỏ có khóa học thêm vào quá 24 giờ chưa thanh toán thì gửi email liệt kê khóa học (báo nếu flash sale sắp kết thúc). Mỗi lần giỏ thay đổi chỉ nhắc một lần. Cấu hình mục `CartReminder` |

### Tiện ích người học

| Chức năng | Mô tả |
| --- | --- |
| Sửa video bài học | Video mẫu cũ trỏ tới bucket công khai của Google, nay bị chặn (403). Thay bằng video mẫu tự tạo (40 giây, 240 KB) đóng gói trong `public/videos/demo`; DB demo cũ tự chuyển đường dẫn khi khởi động. Video lỗi thì trình học hiện thông báo và nút "Tải lại video" (lấy lại liên kết có chữ ký) |
| Hỏi đáp theo bài học | Tab "Hỏi đáp" dưới video: học viên đã ghi danh, giảng viên của khóa và Admin cùng xem, cùng trả lời; câu trả lời của giảng viên có nhãn "Giảng viên"; người hỏi hoặc giảng viên đánh dấu "Đã giải đáp". Giảng viên nhận thông báo khi có câu hỏi, học viên nhận thông báo khi có trả lời. Trang `/giang-day/hoi-dap` gom câu hỏi chưa trả lời |
| Ghi chú theo mốc thời gian | Tab "Ghi chú": ghi chú riêng tư, tự gắn giây hiện tại của video; bấm mốc thời gian để tua tới đúng đoạn; sửa, xóa; sắp xếp theo thời gian video |
| Nhắc học | Công việc nền mỗi 6 giờ: khóa học đang dở mà học viên không vào học 7 ngày thì gửi một email (gom các khóa) kèm thông báo "Học tiếp nhé!"; mỗi đợt bỏ dở nhắc một lần, quay lại học rồi bỏ dở tiếp mới nhắc lại |
| Tắt email nhắc nhở | Hồ sơ cá nhân có công tắc "Nhận email nhắc nhở" áp dụng cho nhắc học và nhắc giỏ hàng; email giao dịch vẫn gửi |

## 4. Các điều chỉnh trong tuần

| # | Điều chỉnh | Lý do | Kết quả |
| --- | --- | --- | --- |
| 1 | Giá khuyến mãi tính theo thời điểm (`EffectivePriceAt`) thay vì chỉ so giá | Flash sale cần tự hết hạn mà không phải chờ công việc nền xóa dữ liệu | Báo giá, đặt hàng, danh sách, lọc và sắp xếp theo giá đều dùng giá còn hiệu lực |
| 2 | Kiểm tra chứng chỉ khi hoàn tiền tính cả người nhận quà | Đơn quà tặng: người học là người nhận, không phải người mua | Không hoàn tiền đơn quà mà người nhận đã có chứng chỉ |
| 3 | Chế độ demo SQLite tự thêm cả cột mới (không chỉ bảng mới) | Migration này thêm cột vào bảng đã có | DB demo cũ chạy được ngay, giữ dữ liệu |
| 4 | Email dùng bộ mã hóa HTML giữ nguyên chữ Việt | `WebUtility.HtmlEncode` đổi chữ có dấu thành `&#250;` (phát hiện nhờ kiểm thử) | Email dễ đọc khi xem mã nguồn, nội dung kiểm thử được |
| 5 | Nút trái tim đặt cạnh liên kết thẻ khóa học, không lồng bên trong | Nút nằm trong thẻ `<a>` là HTML không hợp lệ, khó dùng bằng bàn phím | Thẻ vẫn bấm được toàn bộ, nút trái tim có nhãn riêng cho trình đọc màn hình |

## 5. Khó khăn và cách xử lý

| Khó khăn | Cách xử lý |
| --- | --- |
| Lần build bị ngắt giữa chừng để lại tệp cache hỏng trong `obj` (lỗi `DefineStaticWebAssets`) | Xóa thư mục `obj` của project API rồi build lại; các lệnh dài chuyển sang chạy nền |
| Kịch bản E2E chuyển trang ngay sau khi bấm trái tim làm hủy yêu cầu lưu | Chờ thông báo "Đã thêm … vào yêu thích" (lưu xong ở máy chủ) rồi mới chuyển trang |

## 6. Đánh giá so với kế hoạch

- **Đã xong:** nhóm 2 (tăng doanh số) gồm 6 tính năng đã đề xuất.
- **Đã xong thêm:** sửa video bài học, nhóm tiện ích người học mức ưu tiên cao (hỏi đáp, ghi chú, nhắc học).
- **Chưa xong:** tiện ích mức trung bình (phụ đề, nút tốc độ và phím tắt trình phát, ôn lại câu sai, tìm bài trong mục lục); cập nhật báo cáo đồ án và slide; chạy thử trên SQL Server thật.

## 7. Kế hoạch còn lại của tuần

| Việc | Đầu ra |
| --- | --- |
| Bổ sung chức năng nhóm 1 và nhóm 2 vào báo cáo đồ án (chương 2, 3) và slide | `thesis/doc`, `thesis/pdf`, `thesis/abs` |
| Thử chạy 7 script SQL trên SQL Server thật | Ghi kết quả vào báo cáo kiểm thử |
| Chốt báo cáo tuần 04 vào 02/12 | Cập nhật tệp này |
