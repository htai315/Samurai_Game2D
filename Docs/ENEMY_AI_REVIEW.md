# Rà soát và nâng cấp AI địch

## Phạm vi thực tế

- Bảy prefab thường dùng `GhostController`: Bat, Demon, Ghost, Goblin, Golem, Mushroom, Skeleton.
- Boss trong các màn dùng `Boss1.prefab` và `Boss1.controller`. `Boss1Controller.cs` trước đây khai báo class `NewMonoBehaviourScript`, không khớp tên file. Nay class đúng tên, kế thừa cùng bộ điều khiển với địch thường; giữ GUID và các trường serialize để bảo toàn override trong scene.
- `EnemyAI.cs` là một bản AI khác, chưa được tham chiếu trong scene/prefab. Nó còn trộn bool/trigger Attack, chưa có damage, dùng MovePosition trong Update và tạo patrol GameObject phụ. Không xóa hoặc chuyển scene sang bản này.
- BossDino, Boss_Slime và Boss_Samurai không được tham chiếu trong scene/prefab hiện tại; không thay đổi những bộ asset này. Lỗi bool/trigger và thiếu event của BossDino không phải lỗi của boss đang được sử dụng.

## Vấn đề đã sửa

| Trước | Hiện tại |
| --- | --- |
| Transform.Translate trong Update dù có Rigidbody | Vận tốc ngang qua Rigidbody trong FixedUpdate, giữ vận tốc dọc |
| Ra ngoài biên tuần tra có thể đảo hướng liên tục | Đổi hướng theo biên đang đi tới, có khoảng nghỉ; trạng thái ReturnHome riêng |
| Lose-agro timer không reset khi thấy lại người chơi | Đếm khoảng thời gian mất dấu liên tục, reset khi nhìn thấy lại |
| Mất Player reference không tìm lại; không có player thì đứng bất động | Tìm lại theo nhịp 0,5 giây khi mất reference; vẫn có thể tuần tra |
| Đuổi xuyên tường/không có giới hạn đuổi | Dò Ground, line-of-sight và giới hạn khoảng cách khỏi vị trí gốc |
| Đánh rồi quay theo người chơi/chạy ngay khi người chơi rời tầm | Giữ vị trí và hướng trong toàn bộ animation đánh; chỉ đuổi lại sau hồi chiêu động tác |
| Nhiều transition attack có Exit Time = 0 | Attack chạy trọn clip, không loop, thoát ở normalized time 1 |
| Event có thể gây damage sau chết/tắt AI, hoặc lần thứ hai khi cú đầu hụt | Cần một đòn đã được chấp nhận; mỗi đòn tiêu thụ đúng một cửa sổ damage kể cả hụt |
| Dò hit bằng OverlapCircleAll tạo mảng mới; chỉ tìm component trên collider gốc | Tái sử dụng List/HashSet, tìm PlayerHealth ở parent, loại collider trùng |
| Vùng tròn có thể đánh phía sau/xuyên địa hình | Kiểm tra phía trước và line-of-sight ở thời điểm gây damage |
| Ép attackDamage về int | Truyền float cho PlayerHealth, không mất sát thương thập phân |
| HitShake rung Transform vật lý và nhiều coroutine chồng nhau | Một hiệu ứng nháy màu ngắn, không đổi vị trí/collider |
| Health reset trong Start | Khởi tạo trong Awake; damage trước Start không bị khôi phục |
| Chết chỉ tắt collider đầu tiên, phụ thuộc hoàn toàn event cuối | Tắt toàn bộ hitbox/physics, hủy đòn; địch thường có fallback dọn sau 3 giây |
| Goblin/Mushroom/Skeleton chỉ có clip chạy cả lúc đứng | Thêm Rest dùng tư thế đầu clip chạy; đổi theo isMoving, không sinh thêm sprite |
| Boss có Idle nhưng chưa nối với Walk | Nối Idle/Walk và attack từ cả hai trạng thái |

## Các lựa chọn hành vi

- Các trường cũ về HP, damage, tốc độ, khoảng tuần tra, cooldown, tầm phát hiện và tầm đánh được giữ nguyên. Không sửa bố cục hoặc override trong scene.
- `obstacleLayers`: mặc định Ground (layer 6), chỉ lấy collider solid, không lấy trigger. Nếu thêm địa hình ở layer khác phải cấu hình mask tương ứng.
- `avoidLedges`: bật cho địch mặt đất; tắt trên prefab Bat, Demon, Ghost. Không tự chuyển chúng sang bay tự do theo trục Y.
- `groundProbeDistance`: mặc định 0,5; cần chỉnh theo collider/độ cao đặt prefab. Sau đợt nâng cấp navigation, địch mặt đất chỉ đi khi dò được sàn dưới chân và phía trước; prefab đặt lơ lửng cần chỉnh vị trí/probe, không được tự coi là địch bay.
- `maxChaseDistance`: 0 nghĩa là tự lấy hai lần giá trị lớn hơn giữa patrolDistance và agroRange. Có thể đặt riêng từng prefab/instance.
- `turnWait`: nghỉ 0,2 giây khi quay đầu tuần tra.
- Đường về bị chặn: chuyển sang đứng canh và thử lại; có thể đánh mục tiêu gần trong tầm nhìn. Giới hạn đuổi có vùng đệm để tránh đổi qua lại liên tục. Chi tiết và kiểm thử bổ sung: [REPAIR_ENEMY_NAVIGATION.md](REPAIR_ENEMY_NAVIGATION.md).
- Damage vẫn phụ thuộc Animation Event `DoDamage`. State attack phải có tag `EnemyAttack` và parameter `Attack` kiểu Trigger. Bộ điều khiển có timeout để không khóa vĩnh viễn nếu animation không vào/thoát đúng.
- Ghost/Boss1 vốn có hai event DoDamage trong clip. Giữ event asset, nhưng mỗi đòn chỉ gây damage một lần, phù hợp ý định cờ hasDealtDamage cũ; cú hụt đầu không được thử trúng lại ở event sau.
- Trúng đòn không tự gây stun cho địch: chỉ đổi feedback; không thêm cơ chế stun-lock hay nerf/buff ngầm. Chết thì hủy ngay đòn đang chuẩn bị.
- Boss chưa có death clip trong controller đang dùng nên vẫn được dọn ngay khi chết; không gán clip chết từ một bộ nhân vật khác.

## Kiểm chứng

`EnemyBehaviorValidation` chạy sau bộ hồi quy giai đoạn 1–5, trong bản sao dự án và định danh save riêng.

- Dùng Animator/clip/event thật của cả 8 prefab: vào attack, damage, kết thúc và cooldown.
- Chuẩn hóa vị trí/hitbox trong bài thử để kiểm tra logic độc lập với thiết kế bản đồ; có collider con Player và scale âm của Bat.
- Bỏ invulnerability tạm thời trong phép thử duplicate để tránh che lỗi đánh nhiều lần.
- Kiểm tra khóa hướng, miss ở phía sau, event thứ hai không đổi miss thành hit, pause và chết giữa đòn.
- Kiểm tra patrol ngoài biên, leash, reset timer khi thấy lại, tìm lại player, tường/vực và trường hợp flyer.
- Kiểm tra damage âm/0, feedback không đổi Transform, tắt collider/physics khi chết và dọn xác.
- Toàn bộ hồi quy save, spawn, checkpoint, Play Again, combat người chơi và chuyển màn tiếp tục chạy.
- Log: `Logs/repair-enemy-ai-final.log` (không đưa log vào Git). Build C# không lỗi/cảnh báo; kết quả Unity và các assertion nằm trong log.

## Giới hạn và việc nên thử trong game

Đây là AI tuần tra/đuổi/đánh theo chiều ngang, chưa phải pathfinding platformer: không tự nhảy qua vực, leo tầng hay tìm đường vòng quanh tường. Các prefab hiện dùng Rigidbody kinematic + collider trigger; nâng cấp này giữ cấu hình đó, thêm truy vấn terrain thay vì chuyển toàn bộ sang vật lý động.

Chưa có đo Profiler trên bản build/máy đích, nên không khẳng định tăng FPS. Đã loại mảng overlap được tạo mỗi đòn, tìm component lặp không cần thiết và rung Transform; truy vấn terrain bổ sung cũng có chi phí cần đo nếu tăng số địch lớn.

Cần chơi thử từng màn để tinh chỉnh kích thước collider, attackPoint, mask/ground probe và cảm giác wind-up/recovery. Boss đang có attackRange 10, agroRange 5, damage override 10 hoặc 30; những giá trị này vẫn giữ nguyên và có thể gây chết trong một hit. Sau khi AI hoạt động đúng, nên cân bằng riêng boss và các thời điểm event dựa trên gameplay thực tế. Rest dùng tư thế đầu clip chạy là giải pháp không thêm asset; có thể thay bằng clip idle riêng khi có đồ họa phù hợp.
