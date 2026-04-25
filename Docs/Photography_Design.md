# 攝影系統設計文件

> 本文件為賽道攝影模擬器的攝影系統設計規格。
> 開發相關功能前請先閱讀本文件理解需求；完成新功能後請同步更新本文件。

**相關文件：** [[Photography_Controls|操作手冊]] · [[BikeAI_MotoGP_Design|BikeAI 設計文件]]

---

## 一、目標

模擬真實賽道攝影師的拍攝體驗：自由移動攝影機、操作專業相機參數（光圈、快門、ISO、鏡頭）、追焦 AI 摩托車、按下快門擷取畫面。重點在於「操作手感像真實相機」。

---

## 二、系統架構總覽

```
TrackPhotoRig ([ExecuteAlways])
  ├── 鏡頭系統：雙鏡頭切換（SwitchLens）、平滑變焦（SetTargetFocal）、畸變、暗角
  ├── Physical Camera：感光元件尺寸、焦距、光圈、快門、ISO
  ├── AF-C 連續對焦：主體速度預測、對焦範圍限制
  ├── 搖攝輔助（Pan Assist）：panAssist（使用者意圖）+ panAssistSystemPaused（系統暫停）
  ├── 曝光系統：快門優先 Auto ISO、ND 濾鏡、測光
  ├── 連拍驅動模式：Single / ContinuousLow / High / Custom
  ├── 手震模擬：雙頻 Perlin noise
  ├── 滾動快門近似：投影矩陣剪切
  └── OnShutter 事件 → 觸發擷取管線

ViewfinderOverlay（觀景窗繪製，獨立組件）
  ├── IMGUI 繪製：三分線、中心十字、水平線、安全框
  ├── 對焦框：合焦時綠色、未合焦時紅色 + 對焦距離顯示
  ├── 反光鏡遮黑（讀取 TrackPhotoRig.MirrorBlackoutUntil）
  └── 各元素可獨立開關

PhotoRigController（唯一輸入層）
  ├── 鍵盤操控所有 Rig 參數（全部透過 API 方法）
  ├── 直接呼叫 SwitchLens()（無反射）
  ├── 滾輪變焦透過 SetTargetFocal()
  └── Tab/BackQuote 切換追焦目標（整合 BikeAITrafficManager）

PhotoFreeCam（自由視角）
  ├── WASD 移動 + 滑鼠視角（不處理變焦）
  ├── 移動時呼叫 PauseSystemPanAssist()
  └── 靜止後呼叫 ResumeSystemPanAssist()

TrackPhotoHUD（IMGUI）
  └── 底部狀態列：快門速度、光圈、曝光補償、焦距

PhotoCaptureManager（擷取管理）
  ├── 訂閱 OnShutter 事件
  ├── 管理 RTHandle 生命週期
  ├── 排程 CopyColorAfterPost 擷取
  └── AsyncGPUReadback → Texture2D → OnPhotoReady

CopyColorAfterPost（HDRP CustomPass）
  ├── Injection Point = After Post Process
  ├── 實例化設計（支援多相機）
  └── Blit cameraColorBuffer → RTHandle

PhotoPrintSpawner（照片輸出）
  └── 在世界空間生成 Quad 顯示拍攝結果

PhotoScoreEvaluator（照片評分）
  ├── 構圖評分：主體三分點距離 + 邊緣懲罰
  ├── 曝光評分：ISO 過高懲罰 + 曝光補償偏離
  ├── 銳利度評分：安全快門規則 + 搖攝補償
  └── 對焦評分：景深計算合焦判定

PhotoGallery（照片瀏覽）
  ├── F1 開關圖庫（暫停遊戲）
  ├── 縮圖一覽 + 點擊全螢幕
  ├── EXIF 資訊 + 評分明細
  └── 方向鍵導覽 + Delete 刪除
```

---

## 三、已完成功能

### 3.1 Physical Camera 基礎 ✅
- 感光元件尺寸（36×24mm full-frame）
- 焦距、光圈、快門速度、ISO 寫入 Unity `Camera` 組件
- `[ExecuteAlways]`：Editor 與 Play mode 皆可預覽

### 3.2 鏡頭系統 ✅
- 雙鏡頭預設：24-70mm f/2.8-4.0 / 100-400mm f/4.5-6.3
- 鍵盤 `1`/`2` 切換，滾輪變焦
- 平滑變焦插值（`smoothZoom` + `zoomLerp`）
- 焦距相依最小光圈（變焦鏡頭暗角模擬）
- HDRP Volume Override 連動：Lens Distortion + Vignette 隨焦距變化

### 3.3 AF-C 連續對焦 ✅
- 追蹤 `subject` Transform 距離
- 主體速度預測（`afLeadSec`）
- 對焦範圍限制（`afMinM` ~ `afMaxM`）
- 手動對焦：指定距離 / 世界座標 / 螢幕點擊 Raycast

### 3.4 搖攝輔助（Pan Assist）✅
- 攝影機自動追蹤 `subject` 方向
- `panStrength` 控制鎖定程度（0~1）
- 最大轉速限制（`panMaxYawSpeedDeg`）
- `PhotoFreeCam` 移動滑鼠時暫停，靜止後延遲恢復

### 3.5 曝光系統 ✅
- **快門優先模式**：固定快門速度，Auto ISO 補償曝光
- **測光**：從 `mainLight.intensity` 經 `luxToEV100` AnimationCurve 推算場景 EV100
- **ISO 計算**：由光圈/快門推算所需 ISO，`meterWeight` 平滑
- **ND 濾鏡**：0~8 stops，兩種模式（減光 / ISO 補償）
- **曝光補償**：±EV，疊加在後處理 Exposure compensation
- **Motion Blur 連動**：快門速度自動調整 HDRP MotionBlur intensity

### 3.6 連拍驅動模式 ✅
- Single / ContinuousLow (5fps) / ContinuousHigh (15fps) / Custom
- Burst 張數限制（`burstLimit`）
- 反光鏡遮黑模擬（`blackoutSec`）

### 3.7 手震模擬 ✅
- 雙頻 Perlin noise（低頻晃動 + 高頻抖動）
- 焦距越長放大越多（`shakeFocalAmplify`）
- 可限定僅在搖攝時啟用

### 3.8 滾動快門近似 ✅
- 攝影機角速度 → 投影矩陣水平剪切
- 可調讀出時間（`readoutMs`）與剪切強度

### 3.9 觀景窗 Overlay ✅
- IMGUI 繪製：三分線、中心十字、水平線、安全框（可調比例）
- 反光鏡遮黑動畫
- 各元素可獨立開關

### 3.10 擷取管線 ✅
- HDRP CustomPass `CopyColorAfterPost` 在後處理後 blit
- `PhotoCaptureManager` 管理 RTHandle、AsyncGPUReadback
- 超時保護（30 frame timeout）
- `PhotoPrintSpawner` 世界空間照片顯示

### 3.11 自由視角攝影機 ✅
- WASD + 滑鼠自由移動
- 加速（Shift）/ 減速（Ctrl）
- 滑鼠平滑視角（`lookSmooth`）
- 與 Pan Assist 的暫停/恢復整合

### 3.12 HUD 狀態列 ✅
- IMGUI 底部黑色半透明條
- 顯示：快門速度、光圈、曝光補償、焦距
- 格式化輸出（1/125、f/2.8、+0.0、200mm）

---

## 四、已知架構問題

### 4.1 TrackPhotoRig 職責過重（God Object）✅ 已修正
**原況**：單一類別 ~693 行，包含相機模擬、輸入處理、IMGUI 繪製。
**修正**：觀景窗 overlay 拆分為 `ViewfinderOverlay` 獨立組件，Rig 自身輸入處理移除。現為 ~578 行。

### 4.2 輸入處理重複衝突 ✅ 已修正
**原況**：`TrackPhotoRig.HandleLensInputs()`、`PhotoRigController`、`PhotoFreeCam` 三處同時處理鏡頭切換與滾輪變焦。
**修正**：移除 `TrackPhotoRig.HandleLensInputs()`（僅保留 `UpdateLensInterpolation` 做平滑插值），移除 `PhotoFreeCam.HandleLens()`，統一由 `PhotoRigController` 透過 `SetTargetFocal()` 處理變焦。

### 4.3 反射呼叫 private 方法 ✅ 已修正
**原況**：`PhotoRigController.TrySwitchLensA/B()` 用 `System.Reflection` 呼叫 `SwitchLens()`。
**修正**：`SwitchLens()` 改為 `public`，`PhotoRigController` 直接呼叫 `rig.SwitchLens(rig.lensA, true)`。

### 4.4 API 一致性不足 ✅ 已修正
**原況**：部分操作走方法（`SetISOManual`、`TakeOneShot`），部分直接改 public field（`exposureCompEV`、`ndStops`、`afContinuous`）。
**修正**：新增統一 API 方法：`SetShutterSpeed`、`SetAperture`、`SetExposureComp`、`SetNDStops`、`SetAFContinuous`、`ManualFocusAdjust`、`SetDriveMode`、`SetPanAssist`、`SetHandheldShake`、`SetRollingShutter`。`PhotoRigController` 已全面改用方法呼叫。同時新增合焦判定 API：`GetSubjectDistance()`、`IsFocusConfirmed()`。

### 4.5 CopyColorAfterPost 全靜態 ✅ 已修正
**原況**：`pendingRT`、`pendingCam`、`pending` 皆為 static，不支援多相機。
**修正**：所有欄位改為實例成員，`RequestCapture`/`ClearRequest` 改為實例方法。新增 `FindInstance()` 靜態輔助方法（遍歷場景 `CustomPassVolume` 尋找實例）。`PhotoCaptureManager` 在 `OnEnable` 時透過 `FindInstance()` 取得參照。

### 4.6 PanAssist 狀態所有權不清 ✅ 已修正
**原況**：`PhotoFreeCam` 與 `PhotoRigController` 都可直接寫入 `rig.panAssist`。
**修正**：新增 `panAssistSystemPaused` + `PauseSystemPanAssist()` / `ResumeSystemPanAssist()`。`panAssist` 為使用者意圖，`panAssistSystemPaused` 為系統暫停，兩者獨立。`PhotoFreeCam` 僅操作系統暫停，不再覆蓋使用者設定。

---

## 五、待開發功能

### 5.1 架構重構（對應 4.1~4.3）

**目標**：解決已知架構問題，提升可維護性。

**重構方案**：

#### Step 1：拆分觀景窗 Overlay
- 從 `TrackPhotoRig` 中提取 `OnGUI`、`DrawOverlay`、`DrawLine`、`DrawRect`、`DrawFilledRect`、`Tex1x1`、反光鏡遮黑邏輯
- 建立 `ViewfinderOverlay` 組件，掛在同一 GameObject
- `ViewfinderOverlay` 讀取 `TrackPhotoRig` 的公開屬性（`mirrorBlackoutUntil` 等）

#### Step 2：統一輸入處理
- 移除 `TrackPhotoRig.HandleLensInputs()`（鍵盤 1/2 + 滾輪變焦）
- `PhotoRigController` 成為唯一輸入來源，透過公開方法操作 Rig
- 將 `SwitchLens()` 改為 `public`，移除反射呼叫
- 釐清 `PhotoFreeCam.HandleLens()` 與 `PhotoRigController` 滾輪的衝突

#### Step 3：PanAssist 狀態管理
- `TrackPhotoRig` 新增 `panAssistUserEnabled`（使用者意圖）
- `PhotoFreeCam` 操作 `panAssistSystemPaused`（系統暫停）
- 實際 panAssist = `userEnabled && !systemPaused`

---

### 5.2 自動追焦目標選擇 ✅ 已完成

**實作**：
- `PhotoRigController` 新增 `Tab`（下一台）/ `` ` ``（上一台）按鍵循環切換
- 透過 `BikeAITrafficManager.Instance.RankedAgents` 取得依名次排序的車輛列表
- 選擇後呼叫 `rig.SetSubject()` 設定追焦目標

---

### 5.3 對焦點顯示與合焦反饋 ✅ 已完成

**實作**：
- `ViewfinderOverlay` 新增對焦框繪製（中央四角方括號）
- 合焦時框為綠色，未合焦時為紅色
- 框下方顯示當前對焦距離（公尺）
- 合焦判定基於景深計算（CoC = 0.03mm，全片幅）
- `TrackPhotoRig.IsFocusConfirmed()` 提供合焦狀態查詢

---

### 5.4 照片評分系統 ✅ 已完成

**實作**：
- 新增 `PhotoScoreEvaluator` 組件
- 四項評分維度，各項 0~100 分：
  - **構圖**（30%）：主體三分點距離 + 邊緣懲罰
  - **曝光**（25%）：ISO 過高懲罰 + 曝光補償偏離
  - **銳利度**（25%）：安全快門規則（1/焦距）+ 搖攝補償
  - **對焦**（20%）：景深計算合焦判定 + 距離誤差比例
- 每張照片記錄 EXIF 快照（快門/光圈/焦距/ISO/對焦距離/時間戳）

---

### 5.5 照片瀏覽 / 圖庫 ✅ 已完成

**實作**：
- 新增 `PhotoGallery` 組件
- `F1` 開關圖庫模式（暫停遊戲、顯示滑鼠游標）
- 縮圖一覽：網格排列，點擊選取，再點擊進入全螢幕
- 全螢幕檢視：顯示 EXIF 資訊 + 評分明細（C/E/S/F）
- 方向鍵左右導覽，`Esc` 返回縮圖，`Delete` 刪除照片
- 分數顏色標示：≥80 綠色、≥60 黃色、<60 紅色

---

## 六、開發優先順序

| 優先級 | 功能 | 狀態 |
|--------|------|------|
| P0 | Physical Camera 基礎 | ✅ 完成 |
| P0 | 鏡頭系統（雙鏡頭、變焦） | ✅ 完成 |
| P0 | AF-C 連續對焦 | ✅ 完成 |
| P0 | 搖攝輔助 | ✅ 完成 |
| P0 | 曝光系統 + ND | ✅ 完成 |
| P0 | 連拍驅動模式 | ✅ 完成 |
| P0 | 擷取管線 | ✅ 完成 |
| P0 | 自由視角攝影機 | ✅ 完成 |
| P1 | 手震模擬 | ✅ 完成 |
| P1 | 滾動快門近似 | ✅ 完成 |
| P1 | 觀景窗 Overlay | ✅ 完成 |
| P1 | HUD 狀態列 | ✅ 完成 |
| P1 | 照片世界顯示 | ✅ 完成 |
| P2 | 架構重構（Overlay 拆分 + 輸入統一 + PanAssist） | ✅ 完成 |
| P2 | API 一致性修正 + CopyColorAfterPost 實例化 | ✅ 完成 |
| P2 | 自動追焦目標選擇 | ✅ 完成 |
| P3 | 對焦點顯示與合焦反饋 | ✅ 完成 |
| P3 | 照片評分系統 | ✅ 完成 |
| P3 | 照片瀏覽 / 圖庫 | ✅ 完成 |

---

## 七、設計原則

1. **手感優先**：一切以「操作起來像真實相機」為判斷標準
2. **物理正確**：曝光三角（光圈/快門/ISO）遵守真實攝影公式
3. **參數化可調**：所有相機參數暴露為 `[SerializeField]`，Inspector 即時預覽
4. **模擬層與輸入層分離**：`TrackPhotoRig` 負責模擬，`PhotoRigController` 負責輸入
5. **HDRP 整合**：善用 Physical Camera、Volume Override、CustomPass

---

## 八、檔案對照表

| 檔案 | 職責 |
|------|------|
| `Assets/Core/Phot/Runtime/TrackPhotoRig.cs` | 核心相機模擬（Physical Camera + AF + 曝光 + 效果） |
| `Assets/Core/Phot/Runtime/PhotoRigController.cs` | 唯一鍵盤/滑鼠輸入層 → Rig 參數操作 |
| `Assets/Core/Phot/Runtime/PhotoFreeCam.cs` | WASD + 滑鼠自由視角控制（不處理變焦） |
| `Assets/Core/Phot/Runtime/TrackPhotoHUD.cs` | IMGUI 底部狀態列 |
| `Assets/Core/Phot/Runtime/ViewfinderOverlay.cs` | 觀景窗 Overlay 繪製（從 TrackPhotoRig 拆分） |
| `Assets/Core/Phot/Runtime/PhotoCaptureManager.cs` | 擷取流程管理（RTHandle + AsyncGPUReadback） |
| `Assets/Core/Phot/Runtime/CopyColorAfterPost.cs` | HDRP CustomPass（After Post Process blit） |
| `Assets/Core/Phot/Runtime/PhotoPrintSpawner.cs` | 世界空間照片 Quad 生成 |
| `Assets/Core/Phot/Runtime/PhotoScoreEvaluator.cs` | 照片評分（構圖/曝光/銳利度/對焦） |
| `Assets/Core/Phot/Runtime/PhotoGallery.cs` | 照片圖庫瀏覽（縮圖/全螢幕/EXIF/評分） |
| `Assets/Core/Phot/Prefab/PF_PhotographyRig.prefab` | 完整攝影系統 Prefab |

---

## 九、變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-25 | 建立設計文件，記錄已完成功能、已知架構問題、待開發規劃 |
| 2026-04-25 | 架構重構：拆分 ViewfinderOverlay、統一輸入處理（移除反射）、PanAssist 雙層狀態管理 |
| 2026-04-25 | 資料夾整理：所有腳本統一至 Phot/Runtime/，場景移入 Phot/，建立 PF_PhotographyRig Prefab |
| 2026-04-25 | 4.4 API 一致性修正：新增所有參數 setter 方法，PhotoRigController 全面改用 API |
| 2026-04-25 | 4.5 CopyColorAfterPost 改為實例化設計，PhotoCaptureManager 透過 FindInstance() 取得參照 |
| 2026-04-25 | 5.2 自動追焦目標選擇：Tab/BackQuote 循環切換 BikeAI 車輛 |
| 2026-04-25 | 5.3 對焦點顯示：ViewfinderOverlay 對焦框 + 合焦變色 + 距離顯示 |
| 2026-04-25 | 5.4 照片評分系統：PhotoScoreEvaluator（構圖/曝光/銳利度/對焦四維度） |
| 2026-04-25 | 5.5 照片圖庫：PhotoGallery（縮圖/全螢幕/EXIF/評分/刪除） |
