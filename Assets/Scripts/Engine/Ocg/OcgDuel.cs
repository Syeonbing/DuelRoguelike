// ocgcore 듀얼 하나를 감싸는 C# 클래스.
// 화면 코드와 분리된 엔진 계층이다. 로그는 직접 출력하지 않고 OcgDuelOptions.Log로 넘긴다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DuelRoguelike.Engine.Ocg
{
    /// <summary>엔진이 카드 데이터를 요청할 때 돌려줄 값 (카드 DB 한 줄).</summary>
    public struct OcgCardData
    {
        public uint Code;
        public uint Alias;
        public ushort[] SetCodes;
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

    public struct OcgPlayerOptions
    {
        public uint StartingLP;
        public uint StartingDrawCount;
        public uint DrawCountPerTurn;

        public static OcgPlayerOptions Default => new OcgPlayerOptions
        {
            StartingLP = 8000,
            StartingDrawCount = 5,
            DrawCountPerTurn = 1,
        };
    }

    public sealed class OcgDuelOptions
    {
        /// <summary>난수 시드 4개. 전부 0이면 듀얼을 만들 수 없다.</summary>
        public ulong[] Seed = new ulong[4];
        public OcgDuelFlags Flags = OcgDuelFlags.ModeMR5;
        public OcgPlayerOptions Team1 = OcgPlayerOptions.Default;
        public OcgPlayerOptions Team2 = OcgPlayerOptions.Default;

        /// <summary>카드 코드로 카드 데이터를 찾는다. 모르는 카드면 null.</summary>
        public Func<uint, OcgCardData?> CardSource;

        /// <summary>스크립트 이름(예: c12345.lua)으로 Lua 소스를 찾는다. 없으면 null.</summary>
        public Func<string, byte[]> ScriptSource;

        /// <summary>엔진 로그와 콜백 안에서 난 예외를 받는다.</summary>
        public Action<OcgLogType, string> Log;
    }

    /// <summary>엔진이 보낸 메시지 하나. Body는 메시지 종류 바이트를 뺀 나머지다.</summary>
    public readonly struct OcgMessage
    {
        public readonly OcgMessageType Type;
        public readonly byte[] Body;

        public OcgMessage(OcgMessageType type, byte[] body)
        {
            Type = type;
            Body = body;
        }
    }

    public sealed class OcgException : Exception
    {
        public OcgException(string message) : base(message) { }
    }

    public sealed class OcgDuel : IDisposable
    {
        // 네이티브 코드가 부르는 콜백. 가비지 컬렉터가 치우지 않도록 정적 필드로 잡아 둔다.
        static readonly OcgNative.DataReader s_cardReader = OnCardRead;
        static readonly OcgNative.DataReaderDone s_cardReaderDone = OnCardReadDone;
        static readonly OcgNative.ScriptReader s_scriptReader = OnScriptRead;
        static readonly OcgNative.LogHandler s_logHandler = OnLog;

        readonly OcgDuelOptions _options;
        GCHandle _self;
        IntPtr _handle;

        OcgDuel(OcgDuelOptions options)
        {
            _options = options;
        }

        public bool IsDisposed => _handle == IntPtr.Zero;

        /// <summary>DLL에 들어 있는 ocgcore C API 버전.</summary>
        public static (int Major, int Minor) GetVersion()
        {
            OcgNative.OCG_GetVersion(out int major, out int minor);
            return (major, minor);
        }

        public static OcgDuel Create(OcgDuelOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Seed == null || options.Seed.Length != 4)
                throw new ArgumentException("Seed는 ulong 4개여야 합니다.", nameof(options));

            var (major, minor) = GetVersion();
            if (major != OcgNative.BoundVersionMajor)
                throw new OcgException(
                    $"ocgcore API 버전이 맞지 않습니다. DLL: {major}.{minor}, C# 선언: {OcgNative.BoundVersionMajor}.{OcgNative.BoundVersionMinor}");

            var duel = new OcgDuel(options);
            duel._self = GCHandle.Alloc(duel);
            var payload = GCHandle.ToIntPtr(duel._self);

            var native = new OcgNative.DuelOptions
            {
                Seed0 = options.Seed[0],
                Seed1 = options.Seed[1],
                Seed2 = options.Seed[2],
                Seed3 = options.Seed[3],
                Flags = (ulong)options.Flags,
                Team1 = ToNative(options.Team1),
                Team2 = ToNative(options.Team2),
                CardReader = Marshal.GetFunctionPointerForDelegate(s_cardReader),
                Payload1 = payload,
                ScriptReader = Marshal.GetFunctionPointerForDelegate(s_scriptReader),
                Payload2 = payload,
                LogHandler = Marshal.GetFunctionPointerForDelegate(s_logHandler),
                Payload3 = payload,
                CardReaderDone = Marshal.GetFunctionPointerForDelegate(s_cardReaderDone),
                Payload4 = payload,
                EnableUnsafeLibraries = 0,
            };

            var status = (OcgDuelCreationStatus)OcgNative.OCG_CreateDuel(out duel._handle, ref native);
            if (status != OcgDuelCreationStatus.Success || duel._handle == IntPtr.Zero)
            {
                duel._handle = IntPtr.Zero;
                duel._self.Free();
                throw new OcgException($"듀얼을 만들지 못했습니다: {status}");
            }
            return duel;
        }

        /// <summary>전역 스크립트(constant.lua, utility.lua 등)를 직접 불러온다.</summary>
        public bool LoadScript(string name, byte[] source)
        {
            ThrowIfDisposed();
            return OcgNative.OCG_LoadScript(_handle, source, (uint)source.Length, ToUtf8z(name)) != 0;
        }

        public void AddCard(byte team, byte duelist, uint code, byte controller, OcgLocation location, uint sequence, OcgPosition position)
        {
            ThrowIfDisposed();
            var info = new OcgNative.NewCardInfo
            {
                Team = team,
                Duelist = duelist,
                Code = code,
                Controller = controller,
                Location = (uint)location,
                Sequence = sequence,
                Position = (uint)position,
            };
            OcgNative.OCG_DuelNewCard(_handle, ref info);
        }

        /// <summary>
        /// 덱에 카드 한 장을 넣는다. 엑스트라 덱 몬스터는 엔진이 엑스트라 덱으로 옮긴다.
        /// 엔진은 시작할 때 덱을 섞지 않으므로, 섞는 것은 부르는 쪽이 넣기 전에 한다.
        /// </summary>
        public void AddDeckCard(byte team, uint code) =>
            AddCard(team, 0, code, team, OcgLocation.Deck, 0, OcgPosition.FaceDownDefense);

        public void Start()
        {
            ThrowIfDisposed();
            OcgNative.OCG_StartDuel(_handle);
        }

        /// <summary>엔진을 진행한다. 메시지가 쌓이거나 응답이 필요하거나 듀얼이 끝나면 돌아온다.</summary>
        public OcgDuelStatus Process()
        {
            ThrowIfDisposed();
            return (OcgDuelStatus)OcgNative.OCG_DuelProcess(_handle);
        }

        /// <summary>엔진이 보낸 메시지를 꺼낸다. 버퍼 형식: [uint32 길이][종류 1바이트 + 본문] 반복.</summary>
        public List<OcgMessage> ReadMessages()
        {
            ThrowIfDisposed();
            var result = new List<OcgMessage>();
            IntPtr ptr = OcgNative.OCG_DuelGetMessage(_handle, out uint length);
            if (ptr == IntPtr.Zero || length == 0)
                return result;

            var buffer = new byte[length];
            Marshal.Copy(ptr, buffer, 0, (int)length);

            int offset = 0;
            while (offset < buffer.Length)
            {
                if (offset + 4 > buffer.Length)
                    throw new InvalidDataException($"메시지 길이 필드가 잘렸습니다 (위치 {offset}).");
                int size = BitConverter.ToInt32(buffer, offset);
                offset += 4;
                if (size <= 0 || offset + size > buffer.Length)
                    throw new InvalidDataException($"메시지 길이가 잘못됐습니다: {size} (위치 {offset}).");

                var body = new byte[size - 1];
                Buffer.BlockCopy(buffer, offset + 1, body, 0, size - 1);
                result.Add(new OcgMessage((OcgMessageType)buffer[offset], body));
                offset += size;
            }
            return result;
        }

        /// <summary>한 영역의 카드 수 (덱 등 내용을 보면 안 되는 영역에 쓴다).</summary>
        public uint QueryCount(byte team, OcgLocation location)
        {
            ThrowIfDisposed();
            return OcgNative.OCG_DuelQueryCount(_handle, team, (uint)location);
        }

        /// <summary>한 영역의 카드 전부를 조회한다. 결과 형식은 DuelQueryReader가 읽는다.</summary>
        public byte[] QueryLocation(byte team, OcgLocation location, OcgQuery flags)
        {
            ThrowIfDisposed();
            var info = new OcgNative.QueryInfo { Flags = (uint)flags, Controller = team, Location = (uint)location };
            return CopyBuffer(OcgNative.OCG_DuelQueryLocation(_handle, out uint length, ref info), length);
        }

        /// <summary>양쪽 LP, 각 영역 카드 수, 현재 체인을 조회한다.</summary>
        public byte[] QueryField()
        {
            ThrowIfDisposed();
            return CopyBuffer(OcgNative.OCG_DuelQueryField(_handle, out uint length), length);
        }

        static byte[] CopyBuffer(IntPtr ptr, uint length)
        {
            var buffer = new byte[length];
            if (ptr != IntPtr.Zero && length > 0)
                Marshal.Copy(ptr, buffer, 0, (int)length);
            return buffer;
        }

        public void SetResponse(byte[] response)
        {
            ThrowIfDisposed();
            OcgNative.OCG_DuelSetResponse(_handle, response, (uint)response.Length);
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                OcgNative.OCG_DestroyDuel(_handle);
                _handle = IntPtr.Zero;
            }
            if (_self.IsAllocated)
                _self.Free();
        }

        void ThrowIfDisposed()
        {
            if (_handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(OcgDuel));
        }

        void Report(OcgLogType type, string message)
        {
            try { _options.Log?.Invoke(type, message); }
            catch { /* 로그 처리 중 예외는 네이티브 쪽으로 넘기지 않는다. */ }
        }

        static OcgNative.Player ToNative(OcgPlayerOptions p) => new OcgNative.Player
        {
            StartingLP = p.StartingLP,
            StartingDrawCount = p.StartingDrawCount,
            DrawCountPerTurn = p.DrawCountPerTurn,
        };

        static OcgDuel FromPayload(IntPtr payload) =>
            payload == IntPtr.Zero ? null : GCHandle.FromIntPtr(payload).Target as OcgDuel;

        // ---- 네이티브 콜백. 예외가 네이티브 코드로 넘어가면 에디터가 죽을 수 있으므로 전부 여기서 잡는다. ----

        [AOT.MonoPInvokeCallback(typeof(OcgNative.DataReader))]
        static void OnCardRead(IntPtr payload, uint code, IntPtr dataPtr)
        {
            var data = new OcgNative.CardData { Code = code };
            OcgDuel duel = null;
            try
            {
                duel = FromPayload(payload);
                var card = duel?._options.CardSource?.Invoke(code);
                if (card.HasValue)
                {
                    var c = card.Value;
                    data.Code = c.Code;
                    data.Alias = c.Alias;
                    data.SetCodes = AllocSetCodes(c.SetCodes);
                    data.Type = c.Type;
                    data.Level = c.Level;
                    data.Attribute = c.Attribute;
                    data.Race = c.Race;
                    data.Attack = c.Attack;
                    data.Defense = c.Defense;
                    data.LeftScale = c.LeftScale;
                    data.RightScale = c.RightScale;
                    data.LinkMarker = c.LinkMarker;
                }
            }
            catch (Exception e)
            {
                duel?.Report(OcgLogType.Error, $"카드 데이터 콜백 예외 ({code}): {e}");
            }
            Marshal.StructureToPtr(data, dataPtr, false);
        }

        // 엔진이 카드 데이터를 복사한 뒤 부른다. OnCardRead에서 잡은 setcodes 메모리를 여기서 푼다.
        [AOT.MonoPInvokeCallback(typeof(OcgNative.DataReaderDone))]
        static void OnCardReadDone(IntPtr payload, IntPtr dataPtr)
        {
            try
            {
                var data = Marshal.PtrToStructure<OcgNative.CardData>(dataPtr);
                if (data.SetCodes != IntPtr.Zero)
                    Marshal.FreeHGlobal(data.SetCodes);
            }
            catch (Exception e)
            {
                FromPayload(payload)?.Report(OcgLogType.Error, $"카드 데이터 해제 콜백 예외: {e}");
            }
        }

        [AOT.MonoPInvokeCallback(typeof(OcgNative.ScriptReader))]
        static int OnScriptRead(IntPtr payload, IntPtr duelHandle, IntPtr namePtr)
        {
            OcgDuel duel = null;
            try
            {
                duel = FromPayload(payload);
                string name = PtrToStringUtf8(namePtr);
                byte[] source = duel?._options.ScriptSource?.Invoke(name);
                if (source == null)
                    return 0;
                return OcgNative.OCG_LoadScript(duelHandle, source, (uint)source.Length, ToUtf8z(name));
            }
            catch (Exception e)
            {
                duel?.Report(OcgLogType.Error, $"스크립트 콜백 예외: {e}");
                return 0;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(OcgNative.LogHandler))]
        static void OnLog(IntPtr payload, IntPtr messagePtr, int type)
        {
            try
            {
                FromPayload(payload)?.Report((OcgLogType)type, PtrToStringUtf8(messagePtr));
            }
            catch
            {
                // 로그 콜백에서 난 예외는 버린다.
            }
        }

        // ---- 도우미 ----

        // 0으로 끝나는 uint16 배열을 비관리 메모리에 만든다. 비어 있으면 NULL(엔진이 허용).
        static IntPtr AllocSetCodes(ushort[] setCodes)
        {
            if (setCodes == null || setCodes.Length == 0)
                return IntPtr.Zero;
            var terminated = new short[setCodes.Length + 1];
            for (int i = 0; i < setCodes.Length; i++)
                terminated[i] = unchecked((short)setCodes[i]);
            IntPtr ptr = Marshal.AllocHGlobal(terminated.Length * sizeof(short));
            Marshal.Copy(terminated, 0, ptr, terminated.Length);
            return ptr;
        }

        static byte[] ToUtf8z(string s)
        {
            int count = Encoding.UTF8.GetByteCount(s);
            var bytes = new byte[count + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        static string PtrToStringUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;
            int length = 0;
            while (Marshal.ReadByte(ptr, length) != 0)
                length++;
            var bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
