# 会話履歴の削除スクリプトの共通部品 (Windows 版): 矢印キーで選ぶ画面。
# session_menu.py (WSL・Linux 版)と同じ操作にしている。直すときは両方を直す。
# Remove-ClaudeSession.ps1 と Remove-KiroSession.ps1 が dot-source で読み込む。

# 全角を 2 桁として表示幅を数える
function Get-CellWidth([string]$Text) {
    $w = 0
    foreach ($c in $Text.ToCharArray()) {
        $n = [int]$c
        $wide = ($n -ge 0x1100 -and $n -le 0x115F) -or ($n -ge 0x2E80 -and $n -le 0xA4CF) -or
                ($n -ge 0xAC00 -and $n -le 0xD7A3) -or ($n -ge 0xF900 -and $n -le 0xFAFF) -or
                ($n -ge 0xFE30 -and $n -le 0xFE6F) -or ($n -ge 0xFF00 -and $n -le 0xFF60) -or
                ($n -ge 0xFFE0 -and $n -le 0xFFE6)
        $w += if ($wide) { 2 } else { 1 }
    }
    return $w
}

# 表示幅が $Max に収まるように切り詰める
function Limit-CellWidth([string]$Text, [int]$Max) {
    if ((Get-CellWidth $Text) -le $Max) { return $Text }
    $sb = New-Object System.Text.StringBuilder
    $w = 0
    foreach ($c in $Text.ToCharArray()) {
        $cw = Get-CellWidth ([string]$c)
        if ($w + $cw -gt $Max - 1) { break }
        [void]$sb.Append($c)
        $w += $cw
    }
    return $sb.ToString() + '…'
}

# 選択結果。Action は ok / back / quit
function New-SelectionResult([string]$Action, [object[]]$Items = @()) {
    [pscustomobject]@{ Action = $Action; Items = $Items }
}

# 矢印キー・スペースで選ぶ
function Read-SelectionMenu([object[]]$Items, [scriptblock]$Format, [string]$Prompt, [string]$BackLabel) {
    $esc = [char]27
    $selected = New-Object 'System.Collections.Generic.HashSet[int]'
    $cursor = 0
    $offset = 0
    $pageSize = [Math]::Max(5, [Console]::WindowHeight - 5)
    $rows = [Math]::Min($Items.Count, $pageSize)
    $labels = @($Items | ForEach-Object { & $Format $_ })

    Write-Host $Prompt
    Write-Host "↑↓:移動  Space:選択/解除  Tab:選択して下へ  a:全選択/全解除  Enter:決定(未選択ならカーソル行)  Esc:$BackLabel  q/Ctrl+C:終了" -ForegroundColor DarkGray
    for ($i = 0; $i -le $rows; $i++) { Write-Host '' }
    $top = [Console]::CursorTop - ($rows + 1)
    $oldVisible = [Console]::CursorVisible
    [Console]::CursorVisible = $false
    $oldCtrlC = [Console]::TreatControlCAsInput
    [Console]::TreatControlCAsInput = $true   # Ctrl+C を強制終了にせず、キーとして受け取って q と同じ扱いにする

    try {
        while ($true) {
            if ($cursor -lt $offset) { $offset = $cursor }
            if ($cursor -ge $offset + $rows) { $offset = $cursor - $rows + 1 }
            $width = [Console]::WindowWidth - 1
            for ($r = 0; $r -lt $rows; $r++) {
                $i = $offset + $r
                $mark = if ($selected.Contains($i)) { '[x]' } else { '[ ]' }
                $arrow = if ($i -eq $cursor) { '>' } else { ' ' }
                $line = Limit-CellWidth "$arrow $mark $($labels[$i])" $width
                [Console]::SetCursorPosition(0, $top + $r)
                if ($i -eq $cursor) { Write-Host -NoNewline $line -ForegroundColor Cyan }
                else                { Write-Host -NoNewline $line }
                Write-Host -NoNewline "$esc[K"
            }
            [Console]::SetCursorPosition(0, $top + $rows)
            Write-Host -NoNewline "$($selected.Count) / $($Items.Count) 件選択中$esc[K" -ForegroundColor DarkGray

            $key = [Console]::ReadKey($true)
            if ($key.Key -eq 'C' -and ($key.Modifiers -band [ConsoleModifiers]::Control)) {
                return New-SelectionResult 'quit'
            }
            # IME がオンだと Space・a・q が全角文字 (キーの種類なし)で届くので、文字から読み替える
            $keyName = switch ($key.KeyChar) {
                ([char]0x3000) { 'Spacebar' }
                { 'ａ', 'Ａ' -ccontains $_ } { 'A' }
                { 'ｑ', 'Ｑ' -ccontains $_ } { 'Q' }
                default { $key.Key.ToString() }
            }
            switch ($keyName) {
                'UpArrow'   { if ($cursor -gt 0) { $cursor-- } }
                'DownArrow' { if ($cursor -lt $Items.Count - 1) { $cursor++ } }
                'PageUp'    { $cursor = [Math]::Max(0, $cursor - $rows) }
                'PageDown'  { $cursor = [Math]::Min($Items.Count - 1, $cursor + $rows) }
                'Home'      { $cursor = 0 }
                'End'       { $cursor = $Items.Count - 1 }
                'Spacebar'  { if (-not $selected.Add($cursor)) { [void]$selected.Remove($cursor) } }
                'Tab'       {
                    # 選択/解除して隣へ進む (Tab=下、Shift+Tab=上)。連続した行を続けて選びやすい
                    if (-not $selected.Add($cursor)) { [void]$selected.Remove($cursor) }
                    if ($key.Modifiers -band [ConsoleModifiers]::Shift) { if ($cursor -gt 0) { $cursor-- } }
                    elseif ($cursor -lt $Items.Count - 1) { $cursor++ }
                }
                'A'         {
                    if ($selected.Count -eq $Items.Count) { $selected.Clear() }
                    else { 0..($Items.Count - 1) | ForEach-Object { [void]$selected.Add($_) } }
                }
                'Enter'     {
                    # 何も選んでいなければ、カーソル位置の 1 件を対象にする
                    $picked = if ($selected.Count -gt 0) { @($selected | Sort-Object) } else { @($cursor) }
                    return New-SelectionResult 'ok' @($picked | ForEach-Object { $Items[$_] })
                }
                'Backspace' { return New-SelectionResult 'back' }
                'Escape'    { return New-SelectionResult 'back' }
                'Q'         { return New-SelectionResult 'quit' }
            }
        }
    }
    finally {
        [Console]::TreatControlCAsInput = $oldCtrlC
        [Console]::CursorVisible = $oldVisible
        [Console]::SetCursorPosition(0, $top + $rows + 1)
    }
}

# 番号入力で選ぶ (キー操作できないとき用)
function Read-SelectionText([object[]]$Items, [scriptblock]$Format, [string]$Prompt, [string]$BackLabel) {
    Write-Host $Prompt
    for ($i = 0; $i -lt $Items.Count; $i++) {
        Write-Host ('{0,3}. {1}' -f ($i + 1), (& $Format $Items[$i]))
    }
    while ($true) {
        $in = Read-Host "番号 (例: 1,3,5-7 / a=すべて / b=$BackLabel / q=終了)"
        if ($null -eq $in -or $in.Trim() -match '^[qQ]$') { return New-SelectionResult 'quit' }
        $in = $in.Trim()
        if ($in -eq '' -or $in -match '^[bB]$') { return New-SelectionResult 'back' }
        if ($in -match '^[aA]$') { return New-SelectionResult 'ok' $Items }
        $idx = New-Object System.Collections.Generic.SortedSet[int]
        $ok = $true
        foreach ($part in $in -split '[,\s]+' | Where-Object { $_ }) {
            if ($part -match '^(\d+)-(\d+)$') { $a = [int]$Matches[1]; $b = [int]$Matches[2] }
            elseif ($part -match '^(\d+)$')   { $a = $b = [int]$Matches[1] }
            else { $ok = $false; break }
            if ($a -lt 1 -or $b -gt $Items.Count -or $a -gt $b) { $ok = $false; break }
            $a..$b | ForEach-Object { [void]$idx.Add($_) }
        }
        if ($ok -and $idx.Count -gt 0) {
            return New-SelectionResult 'ok' @($idx | ForEach-Object { $Items[$_ - 1] })
        }
        Write-Host '入力が正しくありません。' -ForegroundColor Yellow
    }
}

function Read-Selection([object[]]$Items, [scriptblock]$Format, [string]$Prompt, [string]$BackLabel) {
    if ([Console]::IsInputRedirected) { return Read-SelectionText $Items $Format $Prompt $BackLabel }
    return Read-SelectionMenu $Items $Format $Prompt $BackLabel
}
