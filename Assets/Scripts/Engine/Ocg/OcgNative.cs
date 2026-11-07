// ocgcore C API의 P/Invoke 선언.
// 원본: External/ygopro-core/ocgapi.h, ocgapi_types.h (AGPL-3.0-or-later)
// 구조체 배치는 ocgcore C API 11.0 기준이다. API 버전이 바뀌면 원본 헤더와 다시 맞춰 본다.
using System;
using System.Runtime.InteropServices;

namespace DuelRoguelike.Engine.Ocg
{
    internal static class OcgNative
    {
        public const string Library = "ocgcore";

        // 이 선언이 맞춰진 API 버전 (ocgapi_types.h의 OCG_VERSION_MAJOR, OCG_VERSION_MINOR)
        public const int BoundVersionMajor = 11;
        public const int BoundVersionMinor = 0;

        // typedef void (*OCG_DataReader)(void* payload, uint32_t code, OCG_CardData* data);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DataReader(IntPtr payload, uint code, IntPtr data);

        // typedef void (*OCG_DataReaderDone)(void* payload, OCG_CardData* data);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DataReaderDone(IntPtr payload, IntPtr data);

        // typedef int (*OCG_ScriptReader)(void* payload, OCG_Duel duel, const char* name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ScriptReader(IntPtr payload, IntPtr duel, IntPtr name);

        // typedef void (*OCG_LogHandler)(void* payload, const char* string, int type);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogHandler(IntPtr payload, IntPtr message, int type);

        // OCG_CardData (64비트에서 64바이트)
        [StructLayout(LayoutKind.Sequential)]
        public struct CardData
        {
            public uint Code;
            public uint Alias;
            public IntPtr SetCodes; // uint16_t*, 0으로 끝나는 배열. NULL 허용
            public uint Type;
            public uint Level;
            public uint Attribute;
            public ulong Race;
            public int Attack;
            public int Defense;
            public uint LeftScale;
            public uint RightScale;
            public uint LinkMarker;
        }

        // OCG_Player
        [StructLayout(LayoutKind.Sequential)]
        public struct Player
        {
            public uint StartingLP;
            public uint StartingDrawCount;
            public uint DrawCountPerTurn;
        }

        // OCG_DuelOptions (64비트에서 136바이트)
        [StructLayout(LayoutKind.Sequential)]
        public struct DuelOptions
        {
            public ulong Seed0;
            public ulong Seed1;
            public ulong Seed2;
            public ulong Seed3;
            public ulong Flags;
            public Player Team1;
            public Player Team2;
            public IntPtr CardReader;
            public IntPtr Payload1;
            public IntPtr ScriptReader;
            public IntPtr Payload2;
            public IntPtr LogHandler;
            public IntPtr Payload3;
            public IntPtr CardReaderDone;
            public IntPtr Payload4;
            public byte EnableUnsafeLibraries;
        }

        // OCG_NewCardInfo
        [StructLayout(LayoutKind.Sequential)]
        public struct NewCardInfo
        {
            public byte Team;
            public byte Duelist;
            public uint Code;
            public byte Controller;
            public uint Location;
            public uint Sequence;
            public uint Position;
        }

        // OCG_QueryInfo
        [StructLayout(LayoutKind.Sequential)]
        public struct QueryInfo
        {
            public uint Flags;
            public byte Controller;
            public uint Location;
            public uint Sequence;
            public uint OverlaySequence;
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern void OCG_GetVersion(out int major, out int minor);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int OCG_CreateDuel(out IntPtr duel, ref DuelOptions options);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern void OCG_DestroyDuel(IntPtr duel);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern void OCG_DuelNewCard(IntPtr duel, ref NewCardInfo info);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern void OCG_StartDuel(IntPtr duel);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int OCG_DuelProcess(IntPtr duel);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OCG_DuelGetMessage(IntPtr duel, out uint length);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern void OCG_DuelSetResponse(IntPtr duel, byte[] buffer, uint length);

        // name은 0으로 끝나는 UTF-8 바이트 배열로 넘긴다.
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int OCG_LoadScript(IntPtr duel, byte[] buffer, uint length, byte[] name);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint OCG_DuelQueryCount(IntPtr duel, byte team, uint location);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OCG_DuelQuery(IntPtr duel, out uint length, ref QueryInfo info);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OCG_DuelQueryLocation(IntPtr duel, out uint length, ref QueryInfo info);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OCG_DuelQueryField(IntPtr duel, out uint length);
    }
}
