using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.Storage;
using MmmTool.Messages.Core;
using MmmTool.Users.Core;

namespace MmmTool.Messages.Main;

/// <summary>メッセージ送信の ViewModel (ページとウィンドウで共有する入力欄の状態)</summary>
/// <param name="repository">宛先の読み出しと、送信依頼の保存先</param>
/// <param name="settings">設定ストア (前回送った宛先を覚える)</param>
/// <param name="currentUser">今のユーザー (本文の署名に使う)</param>
/// <remarks>外部へ送る処理は、MmmTool には無い。「送信」は、依頼の行を DB に書くだけで、実際に送るのは MmmBatch。</remarks>
public sealed partial class MessageSendViewModel(IMessageSendRequestRepository repository, ISettingsStore settings, CurrentUser currentUser) : ObservableObject
{
    /// <summary>本文を署名だけの状態にしたとき (カーソルを本文の先頭へ置くため、画面が受ける)</summary>
    public event EventHandler? BodyReset;

    /// <summary>本文の最初の入力 (改行 2 つと「○○より」)。今のユーザーが分からなければ空文字</summary>
    /// <remarks>送る人が何を送るか見えるよう、本文欄に実際に入れる (送るときに足さない)。書き換えても消してもよい。</remarks>
    private string Signature => MessageDraft.CreateSignature(currentUser.User?.DisplayName);

    /// <summary>本文を署名だけの状態にする</summary>
    private void ResetBody()
    {
        Body = Signature;
        BodyReset?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>選べる宛先 (<see cref="InitializeAsync"/> で DB から読む)</summary>
    public ObservableCollection<MessageRecipientItem> Recipients { get; } = [];

    /// <summary>宛先を DB から読む</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>
    /// DB モードでないときは読まない。読めなかったときは、結果の表示に出す (続けられる失敗。今の一覧は変えない)。
    /// 宛先は、選んでいたもの、なければ前回送ったもの、それも無ければ先頭を選ぶ。宛先が 1 件も無いときは、選ばず、送れない状態のまま案内を出す (落とさない)。
    /// </remarks>
    public async Task InitializeAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        // 入力中の本文は上書きしない (ページを開き直しても、書きかけが残る)
        if (Body.Length == 0)
        {
            ResetBody();
        }

        try
        {
            var wantedId = SelectedRecipient?.ChannelId ?? settings.Get(LastChannelIdKey, 0);
            var loaded = await repository.GetRecipientsAsync();

            Recipients.Clear();
            foreach (var recipient in loaded)
            {
                Recipients.Add(new MessageRecipientItem(recipient));
            }
            SelectedRecipient = Recipients.FirstOrDefault(r => r.ChannelId == wantedId) ?? Recipients.FirstOrDefault();
            HasNoRecipients = Recipients.Count == 0;
        }
        catch (DataFileException ex)
        {
            IsResultError = true;
            ResultMessage = $"宛先を読み込めませんでした: {ex.Message}";
            IsResultOpen = true;
        }
    }

    /// <summary>前回送った宛先の送信先の番号を保存する、設定ストアのキー (この PC の設定)</summary>
    private const string LastChannelIdKey = "Messages.LastChannelId";

    /// <summary>宛先が 1 件も無いか (案内を出す)</summary>
    [ObservableProperty]
    public partial bool HasNoRecipients { get; set; }

    /// <summary>DB モード (サーバーモード)か。false のときは送れない</summary>
    public bool IsAvailable { get; } = repository.IsAvailable;

    /// <summary>DB モードでないことを知らせるか</summary>
    public bool IsUnavailableVisible => !IsAvailable;

    /// <summary>選んだ宛先</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial MessageRecipientItem? SelectedRecipient { get; set; }

    /// <summary>タイトル (任意)</summary>
    [ObservableProperty]
    public partial string TitleText { get; set; } = "";

    /// <summary>本文</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Body { get; set; } = "";

    /// <summary>結果の表示が出ているか</summary>
    [ObservableProperty]
    public partial bool IsResultOpen { get; set; }

    /// <summary>結果が失敗か</summary>
    [ObservableProperty]
    public partial bool IsResultError { get; set; }

    /// <summary>結果の文言</summary>
    [ObservableProperty]
    public partial string ResultMessage { get; set; } = "";

    /// <summary>送れる状態か (サーバーモードで、宛先と本文がある)</summary>
    /// <returns>送れるとき true</returns>
    private bool CanSend() => IsAvailable && MessageDraft.CanSend(SelectedRecipient is not null, Body, Signature);

    /// <summary>送信を依頼する (依頼の行を DB に書く)</summary>
    /// <returns>依頼の保存の完了を表すタスク</returns>
    /// <remarks>失敗は画面に出す (続けられる失敗)。成功したら本文を署名だけの状態に戻す。</remarks>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var recipient = SelectedRecipient!;
        bool added;
        try
        {
            added = await repository.AddAsync(recipient.ChannelId, TitleText.Trim(), Body.Trim());
        }
        catch (DataFileException ex)
        {
            IsResultError = true;
            ResultMessage = $"送信を依頼できませんでした: {ex.Message}";
            IsResultOpen = true;
            return;
        }

        // 選んだあとに送信先が削除されたときは、本文を残して、宛先を選び直してもらう
        if (!added)
        {
            IsResultError = true;
            ResultMessage = $"送信を依頼できませんでした: {MessageDraft.RecipientMissingMessage}";
            IsResultOpen = true;
            return;
        }

        ResetBody();
        IsResultError = false;
        ResultMessage = $"{recipient.DisplayName} に送信を依頼しました";
        IsResultOpen = true;

        // 次に開いたとき、同じ宛先を選んでおくため。覚えられなくても、依頼は書けているので、続ける
        try
        {
            await settings.SetAsync(LastChannelIdKey, recipient.ChannelId);
        }
        catch (DataFileException ex)
        {
            ResultMessage += $" (宛先を覚えられませんでした: {ex.Message})";
        }
    }
}
