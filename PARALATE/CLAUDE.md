# PARALATE_PROTO

เกม Stealth มุมกล้องตายตัว (fixed camera ต่อห้อง) สไตล์ PS1 / low–mid poly
Unity 6000.5 · URP 17.5 (ย้ายจาก HDRP แล้ว) · Cinemachine 3 · Input System · TextMeshPro

## กติกาการทำงาน (ทุก session)

- ตอบเป็นภาษาไทย คอมเมนต์ในโค้ดเขียนภาษาไทย
- ทำตามที่สั่ง แล้วอธิบายด้วยว่า "ใช้หลักการอะไร ทำยังไง"
- **ห้าม commit / push จนกว่าผู้ใช้จะสั่ง** (ผู้ใช้ขอเทสก่อนเสมอ)
- เสียงทุกไฟล์ต้องเป็น **CC0 มีเครดิต และห้ามสังเคราะห์เสียงเองทุกกรณี** — ลงเครดิตใน `Assets/Resources/Audio/CREDITS.md`
- ปุ่มทดสอบ L / [ / ] ใน `PlayTestHotKeys.cs` ห้ามแก้จนกว่าจะสั่ง
- ทางแยก session: session นี้ (Code & System) ดูแลโค้ดและระบบ ส่วน Art / Level Design / ผังแมพ อยู่อีก session — งานที่ต้องแก้โค้ดจากฝั่ง Design ให้จดเป็นรายการส่งมาฝั่ง Code

## ตรวจงาน

- Compile check โดยไม่ต้องรอ Unity: `python .claude/compile_check.py` (ต้องได้ 0 Error / 0 Warning)
- Unity Editor มักเปิดค้างไว้และต่อ Unity MCP (`.mcp.json` → relay) ใช้ `Unity_RunCommand` แทน batchmode
  - โค้ดต้องเป็น `internal class CommandScript : IRunCommand { public void Execute(ExecutionResult result) }`
  - ห้ามใช้ `System.Reflection`; ใช้ `SerializedObject` อ่าน/เขียน field ที่เป็น private
  - เช็ก `scene.isDirty` ก่อนแก้และบันทึก Scene (ถ้า dirty แปลว่าผู้ใช้มีงานค้าง ห้ามบันทึกทับ)
  - หลังแก้สคริปต์: `AssetDatabase.Refresh()` แล้วดู `Unity_GetConsoleLogs`
- ห้ามแก้ YAML ของ Scene/Prefab ด้วยมือ ให้แก้ผ่าน MCP หรือสคริปต์ Editor
- MonoBehaviour ที่บันทึกลง Scene/Prefab ต้องอยู่ในไฟล์ชื่อเดียวกับคลาส
- บอกผู้ใช้ตรงๆ ว่าอะไรยังไม่ได้เทสในเกม

## โครงโค้ด (`Assets/Scripts`)

- `EVAS/` — AI ศัตรู (AlertState, EnemyController, FieldOfView, GOAP, RoomZone), ผู้เล่น (PlayerHealth, GunAction, Knockout), Noise
- `SUN/` — กล้องและการเดิน (`Player&Cam/`: TopDownPlayerController, CameraZone, CameraFollowAnchor, WallHug, Vent), Interact (HingedDoor, Pickup), Level (SceneFlow, SceneLoader), HUD
- `Systems/` — Core (PlayerControls, SfxLibrary, CameraCutHistoryReset), UI (UIBuilder, PauseMenu, GameLog), Weapons, XGear, Enemy (SecurityCamera), Editor (auto-setup)

ระบบที่ควรรู้:
- **กล้องห้อง**: CameraZone (BoxCollider trigger) เปิด CinemachineCamera ด้วย Priority — 1 มุม = 1 vcam ตาม Anchor (CameraFollowAnchor)
  - Move Space ต่อโซน: PlayerDefault / CameraRelative / WorldAxes / CustomAxes
  - Anchor ใหม่ใช้ Height Mode = SceneHeight (เห็นใน Editor ตรงกับตอนเล่น); ของเก่าคลิกขวา "Match Runtime Height"
  - Brain ใช้ Cut; `CameraCutHistoryReset` (URP) ล้าง history ตอน Cut
  - กล้อง TPS ต่อด่าน: `PlayerTpsCameraRig` ใต้ Player (Tools > Paralate > Camera > Add TPS Camera Rig) ใช้ใน MetroCameraTest
    Priority: ห้อง 20–900 < TPS 950 < WallHug 1000 < Vent 1500 < เล็งไหล่ 1800 < Eye Wide 1900 < Sniper 2000
    `GunAction.aimStyle` Auto: Sniper = ซูมบุคคลที่หนึ่ง / USP, AK = เล็งไหล่แบบ RE4 (เฉพาะ Scene ที่มี TPS rig)
- **ศัตรู**: AlertState (Neutral → Suspicious → Alert → Chasing → Return), RoomZone เรียกกำลังเสริมในห้อง, SecurityCamera มีจุดบอดใต้กล้องและเป็น "ตา" ส่งตำแหน่งให้ศัตรูที่ถูกเรียก
  - SecurityCamera ยิงพังได้ (`maxHealth` 10, ผ่าน `IShootable` ที่ GunAction เช็กต่อจาก EnemyHealth)
  - Paranoid 2 ชั้น: รายตัว (`AlertState.paranoidLevel`) + ทั้งด่าน (`LevelParanoia`, ค่าใน `Resources/ParanoiaSettings.asset`) ทั้งด่านเริ่ม 0 ทุกครั้งที่โหลดฉาก
- **Input**: `PlayerInput.inputactions` map "Player" อ่านผ่าน `PlayerControls`
- **UI**: ผู้ใช้ต้องปรับหน้าตาใน Editor ได้เสมอ → หน้าต่างใหม่ให้ทำเป็น View + Prefab ใน `Assets/Resources/UI/`
  - View (`Systems/UI/Views/*View.cs`) = ช่องอ้างอิงปุ่ม/ข้อความ + `BuildDefault()` หน้าตาเริ่มต้น; สคริปต์การทำงานเรียก `UIPrefabs.Spawn` แล้วเติมข้อมูล/ผูกปุ่ม
  - รายการที่ซ้ำใช้ Template (วัตถุที่ปิดไว้ใน Prefab) + `UIPrefabs.Clone`
  - Prefab ปัจจุบัน: WeaponHUD, CommandLine, PauseMenu, SaveSlots (สร้าง/รีเซ็ตที่ Tools > Paralate > UI)
  - WeaponHUD และ PauseMenu วางซ้อน (Nested Prefab) อยู่ใน `Prefabs/UI/MainHUD` สคริปต์ใช้ตัวใน Scene ก่อน ไม่มีค่อย Spawn
  - ห้าม Rebuild ทับ Prefab ที่ผู้ใช้แก้แล้วโดยไม่ถาม; MonoBehaviour ใน Prefab ต้องอยู่ไฟล์ชื่อเดียวกับคลาส
  - หน้าที่ยังสร้างด้วยโค้ดล้วน (`UIBuilder`): UpgradeUI, HudToast (`HudToast.Enabled = false` ปิดทั้งเกม)
- **Main Menu**: ปุ่ม Load Game เปิด `LoadGamePanel` (Prefab SaveSlots + SaveLoadUI mainMenuMode) ใน Scene MainMenu
- **เสียง**: `SfxLibrary.Play("ชื่อไฟล์")` จาก `Assets/Resources/Audio/SFX` (ชื่อ `_1.._12` สุ่มอัตโนมัติ, ปรับดังเบาที่ Trims)
- **Scene**: ชื่อ Scene ตั้งใน `Assets/Resources/SceneFlow.asset`; `SceneLoader` เลือก Scene ใน Inspector
- **Logs/Memo**: ScriptableObject ใน `Assets/Resources/Logs` (Paralate/Log Entry), ปลดล็อกด้วย `LogUnlockTrigger` หรือ `GameLog.Unlock(id)`
- **Keycard**: เลขระดับ Lv.1+ (บัตรไม่หมด) ใช้กับ HingedDoor / ประตูล็อก
- **CommandLine** (`Systems/Command/`): Q เปิด → WASD ใส่ลูกศร → Q ยืนยัน
  - สูตรของไอเทมอยู่ใน ItemData ของแต่ละไอเทม (`Assets/Resources/Items/*.asset` ส่วน CommandLine) / สูตรที่ไม่ใช่ไอเทม (SaveGame) อยู่ใน `Resources/CommandBook.asset`
  - เก็บไอเทม/ปืน/ชิปครั้งแรก = ปลดล็อกสูตร (`CommandBook.LearnFromItem`) ขึ้นใน Esc > Logs > Command
  - CommandKind: Weapon = เปลี่ยนปืนในช่อง Weapon / Consumable = ใช้ทันที / XGearChip = ใส่ Active Chip เข้าช่อง X-Gear
- **WeaponWheel**: Tab + ลูกกลิ้ง (บนลงล่าง) X-Gear / Weapon / Knife / Unarmed — HUD ซ่อนเอง ขึ้นตอนกด Tab และค้าง 1 วิหลังเปลี่ยน
  - Weapon: ปืนชิ้นแรกที่เก็บได้เข้าช่องเอง เปลี่ยนด้วย Command ไม่มี Cooldown ไม่หายตอนหมดแม็ก รีโหลดจากกระสุนในกระเป๋า (ติดตัวข้ามด่าน `Weapon_Equipped`)
  - Knife ติดตัวตั้งแต่เริ่ม (`PickupSystem.knifePrefab` บน Player.prefab) คลิกซ้ายฟันตรงหน้า หยุดเดินระหว่างฟัน (Trigger `KnifeSlash`)
  - คลิกขวาหันตามเมาส์ได้เฉพาะตอนถือปืน (มือเปล่า/มีดไม่หัน)
- **อนิเมชั่นผู้เล่น** (`PlayerAnim.controller`): Idle / Walk / Sneak / CrouchIdle / WallHug / Run (Blend `RunBlend` SlowRun→FastRun, `isRun`) / Climb (speed = `ClimbSpeed`)
  - ปีนบันได: ตำแหน่งคำนวณจาก `Ladder` (Collider ทึบของบันได) ทุกเฟรม — ปรับ `standDistance` / `lateralOffset` / `topReachOffset` ที่ Ladder (เลือกแล้วเห็นเส้นเหลือง)
  - คลิป MigeClimbing / MigeClimbingEdge ต้องเป็น Root XZ = Center of Mass, Y = Feet, ไม่ Bake (ถ้าใช้ Original ตัวจะเยื้อง)
  - ถึงยอด: state `ClimbTopExit` (MigeClimbingEdge, ความเร็ว `topExitAnimSpeed`) เริ่มเมื่อเท้าต่ำกว่าพื้นบน `topExitStartBelowFloor` (0.95 ม.) โค้ดเลื่อนตัวตามจังหวะท่า
  - ทดสอบด้วย MCP ใน Play Mode: Unity ต้องมีโฟกัสถึงจะรับปุ่มจำลอง (ไม่มีโฟกัส = ใช้ `Application.runInBackground` + `QueuePlayerLoopUpdate` และเรียก routine ตรง)
  - วิ่งเร่งจาก walk → sprint ใน `sprintAccelerationTime`; เสียงเดินจับจังหวะเท้าแตะพื้นจากกระดูกเท้า (`PlayerAudioController.syncWithAnimation`)
- **เมนู Esc**: Inventory (ตาราง Inventory + Consumable คลิกขวา Use/Drop) / X-Gear (ชิป Active/Passive/Map) / Logs / Setting / Exit
- **Save/Load** (`Systems/Save/`): สูตร SaveGame เปิดหน้า 3 ช่อง ไฟล์ JSON ที่ `persistentDataPath/saves` (ไม่บันทึกสถานะศัตรู)
- **Takedown** (`KnockoutSystem`): F = Strangle → F แทงคอ (ศพนอนพื้น) / Ctrl ค้าง `EnemyHealth.chokeHoldSeconds` (ร่างหาย)
  - Animator Trigger: ผู้เล่น Strangle/StabKill/Choke/StrangleRelease, ศัตรู Strangled/StabbedDeath/Choked (ยังไม่มีคลิป)
- **X-Gear (Universal)**: ชิปทุกชิ้นอยู่ในคลัง (ไม่มีการติดตั้ง/Installer แล้ว) — Passive ได้ผลซ้อนตามจำนวน, Active เรียกด้วย Command แล้วคลิกซ้าย (ใช้ Charge), Map Chip = `MapArea` (กล่องโซนในฉาก id `map_*`) เข้าโซนที่มีชิป Minimap ขึ้นเอง
- **Safe Room** (`SafeRoom`): Medkit/X-Gear Cell; กล้องอยู่ใน `SafeRoom/SafeRoomCameras`

## Art & Level Design

- ผังแมพ draft v1: https://claude.ai/artifact/T4qyZ61V6wZXps11FuicK4
  สถานีรถไฟใต้ดินร้าง → คลังบริษัทเก่า → บริษัทใหม่ (ลง B3 แล็บลับ แล้ว Lockdown หนีขึ้นลานเฮลิคอปเตอร์ดาดฟ้า), Keycard Lv.1–4 คุมลำดับ
- Texture: prop เล็ก 128–256, ตัวละคร/ผิวใหญ่ 512, ตั้งที่ Max Size ใน Import Settings (ไม่ย่อไฟล์ต้นฉบับ), รักษา texel density ให้เท่ากันทั้งฉาก, ลุค PS1 ใช้ Filter Point
- ฟอนต์ UI: IBM Plex Mono, IBM Plex Sans Thai, Orbitron
- Minimap: ตอนนี้ใช้กล้องมุมบนจัดตามกล่อง `MapArea` ของแต่ละโซน — แผนเดิม (ภาพ 2D bake ต่อชั้น) ทำหลังแมพเสร็จ
- Scene ทดสอบหลัก: `HideOut`; ฉาก New Game: `Simulation 1`; ฮับ: `Base`
