using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Users.Core;

namespace MmmTool.Features.Users.Registration;

/// <summary>
/// ユーザーの登録の画面。この PC を、リマインダーを共有するユーザーとして登録する。
/// </summary>
/// <remarks>
/// 表示名は、Windows のユーザー名を初期値にして、変えられる。MAC アドレスは、この PC のネットワークアダプターのもの
/// (1 つなら表示だけ、複数なら選ぶ)。登録できたら、今のユーザーに設定して、<see cref="CloseRequested"/> で画面を閉じてもらう。
/// </remarks>
public sealed partial class UserRegistrationViewModel : ObservableObject
{
    /// <summary>表示名が未入力のときのエラー</summary>
    private const string NameRequiredMessage = "表示名を入力してください。";

    /// <summary>ユーザーの保存先</summary>
    private readonly IAppUserRepository _users;

    /// <summary>今のユーザー</summary>
    private readonly CurrentUser _currentUser;

    /// <summary>現在日時</summary>
    private readonly TimeProvider _time;

    /// <summary>ViewModel を作る</summary>
    /// <param name="users">ユーザーの保存先</param>
    /// <param name="macAddresses">この PC の MAC アドレス</param>
    /// <param name="currentUser">今のユーザー (登録できたら設定する)</param>
    /// <param name="time">現在日時の提供元</param>
    public UserRegistrationViewModel(IAppUserRepository users, IMacAddressProvider macAddresses, CurrentUser currentUser, TimeProvider time)
    {
        _users = users;
        _currentUser = currentUser;
        _time = time;

        MacAddresses = [.. macAddresses.GetRegistrationAdapters().Select(adapter => new MacAddressOption(adapter.MacAddress, adapter.Name))];
        SelectedMacAddress = MacAddresses.FirstOrDefault();
        DisplayName = Environment.UserName;
        if (MacAddresses.Count == 0)
        {
            SaveError.Show("この PC のネットワークアダプターが見つからないため、登録できません。");
        }
    }

    /// <summary>画面を閉じてほしい</summary>
    /// <remarks>登録したユーザーを渡す。</remarks>
    public event EventHandler<AppUser?>? CloseRequested;

    /// <summary>表示名の最大文字数</summary>
    public int DisplayNameMaxLength => AppUser.DisplayNameMaxLength;

    /// <summary>表示名 (初期値は Windows のユーザー名)</summary>
    [ObservableProperty]
    public partial string DisplayName { get; set; } = "";

    /// <summary>表示名のエラー。無ければ null</summary>
    [ObservableProperty]
    public partial string? NameError { get; private set; }

    /// <summary>この PC の MAC アドレスの選択肢 (接続中のものが先)</summary>
    public IReadOnlyList<MacAddressOption> MacAddresses { get; }

    /// <summary>選んでいる MAC アドレス</summary>
    [ObservableProperty]
    public partial MacAddressOption? SelectedMacAddress { get; set; }

    /// <summary>MAC アドレスが 1 つだけか (選ばずに、表示だけにする)</summary>
    public bool HasSingleMacAddress => MacAddresses.Count == 1;

    /// <summary>MAC アドレスが複数あるか (選ばせる)</summary>
    public bool HasMultipleMacAddresses => MacAddresses.Count > 1;

    /// <summary>登録のエラー</summary>
    public ErrorState SaveError { get; } = new();

    /// <summary>入力値を検証して登録し、閉じる</summary>
    /// <returns>登録の完了を表すタスク</returns>
    /// <remarks>表示名が未入力ならエラーを出して登録しない。登録に失敗したらエラーを出して閉じない。</remarks>
    [RelayCommand]
    private async Task RegisterAsync()
    {
        NameError = null;
        if (SelectedMacAddress is not { } mac)
        {
            return;
        }

        SaveError.Clear();
        var name = DisplayName.Trim();
        if (name.Length == 0)
        {
            NameError = NameRequiredMessage;
            return;
        }

        try
        {
            var user = await _users.AddAsync(new AppUser
            {
                DisplayName = name,
                MacAddress = mac.Value,
                ValidFrom = DateOnly.FromDateTime(_time.GetLocalNow().DateTime),
            });
            _currentUser.Set(user);
            CloseRequested?.Invoke(this, user);
        }
        catch (DataFileException ex)
        {
            SaveError.Show(ex.Message);
        }
    }

    /// <summary>表示名を変えたら、エラーを消す</summary>
    /// <param name="value">変えたあとの表示名</param>
    partial void OnDisplayNameChanged(string value) => NameError = null;
}
