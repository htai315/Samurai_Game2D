# Nhóm 1 — sát thương và vật phẩm kiếm

## Đã triển khai

- Vụ nổ Fireball nhận cả EnemyHealth1 và BossHealth ở object cha; mỗi mục tiêu chỉ nhận một lần dù có nhiều collider hoặc event lặp.
- SwordProjectile nhận diện qua component máu ở cha thay vì phụ thuộc tag/component ngay trên collider. Collider con không cần tag Enemy; object chỉ có tag Enemy nhưng không có máu không làm mất đạn. Bỏ qua mục tiêu đã chết.
- SwordPickup vĩnh viễn có ID riêng cho instance, lưu vào collectedPickups dưới khóa `scene:sword:id`. Continue lọc kiếm đã nhặt trước khi cho tương tác; Play Again dùng lại snapshot đầu màn nên phục hồi đồng bộ cả bonus và kiếm.
- Gán 5 ID vào đúng 5 instance Sword hiện có, không đổi vị trí hay các override khác. Mở rộng validator ID hiện tại cho cả coin và kiếm; khi lưu scene sẽ sửa ID thiếu/trùng/inherited, build từ chối ID không hợp lệ.
- Nhặt kiếm hỗ trợ collider con Player, chặn pause/chết/chuyển màn và callback lặp. Thiếu ID thì không phát thưởng để tránh tạo lỗ hổng save mới.

## Tương thích và phạm vi

- Không đổi schema/version save: dùng danh sách collectedPickups đã có. Không xóa save hay trừ permanentBonus đang lưu.
- Save cũ không ghi lịch sử kiếm, nên không thể xác định những kiếm đã nhặt trước bản sửa. Các kiếm đó có thể xuất hiện lại một lần; từ lần nhặt được ghi nhận trong bản này trở đi, Continue không phát thưởng lặp. Không suy đoán lịch sử từ tổng bonus vì bonus có thể đến từ nguồn khác.
- Chỉ lưu trạng thái kiếm vĩnh viễn; buff có thời hạn vẫn theo hành vi cũ, chưa lưu thời gian còn lại.
- Bình máu, quy tắc xuyên địa hình của kiếm khí, AI ReturnHome và điều khiển/UI chưa sửa trong nhóm này. Các nhóm tiếp theo trong bản rà soát vẫn còn chờ triển khai.

## Kiểm thử

- Build C# và chạy toàn bộ RepairValidation trong bản sao `.utmp`, dùng định danh save riêng, không đụng save người dùng.
- DamagePickupValidation: boss có hai collider con không tag; burst/event lặp; kiếm khí không bị object giả tag Enemy nuốt; pause; kiếm khí gây damage boss đúng một lần.
- Dùng Sword thật trong Lv3-Scene3: nhặt bằng collider con, chặn pause/chết, Save & Quit, Continue, gọi lại callback không cộng bonus, Play Again phục hồi kiếm và bonus đầu màn.
- Bộ test asset kiểm tra ID khi mở từng scene; hồi quy các giai đoạn trước và 8 prefab địch chạy trước nhóm test mới.
- Log: `Logs/repair-damage-pickup.log` (không đưa log vào Git).
