using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Clipboards;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Reminders.ChannelEdit;

/// <summary>
/// 送信先の登録画面。送信先 1 件を新規登録・編集して保存する。
/// </summary>
/// <remarks>
/// 種類は、新規のときだけ選べて、編集では変えない。
/// ntfy は、トピック名を自動で作って見せる。コピーと作り直しもできる。
/// Discord は、Webhook の URL を貼る。画面では伏せる。
/// Web 版通知は、Web 版の画面でブラウザが宛先を作るので、ここでは新規に登録できず、登録名だけを直せる。宛先の値は、そのまま保つ。
/// 保存に成功したら <see cref="CloseRequested"/> で画面を閉じてもらう。
/// </remarks>
public sealed partial class NotificationChannelEditViewModel : ObservableObject
{
    /// <summary>登録名が未入力のときのエラー</summary>
    private const string NameRequiredMessage = "登録名を入力してください。";

    /// <summary>Discord の Webhook の URL が正しくないときのエラー</summary>
    private const string UrlInvalidMessage = "Discord の Webhook の URL を入力してください。";

    /// <summary>ntfy のトピック名が正しくないときのエラー</summary>
    private const string TopicInvalidMessage = "トピック名は、半角の英数字・ハイフン・アンダースコアで、64 文字までにしてください。";

    /// <summary>送信先の保存先</summary>
    private readonly INotificationChannelRepository _channels;
    /// <summary>確認ダイアログ</summary>
    private readonly IDialogService _dialogs;
    /// <summary>クリップボード</summary>
    private readonly IClipboardService _clipboard;

    /// <summary>編集対象。新規なら null</summary>
    private NotificationChannel? _target;

    /// <summary>ViewModel を作る</summary>
    /// <param name="channels">送信先の保存先</param>
    /// <param name="dialogs">確認ダイアログを開く</param>
    /// <param name="clipboard">クリップボード</param>
    public NotificationChannelEditViewModel(INotificationChannelRepository channels, IDialogService dialogs, IClipboardService clipboard)
    {
        _channels = channels;
        _dialogs = dialogs;
        _clipboard = clipboard;
        KindOptions = CreateOptions(NotificationChannelKind.Ntfy, NotificationChannelKind.Discord);
        SelectedKind = KindOptions[0];
        Load(null);
    }

    /// <summary>画面を閉じてほしい</summary>
    /// <remarks>保存したときは保存した内容、キャンセルのときは null を渡す。</remarks>
    public event EventHandler<NotificationChannel?>? CloseRequested;

    /// <summary>画面のタイトル</summary>
    /// <remarks>新規は「送信先の登録」、編集は「送信先の編集」。</remarks>
    [ObservableProperty]
    public partial string WindowTitle { get; private set; } = "";

    /// <summary>種類の選択肢</summary>
    /// <remarks>新規は ntfy と Discord。編集は、種類を変えられないので、登録済みの種類だけ。</remarks>
    [ObservableProperty]
    public partial IReadOnlyList<NotificationChannelKindOption> KindOptions { get; private set; }

    /// <summary>選んでいる種類</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNtfy))]
    [NotifyPropertyChangedFor(nameof(IsDiscord))]
    [NotifyPropertyChangedFor(nameof(IsWebPush))]
    public partial NotificationChannelKindOption SelectedKind { get; set; }

    /// <summary>種類を選べるか (新規のときだけ)</summary>
    [ObservableProperty]
    public partial bool IsKindEditable { get; private set; }

    /// <summary>種類が ntfy か</summary>
    public bool IsNtfy => SelectedKind.Kind == NotificationChannelKind.Ntfy;

    /// <summary>種類が Discord か</summary>
    public bool IsDiscord => SelectedKind.Kind == NotificationChannelKind.Discord;

    /// <summary>種類が Web 版通知か</summary>
    public bool IsWebPush => SelectedKind.Kind == NotificationChannelKind.WebPush;

    /// <summary>登録名</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = "";

    /// <summary>登録名のエラー。無ければ null</summary>
    [ObservableProperty]
    public partial string? NameError { get; private set; }

    /// <summary>ntfy のトピック名 (最初は自動で作ったもの。自分で決めてもよい)</summary>
    [ObservableProperty]
    public partial string NtfyTopic { get; set; } = "";

    /// <summary>ntfy のトピック名のエラー。無ければ null</summary>
    [ObservableProperty]
    public partial string? TopicError { get; private set; }

    /// <summary>Discord の Webhook の URL</summary>
    [ObservableProperty]
    public partial string DiscordUrl { get; set; } = "";

    /// <summary>Discord の Webhook の URL のエラー。無ければ null</summary>
    [ObservableProperty]
    public partial string? UrlError { get; private set; }

    /// <summary>保存のエラー</summary>
    public ErrorState SaveError { get; } = new();

    /// <summary>入力欄に読み込む</summary>
    /// <param name="target">編集する送信先。新規なら null</param>
    /// <remarks>新規は ntfy で、登録名は空、トピック名は新しく作ったもの。入力欄には例を薄く出す。編集は対象の値を読み込み、種類は変えられない。</remarks>
    public void Load(NotificationChannel? target)
    {
        _target = target;
        WindowTitle = target is null ? "送信先の登録" : "送信先の編集";
        IsKindEditable = target is null;
        KindOptions = target is null ? CreateOptions(NotificationChannelKind.Ntfy, NotificationChannelKind.Discord) : CreateOptions(target.Kind);
        SelectedKind = KindOptions[0];
        Name = target?.Name ?? "";
        NtfyTopic = target is { Kind: NotificationChannelKind.Ntfy } ? target.Value : NotificationChannelRules.CreateNtfyTopic();
        DiscordUrl = target is { Kind: NotificationChannelKind.Discord } ? target.Value : "";

        // 読み込みでエラーは出さない (エラーは保存しようとしたときだけ)
        NameError = null;
        UrlError = null;
        TopicError = null;
        SaveError.Clear();
    }

    /// <summary>登録名が変わったら、登録名のエラーを消す</summary>
    /// <param name="value">変更後の登録名</param>
    partial void OnNameChanged(string value) => NameError = null;

    /// <summary>トピック名が変わったら、トピック名のエラーを消す</summary>
    /// <param name="value">変更後のトピック名</param>
    partial void OnNtfyTopicChanged(string value) => TopicError = null;

    /// <summary>URL が変わったら、URL のエラーを消す</summary>
    /// <param name="value">変更後の URL</param>
    partial void OnDiscordUrlChanged(string value) => UrlError = null;

    /// <summary>入力値を検証して保存し、閉じる</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>登録名が未入力・Discord の URL が正しくないときは、エラーを出して保存しない。保存に失敗したらエラーを出して閉じない。</remarks>
    [RelayCommand]
    private async Task SaveAsync()
    {
        SaveError.Clear();
        var name = Name.Trim();
        // Web 版通知の宛先の値は、ここでは作れも直せもしないので、読み込んだ値のまま保つ
        var value = IsNtfy ? NtfyTopic.Trim() : IsDiscord ? DiscordUrl.Trim() : _target?.Value ?? "";

        var valid = true;
        if (name.Length == 0)
        {
            NameError = NameRequiredMessage;
            valid = false;
        }
        if (IsNtfy && !NotificationChannelRules.IsNtfyTopic(value))
        {
            TopicError = TopicInvalidMessage;
            valid = false;
        }
        if (IsDiscord && !NotificationChannelRules.IsDiscordWebhookUrl(value))
        {
            UrlError = UrlInvalidMessage;
            valid = false;
        }
        if (!valid)
        {
            return;
        }

        try
        {
            NotificationChannel? saved;
            if (_target is { } target)
            {
                var updated = target with { Name = name, Value = value };
                saved = await _channels.UpdateAsync(updated) ? updated : null;
                if (saved is null)
                {
                    SaveError.Show("保存できませんでした。この送信先は、削除されたか、ほかの人が登録したものです。");
                    return;
                }
            }
            else
            {
                saved = await _channels.AddAsync(new NotificationChannel { Kind = SelectedKind.Kind, Name = name, Value = value });
            }
            CloseRequested?.Invoke(this, saved);
        }
        catch (Exception ex) when (ex is DataFileException or ArgumentException)
        {
            SaveError.Show(ex.Message);
        }
    }

    /// <summary>保存せずに閉じる</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, null);

    /// <summary>種類の選択肢を作る</summary>
    /// <param name="kinds">選択肢にする種類 (並べる順)</param>
    /// <returns>種類と画面に出す名前の組</returns>
    private static NotificationChannelKindOption[] CreateOptions(params NotificationChannelKind[] kinds)
        => [.. kinds.Select(kind => new NotificationChannelKindOption(kind, NotificationChannelKinds.ToText(kind)))];

    /// <summary>ntfy のトピック名をクリップボードへコピーする</summary>
    [RelayCommand]
    private void CopyTopic() => _clipboard.SetText(NtfyTopic);

    /// <summary>ntfy のトピック名を作り直す</summary>
    /// <returns>作り直しの完了を表すタスク</returns>
    /// <remarks>古いトピック名は使えなくなり、スマホの ntfy のアプリにも新しいものを入れ直す必要があるので、確認してから。保存したときに反映される。</remarks>
    [RelayCommand]
    private async Task RegenerateTopicAsync()
    {
        if (await _dialogs.ConfirmAsync("トピック名の作り直し", "トピック名を作り直します。\nスマホの ntfy のアプリにも、新しいトピック名を登録し直してください。", "作り直す", "キャンセル"))
        {
            NtfyTopic = NotificationChannelRules.CreateNtfyTopic();
        }
    }
}
