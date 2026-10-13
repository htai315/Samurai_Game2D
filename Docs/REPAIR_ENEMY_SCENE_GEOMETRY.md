# Vùng đánh và cấu hình địch trong scene

## Nguyên nhân được tái hiện

- Skeleton SampleScene có đáy collider ở y=3,8034 trong khi mặt sàn y=4. BoxCast bắt đầu chồng địa hình, nên chặn cả hai hướng. Đây không phải mất Player reference.
- Golem SampleScene có điểm gốc cao hơn player đứng đất khoảng 1,284. Điều kiện cũ đo khoảng cách hai điểm gốc với bán kính 1 nên từ chối bắt đầu đánh, dù collider player đã nằm trong vòng tròn damage quanh attackPoint. Nhảy làm khoảng cách điểm gốc nhỏ lại và vượt qua điều kiện này.
- Fixture cũ chuẩn hóa collider/attackPoint nên không bắt được sai lệch từ asset và vị trí đặt trong scene.

## Sửa đổi

- `GhostController`: dùng chung truy vấn circle quanh attackPoint, mặt trước và line-of-sight cho quyết định bắt đầu đánh lẫn Animation Event gây damage. Không đo khoảng cách giữa hai pivot để bắt đầu đánh nữa.
- Phát hiện và line-of-sight dùng tâm collider thay vì pivot của player. Kiểm tra vật cản và mặt trước được áp dụng trước khi ghi nhận collider đã gây damage, tránh collider bị che làm mất cơ hội của collider hợp lệ khác.
- Giữ cooldown, khóa hướng trong đòn, một lần gây damage/miss, khóa pause/chết/chuyển màn. Không đổi damage, HP hoặc bán kính attackRange.
- Skeleton prefab: nâng đáy collider 0,32 đơn vị bằng cách thu chiều cao và dời tâm, giữ nguyên đỉnh, vị trí sprite và vị trí scene. Áp dụng cho cả sáu Skeleton trong năm màn. Đưa attackPoint.x từ 1,92 về 1 để bỏ khoảng trống sát người ở phía trước; vòng tròn cũng ngắn lại phía ngoài tương ứng.
- LV2: nâng hai Golem, hai Ghost và Boss1 khỏi mặt sàn; dịch Bat tại x=60,39872 sang x=58,5 để thân không chồng phần địa hình nhô cao bên phải.
- LV3: nâng Golem (1), Goblin và Mushroom khỏi mặt sàn.
- LV4: nâng Golem (1) khỏi mặt sàn.
- Các vị trí được nâng có đáy collider cách mặt sàn khoảng 0,03. Không bỏ dò tường/né vực và không tự dịch chuyển địch lúc runtime.

## Kiểm chứng

`EnemySceneGeometryValidation.Audit` được gọi từ `RepairValidation`, chỉ chạy batchmode trong bản sao dự án. Nó mở tất cả scene trong Assets/Scenes mà không lưu, sử dụng collider/scale/attackPoint/terrain thật:

- 59 địch đang hoạt động ở năm màn chơi, gồm Boss1.
- Kiểm tra di chuyển trái/phải tại vị trí đặt ban đầu; báo lỗi nếu bị chặn cả hai hướng.
- Đặt collider player thật ở hai phía, ba khoảng cách ngang và ba độ cao (đứng sàn, +0,5, +1). Những vị trí không tìm được mặt sàn được bỏ qua, không tính là đạt.
- Đối chiếu quyết định vào tầm với khoảng cách tới điểm gần nhất trên collider player, mặt trước và tầm nhìn; yêu cầu mỗi địch mặt đất có ít nhất một tình huống đứng đất đánh được.
- `EnemyBehaviorValidation` thêm kiểm tra bắt đầu đòn với attackPoint/collider prefab nguyên bản ở cả hai phía, trước các fixture chuẩn hóa và bài kiểm tra animation/event thật.

Log: `Logs/enemy-geometry-regression.log`.

Kết quả cuối: 59 địch, 1.044 tình huống hình học, 0 lỗi; không còn instance bị chặn cả hai hướng tại điểm đặt ban đầu. Cả tám prefab vượt qua kiểm thử cấu hình gốc hai phía và animation/event thật. Toàn bộ hồi quy AI, player, save/checkpoint, chuyển màn và pickup đạt; Unity thoát mã 0. Build C#: 0 lỗi, 0 cảnh báo.

Trong quá trình cập nhật hồi quy, fixture dịch Rigidbody tức thời được sửa để đồng bộ cả Transform/attackPoint và physics trước khi truy vấn. Không thay điều kiện gameplay để bỏ qua assertion cũ.

## Giới hạn

Đây là kiểm tra có hệ thống cho các trường hợp nêu trên, không chứng minh mọi vị trí trong màn đều hợp lệ. Địch bay giữ độ cao thiết kế, không tự hạ xuống đánh người đứng xa bên dưới; địch vẫn không tìm đường vòng/nhảy qua vật cản. Boss giữ bán kính đánh 10 như cũ, nên vẫn cần cân bằng riêng. Chưa xác nhận khung hình vũ khí khớp tuyệt đối vùng damage bằng chơi thử hình ảnh.
