# Giai đoạn 5 — chuyển màn và Ending

## Thay đổi

- `SceneTransition` là bộ điều phối độc lập với scene và PersistentRoot: kiểm tra màn đích, khóa yêu cầu trùng, tải bất đồng bộ, tự giải phóng khóa và overlay khi hoàn tất.
- Cổng thường/cổng tối giản, EndItemGate, New Game, Continue, Play Again, Quit, Save & Quit, TutorialEnd và Ending đều đi qua cùng bộ điều phối. Giữ nguyên điểm đến đã serialize trong scene/prefab.
- Fade dùng overlay đen riêng, tồn tại xuyên màn. CanvasGroup/Animator được gán trên cổng vẫn quyết định có fade hay không; giữ thời lượng cũ, không sửa CanvasGroup bất kỳ trong màn mới. Animator fade tùy biến không còn được phát — thay bằng fade đen chung. Cổng không cấu hình fade vẫn giữ delay cũ.
- Khóa điều khiển và tạm dừng physics nhân vật từ lúc chấp nhận chuyển màn tới hết fade-in. Khôi phục trạng thái `simulated` trước đó; không tự bật physics vốn đã tắt. Chặn cổng khi pause/chết/hurt/respawn.
- Tạm ngừng EventSystem trong lúc chuyển màn, tránh menu và HUD nhận input đồng thời khi Continue tạo root trước khi tải xong.
- Dọn root qua `PersistentRoot.Instance`, không phụ thuộc tên GameObject; dọn trước khi scene đích kích hoạt. Camera con đi theo vòng đời root, không gọi DontDestroyOnLoad sai cấp.
- Ending đăng ký/hủy đúng callback prepare, finish và error; video đã prepare có thể phát ngay; skip/disable dừng video, callback prepare tới muộn không phát lại. Thiếu video/lỗi phát chuyển sang phần cảm ơn.
- TutorialEnd không ghi save rỗng chỉ có tên scene. Không bật lại TutorialScene đang bị tắt trong Build Settings.
- Không sửa bố cục scene, điểm spawn, chỉ số chiến đấu hay dữ liệu save người dùng trong đợt này.

## Kiểm thử

Chạy `RepairValidation.Run` bằng Unity 6000.0.49f1 trong bản sao dự án `.utmp/Phase3Validation-a48f5f4e209f4cc2ae0b841f76051956`, dùng định danh ứng dụng riêng để cô lập save.

- Hồi quy các giai đoạn trước: save/migration, 179 coin, 7 enemy controller, checkpoint, rollback Play Again, Continue, mana/cooldown/combat/animation events.
- Kiểm tra điểm đến của cổng và Ending trong các scene đang bật trong Build Settings.
- `Phase5Validation`: điểm đến sai không đổi spawn/khóa nhân vật; gate khi pause/chết; hai yêu cầu liên tiếp; hủy cổng ngay khi bắt đầu; lock xuyên sceneLoaded/fade-in; physics phục hồi đúng; CanvasGroup không liên quan giữ nguyên; cổng tối giản; dọn root tên Clone; skip Ending lặp; callback prepare muộn; New Game sau Ending chỉ có một root.
- Kết quả cuối: xem `Logs/repair-phase-5-final.log` (log không đưa vào Git).

## Cần kiểm tra trực quan

Kiểm thử batch/headless không xác nhận được hình ảnh fade hay codec/âm thanh video trên bản build thật. Trong Unity, thử đi hết các cổng, kiểm tra fade đen không che HUD sau khi xong, xem trọn video Ending rồi thử skip, và bấm lại menu sau mỗi luồng. Build một bản Windows để kiểm tra video/âm thanh trước phát hành.
