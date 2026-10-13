# Sửa hành vi quay về và dò địa hình

## Phạm vi

Thay đổi `GhostController` dùng chung cho bảy prefab địch thường và Boss1, bổ sung kiểm thử trong `EnemyBehaviorValidation`. Không thay chỉ số chiến đấu, animation, vị trí scene hoặc dữ liệu save.

## Hành vi mới

- Khi quay về mà không tiến gần điểm gốc ít nhất 0,01 đơn vị trong `returnStuckDelay` (mặc định 0,75 giây), chuyển sang `GuardHome`: đứng canh và thử đường về sau `returnRetryDelay` (0,5 giây).
- Có thể phản công người chơi ở gần trong tầm đánh và tầm nhìn khi đang rút lui. Không tiếp tục đuổi ra ngoài giới hạn. Vẫn dùng cooldown và cửa sổ damage hiện có.
- Sau khi vượt giới hạn đuổi, phải trở vào vùng 80% giới hạn mới được đuổi lại. Vùng đệm này hạn chế đổi trạng thái liên tục ở ranh giới.
- Khi không còn bị buộc rút lui, có thể phát hiện và đuổi lại người chơi trước khi về đúng điểm gốc.
- Thay tia ngang từ tâm bằng BoxCast bao quanh thân collider, có khoảng inset `navigationSkin` (0,02), để nhận cả vật cản thấp. Bộ lọc bỏ qua trigger, dùng lại mảng kết quả.
- Địch mặt đất cần có sàn dưới chân và phía trước mới đi. Bat/Demon/Ghost vẫn dùng `avoidLedges = false`; việc này chỉ bỏ kiểm tra sàn, không bỏ kiểm tra tường.

## Kiểm chứng

Chạy Unity 6000.0.49f1 batchmode trong bản sao dự án với định danh save riêng. Log: `Logs/repair-enemy-navigation.log`; Unity thoát mã 0, `RepairValidation` báo `ALL CHECKS PASSED`.

Các assertion navigation bổ sung:

- Vật cản thấp chặn toàn thân; trigger không chặn.
- Không có sàn: địch mặt đất dừng, flyer vẫn đi.
- Đường về bị chặn: chuyển đứng canh, có thể bắt đầu đòn phòng thủ, không đuổi tiếp ra ngoài giới hạn.
- Khi bỏ vật cản, vận tốc yêu cầu lại hướng về điểm gốc.
- Vùng đệm giới hạn giữ trạng thái rút lui; vào vùng an toàn thì bắt lại mục tiêu.

Các bài kiểm tra dùng geometry chuẩn hóa và gọi cập nhật/truy vấn physics trực tiếp; ngưỡng kẹt được đặt chủ động trong fixture. Chưa thay thế việc chơi thử mọi vị trí địch trên bản đồ hoặc đo hành vi qua nhiều tốc độ khung hình.

Bộ hồi quy cũng chạy lại animation/event thật của tám prefab, hit/miss một lần, cooldown, pause/chết, save/continue, checkpoint/PlayAgain, kỹ năng player, chuyển màn và pickup kiếm. Build C# trước lần chạy: 0 lỗi, 0 cảnh báo.

## Giới hạn và chơi thử

- Chưa có tìm đường vòng, tự nhảy hoặc leo tầng. Chướng ngại cố định vẫn có thể khiến địch đứng canh lâu dài; không dịch chuyển địch hay tự đổi điểm gốc để che việc kẹt.
- BoxCast dùng bounds thẳng trục của collider. Cần chơi thử với kích thước collider thực tế, sàn dốc và platform; chưa khẳng định hỗ trợ platform một chiều.
- Địch kinematic đặt lơ lửng xa sàn hơn `groundProbeDistance` sẽ đứng yên nếu bật `avoidLedges`. Cần chỉnh điểm đặt/probe, không tắt tránh vực để chữa cấu hình sai của địch mặt đất.
- Giữ nguyên HP, damage, tầm đánh và tầm phát hiện, kể cả cấu hình boss hiện tại. Cân bằng sức mạnh/cảm giác animation là bước riêng.
- Chưa đo Profiler trên máy đích; không khẳng định tăng FPS. Dò toàn thân chính xác hơn tia tâm nhưng cần đo chi phí khi có nhiều địch.
