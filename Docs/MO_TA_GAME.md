# Mô tả chi tiết Samurai_Game2D

## 1. Tổng quan

Samurai_Game2D là dự án hành động đi cảnh 2D một người chơi bằng Unity 6/C#.
Người chơi điều khiển samurai, vượt địa hình và các nhóm quái, thu thập tài nguyên,
nâng cấp sức mạnh rồi tiến tới trận cuối và Ending. Hướng mỹ thuật là pixel art,
tông tối/ánh trăng, kết hợp hiệu ứng kiếm, lửa, dư ảnh và UI lấy cảm hứng samurai.

Tên chương/banner tạo không khí hành trình và đối đầu bóng tối. Đây không phải
cam kết về một hệ thống hội thoại, nhiệm vụ phân nhánh hay cốt truyện tương tác
chưa được triển khai. Dự án hiện tập trung vào platforming, chiến đấu và tiến trình.

Vòng chơi chính: khám phá → vượt địa hình/đánh quái → thu thập coin/vật phẩm →
nâng cấp và quản lý máu/mana → tới checkpoint/cổng → sang màn tiếp theo.

## 2. Nội dung màn chơi

Danh sách dưới đây đối chiếu Build Settings và catalog banner hiện tại:

| Chặng | Scene | Tên chương hiển thị |
| --- | --- | --- |
| Menu | `MenuScene` | Menu bắt đầu/tiếp tục |
| I | `SampleScene` | U Minh Lâm Cảnh |
| II | `LV2-Scene2` | Hoang Sơ Huyền Huyệt |
| III | `Lv3-Scene3` | Thạch Quỷ Di Tích |
| IV | `Lv4-Scene4` | Hỏa Ma Địa Ngục |
| V | `LV5-Scene5` | Nguyệt Ảnh Thiên Lộ |
| VI | `LV6-MoonlitCitadel` | Nguyệt Quang Nguy Thành |
| Kết | `Ending` | Kết thúc hành trình |

`TutorialScene` có trong dự án nhưng đang bị tắt trong danh sách build, không phải
một bước bắt buộc của luồng chơi hiện hành. Màn VI nối sau màn V và dẫn tới Ending;
checkpoint, cổng chuyển màn và trạng thái đầu màn tham gia quản lý tiến trình.

## 3. Player và combat

### Di chuyển

Player sử dụng Rigidbody2D, phát hiện đất/vách, nhảy hai lần, hỗ trợ coyote time,
jump buffer, nhảy cao/thấp theo thời điểm nhả phím, trượt và đạp tường. Dash theo
hướng nhìn có kiểm tra địa hình, chi phí mana/hồi chiêu và miễn sát thương thường.
Tử vong môi trường được xử lý riêng, không bị vô hiệu chỉ bởi dash.

### Bộ hành động

- Combo cận chiến ba đòn và đòn điều hướng lên/xuống.
- Đòn lao xuống, phản hồi va chạm và cơ chế nảy trong điều kiện tương ứng.
- Kiếm khí, kỹ năng lửa và các projectile/hiệu ứng vùng liên quan.
- Parry đúng nhịp, phản hồi đỡ/phản xạ và thưởng mana theo loại đòn.
- Tĩnh tâm hồi máu bằng mana khi đứng đất và giữ niệm đủ thời gian.
- Nhất Kiếm Đoạt Mệnh bằng Q/O: tụ thế, lướt có kiểm tra vật cản, đánh dấu mục tiêu
  trên đoạn thực sự đi qua rồi áp dụng damage tại thời điểm kết chiêu.

Các hành động dùng quyền sở hữu chung để tránh cùng lúc tiêu mana hoặc gây damage
không hợp lệ. Dash-cancel cận chiến là ngoại lệ có chủ đích. Buffer đầu vào giữ một
lệnh hợp lệ trong khoảng ngắn; pause/hurt/chết/respawn/chuyển màn xóa lệnh chờ.
Phần đuôi đòn đánh có thể nhường cho di chuyển/lệnh tiếp theo sau khi sự kiện chém
đã phát; không bỏ qua windup hoặc nhân đôi damage từ AnimationEvent.

Chiêu O có VFX tụ kiếm, ribbon theo đường lướt, dư ảnh đỏ và chém chéo theo nhịp,
giới hạn số geometry tái sử dụng. Sprite pose vẫn là animation có sẵn, không phải
một bộ khung hình nhân vật mới được vẽ riêng. Tuyệt kỹ dùng mana/cooldown, không
phải chiêu kích hoạt bằng thanh nộ.

## 4. Địch và boss

Các prefab địch gồm Bat, Boss1, Demon, Ghost, Goblin, Golem, Mushroom và Skeleton.
Địch có cấu hình riêng về máu, khả năng tấn công, di chuyển và hiệu ứng chết.
Không suy toàn bộ hành vi chỉ từ tên GameObject; override prefab/instance vẫn
là dữ liệu tác giả của màn.

AI chia thành nhận biết không gian, tìm/giữ mục tiêu, motor di chuyển và cửa sổ
tấn công. Địa hình, tường, mép vực, khoảng cách và trạng thái mục tiêu ảnh hưởng
đến việc truy đuổi/đánh. Trạng thái choáng/chết/ngắt phải chặn đòn hoặc callback
damage không còn hợp lệ. Quyết định trạng thái AI vẫn do actor điều phối; đây
không phải tuyên bố đã thay toàn bộ game bằng một FSM framework mới.

Boss có quản lý phase/enrage, kỹ năng riêng và phần hiển thị tách biệt. Thanh máu
boss hỗ trợ phản hồi giảm máu và lifecycle khi thay boss/chuyển màn. Các kỹ năng
như lướt bóng/wave vẫn tuân thủ va chạm, single-hit và quy tắc ngắt tương ứng.

## 5. Tài nguyên, nâng cấp và persistence

Máu/mana phục vụ sống sót và thực hiện kỹ năng; số mạng phục vụ vòng chết/hồi sinh.
Coin đến từ pickup hoặc thưởng theo cấu hình và dùng mua nâng cấp/hộ phù. Các
thuộc tính gồm máu tối đa, mana tối đa, damage cơ bản và tốc độ. Hộ phù lửa, gió,
hút coin bổ sung hiệu ứng cho bộ hành động hoặc thu thập.

Ví tiền có một nguồn trạng thái; giao dịch kiểm tra điều kiện và số dư trước khi
commit, rồi mới thông báo UI. Pickup có định danh để lưu trạng thái đã nhặt và
tránh nhận thưởng lặp không chủ đích. Buff kiếm có thể tạm thời hoặc vĩnh viễn
tùy cấu hình vật phẩm.

Save schema hiện là v2, lưu JSON qua SaveSystem tại `Application.persistentDataPath`.
Snapshot gồm scene/vị trí, vitals, chỉ số, coin, mạng, hộ phù, bonus vĩnh viễn,
pickup đã nhặt và snapshot đầu màn. Có kiểm tra hợp lệ, cơ chế file tạm/backup
và xử lý save cũ được hỗ trợ. Đây là save local, không phải cloud save hay nhiều
slot đã được thiết kế trong tài liệu này.

PlayerSaveBridge chụp/áp dụng trạng thái. F5 lấy bridge của chính player sở hữu
adapter; Save & Quit/Play Again dùng PlayerContext của root chính. Checkpoint,
snapshot đầu màn và save xuống đĩa là các khái niệm khác nhau; Play Again sử dụng
mốc đầu màn, không đơn thuần giữ nguyên trạng thái sau khi chết.

## 6. Nghe nhìn và UI

Combat có hit-stop, rung camera, slash/spark, dust, dư ảnh, số damage và combo.
Âm thanh dùng AudioSource pool, clip tài nguyên nếu có và fallback tổng hợp runtime.
Các manager âm thanh/VFX được chuẩn bị lúc khởi tạo để giảm cold hitch ở hành động
đầu. Một số bụi/hiệu ứng vẫn dùng Instantiate/coroutine; không phải zero-allocation.

UI gồm HUD player, trạng thái kỹ năng, combo, boss health, viền cảnh báo ít máu,
banner chương, hướng dẫn F1, bảng nâng cấp, đền thờ và menu pause/cài đặt/lưu.
Modal có quy tắc sở hữu pause, tránh bảng này tự mở lại game khi bảng khác còn giữ.
Hit-stop là slowdown ngắn; không được ghi đè quyền pause hoặc chuyển màn.

## 7. Công nghệ và cấu trúc mã nguồn

Phiên bản dự án: **Unity 6000.0.49f1**. Package hiện có URP 17.0.4,
Cinemachine 3.1.5, Input System 1.14.2 và Unity UI. Input gameplay hiện vẫn đọc
phím qua API `Input.GetKey/GetKeyDown/GetKeyUp`; có package Input System không
đồng nghĩa mọi hành động đã chuyển sang Input Actions/remap.

| Khu vực | Trách nhiệm |
| --- | --- |
| `Assets/Scripts/Core` | Luật/model thuần C#, namespace `Samurai.Core.*`, assembly `Samurai.Core` không phụ thuộc Unity |
| `Assets/Scripts/Application` | Player/scene context, giao tiếp damage/combat, nguồn dữ liệu hiển thị, ranh giới thời gian |
| `Assets/Scripts/Player` | Điều khiển, movement/jump/dash, combat, kỹ năng, vitals/stats và save adapter |
| `Assets/Scripts/Enemy/Controllers` | Điều phối địch cùng sensor, target acquisition, navigation motor và health |
| `Assets/Scripts/Boss` | Health/phase, kỹ năng và trình bày boss |
| `Assets/Scripts/SaveGame` | Dữ liệu, đọc/ghi và trạng thái save runtime |
| `Assets/Scripts/UI` | HUD, modal, menu, chuyển màn và phiên chơi |
| `Assets/Editor` | Validator, runner và fixture phục vụ kiểm tra/hỗ trợ phát triển |
| `Assets/Scenes`, `Assets/Prefabs`, `Assets/Animation` | Dữ liệu màn, prefab và clip/controller |

Các MonoBehaviour hiện giữ type/assembly identity trong Assembly-CSharp để bảo
toàn serialized reference. GameManager và một số script Canvas/AnimationEvent cũ
vẫn là adapter tương thích có người dùng, không nên xóa chỉ vì còn tên legacy.

Ví dụ phân tách: actor quyết định damage có được nhận không → phát kết quả đã
commit → presentation cập nhật combo/âm thanh/VFX. Renderer HUD nhận snapshot,
không tự sở hữu luật combat. Đây là ranh giới rõ hơn, không phải tuyên bố toàn
bộ code đã hoàn toàn event-only hoặc không còn lookup toàn cục.

## 8. Kiểm tra và phát triển tiếp

- Mở game từ MenuScene, thử toàn luồng tới Ending, chết/respawn/checkpoint,
  Save & Quit/Continue/Play Again và các kỹ năng khi sát tường/đông mục tiêu.
- Runner Editor kiểm tra hành vi, input/gates, save, prefab, UI và baseline. Những
  runner yêu cầu batch/copy phải chạy trên bản sao cách ly, với save identifiers
  riêng; không chạy batch editor thứ hai vào dự án đang mở.
- Fixture JSON kiểm tra hash/GUID/input không phải cấu hình balance. Khi sửa code
  được bảo vệ bằng hash, ghi nhận revision được duyệt thay vì bỏ kiểm tra hoặc
  recapture toàn bộ baseline để ép test qua.
- Giữ cả file `.meta` khi di chuyển asset; không đổi GUID, tên AnimationEvent hoặc
  type MonoBehaviour tùy tiện. Không xóa tài liệu bản quyền của asset bên thứ ba.
- Giá/HP/cooldown trong prefab có thể khác default C#; đọc dữ liệu thực tế khi cân bằng.

Các test tự động đã có không thay thế playtest cảm giác hay đo hiệu năng thiết bị.
Bài FPS đã thực hiện là render ngoài màn hình có kiểm soát, AI tắt, không phải
cam kết FPS khi chơi thật đầy đủ UI/AI. Dữ liệu JSON benchmark được giữ làm tham khảo.
Chưa hoàn tất capture Profiler của phiên chơi gây khựng; không tuyên bố mọi nguồn
lag đã được xác định. Trước phát hành cần kiểm tra bản build hiển thị thật, âm
thanh, camera, hiệu năng và tính tương thích trên máy mục tiêu.
