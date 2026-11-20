# Phân biệt đánh hụt, trúng đơn và trúng nhiều mục tiêu bằng chiêu O

**Đã triển khai và kiểm tra trên Windows Player.** Người chơi phải nhận ra kết quả của Nhất Kiếm Đoạt Mệnh qua vị trí, âm thanh và nhịp kết chiêu, kể cả khi ẩn số sát thương.

## Các bước thực hiện

- [x] Lập ba cảnh thử lặp lại được với 0, 1 và 3 mục tiêu đứng trong hành lang lướt. Ghi cùng khoảng cách camera, số mana ban đầu và thao tác giữ hướng khi kết thúc.
- [x] Dùng Graphify tra PlayerUltimateSkill và CombatFeedbackChannel. Lần theo JudgmentCutSequence từ tụ thế, đoạn lướt thực, tập mục tiêu, kết quả nhận sát thương đến lúc trả quyền điều khiển. Ghi lại thời điểm chấp nhận lệnh di chuyển và dash trong bản hiện tại để làm mốc.
- [x] Dùng game-feel soạn ba kết quả. Hụt chỉ có gió dọc đường kiếm. Trúng đơn có tiếng kim loại và điểm sáng tại đối thủ đúng nhịp xác nhận sát thương. Trúng nhiều mục tiêu để vệt cắt đi theo thứ tự trên đoạn lướt thực rồi nhấn âm trầm và rung một lần ở nhát cuối.
- [x] Dùng lại bộ vẽ đường kiếm, pool và âm thanh hiện có. Chọn mục tiêu từ các hit đã được chấp nhận. Ca đối thủ sau tường, nhiều collider hoặc đã chết phải được thử riêng. Dùng cùng quy tắc khi lướt từ phải sang trái.
- [x] Đo lại thời điểm trả điều khiển. Nếu hiệu ứng cuối còn chạy, để phần hiển thị tự kết thúc theo vòng đời của nó. Điều chỉnh âm lượng và điểm lóe trước khi cân nhắc kéo dài hit-stop.
- [x] Đối chiếu CombatUpgradeValidation và PlayerResponsivenessValidation. Thử pause ở từng pha, ngắt bởi hurt/death, disable và đổi màn. Kiểm tra màu sprite, gravity, vệt kiếm và quyền thực hiện đòn tiếp theo sau mỗi lần ngắt.

## Tiêu chí duyệt

Ba clip cùng khung hình phải cho phép nhận ra hụt, trúng một hoặc trúng nhiều khi ẩn số sát thương. Clip ba mục tiêu cho thấy đúng thứ tự hai chiều di chuyển. Vật cản chặn đường lướt cũng chặn phản hồi trúng phía sau. Đối chiếu bản trước/sau về mana, hồi chiêu, sát thương và khung đầu tiên nhận lại lệnh. Lưu kết quả cùng clip. Chạy cập nhật Graphify sau thay đổi C#.

## Kết quả · 19/11/2026

Mốc trước/sau trên cùng probe 0/1/3 địch, mỗi địch có hai collider: 20 mana, hồi 8 giây, 30 sát thương mỗi địch. Bản sau trả quyền ở khoảng 1,194–1,195 giây, ghi nhận đi tiếp và lướt ở 1,204–1,209 giây (bản trước 1,192–1,194 và 1,202–1,206). Đây là phép đo theo frame trong Player thử. Ba nhát thị giác trên ba mục tiêu xuất hiện theo hướng lướt ở khoảng 0,67 / 0,73 / 0,80 giây. Ca hụt giữ tiếng gió ở nhịp kết; ca trúng đặt lóe sáng tại mục tiêu đầu và dùng một rung kết. Cửa sổ damage và thời điểm trừ máu vẫn đồng loạt.

Đã thử vật cản, mục tiêu chết hoặc bị vô hiệu trước nhát kết, hai hướng lướt, pause ở bốn pha, hurt, tử vong, disable và chuyển màn. Clip dùng camera cố định với mục tiêu thử: Logs/Ultimate-0.gif, Ultimate-1.gif, Ultimate-3.gif. Các GIF không thu âm; chất lượng phối âm cần nghe khi chơi. Bộ validation qua Editor vẫn chờ license.
