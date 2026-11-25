# Báo cáo tiến độ Tuần 03

| | |
| --- | --- |
| Đồ án | Website bán khóa học trực tuyến (EduViet) |
| Sinh viên | Phạm Hoàng Duy, MSSV 170125375, lớp DK25TTC5 |
| Thời gian | **Thứ Năm 19/11/2026 – Thứ Tư 25/11/2026** |
| Trạng thái | Đã kết thúc (chốt số liệu ngày 25/11/2026) |
| Commit trong tuần | Nhóm tính năng vận hành bán hàng (25/11) |

## 1. Tóm tắt tuần

- Rà soát website dưới góc độ bán khóa học thật và bổ sung **nhóm tính năng vận hành**: chia doanh thu và rút tiền cho giảng viên, email xác nhận thanh toán, học viên tự yêu cầu hoàn tiền, biên nhận PDF, xuất CSV cho Admin.
- CSDL thêm 3 bảng (`InstructorEarnings`, `InstructorPayouts`, `RefundRequests`), tổng **44 bảng**; migration thứ 5 `InstructorPayouts` kèm script SQL Server.
- Kiểm thử: **backend 55/55, E2E 27/27** đạt (thêm 7 test backend và 1 kịch bản E2E).

| Chỉ số | Cuối tuần 02 | Cuối tuần 03 |
| --- | --- | --- |
| Test backend | 48 | 55 |
| Kịch bản E2E | 26 | 27 |
| Bảng dữ liệu | 41 | 44 |
| Migration CSDL | 4 | 5 (thêm `InstructorPayouts`) |

## 2. Công việc theo ngày

| Ngày | Nội dung | Kiểm thử |
| --- | --- | --- |
| Thứ Năm 19/11 – Thứ Ba 24/11 | Không có thay đổi mã nguồn | |
| Thứ Tư 25/11 | Rà soát các tính năng còn thiếu cho việc bán khóa học; xây dựng nhóm 1 "vận hành": thu nhập và rút tiền của giảng viên, yêu cầu hoàn tiền, email biên nhận, biên nhận PDF, xuất CSV; 3 trang mới (`/giang-day/doanh-thu`, `/quan-tri/hoan-tien`, `/quan-tri/chi-tra`) | 55/55 backend, 27/27 E2E |

## 3. Chức năng mới

| Chức năng | Mô tả |
| --- | --- |
| Thu nhập giảng viên | Mỗi khóa học bán được ghi một dòng vào sổ thu nhập: giảng viên nhận 70% số tiền học viên thực trả (sau mã giảm giá và điểm thưởng). Đơn bị hoàn tiền ghi thêm dòng trừ cùng giá trị. Tiền chỉ rút được sau 7 ngày (hết hạn hoàn tiền) |
| Rút tiền | Giảng viên gửi yêu cầu rút về tài khoản ngân hàng (tối thiểu 100.000đ, không vượt số dư, mỗi lúc một yêu cầu). Admin chuyển khoản ngoài hệ thống rồi xác nhận "Đã chi" kèm mã giao dịch, hoặc từ chối có lý do; giảng viên nhận thông báo |
| Yêu cầu hoàn tiền | Học viên bấm "Yêu cầu hoàn tiền" ở trang đơn hàng. Hệ thống kiểm tra đúng Chính sách hoàn tiền đã công bố: trong 7 ngày, học không quá 20%, chưa có chứng chỉ, đơn có phí, chưa bị từ chối trước đó. Admin duyệt thì đơn được hoàn tiền (thu hồi quyền học, điểm thưởng, thu nhập giảng viên) và gửi email |
| Email và biên nhận | Email xác nhận khi thanh toán thành công và khi được hoàn tiền. Biên nhận PDF (QuestPDF) có bên bán, người mua, từng khóa học, giảm giá, phương thức và mã giao dịch; đơn đã hoàn có dấu "ĐÃ HOÀN TIỀN" |
| Báo cáo cho Admin | Tổng thực thu, doanh thu nền tảng, số phải trả từng giảng viên; xuất CSV đơn hàng (lọc trạng thái, từ khóa, khoảng ngày) và CSV chi trả giảng viên, mở được bằng Excel |

Cấu hình trong `appsettings.json`, mục `Revenue` (tỷ lệ chia, số ngày giữ tiền, mức rút tối thiểu, điều kiện hoàn tiền) và `Invoice` (thông tin bên bán in trên biên nhận).

## 4. Các điều chỉnh trong tuần

| # | Điều chỉnh | Lý do | Kết quả |
| --- | --- | --- | --- |
| 1 | Sổ thu nhập ghi kiểu bút toán (dòng bán + dòng hoàn), không sửa số cũ | Đối soát được lịch sử; hoàn tiền sau khi giảng viên đã rút vẫn trừ đúng | Unique `(OrderItemId, Type)` chặn ghi sổ lặp khi IPN gửi lại |
| 2 | Dòng hoàn có cùng ngày mở khóa với dòng bán nếu dòng bán chưa đến hạn | Tránh số dư có thể rút bị âm tạm thời | Số dư luôn khớp |
| 3 | Admin không xác nhận chi được nếu số dư hiện tại không đủ | Đơn có thể bị hoàn tiền sau khi giảng viên gửi yêu cầu | Báo lỗi và gợi ý từ chối yêu cầu |
| 4 | Tách `StageRefundAsync` khỏi `RefundAsync` | Duyệt yêu cầu hoàn tiền và hoàn tiền đơn phải nằm trong cùng một transaction | Admin hoàn tiền trực tiếp ở trang đơn hàng cũng tự đóng yêu cầu đang chờ |
| 5 | Chế độ demo SQLite tự tạo bảng còn thiếu khi khởi động | DB demo tạo bằng EnsureCreated nên không có migration | Người đang dùng bản demo cũ không phải xóa dữ liệu |
| 6 | Cập nhật trang Chính sách hoàn tiền | Cách gửi yêu cầu chuyển từ email sang nút trên trang đơn hàng | Email vẫn là kênh dự phòng |

## 5. Khó khăn và cách xử lý

| Khó khăn | Cách xử lý |
| --- | --- |
| Bản đầu tiên trừ điểm thưởng hai lần khi tính thu nhập giảng viên (FinalPrice của từng khóa đã trừ sẵn phần điểm) | Kiểm thử tích hợp phát hiện (thu nhập 290.000đ thay vì 340.000đ); sửa lại dùng trực tiếp FinalPrice, thêm test tổng thu nhập khớp số tiền thực thu |
| Hai yêu cầu (rút tiền, hoàn tiền) gửi cùng lúc | Chỉ mục duy nhất có điều kiện (`WHERE [Status] = 'Requested'` / `'Pending'`) ở CSDL, bắt lỗi trả 409 |
| Máy phát triển thỉnh thoảng làm sập tiến trình test .NET | Chạy lại; bộ test ổn định 55/55 qua 4 lần liên tiếp |

## 6. Đánh giá so với kế hoạch

- **Đã xong:** nhóm 1 (vận hành) gồm 5 tính năng đã đề xuất.
- **Chưa xong:** nhóm 2 (tăng doanh số: wishlist, gợi ý khóa học, flash sale, combo, mua tặng, nhắc giỏ hàng); chạy thử trên SQL Server thật; cập nhật báo cáo đồ án và slide với chức năng mới.

## 7. Kế hoạch tuần sau

| Việc | Đầu ra |
| --- | --- |
| Bổ sung chức năng mới vào báo cáo đồ án (chương 2, 3) và slide | `thesis/doc`, `thesis/pdf`, `thesis/abs` |
| Chọn và làm một số tính năng nhóm 2 | Mã nguồn, kiểm thử |
| Thử chạy 5 script SQL trên SQL Server thật | Ghi kết quả vào báo cáo kiểm thử |
