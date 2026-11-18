# abs: Báo cáo trình bày trước Hội đồng

Bài thuyết trình đồ án **Xây dựng website chia sẻ khóa học trực tuyến** (EduViet), có giọng thuyết minh.

| Tệp | Nội dung |
| --- | --- |
| `ThuyetTrinh_PhamHoangDuy_170125375.pptx` | 15 slide, đã nhúng âm thanh thuyết minh từng slide; trình chiếu (F5) thì tự đọc và tự chuyển slide. Lời thuyết trình đầy đủ nằm trong ghi chú (Notes) của mỗi slide |
| `ThuyetTrinh_PhamHoangDuy_170125375.mp4` | Video xuất từ bài trình chiếu (1080p, 9 phút 16 giây, khoảng 16 MB), xem được mà không cần PowerPoint |

## Bố cục slide

| # | Slide | Nội dung chính |
| --- | --- | --- |
| 1 | Tiêu đề | Đề tài, sinh viên, GVHD TS. Nguyễn Nhứt Lam |
| 2 | Nội dung trình bày | 5 phần |
| 3 | Lý do và mục tiêu | Thực trạng, 4 mục tiêu |
| 4 | Công nghệ và kiến trúc tổng thể | ASP.NET Core 10, EF Core, React 19, VNPay/MoMo, Docker |
| 5 | Clean Architecture | 4 tầng Domain, Application, Infrastructure, API |
| 6 | Chức năng theo 4 vai trò | Khách, học viên, giảng viên, quản trị viên |
| 7 | Cơ sở dữ liệu | 41 bảng, 5 nhóm, ERD nhóm khóa học |
| 8 | Soạn và chia sẻ khóa học | Tạo → soạn nội dung → gửi duyệt → Admin duyệt; Nháp → Chờ duyệt → Xuất bản |
| 9 | Mua khóa học và thanh toán | Mã giảm giá, điểm thưởng, IPN có chữ ký, ghi danh tự động |
| 10 | Học và chứng chỉ | Trình học video, bài kiểm tra, chứng chỉ PDF có QR, xác minh công khai |
| 11 | Điểm thưởng | Cách nhận điểm, 1 điểm = 100đ, tối đa 50%, tự hoàn điểm |
| 12 | Bảo mật | JWT, phân quyền, băm mật khẩu, chữ ký giao dịch, kiểm tra tệp, giới hạn tần suất |
| 13 | Kiểm thử và số liệu | 48/48 backend, 26/26 E2E, 138 endpoint, 41 bảng |
| 14 | Đa thiết bị, triển khai, GitHub | Giao diện điện thoại, Docker Compose, quản lý mã nguồn |
| 15 | Kết luận | Đạt được, hạn chế, hướng phát triển, lời cảm ơn |

## Cách làm

- Slide dựng bằng `pptxgenjs` (theme EduViet: xanh `005CF8`, cam `EF8000`, nền tối `0B1B3A`; tiêu đề Cambria, nội dung Calibri). Hình lấy từ `thesis/doc/hinh/`.
- Giọng đọc: giọng nam tiếng Việt `vi-VN-NamMinhNeural` (Microsoft Edge TTS), tốc độ −8%, sinh từng câu rồi ghép, mỗi câu cách nhau 0,35 giây.
- Âm thanh được nhúng bằng PowerPoint: phát khi vào slide, ẩn biểu tượng loa, tự chuyển slide sau khi đọc xong khoảng 1,5 giây. Video xuất bằng chức năng *Export → Create a Video* của PowerPoint, dùng thời gian và thuyết minh đã ghi.

## Sử dụng khi bảo vệ

- Thuyết trình trực tiếp: mở `.pptx`, vào *Slide Show → Set Up Slide Show*, chọn *Manually* để tự bấm chuyển slide, và tắt *Play narrations* nếu tự nói; nội dung nói nằm trong Notes (Presenter View).
- Phát tự động: mở `.pptx` rồi nhấn F5, hoặc phát tệp `.mp4`.
