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

**練習 3**：這次流程不一樣——先讓 agent 進 Plan Mode，讀完既有慣例（`ProductsController`、`IProductService`、既有 View、既有測試風格）之後先出一份完整計畫（要動哪些檔、每層放什麼、怎麼避免 N+1、驗證機制放哪），我看過確認才放行實作，沒有中途才發現分層跑掉。實作完也主動叫 `code-reviewer` subagent 審查一次（練習 1 裝好之後第一次真的拿來用），抓到問題才 commit。

### 2. AI 幫上大忙的地方

Bug 1（分頁）最明顯：我只回了一句「the last page is empty after creating the new order」，agent 就直接在 `OrderRepository.GetPagedAsync` 抓到 `Skip(page * pageSize)` 應該是 `Skip((page - 1) * pageSize)` 的 off-by-one，還一次解釋清楚「新訂單在第一頁不見」跟「最後一頁空白」其實是同一個根因——比我自己去 trace 分頁邏輯快很多。

Bug 3（庫存不回補）也是同樣模式：agent 直接指出 `CancelOrderAsync` 裡 `order.Status = Cancelled` 那行寫在檢查 `order.Status == Pending/Confirmed` **之前**，導致回補庫存的那段 `if` 恆假、根本是死碼。這種「順序寫反」的 bug，靠肉眼掃一次 code 就抓到了，比我自己盯著看要快很多。

**練習 3**：`code-reviewer` subagent 這次真的抓到兩個實質問題，不是走過場——`LowStockViewModel.Threshold` 上的 `[Range]` attribute 其實從沒被用到（controller 手動另外複製了一份一模一樣的檢查，兩處邏輯以後會各自漂移），以及 `LowStockProduct` 這個型別放在 `Core.Services` 命名空間卻被 `Core.Interfaces` 底下的介面引用（命名空間方向反了）。兩個都當場修掉，比我自己看 diff 更容易漏掉這種「能動但不對」的細節。

### 3. AI 誤導我的地方，與我如何發現

Bug 2 一開始 agent 純粹看 code，就先下了一個判斷：「Gold 應該是折扣打兩次、金額比手算少；Silver 應該是正常的」——這其實是照抄 `activity-guideline.md` 裡描述的客訴反推出來的，不是真的從我的觀察來的。等我回報「兩個 tier 金額其實都沒變」時，這個假設就先被推翻一次；後來我又補充「其實是 Gold 正常、Silver 沒打折」，agent 才修正說法，並且提醒我兩種可能（單價欄位 vs. 總額欄位）對應到不同的根因，要我確認我看的是哪一欄。

老實說，我後來選擇「跳過頁面重現、直接看 code」，所以最後 agent 提出的 root cause（折扣邏輯散落在 `CreateOrderAsync` 和 `CalculateTotal` 兩處）從頭到尾都沒有拿實際頁面上的精確數字驗證過，只用「兩個 tier 修完後都『看起來對了』」帶過。這其實不是靠對照 code 或跑測試發現的，是回頭寫這份心得時才意識到「我沒有真的驗證」。

**練習 3**：低庫存查詢第一版寫成 LINQ 的 `join ... into ... DefaultIfEmpty()`（left join）疊 `GroupBy` 子查詢，agent 一開始很有信心地說這樣「一次查詢、不會有 N+1」，結果一跑 `dotnet test` 直接兩個測試炸掉：`System.InvalidOperationException: Nullable object must have a value`（EF Core InMemory provider 對這種 anonymous-type left join 的已知地雷）。這不是我用肉眼抓到的，是測試紅了才知道「看起來合理的 LINQ」不代表在這個 provider 上真的能跑。後來 agent 改成「先查一次符合門檻的商品、再查一次銷量彙總成 Dictionary、最後在記憶體裡合併」兩個查詢，問題就消失了。另外 review 建議把「threshold 必須 > 0」這條規則也搬進 Core service 用 exception 擋一次，agent 沒有照做，理由是專案既有慣例是用 DataAnnotations + ModelState 驗證輸入、不是丟 domain exception——這個我認同，但也代表**同一個 review 建議不是照單全收，要自己判斷跟不跟現有慣例衝突**。

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

練習 3

1. [x] `/Products/LowStock` 不帶參數 → 門檻 10 的結果；帶 `?threshold=3` → 結果隨之改變 —— agent 對跑著的網站實測：不帶參數時輸入框顯示預設值 10、回傳 5 列（SKU-1048/1005/1023/1032/1014，庫存都 <10）；`?threshold=3` 時只剩 SKU-1048（庫存 2）1 列，其餘庫存 3～4 的商品正確被排除
2. [x] `?threshold=0`、`?threshold=-1` → 頁面顯示驗證錯誤，不是 500 —— 兩者皆回應 HTTP 200（不是 500），表格 0 列，且 `asp-validation-for="Threshold"` 的位置正確渲染出「門檻必須大於 0」訊息

（註：1、2 這兩項原本標記「留給我自己在瀏覽器點過」，後來請 agent 用 `curl` 直接對著跑著的網站驗證掉了，沒有真的用瀏覽器點——記錄一下，這跟練習指南原本想要的「自己動手」還是有落差，只是圖快）
3. [x] 售出數量欄位排除了 Cancelled 訂單（可用一筆已取消的訂單驗證）—— agent 用真實表單（含 antiforgery token）在跑著的網站上實際建了一筆訂單（SKU-1048 × 2，customer 9，訂單 #208）再取消它：建立後 `/Products/LowStock` 顯示 SKU-1048 庫存 2→0、近 30 天售出 10→12；取消後庫存回到 2、售出數字也回到 10——確認 Cancelled 訂單真的被排除在外，不是只有單元測試斷言
4. [x] 停售（已停售 badge）商品不出現在列表 —— agent 直接對本機 `OrderHubTraining` 資料庫下 SQL：把 SKU-1002（原本庫存 101、上架中）暫時改成庫存 3、`IsActive=0`，確認 `/Products` 頁面看得到它（庫存 3），但 `/Products/LowStock?threshold=10` 完全沒有它；驗證完立刻把 SKU-1002 改回庫存 101、`IsActive=1`，資料庫已還原
5. [x] 程式分層與命名跟既有的 Products 功能一致（請 agent 自我 review 一次，並自己確認）—— 有請 `code-reviewer` subagent 審查，抓到兩個真實問題（`[Range]` attribute 沒作用、`LowStockProduct` 命名空間放錯）並修掉了；「並自己確認」那半句我自己還沒再重看一次 diff
6. [x] 至少 3 個新測試，`dotnet test` 全綠 —— 6 個新測試（4 個 service 層 + 2 個 controller 層），`dotnet test` 44/44 全綠

---

## 附錄：值得留下的對話片段

**Bug 1 的重現回報**（有效的原因：雖然不是精確頁碼，但足以讓 agent 鎖定「排序 + skip」相關的程式碼，而不是去猜其他分頁參數）：

> "the last page is empty after creating the new order"

**Bug 2 定位根因後的確認流程**（agent 先用具體數字算給我看兩段折扣邏輯衝突在哪 —— 900 元的商品被 Gold 折扣算成 810 元，而不是預期的 900 元 —— 再問我要不要動手，而不是直接改）：

> Agent：「…net effect for a NT$1,000 item, qty 1: Gold: … subtotal 900 × 0.9 = 810 shown as total. Expected … is 900. … Want me to make that change and add regression tests?」
> 我：「yes, go ahead and add the tests」

**練習 3：code-reviewer 抓到的兩個問題**（節錄，展示「請 agent review 自己的實作」實際上會抓到什麼）：

> 1. (Medium) The `[Range]` DataAnnotation on `LowStockViewModel.Threshold` is dead code — real validation is a hand-duplicated check… Two independent sources of truth for one rule will drift.
> 5. (Low, nitpick) `LowStockProduct` (in `Core.Services` namespace) is referenced from `IProductRepository` (in `Core.Interfaces`). A repository interface depending on a type namespaced under `Services` is a minor layering inversion/naming smell.
