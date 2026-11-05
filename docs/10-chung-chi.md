# 10 — Chứng chỉ PDF & trang xác minh (Giai đoạn 6)

## 1. Điều kiện và cách cấp

- **Điều kiện (FR-CERT-01):** hoàn thành 100% bài học **bắt buộc** và **đạt** mọi bài kiểm tra bắt buộc của khóa.
- **Thời điểm cấp:** ngay khi điều kiện được thỏa, dù bước cuối là đánh dấu xong bài học hay nộp bài kiểm tra đạt (`LearningService.RecalculateAsync` gọi `CertificateService.IssueIfEligibleAsync`).
- **Idempotent:** mỗi lượt ghi danh chỉ có một chứng chỉ (unique `EnrollmentId` và `(UserId, CourseId)`). Bỏ đánh dấu một bài rồi hoàn thành lại vẫn giữ nguyên mã cũ. Hai yêu cầu đến cùng lúc sẽ bị unique index chặn bản thứ hai.
- **Mã chứng chỉ:** `EDV-YYYY-XXXXXXXX`, 8 ký tự ngẫu nhiên (bộ sinh số ngẫu nhiên an toàn), bỏ các ký tự dễ nhầm O/0/I/1/L.
- **Thông tin lưu cố định lúc cấp:** tên học viên, tên khóa học, tên giảng viên. Đổi tên sau này không làm thay đổi chứng chỉ đã cấp.
- **Điểm in trên chứng chỉ:** điểm cao nhất của bài thi cuối khóa; nếu không có bài thi cuối khóa thì lấy trung bình điểm cao nhất của các bài bắt buộc.
- Học viên nhận thông báo kèm mã chứng chỉ.
- Admin có nút **"Cấp bù chứng chỉ còn thiếu"** dành cho các lượt ghi danh đã hoàn thành trước khi tính năng này ra đời.

## 2. PDF

Sinh bằng **QuestPDF** (giấy phép Community, miễn phí cho tổ chức doanh thu dưới 1 triệu USD/năm) cùng **QRCoder**:

- Khổ A4 ngang, dải thương hiệu xanh, tên học viên cỡ lớn, khóa học, giảng viên, ngày cấp (giờ Việt Nam), điểm, thời lượng.
- Mã chứng chỉ kèm mã QR và liên kết tới `{App:ClientUrl}/xac-minh/{mã}`.
- Chứng chỉ đã thu hồi có dấu đỏ **"ĐÃ THU HỒI"** (chỉ Admin tải được bản này).
- PDF được tạo **khi tải**, không lưu tệp, nên luôn khớp với trạng thái hiện tại trong DB.

**Font tiếng Việt:** QuestPDF dùng font cài trên máy chủ, thứ tự ưu tiên Arial, Segoe UI, DejaVu Sans, Noto Sans, Liberation Sans.
- Windows có sẵn Arial.
- Linux/Docker cần cài font, ví dụ `apt-get install -y fonts-dejavu-core` hoặc `fonts-noto-core`.
- Muốn PDF giống hệt nhau trên mọi máy chủ, đặt tệp `.ttf` (ví dụ Be Vietnam Pro) vào thư mục `Fonts/` cạnh ứng dụng; font sẽ được đăng ký khi khởi động.

## 3. API

| Method | Đường dẫn | Quyền | Mô tả |
| --- | --- | --- | --- |
| GET | `/api/learning/certificates` | Học viên | Chứng chỉ còn hiệu lực của tôi |
| GET | `/api/certificates/{id}/pdf` | Chủ sở hữu / Admin | Tải PDF |
| GET | `/api/certificates/verify/{code}` | Công khai | Xác minh, không phân biệt chữ hoa/thường |
| GET | `/api/certificates/verify/{code}/pdf` | Công khai | Tải PDF bản gốc (chỉ khi chứng chỉ còn hiệu lực) |
| GET | `/api/admin/certificates` | Admin | Tra cứu theo mã, tên, email, khóa học; lọc theo tình trạng |
| POST | `/api/admin/certificates/{id}/revoke` · `/restore` | Admin | Thu hồi (bắt buộc ghi lý do) / khôi phục |
| POST | `/api/admin/certificates/backfill` | Admin | Cấp bù chứng chỉ còn thiếu |

Các endpoint xác minh công khai có giới hạn 60 yêu cầu/phút/IP để chống dò mã.

## 4. Giao diện

| Trang | Nội dung |
| --- | --- |
| `/xac-minh`, `/xac-minh/:mã` | Trang công khai (có liên kết trên menu và chân trang). Nhập mã hoặc quét QR, kết quả hiển thị như một tấm chứng chỉ: dải xanh "hợp lệ" hoặc dải đỏ "đã thu hồi" kèm lý do; tải bản PDF gốc |
| `/hoc-vien/chung-chi` | Danh sách chứng chỉ của tôi: tải PDF, mở trang xác minh, sao chép liên kết để gửi nhà tuyển dụng |
| Trình học | Hoàn thành khóa thì hiện thông báo kèm nút "Xem chứng chỉ"; đề cương có liên kết "Xem và tải chứng chỉ"; trang kết quả bài kiểm tra hiện mã chứng chỉ |
| `/quan-tri/chung-chi` | Tra cứu, tải PDF, thu hồi có lý do (học viên nhận thông báo), khôi phục, cấp bù |

## 5. Kiểm thử

30/30 test. Hai kịch bản mới:
- Học xong khóa và đạt bài kiểm tra thì có đúng một chứng chỉ với mã đúng định dạng. Hoàn thành lại không cấp thêm. PDF hợp lệ (`%PDF`). Người khác không tải được bằng id. Xác minh công khai hoạt động, mã không tồn tại trả 404. Thu hồi thì trang xác minh báo đã thu hồi kèm lý do và PDF công khai bị chặn; khôi phục được.
- Khóa chưa hoàn thành thì không có chứng chỉ; endpoint cấp bù hoạt động.

Đã chạy thử trên trình duyệt: học viên mẫu học hết khóa "Kỹ năng thuyết trình chuyên nghiệp", đạt bài kiểm tra và nhận chứng chỉ ngay. Mở PDF thấy dấu tiếng Việt, mã QR và đường dẫn xác minh hiển thị đúng.
