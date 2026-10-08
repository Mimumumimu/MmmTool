using Microsoft.Extensions.DependencyInjection;

namespace MmmTool.CliAssist.Main;

/// <summary>CLI のセッション (<see cref="CliSessionViewModel"/>)を作る</summary>
/// <param name="services">セッションを DI から作るためのサービスプロバイダー (ページのスコープのもの)</param>
/// <remarks>ページのスコープに 1 つ。セッションはこのスコープから作るので、ページを捨てる (機能をオフにする)と、シェルも終了する。</remarks>
public sealed class CliSessionFactory(IServiceProvider services)
{
    /// <summary>セッションを作る</summary>
    /// <param name="directory">シェルを始める作業ディレクトリ (Windows のパス)。null なら既定 (ユーザーのフォルダ)</param>
    /// <returns>新しいセッション (シェルは、画面につないだときに起動する)</returns>
    public CliSessionViewModel Create(string? directory)
    {
        var session = services.GetRequiredService<CliSessionViewModel>();
        if (directory is not null)
        {
            session.SetWorkingDirectory(directory);
        }
        return session;
    }
}
