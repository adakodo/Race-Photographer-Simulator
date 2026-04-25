# BikeAI MotoGP 仿真比賽設計文件

> 本文件為機車 AI 仿 MotoGP 比賽功能的設計規格。
> 開發相關功能前請先閱讀本文件理解需求；完成新功能後請同步更新本文件。

**相關文件：** [[Photography_Design|攝影系統設計]] · [[Photography_Controls|操作手冊]]

---

## 一、目標

讓 AI 機車群呈現類似 MotoGP 真實比賽的視覺效果與動態行為，為攝影模擬器提供精采的拍攝素材。重點在於「看起來像真實比賽」，而非物理模擬精確度。

---

## 二、系統架構總覽

```
BikeAITrafficManager (singleton)
  ├── 全域 agent 註冊表
  ├── 每幀按 DistanceOnTrack 排序
  ├── GetFrontAgent() / GetRearAgent() / IsOffsetBlocked()
  └── 未來：比賽狀態管理（圈數、名次、Flag）

BikeAITrackAgent (per-bike MonoBehaviour)
  ├── 移動狀態：distanceOnTrack, currentSpeed, currentOffset
  ├── 傾斜系統：曲率傾斜 + 換道傾斜
  ├── LOD 節流（近/遠鏡頭更新頻率）
  └── strategy.Tick() 驅動決策

BikeAIStrategy_Follow (策略層)
  ├── 巡航速度 + 集團分組
  ├── 彎道速度控制（入彎減速 / 出彎加速）
  ├── Racing Line（外→內→外 走線）
  ├── 個體差異隨機化
  ├── 跟車 + 超車狀態機
  └── 未來：追趕邏輯、攻防、DRS/尾流

BikeAISpawner
  ├── 自動生成指定數量 AI 機車
  ├── 分配集團（packDistribution）
  └── 未來：網格起跑排位
```

---

## 三、已完成功能

### 3.1 三層架構基礎 ✅
- `BikeAITrafficManager`：全域註冊、排序、前後車查詢、偏移碰撞檢測
- `BikeAITrackAgent`：沿 spline 移動、傾斜、LOD 節流
- `BikeAIStrategy_Follow`：策略驅動的速度/偏移決策

### 3.2 彎道行為 ✅
- **入彎減速**：前視 `curveLookAhead` 偵測彎道曲率，依比例降速
- **出彎加速**：偵測後方有彎、前方趨直時給予 `exitBoostRatio` 加速
- **Racing Line**：外→內→外走線（apex 內切 70% + 入彎外卡 30%）

### 3.3 集團分組 (Pack Grouping) ✅
- `numberOfPacks` 組，`packSpeedGap` 區隔巡航速度
- `packDistribution` 自訂各組車數比例
- `withinPackSpeedSpread` 組內速度微差
- `BikeAISpawner` 自動分配並打亂起始位置

### 3.4 個體差異 ✅
- 走線寬度、apex 前視、橫向偏好、過彎激進度隨機化
- `enableVariation` 開關

### 3.5 超車系統 ✅
- 4 狀態機：`None → Preparing → Overtaking → Returning`
- 偵測前車慢於自身時觸發
- 左右兩側嘗試（偏移碰撞檢測）
- 超車中加速 `overtakeSpeedBonus`
- 超越後回歸 racing line

### 3.6 追趕邏輯 (Catch-Up / Rubber Banding) ✅
- **距離感知追趕加速**：前車間距超過 `catchUpThreshold` 時，以 `InverseLerp` 漸進給予速度加成，最大 `catchUpSpeedBonus`
- **後方壓力防禦**：後車在 `rearPressureDistance` 內時，前車微幅加速（`rearPressureSpeedBonus`），製造拉鋸感
- 所有加成受 `agent.MaxSpeed` 限制
- `enableCatchUp` 開關可完全關閉
- 實作於 `ApplyCatchUp()` 方法，在 `ApplyTrafficLogic` 之後呼叫

### 3.7 尾流效應 (Slipstream / Draft) ✅
- **橫向對齊偵測**：前車偏移差 < `slipstreamOffsetTolerance` 時才算在尾流中
- **有效距離範圍**：`slipstreamMinDistance` ~ `slipstreamMaxDistance`，太近進入亂流區失效
- **漸進式加成**：距離越近加成越大，最大 `slipstreamSpeedBonus`
- 所有加成受 `agent.MaxSpeed` 限制
- `enableSlipstream` 開關可完全關閉
- 實作於 `ApplySlipstream()` 方法，在 `ApplyCatchUp` 之後呼叫

### 3.8 攻防互動 (Attack & Defend) ✅
- **防禦封線**：透過 `BikeAITrafficManager.GetAttacker()` 偵測後車正在超車，前車偏移向攻擊者同側封鎖（強度受 `defenseOffsetStrength × aggressiveness` 調節）
- **防禦加速**：防禦中微幅加速 `defenseSpeedBonus`，避免被輕鬆超越
- **反擊超車**：攻擊者消失且確認已被超越時，啟動 `counterAttackTimer`，給予衰減式速度加成追回
- **aggressiveness 個體差異**：0~1 隨機化，影響防禦強度、反擊持續時間與速度加成
- 自己正在超車時不會同時防禦（避免攻防衝突）
- `enableAttackDefend` 開關可完全關閉
- 實作於 `ApplyAttackDefend()` 方法，在 `ApplySlipstream` 之後呼叫

### 3.10 車體尺寸與競爭感提升 (Bike Dimensions & Competitive Racing) ✅
- **車體尺寸**：`BikeAITrackAgent` 新增 `bikeLength`（預設 2.1m）與 `bikeWidth`（預設 0.65m），所有間距計算改為 edge-to-edge
- **間距修正**：`GetFrontAgent` / `GetRearAgent` / `IsOffsetBlocked` / `ApplyLateralAvoidance` / `ApplySlipstream` 全面使用車體尺寸，消除點對點距離造成的重疊抖動
- **偏好側超車**：每台車依 `defaultOffset` 隨機偏好決定優先超車側（偏左優先左超、偏右優先右超），增加超車多樣性
- **可變超車偏移**：激進車手（`aggressiveness` 高）超車切線更近，保守車手繞更遠
- **超車晚煞車**：處於超車狀態時允許更高彎道速度（依 `aggressiveness` 縮放 5~15%），模擬 MotoGP dive-bomb

### 3.9 賽道規則 (Race Rules & Phase Dynamics) ✅
- **圈數追蹤**：`BikeAITrackAgent` 偵測通過起終點線時 `currentLap++`，提供 `TotalRaceDistance`（圈數 × 賽道長 + 當前距離）
- **名次計算**：`BikeAITrafficManager` 每幀依 `TotalRaceDistance` 降序排列 `rankedAgents`，提供 `GetPosition()` / `GetAgentAtPosition()` API
- **比賽階段**：`RacePhase` 列舉（PreRace / EarlyRace / MidRace / LateRace / Finished），依領先車圈數自動切換
- **賽段動態調節**：`BikeAIStrategy_Follow` 依比賽階段套用倍率：
  - 開賽衝刺：超車觸發距離 ×1.5（更積極）、追趕 ×0.5（較弱，讓集團自然形成）
  - 中段穩定：所有倍率 ×1（正常值）
  - 尾段衝刺：超車觸發距離 ×1.3、超車速度加成 ×1.3、追趕 ×1.5（更強）
- **Inspector 可調**：`totalLaps`、`earlyPhaseLaps`、`latePhaseLaps`、各賽段倍率
- `enableRacePhase` 開關，關閉時所有倍率 = 1
- `totalLaps = 0` 為無限圈數模式（永遠 MidRace）

---

## 四、待開發功能

### 4.1 追趕邏輯 (Catch-Up / Rubber Banding) ✅ 已完成 → 見 3.6

**目標**：讓落後的車逐漸追上前方集團，製造更多近身纏鬥場面。

**設計方向**：
- **距離感知加速**：當一台車與前方集團的間距超過閾值 `catchUpThreshold`（如 15m），給予額外速度加成 `catchUpSpeedBonus`
- **漸進式加速**：加成量與間距成正比，避免突然暴衝。使用 `Mathf.InverseLerp(catchUpThreshold, catchUpMaxDistance, gap)` 平滑映射
- **減弱條件**：當間距縮小到 `catchUpThreshold` 以內，加成歸零，轉回正常跟車邏輯
- **後方壓力**：後車接近時前車可微幅加速（防禦），製造拉鋸感
- **不超過 maxSpeed**：追趕加成仍受 `agent.MaxSpeed` 限制

**參數草案**：
```
[Header("Catch-Up — 追趕邏輯")]
float catchUpThreshold = 15f;       // 開始追趕的間距（公尺）
float catchUpMaxDistance = 50f;      // 追趕加速達到最大的間距
float catchUpSpeedBonus = 5f;        // 最大追趕速度加成（m/s）
float rearPressureDistance = 8f;     // 後車施壓距離
float rearPressureSpeedBonus = 1.5f; // 前車防禦加速
```

**實作位置**：`BikeAIStrategy_Follow.Tick()` 內，在 `ApplyTrafficLogic` 之後新增 `ApplyCatchUp()` 方法。

---

### 4.2 尾流效應 (Slipstream / Draft) ✅ 已完成 → 見 3.7

**目標**：跟在前車正後方可獲得速度加成，模擬空氣動力學尾流。

**設計方向**：
- 偵測前車在正後方（橫向偏移差小於閾值）且間距在尾流範圍內
- 給予速度加成，間距越近加成越大
- 可用於蓄力超車：尾流蓄速 → 側移超車

**參數草案**：
```
[Header("Slipstream — 尾流效應")]
bool enableSlipstream = true;
float slipstreamMaxDistance = 12f;    // 尾流有效最大距離
float slipstreamMinDistance = 2f;     // 太近則失效（亂流）
float slipstreamOffsetTolerance = 0.8f; // 橫向對齊容差
float slipstreamSpeedBonus = 3f;     // 最大尾流速度加成
```

---

### 4.3 攻防互動 (Attack & Defend) ✅ 已完成 → 見 3.8

**目標**：讓超車不再是單向行為，被超車的車會嘗試防禦。

**設計方向**：
- **防禦封線**：偵測後車正在超車時，前車偏移向同側封鎖路線
- **反擊超車**：被超越後，可嘗試在下一個彎道利用內線反超
- **激進程度個體差異**：每台車有 `aggressiveness` 參數影響攻防傾向

---

### 4.4 賽段動態 (Race Phase Dynamics) ✅ 已完成 → 見 3.9

**目標**：比賽不同階段展現不同態勢。

**設計方向**：
- **開賽衝刺**：起步後前幾圈集團密集、超車頻繁
- **中段穩定**：集團逐漸拉開、形成穩定節奏
- **尾段衝刺**：最後幾圈追趕加成增大、超車更積極
- 需要 `BikeAITrafficManager` 追蹤圈數與比賽進度

---

### 4.5 失誤與意外 (Mistakes & Incidents)

**目標**：偶發失誤增加真實感與拍攝戲劇性。

**設計方向**：
- **過彎小失誤**：偶爾走線偏大、輕微走寬
- **煞車鎖死**：入彎偶爾多煞一點、速度恢復慢
- **出彎打滑感**：出彎加速時微幅搖擺
- 發生機率很低（< 1-2% per corner），純視覺效果

---

### 4.6 名次追蹤與 HUD（名次追蹤部分已完成 → 見 3.9，HUD 待開發）

**目標**：讓攝影者知道當前比賽局勢。

**設計方向**：
- ~~`BikeAITrafficManager` 計算即時名次（基於 lap + distanceOnTrack）~~ ✅ 已完成
- 可選 HUD 顯示前幾名排序與間距
- 提供 API 供攝影系統查詢「誰在領先」以做自動追焦建議

---

### 4.7 起跑排位 (Grid Start)

**目標**：仿 MotoGP 網格式起跑。

**設計方向**：
- `BikeAISpawner` 支援排位模式（2x 交錯排列）
- 起跑信號後統一出發
- 起步反應時間個體差異

---

## 五、開發優先順序

| 優先級 | 功能 | 狀態 |
|--------|------|------|
| P0 | 三層架構 + 基本移動 | ✅ 完成 |
| P0 | 彎道行為（入彎/出彎/走線） | ✅ 完成 |
| P0 | 集團分組 | ✅ 完成 |
| P0 | 個體差異 | ✅ 完成 |
| P0 | 超車系統 | ✅ 完成 |
| P1 | 追趕邏輯 (Catch-Up) | ✅ 完成 |
| P1 | 尾流效應 (Slipstream) | ✅ 完成 |
| P2 | 攻防互動 (Attack & Defend) | ✅ 完成 |
| P2 | 賽段動態 (Race Phase) | ✅ 完成 |
| P2 | 車體尺寸 + 競爭感提升 | ✅ 完成 |
| **P3** | **失誤與意外** | **🔜 下一步** |
| P3 | 名次 HUD | 待開發（名次追蹤已完成） |
| P3 | 起跑排位 | 待開發 |

---

## 六、設計原則

1. **視覺優先**：一切以「攝影師看到的畫面是否精采」為判斷標準
2. **漸進式開發**：每個功能獨立可測試，不破壞已有行為
3. **參數化可調**：所有閾值暴露為 `[SerializeField]`，方便在 Inspector 調整
4. **策略層隔離**：新邏輯加在 `BikeAIStrategy_Follow` 的獨立方法中，不修改 Agent 層
5. **效能考量**：LOD 節流已就位，新邏輯避免每幀對所有 agent 做 O(n^2) 查詢

---

## 七、變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-03-28 | 建立設計文件，記錄已完成功能與待開發規劃 |
| 2026-03-28 | 完成追趕邏輯 (Catch-Up)：距離感知漸進加速 + 後方壓力防禦 |
| 2026-03-28 | 完成尾流效應 (Slipstream)：橫向對齊偵測 + 距離漸進加成 |
| 2026-03-28 | 完成攻防互動 (Attack & Defend)：防禦封線 + 反擊超車 + aggressiveness 個體差異 |
| 2026-03-28 | 完成賽道規則：圈數追蹤 + 名次計算 + 賽段動態調節（開賽/中段/尾段）|
| 2026-03-28 | 完成車體尺寸系統：edge-to-edge 間距 + 偏好側超車 + 可變偏移 + 晚煞車 |
