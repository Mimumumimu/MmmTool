<#
.SYNOPSIS
  Kiro CLI の会話履歴を選んで削除する。

.DESCRIPTION
  ターミナル内で完結する (別ウィンドウなし)。
  1. 作業フォルダの一覧から選ぶ
  2. 選んだフォルダ内のセッションを選ぶ
  3. 確認のうえ、削除 → 2 に戻る (q で終了するまで繰り返せる)

  一覧の取得と削除は、kiro-cli の公式コマンド (chat --list-sessions / --delete-session)で行う。
  Kiro の削除は戻せない (ごみ箱へは送らない)。

  操作: ↑↓ 移動 / Space 選択・解除 / Tab 選択・解除して下へ (Shift+Tab は上へ) / a 全選択・全解除 / Enter 決定 (未選択ならカーソル行)
        Backspace Esc 1 つ前に戻る / q 終了
  入力がリダイレクトされている場合は番号入力 (例: 1,3,5-7 / a / b=戻る / q=終了)になる。

  -All を付けると手順 2 を省略し、選んだフォルダの全セッションを対象にする。
  -WhatIf を付けると、削除せず対象を表示するだけ。
  Kiro で開いているセッションは消さないこと (先に閉じる)。
#>
[CmdletBinding()]
param(
    [switch]$All,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

# 画面部品 (矢印キーで選ぶ画面)は同じフォルダの SessionMenu.ps1
. (Join-Path $PSScriptRoot 'SessionMenu.ps1')

if (-not (Get-Command kiro-cli -ErrorAction SilentlyContinue)) {
    Write-Host 'kiro-cli が見つかりません。'
    return
}

# 公式の削除コマンドが消し残す <id>.history / <id>.lock を探すフォルダ
$kiroHome = if ($env:KIRO_HOME) { $env:KIRO_HOME } else { Join-Path $env:USERPROFILE '.kiro' }
$sessionDir = Join-Path $kiroHome 'sessions\cli'

# kiro-cli を動かして標準出力の行を返す。エラー出力は捨てる (結果は一覧の取り直しで確かめる)
function Invoke-Kiro([string[]]$KiroArgs) {
    $ErrorActionPreference = 'Continue'   # 標準エラーの出力で止まらないように (この関数の中だけ)
    $old = [Console]::OutputEncoding
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    try { return @(& kiro-cli @KiroArgs 2>$null) }
    finally { [Console]::OutputEncoding = $old }
}

# 更新日時 (ISO 8601 の文字列、または PowerShell が変換した日時)をローカル時刻にする
function ConvertTo-LocalTime($Value) {
    if ($Value -is [datetime]) { return $Value.ToLocalTime() }
    return [datetimeoffset]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture).LocalDateTime
}

# タイトルを 1 行にして 60 文字で切り詰める
function Format-Title($Title) {
    $text = ([string]$Title -replace '\s+', ' ').Trim()
    if (-not $text) { return '(タイトルなし)' }
    if ($text.Length -gt 60) { $text = $text.Substring(0, 60) + '…' }
    return $text
}

# 全作業フォルダのセッションを、フォルダごとにまとめて新しい順に返す
function Get-Groups {
    $text = (Invoke-Kiro @('chat', '--list-sessions', '--all-cwds', '--format', 'json')) -join "`n"
    $start = $text.IndexOfAny([char[]]@('[', '{'))
    if ($start -lt 0) { return @() }
    try { $data = ConvertFrom-Json -InputObject $text.Substring($start) }
    catch { throw "セッション一覧を読み取れません。kiro-cli の出力: $text" }

    @(& {
        foreach ($entry in @($data)) {
            $cwd = if ($entry.cwd) { [string]$entry.cwd } else { '(cwd 不明)' }
            $sessions = @(& {
                foreach ($s in @($entry.sessions)) {
                    if (-not $s.sessionId) { continue }
                    [pscustomobject]@{
                        Id      = [string]$s.sessionId
                        Updated = ConvertTo-LocalTime $s.updatedAt
                        Title   = Format-Title $s.title
                        Cwd     = $cwd
                    }
                }
            } | Sort-Object Updated -Descending)
            if ($sessions.Count -gt 0) {
                [pscustomobject]@{
                    Name     = $cwd
                    Count    = $sessions.Count
                    Last     = $sessions[0].Updated
                    Sessions = $sessions
                }
            }
        }
    } | Sort-Object Last -Descending)
}

function Get-Sessions([object[]]$Groups) {
    @($Groups | ForEach-Object { $_.Sessions } | Sort-Object Updated -Descending)
}

function Remove-KiroSession([string]$Id) {
    # ID はファイル名に使うので、英数字・ハイフン・アンダースコアだけを受け付ける
    if ($Id -notmatch '^[0-9A-Za-z_-]+$') { return }
    # v3 のセッションは、削除できていても「見つからない」というエラーが出るので、結果は呼び出し側が一覧の取り直しで確かめる
    [void](Invoke-Kiro @('chat', '--delete-session', $Id))
    foreach ($ext in 'history', 'lock') {
        $path = Join-Path $sessionDir "$Id.$ext"
        if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force }
    }
}

# 画面の流れ: 1 作業フォルダ選択 → 2 セッション選択 → 3 確認・削除 → 2 へ
$step = 1
$pickedGroups = @()
$sessions = @()
$targets = @()

while ($true) {
    if ($step -eq 1) {
        $groups = Get-Groups
        if (-not $groups) { Write-Host '履歴がありません。'; return }

        $r = Read-Selection $groups {
            param($g) '{0:yyyy-MM-dd HH:mm} | {1,3} 件 | {2}' -f $g.Last, $g.Count, $g.Name
        } '作業フォルダを選択' '終了'
        if ($r.Action -ne 'ok') { Write-Host '終了しました。'; return }

        $pickedGroups = $r.Items
        $sessions = Get-Sessions $pickedGroups
        $step = 2
    }
    elseif ($step -eq 2) {
        if ($All) { $targets = $sessions; $step = 3; continue }

        $multi = @($pickedGroups).Count -gt 1
        $r = Read-Selection $sessions {
            param($s)
            $line = '{0:yyyy-MM-dd HH:mm} | {1}' -f $s.Updated, $s.Title
            if ($multi) { $line += "  [$($s.Cwd)]" }
            $line
        } '削除するセッションを選択' '戻る'
        if ($r.Action -eq 'quit') { Write-Host '終了しました。'; return }
        if ($r.Action -eq 'back') { $step = 1; continue }

        $targets = $r.Items
        $step = 3
    }
    else {
        Write-Host ''
        Write-Host "$(@($targets).Count) 件を削除します (戻せません)。"
        $targets | ForEach-Object { Write-Host ('  {0:yyyy-MM-dd HH:mm} | {1}' -f $_.Updated, $_.Title) }

        if ($WhatIf) {
            Write-Host '(-WhatIf のため削除しません)'
            $step = if ($All) { 1 } else { 2 }
            continue
        }
        $answer = Read-Host '実行しますか？ (y/N)'
        if ($answer -match '^[yY]') {
            foreach ($t in $targets) { Remove-KiroSession $t.Id }

            # 一覧を取り直して、消えたかを確かめる。同じフォルダのセッション選択へ戻る (残りが 1 件も無ければフォルダ選択へ)
            $groups = Get-Groups
            $remainIds = @($groups | ForEach-Object { $_.Sessions } | ForEach-Object { $_.Id })
            $failed = @($targets | Where-Object { $remainIds -contains $_.Id })
            if ($failed.Count -gt 0) {
                Write-Host "$($failed.Count) 件を削除できませんでした (Kiro で開いていないか確認してください)。" -ForegroundColor Yellow
                $failed | ForEach-Object { Write-Host ('  {0:yyyy-MM-dd HH:mm} | {1}' -f $_.Updated, $_.Title) }
            }
            if ($failed.Count -lt @($targets).Count) { Write-Host '完了しました。' -ForegroundColor Green }

            $names = @($pickedGroups | ForEach-Object { $_.Name })
            $pickedGroups = @($groups | Where-Object { $names -contains $_.Name })
            $sessions = if ($pickedGroups) { Get-Sessions $pickedGroups } else { @() }
            $step = if (@($sessions).Count -gt 0 -and -not $All) { 2 } else { 1 }
        }
        else {
            Write-Host '取りやめました。'
            $step = if ($All) { 1 } else { 2 }
        }
        Write-Host ''
    }
}
