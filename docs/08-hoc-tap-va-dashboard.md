# 08 — Học trực tuyến, kiểm tra và dashboard (Giai đoạn 4)

## 1. Học trực tuyến

| Đường dẫn | Mô tả |
| --- | --- |
| `/hoc/:slug` | Vào khóa học, tự chuyển tới bài nên học tiếp (bài gần nhất, hoặc bài đầu tiên chưa xong) |
| `/hoc/:slug/bai/:lessonId` | Video / bài viết, tài liệu tải về, đánh dấu hoàn thành, bài trước / bài sau |
| `/hoc/:slug/kiem-tra/:quizId` | Giới thiệu bài kiểm tra → làm bài (đồng hồ, tự nộp khi hết giờ) → kết quả + đáp án chi tiết + lịch sử |

- Video tự lưu vị trí xem mỗi 15 giây và khi tạm dừng; mở lại sẽ tiếp tục từ vị trí cũ. Xem hết video thì bài tự được đánh dấu hoàn thành.
- % hoàn thành = số bài **bắt buộc** đã xong / tổng số bài bắt buộc.
- Khóa học chuyển sang **Completed** khi đủ 100% bài bắt buộc **và** đạt mọi bài kiểm tra bắt buộc. Nếu bỏ đánh dấu một bài, trạng thái quay về Active. Giai đoạn 6 sẽ cấp chứng chỉ tại thời điểm này.
- Giảng viên sở hữu khóa và Admin mở trình học ở **chế độ xem trước**: xem được mọi bài, không ghi tiến độ, không làm bài kiểm tra.
- Bảo vệ nội dung: chỉ bài học thử mở cho mọi người dùng đã đăng nhập. Các bài khác trả 403 nếu chưa ghi danh. URL video có chữ ký và hết hạn sẽ làm ở Giai đoạn 5 (Cloudinary).

## 2. Bài kiểm tra

| Quy tắc | Cách thực hiện |
| --- | --- |
| Không lộ đáp án | API bắt đầu làm bài trả đề **không có** `isCorrect`; đáp án chỉ trả sau khi nộp |
| Chấm điểm ở server | Câu đúng khi tập đáp án chọn **trùng khớp** tập đáp án đúng; mỗi câu có điểm riêng |
| Giới hạn số lần (BR-10) | Mỗi lần bắt đầu tính một lượt; hết lượt trả 409; tải lại trang thì tiếp tục lượt đang làm |
| Thời gian | Hạn = lúc bắt đầu + giới hạn phút; nộp trễ quá 60 giây thì bị chấm 0 điểm; client tự nộp khi hết giờ |
| Nộp lặp | Nộp lại lượt đã nộp chỉ trả về kết quả cũ, không chấm lại |
| Toàn vẹn lịch sử | Khi đã có người làm, giảng viên chỉ sửa được thông tin chung và lời giải thích, không sửa được câu hỏi/đáp án, và không xóa được bài |
| Trộn câu hỏi | Trộn theo lượt làm (giữ nguyên thứ tự khi tải lại trang) |

Loại câu hỏi: một đáp án, nhiều đáp án, đúng/sai (đúng 2 đáp án, 1 đáp án đúng). Mỗi câu có 2–10 đáp án.

## 3. Dashboard

| Khu vực | Đường dẫn | Nội dung |
| --- | --- | --- |
| Học viên | `/hoc-vien` | Số liệu (khóa đã mua, hoàn thành, số bài, bài kiểm tra đạt, tổng chi), ô "Tiếp tục học", khóa học theo trạng thái kèm tiến độ |
| | `/hoc-vien/ket-qua`, `/hoc-vien/chung-chi` | Lịch sử làm bài; danh sách chứng chỉ (PDF ở Giai đoạn 6) |
| | `/tai-khoan`, `/tai-khoan/don-hang` | Hồ sơ, lịch sử thanh toán |
| Giảng viên | `/giang-day` | Doanh thu, học viên, khóa học, đánh giá; biểu đồ doanh thu và lượt đăng ký 12 tháng; bảng theo khóa học kèm tiến độ trung bình |
| | `/giang-day/khoa-hoc[/:id]` | Danh sách và trang soạn khóa học: thông tin, chương/bài (sắp xếp, học thử, bắt buộc, tài liệu), bài kiểm tra; gửi duyệt / chuyển về nháp / xóa |
| | `/giang-day/hoc-vien`, `/giang-day/danh-gia` | Học viên và tiến độ; trả lời đánh giá |
| Admin | `/quan-tri` | Doanh thu, đơn hàng, người dùng, khóa học, lượt ghi danh, chứng chỉ; 3 biểu đồ 12 tháng |
| | `/quan-tri/khoa-hoc` | Duyệt / từ chối có lý do / gỡ khóa học / đặt nổi bật |
| | `/quan-tri/nguoi-dung`, `/danh-muc`, `/don-hang`, `/ma-giam-gia` | Phân quyền, khóa tài khoản; CRUD danh mục và mã giảm giá; tra cứu đơn hàng |

Biểu đồ dùng Recharts: chuỗi chính màu `brand-600`, cột màu `accent-500`, trục và lưới màu trung tính, tooltip định dạng tiền Việt Nam.

## 4. API mới

| Method | Đường dẫn |
| --- | --- |
| GET | `/api/learning/summary`, `/api/learning/courses?filter=all\|in-progress\|not-started\|completed` |
| GET | `/api/learning/courses/{slug}`, `/api/learning/lessons/{id}` |
| POST/DELETE | `/api/learning/lessons/{id}/complete` |
| PUT | `/api/learning/lessons/{id}/position` |
| GET · POST | `/api/learning/quizzes/{id}` · `/api/learning/quizzes/{id}/attempts` |
| POST · GET | `/api/learning/attempts/{id}/submit` · `/api/learning/attempts/{id}` |
| GET | `/api/learning/quiz-results`, `/api/learning/certificates` |
| GET | `/api/instructor/overview`, `/api/instructor/students` |
| GET/POST | `/api/instructor/courses/{courseId}/quizzes` |
| GET/PUT/DELETE | `/api/instructor/quizzes/{id}` |

## 5. Dữ liệu mẫu bổ sung

Mỗi khóa học đã xuất bản có một "Bài kiểm tra cuối khóa" gồm 5 câu, đạt 70%, tối đa 3 lượt, giới hạn 15 phút. Mỗi lượt ghi danh mẫu đã hoàn thành sẵn một phần bài học.
Muốn tạo lại dữ liệu demo thì xóa `backend/src/OnlineLearning.API/eduviet-demo.db` rồi chạy lại API.
