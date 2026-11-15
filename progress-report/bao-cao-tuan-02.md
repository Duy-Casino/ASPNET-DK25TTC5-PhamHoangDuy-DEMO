# Báo cáo tiến độ Tuần 02

| | |
| --- | --- |
| Đồ án | Website bán khóa học trực tuyến (EduViet) |
| Sinh viên | Phạm Hoàng Duy, MSSV 170125375, lớp DK25TTC5 |
| Thời gian | **Thứ Năm 12/11/2026 – Thứ Tư 18/11/2026** |
| Trạng thái | Đang diễn ra, số liệu cập nhật đến 15/11/2026 |
| Commit trong tuần | Commit "Dùng điểm thưởng trừ khi thanh toán" (15/11) |

## 1. Tóm tắt tuần

- Thêm chức năng **dùng điểm thưởng trừ trực tiếp khi thanh toán**. Học viên tự chọn có dùng hay không, và dùng bao nhiêu điểm.
- Kiểm thử: **backend 48/48, E2E 26/26** đạt (tăng 5 test backend và 1 kịch bản E2E so với cuối tuần 1).

| Chỉ số | Cuối tuần 01 | Hiện tại (15/11) |
| --- | --- | --- |
| Test backend | 43 | 48 |
| Kịch bản E2E | 25 | 26 |
| Migration CSDL | 3 | 4 (thêm `CheckoutPoints`) |

## 2. Công việc theo ngày

| Ngày | Commit | Nội dung | Kiểm thử |
| --- | --- | --- | --- |
| Thứ Năm 12/11 – Thứ Bảy 14/11 | | Không có commit | |
| Chủ Nhật 15/11 | Dùng điểm thưởng trừ khi thanh toán | Backend: báo giá có điểm, trừ điểm khi đặt hàng, hoàn điểm khi hủy/hết hạn/hoàn tiền, trừ lại khi IPN đến muộn; migration `CheckoutPoints` và script SQL. Frontend: mục "Điểm thưởng" ở trang thanh toán, dòng giảm bằng điểm ở hóa đơn và chi tiết đơn, cấu hình quy tắc ở trang Admin | 48/48 backend, 26/26 E2E |
| Thứ Hai 16/11 – Thứ Tư 18/11 | | Chưa đến tại thời điểm lập báo cáo | |

## 3. Chức năng mới: dùng điểm khi thanh toán

| Nội dung | Mô tả |
| --- | --- |
| Lựa chọn của học viên | Bật/tắt "Dùng điểm thưởng để giảm giá"; mặc định tắt. Khi bật, hệ thống điền số điểm tối đa; học viên có thể nhập ít hơn |
| Quy đổi mặc định | 1 điểm = 100đ (bằng tỷ lệ đổi mã giảm giá), tối đa 50% giá trị đơn sau mã giảm giá. Admin chỉnh được |
| An toàn | Backend tự đọc số dư và quy tắc, không tin số tiền từ client; trừ điểm cùng transaction tạo đơn; điểm tự hoàn khi đơn bị hủy, hết hạn hoặc hoàn tiền; không hoàn hai lần |
| Điểm mua hàng | Chỉ tính trên số tiền thật đã trả, không tính phần trả bằng điểm |

Chi tiết: [`thesis/html/14-diem-thuong.md`](../thesis/html/14-diem-thuong.md#11-dùng-điểm-trừ-trực-tiếp-khi-thanh-toán).

## 4. Các điều chỉnh trong tuần

| # | Điều chỉnh | Lý do | Kết quả |
| --- | --- | --- | --- |
| 1 | Thêm cách dùng điểm thứ hai bên cạnh đổi mã giảm giá | Học viên muốn trừ điểm ngay lúc mua, không phải đổi mã trước | Quy tắc mới `checkout_points`; trang Điểm thưởng có gợi ý cách dùng |
| 2 | Giới hạn mặc định 50% giá trị đơn được trả bằng điểm | Giữ phần doanh thu bằng tiền; Admin có thể nâng lên 100% | Cấu hình bằng ngưỡng của quy tắc |
| 3 | Thêm loại giao dịch điểm "Hoàn điểm" (Restore) | Phân biệt hoàn điểm đã dùng với cộng điểm thưởng, để thống kê "tổng tích lũy" không bị thổi phồng | Hiển thị "Hoàn điểm" trong lịch sử học viên và Admin |
| 4 | Ô nhập số điểm bỏ kiểm tra mặc định của trình duyệt | Trình duyệt chặn gửi form bằng tooltip tiếng Anh nên không hiện thông báo của hệ thống (phát hiện nhờ E2E) | Luôn hiện thông báo tiếng Việt từ server |

## 5. Khó khăn và cách xử lý

| Khó khăn | Cách xử lý |
| --- | --- |
| Số dư có thể thay đổi giữa lúc báo giá và lúc đặt hàng | Tính lại giá và trừ điểm trong cùng transaction tạo đơn; không đủ điểm thì không tạo đơn |
| IPN có thể đến sau khi đơn đã hết hạn và điểm đã được hoàn | Khi ghi nhận thanh toán cho đơn đã hết hạn, trừ lại điểm của đơn (tối đa bằng số dư hiện có) |
| DB demo SQLite không tự nâng cấp schema | Sao lưu và tạo lại DB demo sau khi thêm migration |

## 6. Kế hoạch còn lại của tuần

| Việc | Đầu ra |
| --- | --- |
| Viết báo cáo đồ án theo mẫu khoa | `thesis/doc/*.docx`, bản PDF trong `thesis/pdf/` |
| Thử chạy trên SQL Server thật (4 script trong `setup/database/`) | Ghi kết quả vào báo cáo kiểm thử |
| Kiểm tra CI GitHub Actions | CI đạt |
| Chốt báo cáo tuần 02 vào 18/11 | Cập nhật tệp này |
