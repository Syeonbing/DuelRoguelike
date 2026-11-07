// 카드 DB(.cdb)를 읽는 데 필요한 만큼만 감싼 SQLite 읽기 전용 래퍼.
// 네이티브 라이브러리: Assets/Plugins/x86_64/sqlite3.dll (sqlite.org 공식 DLL, 퍼블릭 도메인)
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DuelRoguelike.Engine.Data
{
    internal static class SqliteNative
    {
        public const string Library = "sqlite3";

        public const int SQLITE_OK = 0;
        public const int SQLITE_ROW = 100;
        public const int SQLITE_DONE = 101;
        public const int SQLITE_OPEN_READONLY = 0x00000001;

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_libversion();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_close_v2(IntPtr db);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_errmsg(IntPtr db);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int length, out IntPtr stmt, IntPtr tail);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_step(IntPtr stmt);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern long sqlite3_column_int64(IntPtr stmt, int column);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_column_int(IntPtr stmt, int column);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_column_text(IntPtr stmt, int column);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_column_bytes(IntPtr stmt, int column);
    }

    /// <summary>읽기 전용 SQLite 연결.</summary>
    internal sealed class SqliteReader : IDisposable
    {
        IntPtr _db;

        public static string Version => Marshal.PtrToStringAnsi(SqliteNative.sqlite3_libversion());

        public SqliteReader(string path)
        {
            int rc = SqliteNative.sqlite3_open_v2(ToUtf8z(path), out _db, SqliteNative.SQLITE_OPEN_READONLY, IntPtr.Zero);
            if (rc != SqliteNative.SQLITE_OK)
            {
                string message = _db != IntPtr.Zero ? ErrorMessage() : $"코드 {rc}";
                Dispose();
                throw new InvalidOperationException($"SQLite 파일을 열지 못했습니다: {path} ({message})");
            }
        }

        /// <summary>쿼리를 실행하고 결과 행마다 onRow를 부른다.</summary>
        public void Query(string sql, Action<SqliteRow> onRow)
        {
            if (_db == IntPtr.Zero) throw new ObjectDisposedException(nameof(SqliteReader));
            byte[] sqlBytes = ToUtf8z(sql);
            if (SqliteNative.sqlite3_prepare_v2(_db, sqlBytes, sqlBytes.Length, out IntPtr stmt, IntPtr.Zero) != SqliteNative.SQLITE_OK)
                throw new InvalidOperationException($"SQLite 쿼리 준비 실패: {ErrorMessage()}");
            try
            {
                var row = new SqliteRow(stmt);
                while (true)
                {
                    int rc = SqliteNative.sqlite3_step(stmt);
                    if (rc == SqliteNative.SQLITE_DONE)
                        break;
                    if (rc != SqliteNative.SQLITE_ROW)
                        throw new InvalidOperationException($"SQLite 쿼리 실행 실패: {ErrorMessage()}");
                    onRow(row);
                }
            }
            finally
            {
                SqliteNative.sqlite3_finalize(stmt);
            }
        }

        public void Dispose()
        {
            if (_db != IntPtr.Zero)
            {
                SqliteNative.sqlite3_close_v2(_db);
                _db = IntPtr.Zero;
            }
        }

        string ErrorMessage() => PtrToStringUtf8(SqliteNative.sqlite3_errmsg(_db), -1);

        static byte[] ToUtf8z(string s)
        {
            var bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        internal static string PtrToStringUtf8(IntPtr ptr, int length)
        {
            if (ptr == IntPtr.Zero)
                return null;
            if (length < 0)
            {
                length = 0;
                while (Marshal.ReadByte(ptr, length) != 0)
                    length++;
            }
            var bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    /// <summary>현재 결과 행. Query 콜백 안에서만 쓴다.</summary>
    internal readonly struct SqliteRow
    {
        readonly IntPtr _stmt;

        public SqliteRow(IntPtr stmt) => _stmt = stmt;

        public long GetInt64(int column) => SqliteNative.sqlite3_column_int64(_stmt, column);

        public int GetInt32(int column) => SqliteNative.sqlite3_column_int(_stmt, column);

        /// <summary>NULL이면 null.</summary>
        public string GetText(int column)
        {
            IntPtr text = SqliteNative.sqlite3_column_text(_stmt, column);
            if (text == IntPtr.Zero)
                return null;
            return SqliteReader.PtrToStringUtf8(text, SqliteNative.sqlite3_column_bytes(_stmt, column));
        }
    }
}
