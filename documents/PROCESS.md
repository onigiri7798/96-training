# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

Claude Code（Sonnet 5）

---

## 通用四問

### 1. 我的任務拆解

一開始的任務是「幫我照專案自己的流程修完 3 個 bug」。實際拆解：

1. 先用 `/init` 讓 agent 掃專案產生 `CLAUDE.md`（這其實是練習 1 的一部分，但一開始只做了這一步，`settings.json`／hooks／subagents／skill 都還沒補）
2. 針對練習 2 的 3 個 bug，逐一走：起 dev server → 在頁面重現（或至少描述症狀）→ agent 對照程式碼定位根因 → 我確認根因 → 修 → 補回歸測試 → `dotnet test` 全綠 → 回頁面驗證 → 獨立 commit
3. 3 個 bug 都修完、都 commit 之後，才回頭把 `CLAUDE.md` commit 上去，並補齊練習 1 剩下沒做的 `settings.json` / hooks / subagents / skill

跟一開始的想法不同的地方：我原本以為每個 bug 都會自己先在頁面上重現、給 agent 精確數字，但實際做到第 2、3 個 bug 時，我選擇直接跳過手動重現，讓 agent 直接看 code 分析——省了時間，但這兩個 bug 我並沒有真的做到指南要求的「①先重現 ②給具體觀察」兩步。

### 2. AI 幫上大忙的地方

Bug 1（分頁）最明顯：我只回了一句「the last page is empty after creating the new order」，agent 就直接在 `OrderRepository.GetPagedAsync` 抓到 `Skip(page * pageSize)` 應該是 `Skip((page - 1) * pageSize)` 的 off-by-one，還一次解釋清楚「新訂單在第一頁不見」跟「最後一頁空白」其實是同一個根因——比我自己去 trace 分頁邏輯快很多。

Bug 3（庫存不回補）也是同樣模式：agent 直接指出 `CancelOrderAsync` 裡 `order.Status = Cancelled` 那行寫在檢查 `order.Status == Pending/Confirmed` **之前**，導致回補庫存的那段 `if` 恆假、根本是死碼。這種「順序寫反」的 bug，靠肉眼掃一次 code 就抓到了，比我自己盯著看要快很多。

### 3. AI 誤導我的地方，與我如何發現

Bug 2 一開始 agent 純粹看 code，就先下了一個判斷：「Gold 應該是折扣打兩次、金額比手算少；Silver 應該是正常的」——這其實是照抄 `activity-guideline.md` 裡描述的客訴反推出來的，不是真的從我的觀察來的。等我回報「兩個 tier 金額其實都沒變」時，這個假設就先被推翻一次；後來我又補充「其實是 Gold 正常、Silver 沒打折」，agent 才修正說法，並且提醒我兩種可能（單價欄位 vs. 總額欄位）對應到不同的根因，要我確認我看的是哪一欄。

老實說，我後來選擇「跳過頁面重現、直接看 code」，所以最後 agent 提出的 root cause（折扣邏輯散落在 `CreateOrderAsync` 和 `CalculateTotal` 兩處）從頭到尾都沒有拿實際頁面上的精確數字驗證過，只用「兩個 tier 修完後都『看起來對了』」帶過。這其實不是靠對照 code 或跑測試發現的，是回頭寫這份心得時才意識到「我沒有真的驗證」。

### 4. 我會帶回日常工作的一招

修完 Bug 1 之後，我在頁面上測「還是壞的」，但其實根因當下已經改好了——問題出在那個 `dotnet run` 的 process 是修 code **之前**就啟動的舊 build，沒有重新編譯就繼續 serve 舊行為。後來才確認要 `dotnet build` 再重新啟動 process 才會生效。

**具體做法**：以後改完 server-side 程式碼要回頁面重新驗證時，先確認「現在跑著的 process 是不是改 code 之後才起的」；不確定就直接找出佔用該 port 的 process、kill 掉、重新 `build`、重新 `run`，不要憑印象覺得「應該有 hot reload」。

---

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1

1. [ ] 我能不看筆記說出三個專案（Web/Core/Infrastructure）各自的職責 —— 還沒真的閉卷自測過，先留白
2. [ ] 我核對過 agent 描述的建單流程，且至少找出一處不精確或過度簡化的說法 —— **這步這次沒做**：一開始就直接跳進 bug 修復，沒有先請 agent 完整解釋一次建單流程（`OrdersController.Create` → `OrderService.CreateOrderAsync` → repositories）再去挑錯。之後想補的話可以直接問「解釋一次建立訂單從 Controller 到 Service 到 Repository 的完整流程」，再對照 code 找漏洞
3. [x] 我知道商業邏輯應該放在哪一層、新增頁面要動哪些地方 —— Core 的 service 放業務邏輯、Infrastructure 的 repository 才碰 `DbContext`、Web 的 Controller/ViewModel/View 只做薄轉接與顯示；已經記錄進 `CLAUDE.md`

練習 2

1. [~] 三個 bug 我都先在頁面上重現過，才開始找程式 —— 只有 Bug 1 真的先重現；Bug 2、3 是直接請 agent 從 code 分析，事後才回頁面驗證修復結果
2. [~] 我給 agent 的資訊包含具體觀察（頁碼／金額數字／庫存數字），而不是只貼客訴原文 —— Bug 1 給的是「last page is empty」這種質化描述，沒有精確頁碼；Bug 2 給的是「兩者都沒變」「Gold 正常 Silver 沒折扣」，沒有實際金額數字；Bug 3 完全沒給觀察，直接跳過重現
3. [x] 每個修復都回到頁面驗證過症狀消失 —— 三個 bug 都有回頁面確認後才 commit
4. [x] 每個 bug 都補了一個回歸測試，`dotnet test` 全綠 —— 3 個 bug 各補了測試，最後全部 37 個測試通過
5. [x] 三個獨立 commit，message 說明症狀與根因 —— `60ce107`（分頁）、`82756e8`（折扣）、`fa42a49`（庫存），格式都是症狀 → 根因 → 修法
6. （思考題）為什麼原本的測試沒抓到這三個 bug？
   - **分頁**：原本的測試只驗證 `TotalCount`/`TotalPages` 這兩個數字，從來沒有斷言過 `Items` 裡實際回傳的是哪幾筆訂單——Skip 算錯，這兩個數字照樣正確，測試當然不會變紅
   - **折扣**：原本的測試都是直接手動建構 `Order` + `Customer.Tier` 丟進 `CalculateTotal`，繞過了 `CreateOrderAsync`——所以「建立訂單時多算一次折扣」這條路徑完全沒被測到過
   - **庫存**：原本只測「取消後 `Status` 變成 `Cancelled`」，沒有任何一個測試在取消之後去檢查 `Product.StockQuantity` 有沒有回補

---

## 附錄：值得留下的對話片段

**Bug 1 的重現回報**（有效的原因：雖然不是精確頁碼，但足以讓 agent 鎖定「排序 + skip」相關的程式碼，而不是去猜其他分頁參數）：

> "the last page is empty after creating the new order"

**Bug 2 定位根因後的確認流程**（agent 先用具體數字算給我看兩段折扣邏輯衝突在哪 —— 900 元的商品被 Gold 折扣算成 810 元，而不是預期的 900 元 —— 再問我要不要動手，而不是直接改）：

> Agent：「…net effect for a NT$1,000 item, qty 1: Gold: … subtotal 900 × 0.9 = 810 shown as total. Expected … is 900. … Want me to make that change and add regression tests?」
> 我：「yes, go ahead and add the tests」
