<#
.SYNOPSIS
  Claude Code の会話履歴 (~/.claude/projects 配下の .jsonl)を選んでごみ箱へ移動する。

.DESCRIPTION
  ターミナル内で完結する (別ウィンドウなし)。
  1. プロジェクトの一覧から選ぶ
  2. 選んだプロジェクト内のセッションを選ぶ
  3. 確認のうえ、ごみ箱へ移動 → 1 に戻る (q で終了するまで繰り返せる)

  操作: ↑↓ 移動 / Space 選択・解除 / Tab 選択・解除して下へ (Shift+Tab は上へ) / a 全選択・全解除 / Enter 決定 (未選択ならカーソル行)
        Backspace Esc 1 つ前に戻る / q 終了
  入力がリダイレクトされている場合は番号入力 (例: 1,3,5-7 / a / b=戻る / q=終了)になる。

  -All を付けると手順 2 を省略し、選んだプロジェクトの全セッションを対象にする。
  -WhatIf を付けると、削除せず対象を表示するだけ。
  Claude Code で開いているセッションは消さないこと (先に閉じる)。
#>
[CmdletBinding()]
param(
    [switch]$All,
    [switch]$WhatIf,
    [string]$Root = (Join-Path $env:USERPROFILE '.claude\projects')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName Microsoft.VisualBasic

if (-not (Test-Path $Root)) {
    Write-Host "見つかりません: $Root"
    return
}

# 最初のユーザー発言 (先頭 60 行だけ読む)
function Get-FirstUserMessage([string]$Path) {
    foreach ($line in (Get-Content -LiteralPath $Path -TotalCount 60 -Encoding UTF8)) {
        try { $obj = $line | ConvertFrom-Json } catch { continue }
        if ($obj.type -ne 'user' -or $obj.isMeta) { continue }
        $content = $obj.message.content
        $text = if ($content -is [string]) { $content }
                else { ($content | Where-Object { $_.type -eq 'text' } | ForEach-Object { $_.text }) -join ' ' }
        if (-not $text -or $text.TrimStart().StartsWith('<')) { continue }
        return $text
    }
    return '(発言なし)'
}

# /resume に出るタイトル (custom-title 行の最後のもの。無ければ ai-title 行の最後のもの)。どちらも無ければ最初のユーザー発言
function Get-SessionTitle([string]$Path) {
    $title = $null
    # 1 行ずつ文字列で探す (Select-String はパイプラインを通るので、大きいファイルでは遅い)
    $customLine = $null
    $aiLine = $null
    foreach ($line in [System.IO.File]::ReadLines($Path, [System.Text.Encoding]::UTF8)) {
        if ($line.Contains('"type":"custom-title"')) { $customLine = $line }
        elseif ($line.Contains('"type":"ai-title"')) { $aiLine = $line }
    }
    if ($customLine) {
        try { $title = ($customLine | ConvertFrom-Json).customTitle } catch { }
    }
    if (-not $title -and $aiLine) {
        try { $title = ($aiLine | ConvertFrom-Json).aiTitle } catch { }
    }
    if (-not $title) { $title = Get-FirstUserMessage $Path }
    $title = ($title -replace '\s+', ' ').Trim()
    if ($title.Length -gt 60) { $title = $title.Substring(0, 60) + '…' }
    return $title
}

# 画面部品 (矢印キーで選ぶ画面)は同じフォルダの SessionMenu.ps1
. (Join-Path $PSScriptRoot 'SessionMenu.ps1')

function Get-Projects {
    @(Get-ChildItem -LiteralPath $Root -Directory | ForEach-Object {
        $files = @(Get-ChildItem -LiteralPath $_.FullName -Filter *.jsonl -File)
        [pscustomobject]@{
            Name  = $_.Name
            Count = $files.Count
            Last  = if ($files) { ($files | Measure-Object LastWriteTime -Maximum).Maximum } else { $null }
            Path  = $_.FullName
        }
    } | Where-Object { $_.Count -gt 0 } | Sort-Object Last -Descending)
}

function Get-Sessions([object[]]$Projects) {
    @(& {
        foreach ($p in $Projects) {
            foreach ($f in Get-ChildItem -LiteralPath $p.Path -Filter *.jsonl -File) {
                [pscustomobject]@{
                    Project = $p.Name
                    Updated = $f.LastWriteTime
                    Title   = Get-SessionTitle $f.FullName
                    File    = $f.FullName
                }
            }
        }
    } | Sort-Object Updated -Descending)
}

function Remove-ToRecycleBin([string]$File) {
    [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile($File, 'OnlyErrorDialogs', 'SendToRecycleBin')
    # セッションに付随するフォルダ (<セッションID>/)があれば一緒に移動
    $sub = [IO.Path]::Combine([IO.Path]::GetDirectoryName($File), [IO.Path]::GetFileNameWithoutExtension($File))
    if (Test-Path -LiteralPath $sub -PathType Container) {
        [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($sub, 'OnlyErrorDialogs', 'SendToRecycleBin')
    }
}

# 画面の流れ: 1 プロジェクト選択 → 2 セッション選択 → 3 確認・削除 → 1 へ
$step = 1
$pickedProjects = @()
$sessions = @()
$targets = @()

while ($true) {
    if ($step -eq 1) {
        $projects = Get-Projects
        if (-not $projects) { Write-Host '履歴がありません。'; return }

        $r = Read-Selection $projects {
            param($p) '{0:yyyy-MM-dd HH:mm} | {1,3} 件 | {2}' -f $p.Last, $p.Count, $p.Name
        } 'プロジェクトを選択' '終了'
        if ($r.Action -ne 'ok') { Write-Host '終了しました。'; return }

        $pickedProjects = $r.Items
        $sessions = Get-Sessions $pickedProjects
        $step = 2
    }
    elseif ($step -eq 2) {
        if ($All) { $targets = $sessions; $step = 3; continue }

        $multi = @($pickedProjects).Count -gt 1
        $r = Read-Selection $sessions {
            param($s)
            $line = '{0:yyyy-MM-dd HH:mm} | {1}' -f $s.Updated, $s.Title
            if ($multi) { $line += "  [$($s.Project)]" }
            $line
        } '削除するセッションを選択' '戻る'
        if ($r.Action -eq 'quit') { Write-Host '終了しました。'; return }
        if ($r.Action -eq 'back') { $step = 1; continue }

        $targets = $r.Items
        $step = 3
    }
    else {
        Write-Host ''
        Write-Host "$(@($targets).Count) 件をごみ箱へ移動します。"
        $targets | ForEach-Object { Write-Host ('  {0:yyyy-MM-dd HH:mm} | {1}' -f $_.Updated, $_.Title) }

        if ($WhatIf) {
            Write-Host '(-WhatIf のため削除しません)'
            $step = if ($All) { 1 } else { 2 }
            continue
        }
        $answer = Read-Host '実行しますか？ (y/N)'
        if ($answer -match '^[yY]') {
            foreach ($t in $targets) { Remove-ToRecycleBin $t.File }
            Write-Host '完了しました。' -ForegroundColor Green

            # 同じプロジェクトのセッション選択へ戻る。残りが 1 件も無ければプロジェクト選択へ
            $names = @($pickedProjects | ForEach-Object { $_.Name })
            $pickedProjects = @(Get-Projects | Where-Object { $names -contains $_.Name })
            $sessions = if ($pickedProjects) { Get-Sessions $pickedProjects } else { @() }
            $step = if (@($sessions).Count -gt 0 -and -not $All) { 2 } else { 1 }
        }
        else {
            Write-Host '取りやめました。'
            $step = if ($All) { 1 } else { 2 }
        }
        Write-Host ''
    }
}
