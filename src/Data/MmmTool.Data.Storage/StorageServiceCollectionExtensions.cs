using Microsoft.Extensions.DependencyInjection;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.Connection;
using MmmTool.Data.Sqlite;
using MmmTool.Data.Sqlite.CodeMaster;
using MmmTool.Data.Sqlite.Notes;
using MmmTool.Data.Sqlite.WorkItems;
using MmmTool.Data.SqlServer.Notes;
using MmmTool.Data.SqlServer.WorkItems;
using MmmTool.Data.Storage.WorkItems;
using MmmTool.Notes.Core;
using MmmTool.WorkItems.Core;

namespace MmmTool.Data.Storage;

/// <summary>SQLite と SQL Server の保存先の登録</summary>
public static class StorageServiceCollectionExtensions
{
    /// <summary>作業管理・ノート・コードマスタの保存先を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 設定 (<see cref="DatabaseMode"/>)が DB のときは SQL Server、それ以外は SQLite の <c>Data/MmmTool.db</c>。
    /// 選ぶ処理は共通の部品 <see cref="DatabaseStorageServiceCollectionExtensions.AddStorage{TService, TLocal, TServer}"/> が持つ。
    /// SQLite の型を指すので、SQLite と SQL Server の両方を参照するアプリが呼ぶ。
    /// MmmBatch は SQLite を参照しないので、<c>MmmTool.Data.SqlServer</c> の側には置かない。
    /// SQLite の部品 (<c>AddSqliteData</c>)もここで登録する。
    /// <c>AddSqlServerData</c> のあとに呼ぶ。
    /// コードマスタの編集はローカルの SQLite だけに書ける。
    /// DB モードは全員で共通なので、管理ツールで直す。
    /// 同じ形の保存先が増えたら、ここに 1 行足す。
    /// ホストには足さない。
    /// </remarks>
    public static IServiceCollection AddLocalAndServerStorage(this IServiceCollection services)
    {
        services.AddSqliteData();

        services.AddStorage<IWorkItemRepository, SqliteWorkItemRepository, SqlServerWorkItemRepository>();
        // モードが変わったら、表示しない案件の番号を初期化する (番号がモードごとに別の連番のため)
        services.AddSingleton<IDatabaseModeChangeHandler, WorkItemSelectionReset>();

        services.AddStorage<INoteRepository, SqliteNoteRepository, SqlServerNoteRepository>();

        services.AddSingleton<UnavailableCodeMasterRepository>();
        services.AddStorage<ICodeMasterRepository, SqliteCodeMasterRepository, UnavailableCodeMasterRepository>();
        return services;
    }
}
