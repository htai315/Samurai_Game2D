# Kế hoạch cải tiến chiến đấu, hình ảnh và câu chuyện qua thế kiếm

**Đã duyệt; đang thực hiện bước 1.** Mục tiêu là để người chơi nhìn tư thế địch để chọn cách đáp trả, nhận ra cơ hội phản công và liên hệ một dấu kiếm đầu game với trận cuối. Phạm vi đề xuất gồm cầu ở Sample, một điểm nghỉ ở màn III, hành lang hai tầng và trận cuối ở LV6, cùng phản hồi của chiêu O.

## Căn cứ và cách đọc kết quả

[Trận ở cầu](01-Giao-chien-Sample.md) đã có Skeleton ngắm bắn, Goblin lùi khi hụt và cột hai vết cắt. [Kế hoạch kể chuyện](02-Dau-kiem-sau-man.md) đã xác định hình dấu và các điểm có thể triển khai. [Chiêu O](03-Phan-hoi-chieu-O.md) đã có phép đo hụt, trúng một và trúng nhiều mục tiêu. [Hai tuyến LV6](04-Hai-loi-di-LV6.md) đã có số đo khoảng hở và bài thử di chuyển. Một số bài thử trước áp dụng cấu hình mới lên Player đóng gói cũ. Cần lấy mốc trên scene được import từ bản hiện tại để đánh giá trải nghiệm người chơi thực sự nhận được.

Các ngưỡng playtest bên dưới là tiêu chí đề xuất cho vòng thử nhỏ. Ba người mới đủ để phát hiện tín hiệu dễ hiểu nhầm, còn tỷ lệ đó chỉ mô tả nhóm đã chơi. Mỗi người chơi lượt đầu tự do. Người quan sát ghi hành động trước, hỏi sau để tránh gợi đáp án.

## Các bước thực hiện sau khi duyệt

| Bước | Công việc và phạm vi | Bằng chứng để đánh dấu hoàn thành |
|---|---|---|
| 1. Lấy mốc hiện tại | Dùng bản sao project và save thử riêng. Import scene, tạo Windows Player mới, ghi phiên bản, kích thước cửa sổ, mức âm lượng và cấu hình nhân vật. Quay từ trước cầu qua cột bằng camera gameplay. Kiểm tra các override Skeleton/Goblin được nạp từ scene. | Có clip gốc cùng kết quả build/import. Nếu Editor vướng license, ghi rõ phần bị chặn; kết quả trên Player cũ chỉ dùng để chẩn đoán. |
| 2. Kiểm tra khả năng đọc trận cầu | Cho ba người mới đi từ trước cầu qua hai trận. Ghi lúc họ thấy Skeleton ngắm, cách họ ứng phó lần kế và việc họ có tận dụng cú chém hụt của Goblin. Thử bổ sung Goblin ở đất trống, sát tường và mép vực. | Ít nhất hai trong ba người mô tả được một dấu báo của Skeleton và một cơ hội đánh trả Goblin. Địch lùi an toàn ở mép vực; stun giữa lúc ngắm hủy được phát bắn. |
| 3. Chỉnh hình và nhịp ngay tại chỗ khó đọc | Dựa trên bước 2, thử từng thay đổi riêng: dáng tay/mũi kiếm của Skeleton, độ tương phản phía sau vũ khí hoặc dáng kết thúc cú chém hụt của Goblin. Xem clip ở kích thước chơi, cả bật và tắt tiếng. Chỉ thử đổi thời gian nếu người chơi đã thấy tín hiệu mà vẫn thiếu thời gian đáp trả. | Có cặp clip trước/sau cùng camera, ghi rõ thay đổi nào giúp đọc đòn. Nếu bản gốc đạt tiêu chí, ghi kết quả và chuyển bước. |
| 4. Thu gọn hành vi lính gác | Dùng Graphify lần từ GhostController tới bộ cảm nhận, chỗ phát đạn và phản hồi hình ảnh. Bao quanh hành vi ngắm bằng các đường bắt đầu, ngắt và kết thúc rõ ràng. Tái sử dụng vị trí bắn chung. Giữ trường serialized và các điểm gọi từ animation để scene/prefab cũ tiếp tục hoạt động. | So sánh trước/sau với bắn bình thường, player khuất tầm nhìn, stun lúc ngắm, thiếu attackPoint, disable rồi bật lại và một địch thường. Mỗi lượt kết thúc với đúng số đạn; hướng báo và hướng bắn nhất quán. Cập nhật Graphify sau sửa C#. |
| 5. Dựng mạch dấu kiếm I → III → VI | Hỏi người vừa qua cầu nhớ chi tiết gì. Nếu cột bị bỏ sót, thử đổi hướng mặt cột hoặc vị trí trong khung hình. Tại điểm nghỉ màn III, dựng thử phù điêu thể hiện cùng hướng kiếm. Đối chiếu clip boss thật rồi thử tư thế mở chuỗi hai nhát, với nhát thứ hai ngắn hơn. | Ít nhất hai trong ba người nhận ra liên hệ sau trận cuối khi được hỏi mở. Người bỏ qua phù điêu vẫn đọc được cả hai đòn qua tư thế boss. Chết rồi vào lại trận phải phát tín hiệu đúng nhịp. Sau mốc này mới cân nhắc manh mối phụ ở II, IV, V. |
| 6. Chơi thử LV6 và chiêu O trong giao chiến | Từ ngã rẽ Route 13, quan sát lúc người chơi chọn đường. Đi cả hai chiều dưới dầm, thử nhảy hụt rồi quay lại và đánh ở gần mép bục. Với O, thu hình **có âm thanh** bằng camera chơi thật cho ba ca 0/1/3 mục tiêu, cả hai hướng; thêm ca sát tường và ca mục tiêu di chuyển trong nhịp kết. | Nhìn được mép đáp và phần thưởng từ ngã rẽ, nhảy hụt vẫn trở về tuyến chính. Khi ẩn số sát thương, phân biệt được hụt và trúng; người chơi thấy lại vị trí nhân vật lúc nhận lệnh tiếp. Đối chiếu mana, hồi chiêu, sát thương và thời điểm trả quyền với phép đo cũ. |
| 7. Nghiệm thu xuyên màn | Build từ đúng phiên bản bàn giao. Chạy các bộ kiểm tra sẵn có và chơi một lượt từ Sample tới Ending, gồm checkpoint, chết, hồi sinh, Save/Continue ở LV6 và vào lại boss. Đối chiếu khung hình ở cầu, lúc O nhiều mục tiêu và ở hành lang LV6 trên cùng máy/cấu hình với mốc đầu. | Bảng kết quả ghi phiên bản, ca đạt, ca lỗi, clip liên quan và mục chưa xác minh. Chỉ đánh dấu hoàn thành cho các bước đã có bằng chứng tương ứng. |

## Quyết định sản xuất

- **Ưu tiên trước mắt:** bước 1–3. Đây là phép thử ít tốn công nhất để biết nên chỉnh đồ họa, hành vi hay bố cục. Chụp mốc O và LV6 ngay ở bước 1 nếu dùng chúng để so sánh hiệu năng cuối kỳ.
- **Khi thay đổi làm trận khó đọc hơn:** trở về biến thể trước của đúng thay đổi đang thử. Giữ riêng từng thay đổi hình ảnh, timing và cấu trúc mã để xác định nguyên nhân.
- **Khi thiếu người chơi mới:** vẫn chạy được các bài kỹ thuật và dựng bản thử. Mục nhận diện đòn và dấu kiếm tiếp tục ở trạng thái chờ playtest.
- **Quyết định cần duyệt thêm nếu phát sinh:** đổi luật khóa mục tiêu của O, chỉnh cân bằng sát thương diện rộng hoặc mở rộng nội dung sang cả sáu màn. Mỗi đề xuất cần đi kèm tình huống chơi cụ thể và chi phí nội dung phát sinh.

## Theo dõi

- [ ] 1. Mốc build, cấu hình và clip gốc
- [ ] 2. Playtest hai trận ở cầu
- [ ] 3. Đọc đòn và phản công sau hiệu chỉnh
- [x] 4. Hành vi lính gác và kiểm tra hồi quy (logic Player; import scene thuộc bước 1)
- [ ] 5. Mạch dấu kiếm I → III → VI
- [ ] 6. Hai tuyến LV6 và chiêu O trong trận thật
- [ ] 7. Build bàn giao và nghiệm thu xuyên màn

## Nhật ký bước 1 và phiếu quan sát bước 2 · 19/11/2026

- [x] Đối chiếu SampleScene trên đĩa. SHA-256 F5CEBA5D6504094883A1BE0E3C352398F7EAACA687335B32A93086E78C9B04F9. Cả hai override bridgeGuard và goblinSkirmisher có giá trị 1. Có đối tượng TwinCutPost. Thời gian ngắm trong mã là 0,32 giây.
- [x] Build C# thành công, 0 lỗi và 0 cảnh báo.
- [ ] Windows Player mới và clip qua cầu. Unity 6000.0.49f1 chạy trên bản sao với định danh save riêng, kết thúc mã 1 trước khi import vì thiếu license Editor. Log tại Logs/BridgeBaselineCurrent.log. Kích thước mặc định trong project là 1920 × 1080; cần ghi kích thước thực và âm lượng khi quay.
- [ ] Quan sát ba người chơi lần đầu. Dùng cùng save xuất phát và cùng cấu hình, ghi lại trang bị, HP/mana và âm lượng. Dặn người chơi “Hãy đi qua đoạn này như bình thường”. Ghi mốc thời gian khi địch ngắm, khi người chơi phản ứng, khi Goblin chém hụt và hành động tiếp theo. Nếu chưa xuất hiện cú hụt, ghi “chưa quan sát”. Sau khi qua cột mới hỏi “Nếu chơi lại bạn sẽ làm gì khác?” và “Bạn nhớ chi tiết gì cuối cầu?”.

| Lượt | Phản ứng với Skeleton | Hành động sau khi Goblin hụt | Chi tiết tự nhớ | Clip / thời điểm |
|---|---|---|---|---|
| 1 | Chờ | Chờ | Chờ | — |
| 2 | Chờ | Chờ | Chờ | — |
| 3 | Chờ | Chờ | Chờ | — |

## Tiến độ bước 4 · 19/11/2026

- [x] Gom luồng bắt đầu, theo dõi và hủy ngắm trong GhostController. Kiểm tra hướng, tầm, tầm nhìn và khoảng cách về nhà trước khi phóng. Khi phóng, lưu hướng rồi xóa trạng thái ngắm trước khi tạo đạn.
- [x] Build C# thành công với 0 cảnh báo và 0 lỗi; đã dựng lại chỉ mục Graphify.
- [ ] Chạy hồi quy trên Player chứa bản sửa, gồm mất tầm nhìn, stun, disable/bật lại, thiếu điểm bắn và địch thường. Unity Editor vẫn báo thiếu license ở lần thử mới (Logs/BridgeBaselineRetry.log). Bước 4 đang chờ nghiệm thu runtime.
### Kết quả hồi quy lính gác trên Windows Player · 19/11/2026

- [x] Bảy tiến trình độc lập đều exit 0: bắn sau nhịp ngắm, stagger giữa lúc ngắm, mất tầm nhìn do tường, disable/bật lại giữa lúc ngắm, thiếu attackPoint, địch thường bắn trực tiếp và hồi thế sau cú chém hụt. Log: Logs/BridgeVerified-{shot,cancel,wall,disable,missing,plain,miss}.log.
- Bộ thử biên dịch lại GhostController từ mã hiện tại. Trong bản sao thử, bỏ thuộc tính SerializeField trên hai cờ hành vi mới để đọc được scene đóng gói cũ; bật hành vi bằng probe. Lần ghép nguyên layout serialized mới với dữ liệu Player cũ đã crash trước khi chạy assertion. Phải build lại bằng Editor để nghiệm thu override từ scene mới.
- Probe quan sát thời điểm xuất hiện đạn và kết quả sau ngắt. Hai lượt bổ sung đặt player ở trái và phải: tại nhịp phóng có đúng một đạn; dấu vận tốc ngang khớp phía player và hướng Skeleton. Cả hai exit 0. Log: Logs/BridgeVerified-direction-{left,right}.log.
### Bước 5 · Bản dựng đầu của phù điêu màn III · 20/11/2026

- [x] Đặt TwinCutRelief tại (-5,8; 0,71), phía phải cụm hồi sinh. Hai tư thế lấy từ clip Player_Attack1, quay mặt vào nhau trên tấm đá; hai vết giao nhau dùng lại hình và tỷ lệ tại cột màn I. Cụm chỉ gồm Transform và SpriteRenderer.
- Kiểm tra tĩnh ID, liên kết hierarchy và diff scene đạt. Chờ quan sát độ tương phản, vị trí chân bia và tầm nhìn qua camera thật. Phần tiếp theo là đối chiếu tư thế và thời điểm báo đòn với clip của boss LV6.
### Đối chiếu phù điêu với Boss1 ở LV6 · 20/11/2026

- [x] Theo tham chiếu từ LV6 qua Boss1.prefab và Boss1.controller tới Boss1_Attack. Hai hình trên bia dùng trực tiếp sprite atk_1_3 và atk_1_10 ở giây 0,133 và 0,667 của clip, trước hai sự kiện DoDamage ở giây 0,30 và 0,75. Thu tỷ lệ hình dựa trên sprite 16 pixel/đơn vị để vừa bia.
- Dùng động tác hiện có của boss làm mẫu. Hai mốc sự kiện mô tả animation; lượng sát thương thực tế vẫn do cửa sổ đánh quyết định. Chờ xem qua camera và chơi thử sau checkpoint để đánh giá người chơi có nhận ra hình tương ứng.
### Bước 6 · Chiêu O tại hành lang Route 13 · 20/11/2026

- [x] Trên đường thoáng, cả hai hướng ở hai tầng: lướt 7,5 đơn vị, mục tiêu cùng tầng còn 70/100 HP, mục tiêu tầng khác giữ 100/100, điều khiển đi bộ trở lại. Logs/LaneO-lower-*.log và Logs/LaneOClear-upper-*.log.
- Khi chum còn nguyên ở tầng trên, tiếp xúc ngang với Destructible_Ancient_Urn chặn thân player. Từ trái sang phải, mục tiêu sau chum giữ nguyên HP; từ phải sang trái, đòn trúng mục tiêu trước chum rồi player dừng lại. So sánh sau khi gọi Break trên chum gần bục đã loại bỏ hiện tượng ở cả hai hướng. Chi tiết trong Logs/LaneOContact.log.
- Dùng mã chiêu O hiện tại với Player thử cũ, áp dụng hình học mới lúc chạy, mục tiêu kiểm soát và chạy headless. Chờ nghe âm thanh và đánh giá mép đáp với camera gameplay trong trận thực.