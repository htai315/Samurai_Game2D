# Player: điều khiển, máu và phối hợp UI

## Thay đổi

- `PlayerMovement`: dừng ngang theo trạng thái hướng đang giữ, không theo sự kiện nhả bất kỳ phím A/D/mũi tên nào. Khi giữ hai hướng thì đứng yên; nhả một hướng sẽ đi theo hướng còn giữ. Vận tốc dọc không bị thay đổi.
- `PlayerHealth`: damage/heal phải hữu hạn và dương. Không nhận damage/heal khi chết, pause, chuyển màn hoặc respawn; damage thường vẫn giữ bất tử trong hurt. HP sau damage không âm. Các API phục hồi/max HP bỏ qua NaN/Infinity.
- Thêm `KillOutOfBounds`: chết do môi trường bỏ qua bất tử lúc hurt, không dùng con số damage giả 9999. Chỉ trừ mạng một lần, hủy coroutine hurt trước khi xử lý chết; không bỏ qua khóa pause/chuyển màn/respawn.
- `PlayerOutOfBoundsKiller`: không xử lý ngoài biên trong pause/chuyển màn/respawn/game over. Giữ trường `killByDamage` để tương thích scene; giá trị true nay dùng API chết do môi trường, false vẫn giữ lựa chọn Destroy cũ.
- `PauseSettingsMenu`: không mở khi chết, game over, chuyển màn hoặc respawn; không mở khi game đã bị pause bên ngoài. Chỉ Resume nếu menu thực sự đã mở; không tự gán timeScale ở Start. Tắt component sẽ đóng menu và giải phóng pause do nó tạo.
- `RunSession`: theo dõi `IsGameOver`, đóng pause/stats khi mở game over; bắt đầu chuyển màn xóa khóa và đóng game over. Game over không tự pause thế giới — giữ hành vi cũ — nhưng khóa player và các thao tác UI liên quan.
- `GameManager`: đóng bảng chỉ số khi pause/chết/chuyển màn/respawn/game over. Mở settings đóng bảng chỉ số ngay. Bảng chỉ số vẫn không pause gameplay như trước.
- `PlayerStats`: khóa API nâng cấp khi chết/pause/chuyển màn/respawn/game over, kể cả callback nút đến trước khi panel bị đóng.

Không đổi phím điều khiển, tốc độ, damage, số mạng, chi phí nâng cấp hoặc bố cục UI. Không thay dữ liệu scene/prefab trong đợt này.

## Kiểm thử

Các assertion bổ sung nằm trong `DamagePickupValidation.CheckPlayerControls`, chạy sau hồi quy pickup; dòng kết quả riêng có nhãn `PlayerControlsValidation`.

- Trục di chuyển trái/phải/không input và giữ vận tốc dọc.
- Damage/heal âm, 0, NaN/Infinity; giá trị HP/max HP không hợp lệ.
- Pause đóng stats, chặn sát thương/heal/chết do môi trường/nâng cấp; Resume của menu đã đóng không tự gỡ pause bên ngoài.
- Khóa chuyển màn, damage thập phân, bất tử hurt thông thường.
- Chết do môi trường trong hurt, gọi trùng không trừ thêm mạng, không heal/pause/nâng cấp khi chết.
- Game over đóng stats, không mở pause; chuyển màn giải phóng khóa.

Chạy trong bản sao dự án với định danh save riêng, không dùng save của người chơi. Log: `Logs/repair-player-controls.log`.

Kết quả: Unity 6000.0.49f1 thoát mã 0; `PlayerControlsValidation` và toàn bộ `RepairValidation` báo `ALL CHECKS PASSED`. Build C# hoàn tất với 0 lỗi, 0 cảnh báo. Các hồi quy save/checkpoint, combat, chuyển màn, AI và pickup cũng đạt.

## Giới hạn cần chơi thử

Bài kiểm tra chuyển động đặt trực tiếp giá trị input đã tổng hợp, chưa mô phỏng phím bàn phím vật lý. Cần thử A+D, A+LeftArrow, giữ một phím rồi nhả phím còn lại, Escape/P trong lúc chiến đấu và rơi khỏi biên ngay sau khi trúng đòn. Kiểm thử headless không xác nhận cảm giác animation/UI.

Chưa thêm coyote time/jump buffer, chưa thay cơ chế melee/knockback, chưa cân bằng boss. Các hệ thống pause tương lai cần cơ chế quản lý quyền pause chung nếu muốn nhiều nguồn pause chồng nhau; đợt này chỉ xử lý các luồng UI hiện có.
