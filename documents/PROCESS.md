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

**活動 2 練習 1**：先請 agent 掃一次專案有沒有新練習，它從 git log 注意到新增的 `activity-2-custom-mcp.md` 直接找到了活動 2，順帶把 `mcp-security-attack-vectors.md` 也讀完摘要給我。確認完內容之後我才明確說「照練習 1 把 `src/OrderHub.Mcp` scaffold 出來」——沒有讓它自己猜要不要順便做練習 0（Playwright）或後面幾題，一次只推進一個練習。

### 2. AI 幫上大忙的地方

Bug 1（分頁）最明顯：我只回了一句「the last page is empty after creating the new order」，agent 就直接在 `OrderRepository.GetPagedAsync` 抓到 `Skip(page * pageSize)` 應該是 `Skip((page - 1) * pageSize)` 的 off-by-one，還一次解釋清楚「新訂單在第一頁不見」跟「最後一頁空白」其實是同一個根因——比我自己去 trace 分頁邏輯快很多。

Bug 3（庫存不回補）也是同樣模式：agent 直接指出 `CancelOrderAsync` 裡 `order.Status = Cancelled` 那行寫在檢查 `order.Status == Pending/Confirmed` **之前**，導致回補庫存的那段 `if` 恆假、根本是死碼。這種「順序寫反」的 bug，靠肉眼掃一次 code 就抓到了，比我自己盯著看要快很多。

**練習 3**：`code-reviewer` subagent 這次真的抓到兩個實質問題，不是走過場——`LowStockViewModel.Threshold` 上的 `[Range]` attribute 其實從沒被用到（controller 手動另外複製了一份一模一樣的檢查，兩處邏輯以後會各自漂移），以及 `LowStockProduct` 這個型別放在 `Core.Services` 命名空間卻被 `Core.Interfaces` 底下的介面引用（命名空間方向反了）。兩個都當場修掉，比我自己看 diff 更容易漏掉這種「能動但不對」的細節。

**活動 2 練習 1**：兩個地方比我自己動手快很多。第一，`CLAUDE.md` 寫著「加 NuGet 套件前要先跟我確認」，agent 在跑 `dotnet add package` 之前真的停下來問我 `ModelContextProtocol` 跟 `Microsoft.Extensions.Hosting` 這兩個套件可不可以加，沒有自己先斬後奏。第二，`dotnet new console` 預設把新專案的 `TargetFramework` 建成 `net10.0`，但其他三個專案都是 `net8.0`——agent 自己發現這個不一致並改掉，不然這種「編譯得過但跟專案慣例不一致」的細節我很可能事後才會注意到。

**活動 2 練習 3（before/after 對照）**：模擬「沒有 MCP」的情況——問「哪些商品庫存低於 5？」時不呼叫工具，改用 `sqlcmd -S localhost -d OrderHubTraining -Q "SELECT Sku, Name, StockQuantity FROM Products WHERE StockQuantity < 5 AND IsActive = 1 ORDER BY StockQuantity ASC"` 直接查 DB——要自己想清楚 `IsActive` 這個過濾條件、自己下 `ORDER BY`，而且這台 Windows 機器的 `sqlcmd` 把中文欄位印成亂碼（例如「晨光 行動電源」變成 `���� �Є��Դ`），商品名稱完全看不懂，還得另外處理編碼問題才能對答案。裝上 MCP 之後，同一個問題一次 `low_stock(threshold=5)` 呼叫就拿到乾淨的 JSON：5 筆（SKU-1048/1005/1023/1032/1014，庫存 2～4），中文名稱正常顯示，門檻／排序／停售過濾這些業務規則完全不用自己重新想一遍——直接複用 service 層已經寫好、練習 3（活動 1）驗證過的規則，兩條路徑算出來的商品清單完全一致。

**活動 2 練習 4（`cancel_order`）**：實作完要接回 Claude Code 測試時撞到一個小地雷——`dotnet build src/OrderHub.Mcp` 直接失敗，`MSB3027`/`MSB3021` 說 `OrderHub.Core.dll`/`OrderHub.Infrastructure.dll` 被 `OrderHub.Mcp.exe` (PID 32080) 鎖住——因為練習 3 註冊進 `.mcp.json` 之後，Claude Code 自己就一直開著一個 `dotnet run --project src/OrderHub.Mcp` 常駐行程在餵前三個工具，改完程式碼要重新編譯，那個常駐行程得先關掉才能釋放檔案鎖。`taskkill /PID 32080 /F` 之後 build 才過，但代價是這個 session 裡 Claude Code 對 orderhub MCP 的連線也跟著斷了（`get_order`/`low_stock`/`customer_orders` 全部變成「server disconnected」）——之後要重新用工具就得在 Claude Code 裡手動 reconnect（`/mcp`）。這也順帶回答了 5a 地雷區沒明講的一件事：**改 MCP server 程式碼之後一定要讓現有連線重啟才會生效，不是存檔就自動熱重載**。

### 3. AI 誤導我的地方，與我如何發現

Bug 2 一開始 agent 純粹看 code，就先下了一個判斷：「Gold 應該是折扣打兩次、金額比手算少；Silver 應該是正常的」——這其實是照抄 `activity-guideline.md` 裡描述的客訴反推出來的，不是真的從我的觀察來的。等我回報「兩個 tier 金額其實都沒變」時，這個假設就先被推翻一次；後來我又補充「其實是 Gold 正常、Silver 沒打折」，agent 才修正說法，並且提醒我兩種可能（單價欄位 vs. 總額欄位）對應到不同的根因，要我確認我看的是哪一欄。

老實說，我後來選擇「跳過頁面重現、直接看 code」，所以最後 agent 提出的 root cause（折扣邏輯散落在 `CreateOrderAsync` 和 `CalculateTotal` 兩處）從頭到尾都沒有拿實際頁面上的精確數字驗證過，只用「兩個 tier 修完後都『看起來對了』」帶過。這其實不是靠對照 code 或跑測試發現的，是回頭寫這份心得時才意識到「我沒有真的驗證」。

**活動 2 練習 4**：想用 `npx @modelcontextprotocol/inspector --cli` 這個非互動 CLI 模式直接驗證 `cancel_order` 的 annotations（本來想比照練習 2 的瀏覽器 Inspector，但這次想全程用指令跑），結果同一個指令換幾種參數順序（`--method` 放 target 前/後、加不加 `--` 分隔符）就分別跳出三種不一樣、且互相矛盾的錯誤（`No servers found in config file`／`Target is required`／`Method is required`），花了好幾輪才確認是這個版本 CLI 的參數解析本身不穩定，不是我指令下錯——最後放棄 Inspector CLI，改寫一個十幾行的 Node 腳本直接對 `dotnet run --project src/OrderHub.Mcp` 送原生 JSON-RPC（`initialize` → `tools/list` → `tools/call`），才順利拿到 annotations 跟 `cancel_order` 的回應內容。教訓：官方工具的 `--help` 文字不代表當下裝到的版本行為一致，卡住超過一兩次嘗試就該考慮繞道，而不是一直換參數排列組合硬試。

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

練習 4

1. [x] 重構後 `dotnet test` 全綠 —— 44/44 全綠，包含練習 2、3 補的所有回歸測試；另外對跑著的網站補跑了一次真實表單（建單成功、重複商品仍正確被拒絕），確認不是只有 unit test 綠燈
2. [x] 我能說出這次重構「改善了什麼、沒有改變什麼」。
3. [x] 我有在 code review 的角度看過 diff（不是 agent 說好就好）

### 第二階段 — MCP Server

練習 1

1. [x] `dotnet build src/OrderHub.Mcp` 成功 —— 另外也整個 solution build 過一次，確認新專案沒有連帶弄壞其他三個
2. [x] 一個獨立 commit（訊息說明新增了哪些工具） —— `ab6a2bb`

練習 2

1. [x] 三個工具都列得出來，且 description、參數說明如我所寫 —— Inspector 顯示的 description/參數說明跟 `OrderHubTools.cs` 裡寫的完全一致
2. [x] 手動呼叫 `LowStock`(threshold=10)，回傳的商品和 `/Products` 頁面上的低庫存商品一致 —— 完全對得上
3. [x] 呼叫 `GetOrder` 用一個不存在的 Id，回應是清楚的錯誤訊息而不是 exception dump —— 呼叫 `GetOrder(30000)`，回傳 `找不到訂單 30000`，沒有 exception dump

練習 2 全是手動用 Inspector 驗證，沒有動到 `OrderHubTools.cs` 的程式碼——練習指南本身也沒有要求這步獨立 commit。

練習 3

1. [x] Claude Code 輸入 `/mcp` 能看到 orderhub server 與三個工具 —— `training-repo/.mcp.json` 已經是照指南格式寫好（`command: dotnet`, `args: ["run", "--project", "src/OrderHub.Mcp"]`），連線後 `get_order`/`low_stock`/`customer_orders` 三個工具都可直接呼叫，description 跟 `OrderHubTools.cs` 一致
2. [x] 對照實驗完成且記錄 —— 見上方「AI 幫上大忙的地方」的 before/after 段落：沒有 MCP 得自己寫 SQL、自己記得 `IsActive` 過濾條件、還要處理中文亂碼；有 MCP 一次 `low_stock(threshold=5)` 呼叫拿到乾淨結果，兩邊算出的 5 筆商品（SKU-1048/1005/1023/1032/1014）完全一致
3. [x] `.mcp.json` 進 git，一個獨立 commit

練習 4

1. [x] MCP Inspector 中 `cancel_order` 的 annotations 如所標（`destructiveHint`、`idempotentHint=false`），三個唯讀工具則顯示 read-only —— 瀏覽器版 Inspector CLI 這次卡關（見上方「AI 誤導我的地方」），改用自寫的 Node 腳本對 server 送原生 JSON-RPC `tools/list`：`get_order`/`low_stock`/`customer_orders` 三個都回 `"annotations":{"readOnlyHint":true}`，`cancel_order` 回 `"annotations":{"destructiveHint":true,"idempotentHint":false}`，跟程式碼標註完全一致
2. [x] 對 agent 說「幫我取消訂單 X」：觀察權限確認提示——你按允許之前，資料不會被動到 —— 改完程式碼後為了重新 build，把常駐的 `OrderHub.Mcp.exe`（PID 32080）process kill 掉導致 orderhub 連線斷線；用 `/mcp` 重新連線後，實際對 agent 說「try to cancel order 207」，因為 `cancel_order` 標了 `Destructive = true`，Claude Code 在真的呼叫工具前跳出權限確認提示，按下允許後才執行——確認 210 那次是繞過 client 直接打 JSON-RPC，這次才是真的走 Claude Code 的確認流程。訂單 207（SKU-1001 × 2）取消後回應「訂單 207 已取消,庫存已回補」，查 DB 確認 `Orders.Status`（Id 207）已變成 3（Cancelled），`Products.StockQuantity`（SKU-1001）也已回補
3. [x] 取消一筆待處理訂單成功，回 `/Products` 頁面確認庫存有回補 —— 沒有動用既有客訴單／seed 訂單，怕的是 Cancelled 狀態無法復原；改用 SQL 手動插入一筆一次性測試訂單（#210，客戶 1、SKU-1044 × 2，插入時同步把庫存從 98 扣到 96，模擬真實下單）。呼叫 `cancel_order(210)` 後回應「訂單 210 已取消,庫存已回補」，查 DB 確認 `Products.StockQuantity`（Id 44）真的從 96 回補到 98、`Orders.Status`（Id 210）變成 3（Cancelled）
4. [x] 對同一筆訂單再取消一次、或挑一筆已出貨訂單取消：得到清楚的拒絕訊息而非 exception dump —— 對剛取消的 #210 再呼叫一次 `cancel_order`，回「取消失敗:狀態為 Cancelled 的訂單不可取消」；另外找一筆 seed 資料裡已出貨的訂單（#2，Status=Shipped）呼叫，回「取消失敗:狀態為 Shipped 的訂單不可取消」——兩次都是乾淨的文字訊息，沒有 stack trace
5. [x] 獨立 commit；PROCESS.md 記錄

練習 5

1. [x] MCP Inspector:Resources 分頁讀得到 `orderhub://discount-rules`;Prompts 分頁能帶 `threshold` 參數取得展開後的訊息 —— 瀏覽器版 Inspector 這次沒再試（練習 4 已經踩過 CLI 版的坑），沿用練習 4 那套自寫 Node 腳本改打 `resources/list`／`resources/read`／`prompts/list`／`prompts/get`：resource 內容正確顯示 `Standard:不打折（0%）`／`Silver:折扣 5%`／`Gold:折扣 10%`；`prompts/get(threshold=5)` 回傳的訊息裡 `low_stock 工具(threshold=5)` 這段確實把參數代入進去了
2. [x] Claude Code:`@` 選 resource 後問折扣問題,agent 用 resource 內容作答（Codex 用戶:Inspector 讀出 resource 內容貼進對話,問同一題）—— `/mcp` 重新連線後實際用 `@orderhub:orderhub://discount-rules` 附上 resource,問「what is the current active discount」：agent 完全沒有另外呼叫任何工具或去讀 `OrderService.cs`,系統直接把 resource 全文（`Standard 0%／Silver 5%／Gold 10%`）塞進 context,agent 照原文回答並換算「Gold 買 1000 元應付 900 元」——確認 resource 真的是「background 知識直接進 context」，不是又一種要另外呼叫的 tool
3. [x] Claude Code:`/mcp__orderhub__low_stock_report` 一鍵產出採購建議表 —— 打 `/mcp__orderhub__low_stock_report 30`，展開成 prompt 裡寫的那三句指令（`threshold=30` 有正確代入），agent 接著自動呼叫 `low_stock(threshold=30)` 拿到 11 筆商品,再逐一查近 30 天銷量（排除 Cancelled，沿用 Activity 1 練習 3 同一套統計邏輯）組成採購建議表。這次意外收穫：SKU-1005、SKU-1022 雖然庫存低於門檻，但近 30 天銷量是 0，agent 沒有機械式地照庫存數字建議補貨，而是標成「暫緩，先確認」——prompt 範本裡「再用其他工具了解近期訂單狀況」這句就是為了引導出這種判斷，不是單純把 `low_stock` 的結果直接當補貨清單
4. [x] PROCESS.md 記錄 5c 第 3 點的思考;獨立 commit —— 見下方新增小節

**5c 第 3 點的思考**（折扣規則用 Resource 給 vs. 讓 agent 自己讀 `OrderService.cs`；prompt 範本放 server vs. 每個人自己打一段話）：

- **Resource vs. 讀 code**：這次實作 `OrderHubResources.DiscountRules()` 時故意不把 `0%/5%/10%` 寫死成字串常數，而是建構子注入 `IOrderService`，在方法裡即時呼叫 `orderService.GetDiscountRate(tier)` 組出 markdown——這樣以後 `OrderService` 改折扣率，resource 的文字會自動跟著變，不會出現「resource 說 9 折、code 早就改成 8.5 折」這種兩份真相同時存在的情況（練習指南「地雷區」提到的那個坑，這次是實作當下就避開，不是事後才發現）。如果反過來讓 agent 每次自己去讀 `OrderService.cs` 回答折扣問題，等於每次都要重新花一次工具呼叫＋重新推理那段 `switch` 表達式，多花 token、也多一次看漏某個 tier 的機會，而且團隊沒有一個「對外說法」的單一版本——每個人問出來的解釋用詞可能都不一樣。
- **Prompt 放 server vs. 自己打**：`low_stock_report` 這段話進了 git、有版本控制，任何人連上這個 MCP（不限 Claude Code）打 `/mcp__orderhub__low_stock_report` 都拿到同一段指令；以後想在報告裡加一欄「建議補貨量的信賴區間」之類的需求，只要改 `OrderHubPrompts.cs` 一個地方，所有人下次連線自動拿到新版。如果每個人自己憑印象打這段話，措辭、涵蓋的欄位會各自漂移（有人可能忘記講「排除 Cancelled」），而且沒有單一地方可以一次改進所有人的問法，只能口頭一個個提醒。

練習 0

1. [x] agent 能自己開瀏覽器完成操作並回傳截圖 —— Playwright MCP 這次已經是連線狀態（`browser_navigate`/`browser_snapshot`/`browser_select_option`/`browser_type`/`browser_click`/`browser_take_screenshot` 都可直接呼叫）。實際請 agent「建立一筆新訂單，截圖給我看結果頁」：先 `dotnet run --project src/OrderHub.Web` 起本機網站，navigate 到 `/Orders/Create`，用 `browser_snapshot` 讀出 accessibility tree 拿到客戶下拉／商品下拉／數量欄位的元素 ref，選客戶「陳志明（金卡會員）」、商品 SKU-1044（庫存 98）、數量 2，點「送出訂單」，自動導到 `/Orders/Details/211`，截圖顯示訂單 #211、小計 NT$6,220、會員折扣 (10%) -NT$622、應付總額 NT$5,598——全程沒有人手動點過滑鼠
2. [x] 回想活動 1 練習 2：當時人工重現 bug 的步驟，現在 agent 可以自己做——把這個對比記進 PROCESS.md —— 見下方新增小節

**練習 0 的對比（人工重現 vs. agent 用 Playwright 自己操作）**：活動 1 練習 2 的 Bug 1，是我自己在瀏覽器裡建單、翻頁、肉眼觀察「最後一頁空白」，再把這句話（"the last page is empty after creating the new order"）轉述給 agent——agent 收到的是我對畫面的**文字轉述**，不是畫面本身，過程中不精確的地方（例如沒給精確頁碼）就是這次轉述漏掉的資訊。這次用 Playwright MCP，agent 自己 `browser_snapshot` 讀到的是頁面的 accessibility tree／截圖是頁面本身的畫素——agent 能直接讀到「會員折扣 (10%)」「應付總額 NT$5,598」這些精確文字與數字，不用我用嘴巴形容「折扣好像不太對」再讓 agent 猜。差別是：**人工重現要靠人把觀察轉譯成語言，agent 自己操作則是直接讀取畫面內容，跳過轉譯這一層、也跳過轉譯會失真的風險**；但代價是我這次全程沒有自己盯著瀏覽器看，等於也少了一次「人工核對 agent 說法」的機會——這正好呼應 PROCESS.md 最上面那條原則：agent 的回答永遠要人工驗證，只是這次連「餵給 agent 的觀察」都是 agent 自己生成的，人工驗證的步驟不能省。

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
