# Báo cáo tiến độ số 03

| | |
| --- | --- |
| Thời gian | 07/11/2026 |
| Sinh viên | Phạm Hoàng Duy, MSSV 170125375, lớp DK25TTC5 |
| Commit | `86e19f8`, `85bd72d`, `59a4c9c` |

## 1. Mục tiêu

Tăng uy tín giảng viên, cho học viên trao đổi trực tiếp với giảng viên, và xây dựng hệ thống điểm thưởng để giữ chân người học.

## 2. Công việc đã thực hiện

**Tính năng giảng viên** (migration `InstructorProfileAndMessaging`)
- Hồ sơ giảng dạy: số năm kinh nghiệm, chuyên môn, quá trình làm việc, học vấn, chứng nhận, liên kết; trang công khai làm lại.
- Trang chỉnh sửa hồ sơ `/giang-day/ho-so`: tải ảnh đại diện, validation theo từng ô.
- Tin nhắn 1-1 học viên và giảng viên: chưa đọc/đã xem, thông báo, giới hạn chống spam.
- Uy tín: Admin xác minh giảng viên (huy hiệu); thống kê từ dữ liệu thật (đánh giá, chứng chỉ đã cấp, tỷ lệ và thời gian trả lời).

**Hình ảnh**
- Ba thẻ lợi ích trên trang chủ có hình minh họa (học thử, đồng bộ tiến độ, chứng chỉ có QR).
- Ảnh đại diện giảng viên ảo (ảnh minh họa CC0, người không có thật), kích thước cân đối.

**Điểm thưởng** (migration `RewardPoints`)
- Tích điểm: mua khóa học (10 điểm/100.000đ, làm tròn xuống), hoàn thành khóa, điểm danh hằng ngày, chuỗi 7 ngày, chia sẻ khóa học (chỉ khi người khác mở), giới thiệu bạn bè.
- Đổi điểm lấy mã giảm giá cá nhân. Hoàn tiền đơn hàng thì thu hồi quyền học và điểm tương ứng.
- Trang học viên "Điểm thưởng của tôi"; trang Admin: tổng quan, điều chỉnh điểm có lý do, lịch sử, xuất CSV, cấu hình nhiệm vụ.
- Bảo mật: mọi giao dịch điểm xử lý ở backend trong transaction; cập nhật số dư nguyên tử, không âm; chống cộng trùng bằng unique `(UserId, ReferenceKey)`.

## 3. Kết quả

- Sau tính năng giảng viên: backend **35/35**, E2E **23/23**.
- Sau hình minh họa: backend **36/36**, E2E **23/23**.
- Sau điểm thưởng: backend **43/43**, E2E **25/25**.
- Tài liệu: `thesis/html/14-diem-thuong.md`; script SQL cho từng migration trong `setup/database/`.

## 4. Khó khăn và cách xử lý

| Khó khăn | Cách xử lý |
| --- | --- |
| Chống gian lận điểm chia sẻ | Chỉ cộng khi tài khoản khác và IP khác mở liên kết; nhận diện người mở bằng SHA-256 của IP và User-Agent, không lưu IP thô; tối đa một lần mỗi ngày |
| Hai yêu cầu điểm danh gửi cùng lúc có thể cộng hai lần | Unique index theo ngày; yêu cầu sau bị rollback và trả 409; có test đồng thời |
| Ngày điểm danh lệch múi giờ | Tính ngày theo giờ Việt Nam (UTC+7) |
| Chế độ demo SQLite không tự nâng cấp schema | Ghi rõ trong tài liệu: xóa DB demo để tạo lại khi có migration mới |
| Tiền hiển thị sai định dạng ("100,000đ") | Dùng định dạng vi-VN thống nhất |
| Dịch vụ tạo ảnh AI hết lượt | Dùng ảnh minh họa giấy phép CC0, tự lưu trong `public/avatars/demo` |

## 5. Kế hoạch tiếp theo

- Hoàn thiện README và tổ chức repository theo quy định đồ án.
- Mời cộng tác viên theo dõi tiến độ trên GitHub.
