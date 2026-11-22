SfxLibrary โหลดเสียงจากโฟลเดอร์นี้ตาม "ชื่อไฟล์" (ไม่ต้องลากใส่ Inspector)
วางไฟล์ .wav / .ogg ชื่อตามตาราง ใส่หลายแบบได้ด้วย _1, _2, _3 ... ระบบจะสุ่มเล่นไม่ซ้ำอันเดิมติดกัน
ไม่มีไฟล์ = เงียบ (เสียง UI จะใช้เสียง Click/Hover เดิมของ AudioManager แทน)

ชื่อ                  ใช้ตอน
--------------------  ---------------------------------------------
footstep_1..N         เสียงเดินผู้เล่น (เพิ่มความหลากหลาย) และศัตรูถ้าไม่มี enemy_footstep
enemy_footstep_1..N   เสียงเดินศัตรู
enemy_alert           ศัตรูเข้าสถานะ Alert
weapon_switch         เปลี่ยนช่องอาวุธ (Tab / ปุ่มลัด)
weapon_equip          ชักอาวุธ / ใส่อาวุธจาก Inventory
weapon_unequip        เก็บอาวุธ / เปลี่ยนเป็น Unarmed
wheel_open            เปิดแถบเลือกอาวุธ (Tab)
wheel_tick            เลื่อนลูกกลิ้งเปลี่ยนแท็บ
empty_click           ไกเปล่า (แม็กหมด / ไม่มีกระสุน / X-Gear หมด)
reload_start          เริ่มรีโหลด (ถ้าปืนไม่ได้ใส่คลิปเอง)
reload_finish         รีโหลดเสร็จ
shot_ak47 / shot_usp / shot_sniper   เสียงยิง (ถ้าปืนไม่ได้ใส่คลิปเอง)
scope_in / scope_out  ซูม / เลิกซูม Sniper
headshot              ยิงโดนหัว
knife_swing / knife_hit   ฟันมีด / ฟันโดน
stealth_kill          กด F ฆ่าสำเร็จ
knockout              รัดคอสำเร็จ
player_hurt           ผู้เล่นโดนยิง
vault                 ข้ามสิ่งกีดขวาง
pickup                เสียงเก็บของเสียงเดียวของทั้งเกม (ปืน, กระสุน, ชิป, Keycard, Scrap, Metal) = สำเนาของ ArtAssets/Sounds/SFX/Weapon/EquipAK.wav (ไฟล์ของโปรเจกต์เอง ไม่ใช่ CC0)
xgear_activate        ใช้ Active Chip
xgear_deactivate      ผลของ Active Chip หมด
eyewide_on / eyewide_off   เปิด / ปิด Eye Wide (T)
zoom_in / zoom_out    ซูมกล้อง Eye Wide (ลูกกลิ้ง)
camera_servo          กล้องวงจรปิดหันสุดแล้วหยุด
camera_detect         กล้องวงจรปิดเริ่มเห็นผู้เล่น
camera_alert          กล้องวงจรปิด Alert เรียกศัตรู
menu_open / menu_close    เปิด / ปิดเมนู Esc และ Upgrade Corner
ui_hover / ui_click / ui_error   ปุ่มใน UI ใหม่
upgrade_success       อัปเกรดอาวุธสำเร็จ
craft_success         คราฟต์กระสุนสำเร็จ
chip_insert / chip_remove  ใส่ / ถอด Data ของ X-Gear
note_open / note_close     เปิด / ปิดโน้ต
door_open_1..N         ประตูบานพับเปิด (HingedDoor)
door_close_1..N        ประตูบานพับปิด
door_locked           กดประตูที่ล็อก (ไม่มีไฟล์ = ใช้ empty_click)
