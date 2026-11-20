# Hai cuộc chạm trán trên đường qua cầu

**Đã dựng đoạn mẫu · Đã thử tuyến đi trên Windows Player.** Bản chơi thử bắt đầu trước cầu trong SampleScene và kết thúc tại cột gỗ mang dấu kiếm. Người chơi gặp Skeleton, sau đó gặp Goblin. Thành công khi đi bộ vào từ cùng một mặt sàn đã đủ để giao chiến và hai đối thủ gợi ra hai cách xử lý khác nhau.

## Các bước thực hiện

- [x] Mở SampleScene, ghi vị trí cầu, mặt đất, hai instance gần nhất, hướng nhìn ban đầu, các override về tầm nhìn và tầm đánh. Quay một lượt với player đi bộ từ hai phía. Dùng Graphify tìm EnemyTargetAcquisition, EnemySpatialSensor, EnemyNavigationMotor và EnemyAttackWindow rồi kiểm tra collider, layer và Animation Event trên instance thật.
- [x] Dùng game-ai để lần theo thứ tự phát hiện mục tiêu → tiếp cận → đủ tầm → bắt đầu động tác → xác nhận trúng. Nếu player đứng dưới đất và nhảy xuống cho kết quả khác nhau, ghi bounds của hai cơ thể tại bước đầu tiên có kết quả khác trước khi chỉnh khoảng cách.
- [x] Dựng Skeleton giữ cầu. Khi thấy player, nó quay và tiến vào tầm. Dùng tư thế nâng khiên và kéo vai kiếm ra sau làm dấu báo đòn. Khi chém hụt, để lộ khoảng phản công trước khi nâng khiên trở lại. Chọn frame từ clip hiện có sau khi xem ở tốc độ chơi thật.
- [x] Dựng Goblin ở đoạn sau cầu. Nó áp sát, hạ người, đánh ngắn và lùi khi hụt. Trước bước lùi phải kiểm tra mặt sàn phía sau và khoảng trống của thân. Ở mép vực hoặc sát tường, hồi thế tại chỗ. Dùng game-feel để phân biệt hai động tác ngay cả khi tắt âm.
- [x] Thử bằng override dành cho đoạn mẫu. Đối chiếu các scene dùng chung prefab trước khi đưa thay đổi lên prefab gốc. Ghép cột dấu kiếm của [hạng mục cốt truyện](02-Dau-kiem-sau-man.md) ở khoảng nghỉ sau trận.

## Nghiệm thu

| Tình huống | Điều phải quan sát được |
|---|---|
| Đi bộ từ trái, phải rồi đứng yên | Cả hai quái phát hiện, tiếp cận và đánh khi đủ tầm. Player có thể né hoặc parry bằng dấu báo nhìn thấy. |
| Player trên bục, sau tường hoặc nhảy từ trên xuống | Sát thương chỉ xảy ra khi hình học và cửa sổ đánh cho phép. |
| Chém hụt gần vực, bị choáng lúc vung kiếm | Goblin có chỗ đứng sau hồi thế. Đòn bị ngắt không gây sát thương muộn. |
| Một player có nhiều collider | Một nhát hợp lệ chỉ trừ máu một lần. |

Đối chiếu EnemyBehaviorValidation, EnemySceneGeometryValidation và EnemyConfigurationValidation trong Assets/Editor. Chạy ca mới trên bản sao dự án. Đánh dấu hoàn thành khi có kết quả kiểm tra và video một lượt chơi từ đầu cầu tới cột gỗ. Nếu sửa C#, cập nhật Graphify.

## Kết quả bước 1 · 19/11/2026

| Instance trong Sample | Vị trí local ghi ở scene | Điểm đánh local khi nhìn phải | Thời điểm DoDamage trong clip |
|---|---|---|---|
| Skeleton · 11199810 | (216.87, 5.63) | (1.00, −0.47) | 0,233 giây |
| Goblin · 1520153663 | (183.29, 5.84) | (0.60, −1.06) | 0,300 giây |

Hai instance kế thừa agroRange 5, attackRange 1, chaseStopDistance 0,3 và attackCooldown 1,5 giây. Cả hai nhìn phải ở đầu lượt quan sát. Điểm đặt chân cho từng lượt được dò trên collider Ground. Theo trục X tăng, Goblin hiện đứng trước Skeleton. Khi dựng đoạn mới cần chọn lại điểm đứng và kiểm tra cả lối vào lẫn hai đầu cầu.

**Cách thử.** Trong bản sao Windows Player dùng SampleScene đã đóng gói, đưa player tới mặt đất cách tâm địch khoảng 3,4 đơn vị theo phương ngang, rồi cho đi bằng PlayerInputFrame tới gần. Mỗi lượt khởi động lại scene, không gửi lệnh nhảy. Kiểm tra đồng thời trạng thái vung đòn và máu player. Skeleton và Goblin đều vào đòn và player mất máu khi tiếp cận từ trái hoặc phải. Trong lượt ghi hình từ trái, AI có thể chuyển thẳng sang đánh giữa hai lần lấy mẫu. Kết quả này chỉ xác nhận tình huống tiếp cận gần, chưa đo quãng truy đuổi từ biên tầm nhìn hoặc tình huống hai nhân vật ở hai cao độ.

Bản ghi cục bộ: `Logs/SampleApproachBaseline.gif` (góc camera theo cặp nhân vật), kèm bốn log `Logs/Approach-*.log`. Bước kế tiếp là đo lần phát hiện đầu tiên ở ngoài biên tầm nhìn và so sánh đường đi trên đất với khi đáp xuống, trước khi tạo nhịp giữ cầu riêng cho Skeleton.

## Kết quả bước 2 · 19/11/2026

Trong bản sao Windows Player, tách từng địch khỏi các AI khác, giữ địa hình Sample và dùng collider thật. Với Skeleton, player bắt đầu ngoài tầm nhìn, đi vào rồi đứng lại ngay khi địch đuổi. Với Goblin, player giữ cùng tọa độ X cho từng cặp, bắt đầu sát sàn hoặc thả rơi từ cao hơn 2,5 đơn vị.

| Ca | Phát hiện và đuổi | Sát thương |
|---|---|---|
| Skeleton, hai phía | Chuyển Patrol → Chase tại khoảng cách tâm 4,99, so với agroRange 5. Tiếp tục áp sát khi player dừng. | Đạn trúng khi cách khoảng 4,00–4,12; sau đó chém ở 2,37–2,75. Hai lần trừ máu tách biệt. |
| Goblin, bên trái (X player 180,44) | Nhận biết cả khi ở dưới đất lẫn trên không. Hai lượt cùng bắt đầu chém khoảng 0,37 giây từ khi đặt player. | Mất máu khoảng 0,67 giây ở cả hai lượt. |
| Goblin, bên phải (X player 186,04) | Kết quả tương tự; ở lượt thả rơi, player đã chạm đất trước lúc Goblin bắt đầu chém. | Mất máu khoảng 0,67 giây ở cả hai lượt. |

Các khoảng cách trên đo giữa tâm collider, không phải bán kính hitbox. Kịch bản lấy mẫu theo frame, không khẳng định timing chính xác tuyệt đối. Log cục bộ: Logs/SkeletonEdge-{left,right}.log và bốn file Logs/IsolatedEdge-goblin-*.log. Tiếp theo dựng Skeleton giữ cầu ở instance Sample; xử lý rõ tín hiệu bắn xa đang có và tín hiệu chém gần khi thiết kế thế khiên.

## Bước 3 · Skeleton giữ cầu

Sample bật bridgeGuard trên instance 11199810. Khi ngắm bắn, Skeleton dừng lại, lóe sáng ở vũ khí trong 0,32 giây và kiểm tra lại hướng, tầm và tầm nhìn trước lúc phóng. Đòn kiếm dùng cảnh báo đỏ và clip hiện có. Chém hụt sẽ giữ tư thế cuối đòn thêm 0,38 giây sau khi thoát animation. Stagger hủy phát bắn đang chuẩn bị.

Bốn ca Windows Player đã kiểm tra bắn có báo trước, ngắt phát bắn, hồi thế sau khi hụt và địch thường. Vì bản Player cũ đóng gói scene trước khi có override, probe đặt cờ trực tiếp trên actor. Chờ kiểm tra việc nạp override và độ dễ đọc của động tác trong Editor. Bước tiếp theo: nhịp áp sát và lùi khi hụt cho Goblin.

## Bước 4 · Hành vi Goblin

Instance 1520153663 dùng goblinSkirmisher. Áp sát với tốc độ 2,9 đơn vị/giây, báo chém ở điểm đánh thấp và giữ hướng nhìn khi lùi. Khi hụt, kiểm tra cả bước lùi 0,65 đơn vị rồi kiểm tra tiếp ở mỗi nhịp vật lý. Hồi thế 0,30 giây. Windows Player trên nền kiểm thử ghi nhận lùi 0,65 ở đất trống, 0 ở sát tường và mép vực, đánh trúng không lùi, prefab thường giữ tốc độ 2. Log: Logs/GoblinStep-*.log. Bản Player cũ được bật cờ trực tiếp để thử logic; việc nhập scene mới và độ dễ đọc của động tác cần xác nhận trong Editor. Vị trí instance vẫn theo bố cục cũ, chờ ghép hai trận và cột dấu kiếm ở bước 5.

## Bước 5 · Hai trận và cột gỗ

Tile gỗ của cầu trải từ X=206 đến 229, mặt đất Y=4. Giữ Skeleton ở X=216,87, chuyển Goblin cũ sang X=233,5 và đặt cột TwinCutPost ở X=242. Cột dùng sprite gỗ của DarkForest, hai vết cắt bằng sprite Square, không gắn collider. Probe Windows đi từ X=205 qua lần lượt hai đòn tấn công rồi tới cột. Xem Logs/SampleBridgeRoute.gif và Logs/TwinCutPostPreview.png. Bản Player đã đóng gói từ trước nên probe áp dụng bố trí và cờ AI lúc chạy. Kiểm tra tĩnh xác nhận ID của hierarchy và sprite trong scene; chờ Editor có license để xác nhận import và quay bằng camera chơi chính. Chờ người chơi mới đánh giá dấu kiếm trước khi nhân rộng sang màn II.
