using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Utilities;
using MmmTool.DayContent.Core;

namespace MmmTool.Notes.Main;

/// <summary>
/// 「表示する内容」の枠の中の、他の機能の内容 1 つ分の設定で、出すか・全員か・実績の開始日・選択肢を持つ。
/// </summary>
/// <remarks>
/// 出すかどうかと、選択肢の選びは、この PC の設定に覚える。「自分 / 全員」と実績の開始日は覚えない。
/// 実績の開始日は、表示する日が変わったら、提供する機能が決める既定に戻る。
/// </remarks>
public sealed partial class DayContentSourceItem : ObservableObject
{
    /// <summary>出すかどうかを覚える設定のキー</summary>
    private readonly string _enabledKey;

    /// <summary>この PC の設定の保存先</summary>
    private readonly ISettingsStore _settings;

    /// <summary>変わったときに、画面を作り直す処理</summary>
    private Action? _changed;

    /// <summary>実績の開始日を、コードで設定している最中か (手で変えたと数えないため)</summary>
    private bool _isSettingFrom;

    /// <summary>設定する</summary>
    /// <param name="source">内容の提供元</param>
    /// <param name="settings">この PC の設定の保存先</param>
    /// <param name="isShared">複数の人で共有するか (全員の選択を出す)</param>
    /// <param name="changed">設定が変わったときに、画面を作り直す処理</param>
    public DayContentSourceItem(IDayContentSource source, ISettingsStore settings, bool isShared, Action changed)
    {
        Source = source;
        IsShared = isShared;
        _settings = settings;
        _enabledKey = $"Notes.Sources.{source.Key}.Enabled";
        IsEnabled = settings.Get(_enabledKey, false);
        foreach (var option in source.Options)
        {
            Options.Add(new DayContentOptionItem(option, settings.Get(option.Key, option.Default), OnOptionChanged));
        }

        // 最初の値を入れた間は、変わったことにしない
        _changed = changed;
    }

    /// <summary>内容の提供元</summary>
    public IDayContentSource Source { get; }

    /// <summary>画面に出す名前</summary>
    public string Title => Source.Title;

    /// <summary>複数の人で共有するか</summary>
    /// <remarks>DB モードのとき true で、全員の選択を出す。</remarks>
    public bool IsShared { get; }

    /// <summary>真偽の選択肢</summary>
    public ObservableCollection<DayContentOptionItem> Options { get; } = [];

    /// <summary>実績の開始日の既定を決めた、表示する日 (まだ決めていなければ null)</summary>
    public DateOnly? FromFor { get; private set; }

    /// <summary>実績の開始日 (期間の開始日)</summary>
    public DateOnly From { get; private set; }

    /// <summary>この内容を出すか</summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    /// <summary>全員の分を出すか</summary>
    /// <remarks>false は自分だけ。覚えない。</remarks>
    [ObservableProperty]
    public partial bool Everyone { get; set; }

    /// <summary>実績の開始日の選択部品に渡す値</summary>
    public DateTimeOffset? FromPickerDate
    {
        get => FromFor is null ? null : new DateTimeOffset(From.ToDateTime(TimeOnly.MinValue));
        set
        {
            if (_isSettingFrom || value is not { } picked)
            {
                return;
            }
            var from = DateOnly.FromDateTime(picked.DateTime);

            // 期間の終わり (表示する日)以降は選べない。前日までに直す
            if (FromFor is { } day && from >= day)
            {
                from = day.AddDays(-1);
                _isSettingFrom = true;
                try
                {
                    From = from;
                    OnPropertyChanged(nameof(FromPickerDate));
                }
                finally
                {
                    _isSettingFrom = false;
                }
            }
            else
            {
                From = from;
            }
            _changed?.Invoke();
        }
    }

    /// <summary>選択肢の値</summary>
    public IReadOnlyDictionary<string, bool> OptionValues => Options.ToDictionary(o => o.Key, o => o.IsOn);

    /// <summary>表示する日に合わせて、実績の開始日の既定を決める</summary>
    /// <param name="day">表示する日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>決める処理の完了を表すタスク</returns>
    /// <remarks>表示する日が前に決めたときと同じなら、手で選んだ値を残す。</remarks>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">既定を読めなかった。</exception>
    public async Task EnsureFromAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        if (FromFor == day)
        {
            return;
        }
        var from = await Source.GetDefaultFromAsync(day, cancellationToken);
        _isSettingFrom = true;
        try
        {
            From = from;
            FromFor = day;
            OnPropertyChanged(nameof(FromPickerDate));
        }
        finally
        {
            _isSettingFrom = false;
        }
    }

    /// <summary>出すかどうかと選択肢を、この PC の設定から読み直す</summary>
    /// <returns>値が変わったか</returns>
    /// <remarks>作業報告の窓など、別の画面が同じ設定を変えたときに、合わせるため。読み直した値は、書き戻さず、画面の作り直しもしない。</remarks>
    public bool SyncFromSettings()
    {
        var saved = _changed;
        _changed = null;
        try
        {
            var changed = false;
            var enabled = _settings.Get(_enabledKey, false);
            if (enabled != IsEnabled)
            {
                IsEnabled = enabled;
                changed = true;
            }
            foreach (var option in Options)
            {
                var value = _settings.Get(option.Key, Source.Options.First(o => o.Key == option.Key).Default);
                if (value != option.IsOn)
                {
                    option.IsOn = value;
                    changed = true;
                }
            }
            return changed;
        }
        finally
        {
            _changed = saved;
        }
    }

    /// <summary>出すかどうかが変わったら、覚えて、画面を作り直す</summary>
    /// <param name="value">新しい値</param>
    partial void OnIsEnabledChanged(bool value)
    {
        if (_changed is null)
        {
            return;
        }
        _settings.SetAsync(_enabledKey, value).Forget();
        _changed();
    }

    /// <summary>全員の選択が変わったら、画面を作り直す</summary>
    /// <param name="value">新しい値</param>
    partial void OnEveryoneChanged(bool value) => _changed?.Invoke();

    /// <summary>選択肢が変わったら、覚えて、画面を作り直す</summary>
    /// <param name="option">変わった選択肢</param>
    private void OnOptionChanged(DayContentOptionItem option)
    {
        if (_changed is null)
        {
            return;
        }
        _settings.SetAsync(option.Key, option.IsOn).Forget();
        _changed?.Invoke();
    }
}
