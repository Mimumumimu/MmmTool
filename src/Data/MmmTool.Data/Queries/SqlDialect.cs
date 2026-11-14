using System.Data.Common;

namespace MmmTool.Data.Queries;

/// <summary>DB の種類ごとの SQL の書き方</summary>
/// <remarks>
/// SQL の書き方と値の読み書きの違いを受け持つ。
/// Repository の処理の流れは、DB の種類に依らず 1 つだけ書く。
/// 種類ごとに違うのは、表の名前の付け方・更新日時の書き方・番号の一覧の渡し方・書いた行の受け取り方・値の型の読み書き。
/// それだけを、種類ごとのプロジェクトの <see cref="SqlDialect"/> に残す。
/// </remarks>
public abstract class SqlDialect
{
    /// <summary>更新日時を書く式</summary>
    /// <remarks>
    /// DB サーバーの時計を使う種類は、その関数。
    /// サーバーの時計が無い種類は、アプリが渡すパラメーター <c>@now</c>。
    /// </remarks>
    public abstract string Now { get; }

    /// <summary>更新日時をアプリが渡すか</summary>
    /// <remarks>true のとき、書き込みのたびにパラメーター <c>@now</c> を付ける。</remarks>
    public abstract bool UsesClientClock { get; }

    /// <summary>行のロックの指定</summary>
    /// <remarks>
    /// 行をロックして読むために、表の名前の後ろに付ける。
    /// 同じ行を同時に書く人がいても、読んだ値と書く値が食い違わないようにする。
    /// ロックの指定が要らない種類 (書き込みを 1 つずつ待ち合わせる種類)は空。
    /// </remarks>
    public abstract string RowLockHint { get; }

    /// <summary>表の名前</summary>
    /// <remarks>その DB で使う形にする</remarks>
    /// <param name="name">表の名前</param>
    /// <returns>スキーマがある種類はスキーマつきの名前</returns>
    public abstract string Table(string name);

    /// <summary>コマンドにパラメーターを足す</summary>
    /// <param name="command">コマンド</param>
    /// <param name="name">パラメーターの名前 (<c>@</c> つき)</param>
    /// <param name="value">値 (日付・日時・小数・真偽は、その DB に保存する形に変える)</param>
    public abstract void AddParameter(DbCommand command, string name, object value);

    /// <summary>番号の一覧の SELECT</summary>
    /// <remarks>番号の一覧のパラメーター (<see cref="IdJson"/> の JSON)を、番号の行に戻す。</remarks>
    /// <param name="parameter">パラメーターの名前 (<c>@</c> つき)</param>
    /// <returns><c>IN (…)</c> の中に書ける、番号が 1 列の SELECT</returns>
    public abstract string IdSelect(string parameter);

    /// <summary>列が空白だけの文字ではないという条件</summary>
    /// <param name="column">列 (別名つき)</param>
    /// <returns>条件の SQL</returns>
    public abstract string IsNotBlank(string column);

    /// <summary>行を書く INSERT</summary>
    /// <remarks>書いた行の列を、同じ往復の中で受け取る。</remarks>
    /// <param name="table">表の名前 (<see cref="Table"/> を通す前)</param>
    /// <param name="columns">共通ヘッダーを除く、書く列の並び</param>
    /// <param name="values">共通ヘッダーを除く、<paramref name="columns"/> に対応する値の並び</param>
    /// <param name="returning">受け取る列の並び (表の別名なし)</param>
    /// <returns>
    /// SQL
    /// 共通ヘッダー (削除印・作成日時・作成者・更新日時・更新者)は自動で付ける
    /// パラメーター <c>@user</c> を使う
    /// </returns>
    public abstract string InsertReturning(string table, string columns, string values, string returning);

    /// <summary>行を更新する UPDATE</summary>
    /// <remarks>書いた行の列を、同じ往復の中で受け取る。</remarks>
    /// <param name="table">表の名前 (<see cref="Table"/> を通す前)</param>
    /// <param name="set">SET 句の中身</param>
    /// <param name="where">WHERE 句の中身</param>
    /// <param name="returning">受け取る列の並び (表の別名なし)</param>
    /// <param name="serialize">同じ行を同時に書く人を待たせるか (行が無いときに、挿入が重ならないよう確かめる更新で true)</param>
    /// <returns>SQL</returns>
    public abstract string UpdateReturning(string table, string set, string where, string returning, bool serialize = false);

    /// <summary>真偽の列を読む</summary>
    /// <param name="reader">読み取り中の結果</param>
    /// <param name="index">列の位置</param>
    /// <returns>値</returns>
    public abstract bool ReadBool(DbDataReader reader, int index);

    /// <summary>日時の列を読む</summary>
    /// <param name="reader">読み取り中の結果</param>
    /// <param name="index">列の位置</param>
    /// <returns>値</returns>
    public abstract DateTimeOffset ReadDateTime(DbDataReader reader, int index);

    /// <summary>日付の列を読む</summary>
    /// <param name="reader">読み取り中の結果</param>
    /// <param name="index">列の位置</param>
    /// <returns>値</returns>
    public abstract DateOnly ReadDate(DbDataReader reader, int index);

    /// <summary>時間の列を読む</summary>
    /// <param name="reader">読み取り中の結果</param>
    /// <param name="index">列の位置</param>
    /// <returns>値 (小数 2 桁)</returns>
    public abstract decimal ReadHours(DbDataReader reader, int index);

    /// <summary>小さな整数の列を読む</summary>
    /// <param name="reader">読み取り中の結果</param>
    /// <param name="index">列の位置</param>
    /// <returns>値</returns>
    public abstract byte ReadByte(DbDataReader reader, int index);

    /// <summary>1 つの値として返ってきた日付を日付にする</summary>
    /// <param name="value">1 つの値を返す SQL の結果 (<c>MAX</c> など)</param>
    /// <returns>日付</returns>
    public abstract DateOnly ToDate(object value);

    /// <summary>計算した列の真偽の読み取り</summary>
    /// <param name="reader">読み取り中の結果</param>
    /// <param name="index">列の位置</param>
    /// <returns>0 でなければ true</returns>
    /// <remarks>
    /// <c>CASE</c> の結果など、0 / 1 を真偽にして読む。
    /// 型が DB の種類で変わる計算結果のための読み方。
    /// 表の列は <see cref="ReadBool"/> で読む。
    /// </remarks>
    public static bool ReadFlag(DbDataReader reader, int index)
        => Convert.ToInt64(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture) != 0;

    /// <summary>共通ヘッダーの列を表の別名つきの並びにする</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>列の並び (6 列)</returns>
    public static string Header(string alias)
        => $"{alias}.Id, {alias}.IsDeleted, {alias}.CreatedAt, {alias}.CreatedByUserId, {alias}.UpdatedAt, {alias}.UpdatedByUserId";
}
