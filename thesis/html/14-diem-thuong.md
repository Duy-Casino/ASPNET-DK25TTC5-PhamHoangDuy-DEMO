# 14 — Điểm thưởng (Reward Points)

## 1. Quy tắc mặc định

Admin chỉnh được tại **Quản trị › Điểm thưởng › Nhiệm vụ & quy tắc**.

| Nhiệm vụ (khóa) | Điểm | Điều kiện và chống gian lận |
| --- | --- | --- |
| Mua khóa học (`purchase`) | 10 điểm / 100.000đ | Chỉ cộng trong transaction xác nhận thanh toán từ **IPN đã kiểm tra chữ ký và số tiền** (`OrderFulfillmentService.MarkPaidAsync`). Trang redirect sau thanh toán không cộng điểm. Đơn 0đ không có điểm. |
| Hoàn thành khóa học (`course_completion`) | 100 | Do backend xác định từ tiến độ thật: 100% bài bắt buộc và đạt các bài kiểm tra bắt buộc, cùng điều kiện cấp chứng chỉ. Không có API nào để client tự xin điểm. Mỗi khóa chỉ một lần, kể cả khi ghi danh lại. |
| Điểm danh hằng ngày (`daily_checkin`) | 5 | Một lần mỗi ngày theo giờ Việt Nam (UTC+7). Có unique `(UserId, CheckInDate)`. |
| Điểm danh liên tục (`checkin_streak`) | 30, ngưỡng 7 ngày | Cộng mỗi khi chuỗi đạt bội số của ngưỡng (7, 14, 21…). Bỏ lỡ một ngày, chuỗi bắt đầu lại từ 1. |
| Chia sẻ khóa học (`course_share`) | 5 | Chỉ cộng khi **người khác mở** liên kết chia sẻ, tức khác tài khoản và khác IP người tạo. Tối đa một lần mỗi ngày. Mỗi người xem tính một lần. Liên kết cũ hơn 30 ngày không còn được thưởng. Bấm nút chia sẻ không tự cộng điểm. |
| Giới thiệu bạn bè (`referral`) | 50 | Người được giới thiệu đăng ký bằng mã (`?ref=` hoặc mã đã lưu khi mở liên kết chia sẻ) và **thanh toán thành công đơn có phí đầu tiên**. Mỗi người chỉ được giới thiệu bởi một người. Tự giới thiệu bị bỏ qua. |
| Đổi điểm (`redeem_voucher`) | 100 điểm = 10.000đ, tối thiểu 100 | Tạo mã giảm giá cá nhân (`Coupon.RestrictedToUserId`), dùng một lần, hạn 90 ngày. Chỉ chủ sở hữu dùng được. |
| Dùng điểm khi thanh toán (`checkout_points`) | 1 điểm = 100đ, tối đa 50% giá trị đơn | Học viên **tự chọn** có dùng hay không ở trang thanh toán. Trừ sau mã giảm giá. Điểm bị trừ khi đặt hàng và tự hoàn khi đơn bị hủy, hết hạn hoặc hoàn tiền. Xem mục 1.1. |

### 1.1. Dùng điểm trừ trực tiếp khi thanh toán

**Cách dùng:** ở trang thanh toán, mục **Điểm thưởng**, bật "Dùng điểm thưởng để giảm giá". Hệ thống tự điền số điểm tối đa dùng được. Học viên có thể nhập số điểm ít hơn rồi bấm **Áp dụng**, hoặc tắt đi để thanh toán toàn bộ bằng tiền. Hóa đơn tạm tính hiện dòng "Điểm thưởng (N điểm)".

**Cách tính** (cấu hình tại Quản trị › Điểm thưởng › Nhiệm vụ & quy tắc › "Dùng điểm khi thanh toán"):

| Tham số quy tắc | Ý nghĩa | Mặc định |
| --- | --- | --- |
| Số điểm (`Points`) và số tiền (`AmountUnit`) | `Points` điểm đổi được `AmountUnit` đồng | 1 điểm = 100đ, bằng tỷ lệ đổi mã giảm giá |
| Ngưỡng (`Threshold`) | Phần trăm tối đa của đơn (sau mã giảm giá) được trả bằng điểm, từ 1 đến 100 | 50% |

```
Giá sau mã      = Tạm tính − Giảm theo mã
Tối đa dùng được = min(Số dư, floor(floor(Giá sau mã × Ngưỡng% ) × Points / AmountUnit))
Tiền giảm        = floor(Số điểm dùng × AmountUnit / Points)
Tổng thanh toán  = Giá sau mã − Tiền giảm
```

Ví dụ khóa 390.000đ, số dư 3.000 điểm: dùng tối đa 1.950 điểm, giảm 195.000đ, còn trả 195.000đ. Nếu chỉ dùng 500 điểm thì giảm 50.000đ, trả 340.000đ.

**Vòng đời điểm của đơn:**

| Sự kiện | Xử lý điểm | Mã tham chiếu trong sổ cái |
| --- | --- | --- |
| Đặt hàng có dùng điểm | Trừ điểm trong **cùng transaction** tạo đơn. Số dư không đủ (đã tiêu ở nơi khác sau khi báo giá) thì đơn không được tạo | `checkout:{orderId}` (Spend) |
| Học viên hủy đơn chờ thanh toán | Hoàn lại đủ điểm | `checkout-restore:{orderId}` (Restore) |
| Đơn hết hạn thanh toán | Hoàn lại đủ điểm (job hết hạn, mỗi đơn một transaction) | `checkout-restore:{orderId}` (Restore) |
| IPN hợp lệ đến muộn cho đơn đã hết hạn | Đơn vẫn được ghi nhận đã thanh toán; trừ lại điểm, tối đa bằng số dư hiện có | `checkout-repay:{orderId}` (Spend) |
| Thanh toán thành công | Điểm mua hàng chỉ tính trên **số tiền thật** đã trả, không tính phần trả bằng điểm | `purchase:{orderId}` |
| Admin hoàn tiền | Hoàn lại điểm đã dùng, đồng thời thu hồi điểm mua hàng của đơn | `checkout-refund:{orderId}` (Restore) |

- Số điểm hoàn được tính từ sổ cái (đã trừ trừ đi đã hoàn), nên gọi lặp không hoàn hai lần.
- Hoàn điểm giảm "Tổng đã dùng" thay vì tăng "Tổng tích lũy", để thống kê không bị thổi phồng.
- Nếu điểm trả hết giá trị đơn (Admin đặt ngưỡng 100%), đơn 0đ hoàn tất ngay, không qua cổng thanh toán và không có điểm mua hàng.
- Phần giảm bằng điểm được phân bổ vào từng dòng đơn hàng (`DiscountAllocated`, `FinalPrice`), nên tổng `FinalPrice` luôn bằng `TotalAmount`. Doanh thu giảng viên tính theo `FinalPrice`, tức số tiền thực thu, giống cách tính với mã giảm giá.
- Frontend chỉ gửi `usePoints` và số điểm muốn dùng. Backend tự đọc số dư và quy tắc trong DB, tự tính tiền giảm. Số điểm vượt mức cho phép trả lỗi 400 kèm thông báo tiếng Việt.

### Làm tròn điểm mua hàng

`Số điểm = floor(Số tiền thanh toán hợp lệ × Điểm / Đơn vị tiền)`, tức **làm tròn xuống tới điểm nguyên**. Số tiền tính là tổng đã thanh toán sau mã giảm giá (`Order.TotalAmount`).

Ví dụ với 10 điểm/100.000đ:

| Số tiền thanh toán | Điểm |
| --- | --- |
| 390.000đ | 39 |
| 249.000đ | 24 |
| 9.999đ | 0 |

### Hoàn tiền

Admin chọn **Đơn hàng › Hoàn tiền** sau khi đã hoàn tiền cho khách qua cổng thanh toán. Hệ thống sẽ:

- Chuyển đơn sang `Refunded` và thu hồi quyền học các khóa trong đơn.
- Thu hồi điểm mua hàng của đơn.
- Thu hồi thưởng giới thiệu, nếu thưởng đó được trao nhờ đơn này.

Nếu học viên đã tiêu bớt điểm, hệ thống chỉ thu phần điểm còn lại, vì số dư không bao giờ âm. Đơn đã cấp chứng chỉ không hoàn tiền được, đúng theo chính sách hoàn tiền.

## 2. Thiết kế kỹ thuật

- **Sổ cái duy nhất:** mọi cộng/trừ đều đi qua `RewardService.ApplyAsync`, và luôn chạy trong transaction.
  - Số dư cập nhật bằng **một câu `UPDATE … SET Balance = Balance + x` nguyên tử**, không đọc-sửa-ghi.
  - Lệnh trừ có điều kiện `WHERE Balance >= x`, nên không bao giờ âm. Ngoài ra còn có check constraint `CK_UserPoints_Balance`.
- **Idempotency:** mỗi bút toán có `ReferenceKey` duy nhất theo người dùng, với unique index `(UserId, ReferenceKey)`. Ví dụ:
  - `purchase:{orderId}`
  - `checkin:2026-11-07`
  - `share:2026-11-07` (bảo đảm mỗi ngày chỉ một lần thưởng chia sẻ)
  - `course_completion:{courseId}`
  - `referral:{userId}`
  - `refund:{orderId}`
- **Đồng thời:** hai yêu cầu giống nhau gửi cùng lúc thì yêu cầu sau vi phạm unique index, transaction bị rollback và trả 409. Có test cho trường hợp điểm danh đồng thời.
- **Không tin client:** API không nhận số điểm từ client, trừ điều chỉnh thủ công của Admin. Admin phải có role và phải ghi lý do; lý do được lưu vào lịch sử và báo cho học viên.
- **Quyền:** `/api/rewards/*` yêu cầu JWT. `/api/admin/rewards/*` chỉ dành cho role Admin. Riêng `POST /api/rewards/shares/{token}/open` cho phép ẩn danh, luôn trả 204 và có giới hạn tần suất.
- **Quyền riêng tư:** không lưu IP thô. Người mở liên kết được nhận diện bằng SHA-256 của IP và User-Agent.

### Bảng dữ liệu (migration `RewardPoints`)

| Bảng | Nội dung | Ràng buộc chính |
| --- | --- | --- |
| `UserPoints` | Số dư, tổng tích lũy, tổng đã dùng | Unique `UserId`; check `Balance >= 0` |
| `PointTransactions` | Lịch sử cộng/trừ (không sửa/xóa) | Unique `(UserId, ReferenceKey)`; index theo thời gian, quy tắc |
| `PointRules` | Cấu hình nhiệm vụ | Unique `Key`; check `Points >= 0` |
| `DailyCheckIns` | Lịch sử điểm danh, chuỗi | Unique `(UserId, CheckInDate)` |
| `CourseShares` / `CourseShareOpens` | Liên kết chia sẻ, lượt mở | Unique `Token`; unique `(CourseShareId, VisitorKey)` |
| `PointRedemptions` | Lịch sử đổi điểm | Unique `CouponId`; check `Points > 0` |
| `ReferralCodes` / `Referrals` | Mã giới thiệu, quan hệ giới thiệu | Unique `Code`, `UserId`; unique `ReferredUserId` |

Các cột thêm vào bảng cũ:

- `Orders.RefundedAt`, `Orders.RefundReason`
- `Coupons.RestrictedToUserId`

Migration `CheckoutPoints` thêm `Orders.PointsUsed`, `Orders.PointsDiscountAmount`; check constraint `CK_Orders_Points` (không âm) và `CK_Orders_Math` đổi thành `TotalAmount = SubTotal − DiscountAmount − PointsDiscountAmount`.

### API

| Phương thức | Đường dẫn | Mô tả |
| --- | --- | --- |
| GET | `/api/rewards/summary` | Số dư, điểm danh, nhiệm vụ, mã giới thiệu, cấu hình đổi điểm |
| GET | `/api/rewards/transactions` | Lịch sử điểm của tôi |
| POST | `/api/rewards/check-in` | Điểm danh |
| POST | `/api/rewards/shares` | Tạo liên kết chia sẻ `{ courseId, channel }` |
| POST | `/api/rewards/shares/{token}/open` | Ghi nhận lượt mở (ẩn danh) |
| POST | `/api/rewards/redeem` | Đổi điểm `{ points }` |
| POST | `/api/orders/quote` | Báo giá; thêm `{ usePoints, points }` (points để trống = dùng tối đa). Kết quả có `pointsUsed`, `pointsDiscountAmount`, `points` (số dư, tối đa dùng được, tỷ lệ, % tối đa) |
| POST | `/api/orders` | Đặt hàng; thêm `{ usePoints, points }` |
| GET | `/api/rewards/redemptions` | Mã đã đổi |
| GET | `/api/admin/rewards/overview` · `balances` · `transactions` · `transactions/export` | Thống kê, điểm học viên, lịch sử, xuất CSV |
| GET/POST/PUT/DELETE | `/api/admin/rewards/rules` | Quản lý nhiệm vụ. Nhiệm vụ hệ thống chỉ sửa và bật/tắt, không xóa |
| POST | `/api/admin/rewards/adjustments` | Điều chỉnh thủ công `{ userId, points, reason }` |
| POST | `/api/admin/orders/{id}/refund` | Xác nhận hoàn tiền `{ reason }` |

## 3. Chạy migration, API, frontend

**SQL Server.** Migration tự áp dụng khi khởi động nếu `Database:MigrateOnStartup=true`, ví dụ khi chạy Docker. Muốn chạy tay:

```powershell
cd src\backend
dotnet ef database update -p src\OnlineLearning.Infrastructure -s src\OnlineLearning.API
```

DBA có thể chạy script idempotent `setup/database/migration-RewardPoints.sql`, rồi `setup/database/migration-CheckoutPoints.sql`. Các quy tắc mặc định được seed tự động khi API khởi động; seed chỉ thêm quy tắc còn thiếu, không ghi đè cấu hình Admin đã chỉnh.

**Chế độ demo SQLite.** Chế độ này tạo DB bằng `EnsureCreated` nên không tự nâng cấp schema. Dừng API, đổi tên hoặc xóa `src/backend/src/OnlineLearning.API/eduviet-demo.db`, rồi chạy:

```powershell
cd src\backend\src\OnlineLearning.API
dotnet run --launch-profile demo-sqlite
```

**Frontend:**

```powershell
cd src\frontend\online-learning-client
npm run dev
```

Các trang:

- Học viên: `/hoc-vien/diem-thuong`
- Admin: `/quan-tri/diem-thuong`
- Nút "Chia sẻ khóa học" nằm trên trang chi tiết khóa học.

> Khi chạy thử trên một máy, mở liên kết chia sẻ của chính mình (cùng IP) **không** được cộng điểm. Đây là cơ chế chống gian lận. Muốn thấy điểm chia sẻ, hãy mở liên kết từ thiết bị hoặc mạng khác.

## 4. Kiểm thử

- **Backend, `RewardTests`:** làm tròn điểm; IPN lặp không cộng trùng; thưởng giới thiệu; hoàn tiền thu hồi cả điểm mua hàng lẫn thưởng giới thiệu; điểm danh một lần mỗi ngày, kể cả hai yêu cầu đồng thời; chuỗi 7 ngày và reset sau khi bỏ lỡ; chia sẻ chỉ thưởng khi người khác mở và tối đa một lần mỗi ngày; hoàn thành khóa chỉ thưởng một lần; đổi điểm tạo mã cá nhân và không âm điểm; điều chỉnh thủ công bắt buộc lý do; quyền Admin; xuất CSV.
- **Backend, `CheckoutPointsTests` (5 test):** báo giá không dùng / dùng tối đa / dùng số điểm cụ thể, giới hạn theo số dư và theo %; vượt mức bị từ chối; đặt hàng trừ điểm và hủy đơn hoàn điểm (không hoàn hai lần); thanh toán chỉ cộng điểm trên tiền thật, hoàn tiền trả lại điểm đã dùng; đơn hết hạn hoàn điểm và IPN muộn trừ lại; Admin tắt tính năng, kiểm tra ngưỡng, trả 100% bằng điểm tạo đơn 0đ.
- **E2E, `rewards.spec.ts`:** học viên bật/tắt dùng điểm ở trang thanh toán, nhập quá mức bị báo lỗi, thanh toán và thấy giao dịch trừ điểm trong lịch sử.
- **E2E, `rewards.spec.ts` (các kịch bản trước):** học viên điểm danh, xem nhiệm vụ và lịch sử, chia sẻ tạo liên kết riêng, khách mở liên kết và mã giới thiệu được nhớ khi đăng ký; Admin xem tổng quan, sửa nhiệm vụ, điều chỉnh điểm có lý do, xem lịch sử, và học viên thấy được điều chỉnh.
