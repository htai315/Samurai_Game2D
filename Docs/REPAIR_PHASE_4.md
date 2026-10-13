# Đợt 4: điều kiện dùng skill và vòng đời hành động

## Hành vi

- Giữ nguyên J/W+J/S+J, L, I, U và các giá trị mana, damage, cooldown đã gán
  trong prefab/scene. Cooldown đánh thường vốn khai báo nhưng chưa được dùng
  nay được thực thi.
- Đánh, dash, skill lửa và kiếm khí không chạy chồng nhau. Khi đang đánh/cast,
  không đổi hướng hoặc bắt đầu nhảy/dash/skill khác.
- Không bắt đầu hành động khi chết, bị thương, hồi sinh, pause, chuyển màn,
  hoặc controller/module bị vô hiệu hóa.
- Kiểm tra prefab, điểm phát, Animator state/trigger, mana và cooldown trước
  khi chấp nhận yêu cầu. Yêu cầu bị từ chối không trừ mana hay chạy cooldown.
- Yêu cầu được chấp nhận trừ mana một lần và bắt đầu cooldown. Nếu sau đó bị
  thương/chết/chuyển màn ngắt chiêu thì không hoàn mana hay reset cooldown.
- Pause giữ nguyên hành động đang thi triển và cooldown, không phát đạn/
  sát thương trong lúc pause; resume tiếp tục hành động đó.
- Sự kiện animation chỉ được phát đạn/gây sát thương một lần cho hành động
  đang sở hữu đúng animation. Sự kiện gọi lặp hoặc đến sau khi hủy bị bỏ qua.
- Khóa hành động được giải phóng khi rời animation, kể cả không có sự kiện
  kết thúc. Có timeout nếu trigger không vào state (0,5 giây) hoặc action
  kéo dài bất thường (3 giây); các clip hiện tại đều ngắn hơn giới hạn này.
- Hai skill chuyển về Idle sau khi clip kết thúc, không bắt đầu chuyển
  trước sự kiện phát đạn như cấu hình cũ.
- Mỗi đòn chém/lần nổ/nhịp lửa diện rộng chỉ gây sát thương một lần trên
  một quái dù quái có nhiều collider. Đạn kiếm chỉ xử lý một va chạm;
  một quả cầu lửa chỉ tạo vùng lửa một lần.
- Đạn đã phát trước khi nhân vật chết vẫn tiếp tục tồn tại theo vòng đời cũ.
  Việc hủy chiêu chỉ chặn phần phát đạn chưa xảy ra.

## Kiểm tra

Runner `RepairValidation.Run` chạy lại đợt 1–3 rồi gọi `Phase4Validation`
trên bản sao dự án có thư mục save riêng.

Các ca mới: khóa theo trạng thái, chặn hành động chồng nhau, thiếu mana/
cấu hình, chi phí không hợp lệ, event animation thật của lửa/kiếm/chém,
event lặp, collider trùng, ngắt bởi sát thương/chết/chuyển màn/disable,
pause/resume, mất animation hoặc thiếu sự kiện kết thúc.

Log: `Logs/repair-phase-4-final.log` (không đưa vào Git).
Ngày 12/10/2026, Unity 6000.0.49f1: toàn bộ kiểm tra hồi quy đợt 1–4 đạt,
tiến trình Unity trả mã 0. Rebuild C# runtime/Editor: 0 lỗi, 0 cảnh báo.
Chạy headless không đánh giá được cảm giác điều khiển, hình ảnh/âm thanh.

## Chơi thử trong Editor

1. Bấm xen kẽ J/L/I/U: chỉ một hành động được nhận, mana không trừ cho nút bị chặn.
2. Pause giữa lúc cast: mana/cooldown đứng yên; resume chỉ phát một đạn.
3. Bị đánh/chết trước lúc phát đạn: chiêu bị hủy, không có đạn đến muộn.
4. Hồi sinh và Play Again: đánh/dash/skill dùng lại bình thường.
5. Thử cả 3 đòn chém, hai skill và chuyển màn khi đang cast.

Chưa triển khai đợt tái cấu trúc scene/fade.
