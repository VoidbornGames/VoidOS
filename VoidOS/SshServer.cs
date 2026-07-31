using Cosmos.Kernel.System.Timer;
using System;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;

namespace VoidOS.Ssh
{

    namespace Crypto
    {
        public static class BigEndian
        {
            public static uint ReadUInt32(byte[] buf, int offset)
            {
                return (uint)((buf[offset] << 24) | (buf[offset + 1] << 16)
                            | (buf[offset + 2] << 8) | buf[offset + 3]);
            }

            public static ulong ReadUInt64(byte[] buf, int offset)
            {
                return ((ulong)ReadUInt32(buf, offset) << 32) | ReadUInt32(buf, offset + 4);
            }

            public static void WriteUInt32(byte[] buf, int offset, uint value)
            {
                buf[offset] = (byte)(value >> 24);
                buf[offset + 1] = (byte)(value >> 16);
                buf[offset + 2] = (byte)(value >> 8);
                buf[offset + 3] = (byte)value;
            }

            public static void WriteUInt64(byte[] buf, int offset, ulong value)
            {
                WriteUInt32(buf, offset, (uint)(value >> 32));
                WriteUInt32(buf, offset + 4, (uint)value);
            }

            public static byte[] ToLittleEndian(byte[] bigEndian)
            {
                var le = new byte[bigEndian.Length];
                for (int i = 0; i < bigEndian.Length; i++) le[i] = bigEndian[bigEndian.Length - 1 - i];
                return le;
            }

            public static byte[] FromLittleEndian(byte[] littleEndian)
            {
                var be = new byte[littleEndian.Length];
                for (int i = 0; i < littleEndian.Length; i++) be[i] = littleEndian[littleEndian.Length - 1 - i];
                return be;
            }
        }

        public sealed class Sha256
        {
            private static readonly uint[] K = new uint[]
            {
                0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
                0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
                0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
                0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
                0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
                0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
                0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
                0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
            };

            private uint _h0, _h1, _h2, _h3, _h4, _h5, _h6, _h7;
            private ulong _bitLength;
            private readonly byte[] _buffer = new byte[64];
            private int _bufferLen;

            public Sha256() { Initialize(); }

            public void Initialize()
            {
                _h0 = 0x6a09e667; _h1 = 0xbb67ae85; _h2 = 0x3c6ef372; _h3 = 0xa54ff53a;
                _h4 = 0x510e527f; _h5 = 0x9b05688c; _h6 = 0x1f83d9ab; _h7 = 0x5be0cd19;
                _bitLength = 0;
                _bufferLen = 0;
            }

            public void Update(byte[] data, int offset, int length)
            {
                _bitLength += (ulong)length * 8;
                while (length > 0)
                {
                    int copy = Math.Min(64 - _bufferLen, length);
                    Buffer.BlockCopy(data, offset, _buffer, _bufferLen, copy);
                    _bufferLen += copy;
                    offset += copy;
                    length -= copy;
                    if (_bufferLen == 64)
                    {
                        ProcessBlock(_buffer, 0);
                        _bufferLen = 0;
                    }
                }
            }

            public void Update(byte[] data) => Update(data, 0, data.Length);
            public void Update(byte b) => Update(new byte[] { b }, 0, 1);

            public byte[] Final()
            {
                _buffer[_bufferLen++] = 0x80;
                if (_bufferLen > 56)
                {
                    while (_bufferLen < 64) _buffer[_bufferLen++] = 0;
                    ProcessBlock(_buffer, 0);
                    _bufferLen = 0;
                }
                while (_bufferLen < 56) _buffer[_bufferLen++] = 0;
                for (int i = 7; i >= 0; i--) _buffer[_bufferLen++] = (byte)((_bitLength >> (i * 8)) & 0xff);
                ProcessBlock(_buffer, 0);

                var hash = new byte[32];
                WriteBE(hash, 0, _h0); WriteBE(hash, 4, _h1); WriteBE(hash, 8, _h2); WriteBE(hash, 12, _h3);
                WriteBE(hash, 16, _h4); WriteBE(hash, 20, _h5); WriteBE(hash, 24, _h6); WriteBE(hash, 28, _h7);
                return hash;
            }

            public static byte[] Hash(byte[] data)
            {
                var s = new Sha256();
                s.Update(data);
                return s.Final();
            }

            private static void WriteBE(byte[] buf, int off, uint v)
            {
                buf[off] = (byte)(v >> 24);
                buf[off + 1] = (byte)(v >> 16);
                buf[off + 2] = (byte)(v >> 8);
                buf[off + 3] = (byte)v;
            }

            private void ProcessBlock(byte[] block, int off)
            {
                uint[] w = new uint[64];
                for (int i = 0; i < 16; i++)
                    w[i] = ((uint)block[off + i * 4] << 24) | ((uint)block[off + i * 4 + 1] << 16)
                         | ((uint)block[off + i * 4 + 2] << 8) | block[off + i * 4 + 3];
                for (int i = 16; i < 64; i++)
                {
                    uint s0 = Rotr(w[i - 15], 7) ^ Rotr(w[i - 15], 18) ^ (w[i - 15] >> 3);
                    uint s1 = Rotr(w[i - 2], 17) ^ Rotr(w[i - 2], 19) ^ (w[i - 2] >> 10);
                    w[i] = w[i - 16] + s0 + w[i - 7] + s1;
                }

                uint a = _h0, b = _h1, c = _h2, d = _h3, e = _h4, f = _h5, g = _h6, h = _h7;
                for (int i = 0; i < 64; i++)
                {
                    uint S1 = Rotr(e, 6) ^ Rotr(e, 11) ^ Rotr(e, 25);
                    uint ch = (e & f) ^ ((~e) & g);
                    uint t1 = h + S1 + ch + K[i] + w[i];
                    uint S0 = Rotr(a, 2) ^ Rotr(a, 13) ^ Rotr(a, 22);
                    uint maj = (a & b) ^ (a & c) ^ (b & c);
                    uint t2 = S0 + maj;
                    h = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2;
                }
                _h0 += a; _h1 += b; _h2 += c; _h3 += d;
                _h4 += e; _h5 += f; _h6 += g; _h7 += h;
            }

            private static uint Rotr(uint v, int n) => (v >> n) | (v << (32 - n));
        }

        public sealed class HmacSha256
        {
            private const int BlockSize = 64;
            private readonly byte[] _ipad = new byte[BlockSize];
            private readonly byte[] _opad = new byte[BlockSize];
            private readonly Sha256 _inner = new();

            public HmacSha256(byte[] key)
            {
                byte[] k = key.Length > BlockSize ? Sha256.Hash(key) : key;
                for (int i = 0; i < BlockSize; i++)
                {
                    byte b = i < k.Length ? k[i] : (byte)0;
                    _ipad[i] = (byte)(b ^ 0x36);
                    _opad[i] = (byte)(b ^ 0x5C);
                }
                _inner.Update(_ipad);
            }

            public void Update(byte[] data, int offset, int length) => _inner.Update(data, offset, length);
            public void Update(byte[] data) => _inner.Update(data);

            public byte[] Final()
            {
                byte[] innerHash = _inner.Final();
                var outer = new Sha256();
                outer.Update(_opad);
                outer.Update(innerHash);
                return outer.Final();
            }

            public static byte[] Mac(byte[] key, byte[] data)
            {
                var h = new HmacSha256(key);
                h.Update(data);
                return h.Final();
            }
        }

        public sealed class Aes
        {
            private static readonly byte[] SBox = new byte[]
            {
                0x63,0x7c,0x77,0x7b,0xf2,0x6b,0x6f,0xc5,0x30,0x01,0x67,0x2b,0xfe,0xd7,0xab,0x76,
                0xca,0x82,0xc9,0x7d,0xfa,0x59,0x47,0xf0,0xad,0xd4,0xa2,0xaf,0x9c,0xa4,0x72,0xc0,
                0xb7,0xfd,0x93,0x26,0x36,0x3f,0xf7,0xcc,0x34,0xa5,0xe5,0xf1,0x71,0xd8,0x31,0x15,
                0x04,0xc7,0x23,0xc3,0x18,0x96,0x05,0x9a,0x07,0x12,0x80,0xe2,0xeb,0x27,0xb2,0x75,
                0x09,0x83,0x2c,0x1a,0x1b,0x6e,0x5a,0xa0,0x52,0x3b,0xd6,0xb3,0x29,0xe3,0x2f,0x84,
                0x53,0xd1,0x00,0xed,0x20,0xfc,0xb1,0x5b,0x6a,0xcb,0xbe,0x39,0x4a,0x4c,0x58,0xcf,
                0xd0,0xef,0xaa,0xfb,0x43,0x4d,0x33,0x85,0x45,0xf9,0x02,0x7f,0x50,0x3c,0x9f,0xa8,
                0x51,0xa3,0x40,0x8f,0x92,0x9d,0x38,0xf5,0xbc,0xb6,0xda,0x21,0x10,0xff,0xf3,0xd2,
                0xcd,0x0c,0x13,0xec,0x5f,0x97,0x44,0x17,0xc4,0xa7,0x7e,0x3d,0x64,0x5d,0x19,0x73,
                0x60,0x81,0x4f,0xdc,0x22,0x2a,0x90,0x88,0x46,0xee,0xb8,0x14,0xde,0x5e,0x0b,0xdb,
                0xe0,0x32,0x3a,0x0a,0x49,0x06,0x24,0x5c,0xc2,0xd3,0xac,0x62,0x91,0x95,0xe4,0x79,
                0xe7,0xc8,0x37,0x6d,0x8d,0xd5,0x4e,0xa9,0x6c,0x56,0xf4,0xea,0x65,0x7a,0xae,0x08,
                0xba,0x78,0x25,0x2e,0x1c,0xa6,0xb4,0xc6,0xe8,0xdd,0x74,0x1f,0x4b,0xbd,0x8b,0x8a,
                0x70,0x3e,0xb5,0x66,0x48,0x03,0xf6,0x0e,0x61,0x35,0x57,0xb9,0x86,0xc1,0x1d,0x9e,
                0xe1,0xf8,0x98,0x11,0x69,0xd9,0x8e,0x94,0x9b,0x1e,0x87,0xe9,0xce,0x55,0x28,0xdf,
                0x8c,0xa1,0x89,0x0d,0xbf,0xe6,0x42,0x68,0x41,0x99,0x2d,0x0f,0xb0,0x54,0xbb,0x16
            };

            private static readonly byte[] Rcon = new byte[]
            { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1b, 0x36, 0x6c, 0xd8, 0xab, 0x4d };

            private readonly byte[] _roundKeys;
            private readonly int _nr;

            public Aes(byte[] key)
            {
                if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                    throw new ArgumentException("AES key must be 16, 24, or 32 bytes");
                _nr = key.Length switch { 16 => 10, 24 => 12, _ => 14 };
                _roundKeys = ExpandKey(key);
            }

            public void EncryptBlock(byte[] input, int inOff, byte[] output, int outOff)
            {
                var state = new byte[16];
                Buffer.BlockCopy(input, inOff, state, 0, 16);
                AddRoundKey(state, _roundKeys, 0);
                for (int round = 1; round < _nr; round++)
                {
                    SubBytes(state);
                    ShiftRows(state);
                    MixColumns(state);
                    AddRoundKey(state, _roundKeys, round * 16);
                }
                SubBytes(state);
                ShiftRows(state);
                AddRoundKey(state, _roundKeys, _nr * 16);
                Buffer.BlockCopy(state, 0, output, outOff, 16);
            }

            public void CtrCrypt(byte[] data, int offset, int length, byte[] counter)
            {
                if (counter.Length != 16) throw new ArgumentException("counter must be 16 bytes");
                var keystream = new byte[16];
                int pos = offset;
                while (length > 0)
                {
                    EncryptBlock(counter, 0, keystream, 0);
                    int blockLen = Math.Min(16, length);
                    for (int i = 0; i < blockLen; i++)
                        data[pos + i] ^= keystream[i];
                    for (int i = 15; i >= 0; i--)
                        if (++counter[i] != 0) break;
                    pos += blockLen;
                    length -= blockLen;
                }
            }

            private byte[] ExpandKey(byte[] key)
            {
                int nk = key.Length / 4;
                int totalBytes = 16 * (_nr + 1);
                var w = new byte[totalBytes];
                Buffer.BlockCopy(key, 0, w, 0, key.Length);
                for (int i = nk; i < 4 * (_nr + 1); i++)
                {
                    byte t0 = w[(i - 1) * 4];
                    byte t1 = w[(i - 1) * 4 + 1];
                    byte t2 = w[(i - 1) * 4 + 2];
                    byte t3 = w[(i - 1) * 4 + 3];
                    if (i % nk == 0)
                    {
                        byte tmp = t0; t0 = t1; t1 = t2; t2 = t3; t3 = tmp;
                        t0 = SBox[t0]; t1 = SBox[t1]; t2 = SBox[t2]; t3 = SBox[t3];
                        t0 ^= Rcon[(i / nk) - 1];
                    }
                    else if (nk > 6 && i % nk == 4)
                    {
                        t0 = SBox[t0]; t1 = SBox[t1]; t2 = SBox[t2]; t3 = SBox[t3];
                    }
                    w[i * 4] = (byte)(w[(i - nk) * 4] ^ t0);
                    w[i * 4 + 1] = (byte)(w[(i - nk) * 4 + 1] ^ t1);
                    w[i * 4 + 2] = (byte)(w[(i - nk) * 4 + 2] ^ t2);
                    w[i * 4 + 3] = (byte)(w[(i - nk) * 4 + 3] ^ t3);
                }
                return w;
            }

            private static void SubBytes(byte[] s)
            {
                for (int i = 0; i < 16; i++) s[i] = SBox[s[i]];
            }

            private static void ShiftRows(byte[] s)
            {
                byte tmp;
                tmp = s[1]; s[1] = s[5]; s[5] = s[9]; s[9] = s[13]; s[13] = tmp;
                tmp = s[2]; s[2] = s[10]; s[10] = tmp; tmp = s[6]; s[6] = s[14]; s[14] = tmp;
                tmp = s[15]; s[15] = s[11]; s[11] = s[7]; s[7] = s[3]; s[3] = tmp;
            }

            private static void MixColumns(byte[] s)
            {
                for (int c = 0; c < 4; c++)
                {
                    int i = c * 4;
                    byte a0 = s[i], a1 = s[i + 1], a2 = s[i + 2], a3 = s[i + 3];
                    s[i] = (byte)(Gmul(a0, 2) ^ Gmul(a1, 3) ^ a2 ^ a3);
                    s[i + 1] = (byte)(a0 ^ Gmul(a1, 2) ^ Gmul(a2, 3) ^ a3);
                    s[i + 2] = (byte)(a0 ^ a1 ^ Gmul(a2, 2) ^ Gmul(a3, 3));
                    s[i + 3] = (byte)(Gmul(a0, 3) ^ a1 ^ a2 ^ Gmul(a3, 2));
                }
            }

            private static byte Gmul(byte a, byte b)
            {
                byte p = 0;
                for (int i = 0; i < 8; i++)
                {
                    if ((b & 1) != 0) p ^= a;
                    bool hi = (a & 0x80) != 0;
                    a <<= 1;
                    if (hi) a ^= 0x1b;
                    b >>= 1;
                }
                return p;
            }

            private static void AddRoundKey(byte[] s, byte[] rk, int offset)
            {
                for (int i = 0; i < 16; i++) s[i] ^= rk[offset + i];
            }
        }

        public static class Rng
        {
            [System.Runtime.InteropServices.DllImport("*", EntryPoint = "rng_rdrand16")]
            private static extern int Rdrand16(byte[] outBuf);

            [System.Runtime.InteropServices.DllImport("*", EntryPoint = "rng_have_rdrand")]
            private static extern int HaveRdrand();

            private static readonly bool _hasRdrand;
            private static ulong _s0, _s1;
            private static bool _seeded;

            static Rng()
            {
                _hasRdrand = HaveRdrand() != 0;
            }

            public static void GetBytes(byte[] buffer)
            {
                if (_hasRdrand) FillRdrand(buffer);
                else FillXorshift(buffer);
            }

            public static uint GetUInt32()
            {
                var buf = new byte[4];
                GetBytes(buf);
                return BigEndian.ReadUInt32(buf, 0);
            }

            public static ulong GetUInt64()
            {
                var buf = new byte[8];
                GetBytes(buf);
                return BigEndian.ReadUInt64(buf, 0);
            }

            private static void FillRdrand(byte[] buffer)
            {
                int offset = 0;
                while (offset < buffer.Length)
                {
                    int chunkLen = Math.Min(16, buffer.Length - offset);
                    var chunk = new byte[16];
                    int ok = Rdrand16(chunk);
                    if (ok == 0) { FillXorshift(buffer); return; }
                    Buffer.BlockCopy(chunk, 0, buffer, offset, chunkLen);
                    offset += chunkLen;
                }
            }

            private static void FillXorshift(byte[] buffer)
            {
                if (!_seeded)
                {
                    ulong tsc = ReadTsc();
                    _s0 = tsc | 1UL;
                    _s1 = (tsc ^ 0xDEADBEEFCAFEBABEUL) | 1UL;
                    _seeded = true;
                }
                int offset = 0;
                while (offset < buffer.Length)
                {
                    ulong r = Xorshift128Plus();
                    int n = Math.Min(8, buffer.Length - offset);
                    for (int i = 0; i < n; i++)
                        buffer[offset + i] = (byte)(r >> (i * 8));
                    offset += n;
                }
            }

            private static ulong Xorshift128Plus()
            {
                ulong s1 = _s0;
                ulong s0 = _s1;
                ulong result = s0 + s1;
                _s0 = s0;
                s1 ^= s1 << 23;
                _s1 = s1 ^ s0 ^ (s1 >> 18) ^ (s0 >> 5);
                return result;
            }

            [System.Runtime.InteropServices.DllImport("*", EntryPoint = "read_tsc")]
            private static extern ulong ReadTsc();
        }
    }


    public sealed class SshBuffer
    {
        public byte[] Data;
        public int Position;

        public SshBuffer(byte[] data) { Data = data; Position = 0; }
        public SshBuffer(int capacity) { Data = new byte[capacity]; Position = 0; }
        public int Remaining => Data.Length - Position;

        public byte ReadByte()
        {
            if (Position >= Data.Length) throw new InvalidOperationException("SSH buffer underflow");
            return Data[Position++];
        }

        public uint ReadUInt32()
        {
            if (Position + 4 > Data.Length) throw new InvalidOperationException("SSH buffer underflow");
            uint v = ((uint)Data[Position] << 24) | ((uint)Data[Position + 1] << 16)
                   | ((uint)Data[Position + 2] << 8) | Data[Position + 3];
            Position += 4;
            return v;
        }

        public byte[] ReadBytes(int n)
        {
            if (Position + n > Data.Length) throw new InvalidOperationException("SSH buffer underflow");
            var b = new byte[n];
            Buffer.BlockCopy(Data, Position, b, 0, n);
            Position += n;
            return b;
        }

        public bool ReadBool()
        {
            if (Position >= Data.Length) throw new InvalidOperationException("SSH buffer underflow");
            return Data[Position++] != 0;
        }

        public byte[] ReadStringBytes()
        {
            uint len = ReadUInt32();
            return ReadBytes((int)len);
        }

        public string ReadString()
        {
            return Encoding.ASCII.GetString(ReadStringBytes());
        }

        public BigInteger ReadMpint()
        {
            uint len = ReadUInt32();
            if (len == 0) return BigInteger.Zero;
            byte[] be = ReadBytes((int)len);
            byte[] le = new byte[be.Length];
            for (int i = 0; i < be.Length; i++) le[i] = be[be.Length - 1 - i];
            return new BigInteger(le);
        }

        public string[] ReadNameList()
        {
            string s = ReadString();
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
            return s.Split(',');
        }

        public void WriteByte(byte b)
        {
            Ensure(1);
            Data[Position++] = b;
        }

        public void WriteUInt32(uint v)
        {
            Ensure(4);
            Data[Position++] = (byte)(v >> 24);
            Data[Position++] = (byte)(v >> 16);
            Data[Position++] = (byte)(v >> 8);
            Data[Position++] = (byte)v;
        }

        public void WriteBool(bool b) => WriteByte(b ? (byte)1 : (byte)0);

        public void WriteBytes(byte[] data)
        {
            Ensure(data.Length);
            Buffer.BlockCopy(data, 0, Data, Position, data.Length);
            Position += data.Length;
        }

        public void WriteString(string s)
        {
            byte[] b = Encoding.ASCII.GetBytes(s);
            WriteUInt32((uint)b.Length);
            WriteBytes(b);
        }

        public void WriteStringBytes(byte[] b)
        {
            WriteUInt32((uint)b.Length);
            WriteBytes(b);
        }

        public void WriteMpint(BigInteger n)
        {
            if (n.Sign == 0) { WriteUInt32(0); return; }
            byte[] le = n.ToByteArray();
            int len = le.Length;
            while (len > 1 && le[len - 1] == 0) len--;
            byte[] be = new byte[len];
            for (int i = 0; i < len; i++) be[i] = le[len - 1 - i];
            if ((be[0] & 0x80) != 0)
            {
                var padded = new byte[be.Length + 1];
                Buffer.BlockCopy(be, 0, padded, 1, be.Length);
                be = padded;
            }
            WriteStringBytes(be);
        }

        public void WriteNameList(string[] names) => WriteString(string.Join(",", names));

        public byte[] ToArray()
        {
            var result = new byte[Position];
            Buffer.BlockCopy(Data, 0, result, 0, Position);
            return result;
        }

        private void Ensure(int n)
        {
            if (Position + n <= Data.Length) return;
            int newCap = Math.Max(Data.Length * 2, Position + n);
            var grown = new byte[newCap];
            Buffer.BlockCopy(Data, 0, grown, 0, Data.Length);
            Data = grown;
        }
    }

    public static class SshMsg
    {
        public const byte Disconnect = 1;
        public const byte Ignore = 2;
        public const byte Unimplemented = 3;
        public const byte Debug = 4;
        public const byte ServiceRequest = 5;
        public const byte ServiceAccept = 6;
        public const byte KexInit = 20;
        public const byte NewKeys = 21;
        public const byte KexDhInit = 30;
        public const byte KexDhReply = 31;
        public const byte UserAuthRequest = 50;
        public const byte UserAuthFailure = 51;
        public const byte UserAuthSuccess = 52;
        public const byte UserAuthBanner = 53;
        public const byte GlobalRequest = 80;
        public const byte RequestSuccess = 81;
        public const byte RequestFailure = 82;
        public const byte ChannelOpen = 90;
        public const byte ChannelOpenConf = 91;
        public const byte ChannelOpenFail = 92;
        public const byte ChannelWindowAdj = 93;
        public const byte ChannelData = 94;
        public const byte ChannelExtended = 95;
        public const byte ChannelEof = 96;
        public const byte ChannelClose = 97;
        public const byte ChannelRequest = 98;
        public const byte ChannelSuccess = 99;
        public const byte ChannelFailure = 100;
    }

    public static class SshAlgorithms
    {
        public const string KexDiffieHellmanGroup14Sha256 = "diffie-hellman-group14-sha256";
        public const string HostKeySshRsa = "rsa-sha2-256";
        public const string CipherAes256Ctr = "aes256-ctr";
        public const string CipherAes128Ctr = "aes128-ctr";
        public const string MacHmacSha2256 = "hmac-sha2-256";
        public const string CompressionNone = "none";

        public static readonly string[] KexAlgorithms = { KexDiffieHellmanGroup14Sha256 };
        public static readonly string[] HostKeyAlgorithms = { HostKeySshRsa };
        public static readonly string[] CiphersClientToServ = { CipherAes256Ctr };
        public static readonly string[] CiphersServToClient = { CipherAes256Ctr };
        public static readonly string[] MacsClientToServ = { MacHmacSha2256 };
        public static readonly string[] MacsServToClient = { MacHmacSha2256 };
        public static readonly string[] CompressionClientToServ = { CompressionNone };
        public static readonly string[] CompressionServToClient = { CompressionNone };

        public static string? Negotiate(string[] serverPrefs, string[] clientOffers)
        {
            foreach (var s in serverPrefs)
                foreach (var c in clientOffers)
                    if (s == c) return s;
            return null;
        }
    }

    public static class DiffieHellman
    {
        public static readonly BigInteger P = BigInteger.Parse(
            "32317006071311007300338913926423828248817941241140239112842009751400741706634354222619689417363569347117901737909704191754605873209195028853758986185622153212175412514901774520270235796078236248884246189477587641105928646099411723245426622522193230540919037680524235519125679715870117001058055877651038861847280257976054903569732561526167081339361799541336476559160368317896729073178384589680639671900977202194168647225871031411336429319536193471636533209717077448227988588565369208645296636077250268955505928362751121174096972998068410554359584866583291642136218231078990999448652468262416972035911852507045361090559",
            System.Globalization.NumberStyles.Integer);

        public static readonly BigInteger G = new BigInteger(2);

        public static BigInteger GenerateServerSecret()
        {
            var buf = new byte[256];
            Crypto.Rng.GetBytes(buf);
            var le = new byte[buf.Length + 1];
            Buffer.BlockCopy(buf, 0, le, 0, buf.Length);
            var y = new BigInteger(le);
            var range = P - 2;
            y = BigInteger.Remainder(y, range);
            return y + 2;
        }

        public static BigInteger ComputePublic(BigInteger y)
        {
            return BigInteger.ModPow(G, y, P);
        }

        public static BigInteger ComputeShared(BigInteger e, BigInteger y)
        {
            if (e.Sign < 0 || e.CompareTo(BigInteger.One) <= 0 || e.CompareTo(P) >= 0)
                throw new ArgumentException("Invalid client DH public value");
            return BigInteger.ModPow(e, y, P);
        }
    }

    public static class KeyDerivation
    {
        private static byte[] Mpint(BigInteger n)
        {
            if (n.Sign == 0) return new byte[] { 0, 0, 0, 0 };
            byte[] le = n.ToByteArray();
            int len = le.Length;
            while (len > 1 && le[len - 1] == 0) len--;
            byte[] be = new byte[len];
            for (int i = 0; i < len; i++) be[i] = le[len - 1 - i];
            bool needPad = (be[0] & 0x80) != 0;
            byte[] wire = new byte[4 + be.Length + (needPad ? 1 : 0)];
            wire[0] = (byte)((be.Length + (needPad ? 1 : 0)) >> 24);
            wire[1] = (byte)((be.Length + (needPad ? 1 : 0)) >> 16);
            wire[2] = (byte)((be.Length + (needPad ? 1 : 0)) >> 8);
            wire[3] = (byte)(be.Length + (needPad ? 1 : 0));
            Buffer.BlockCopy(be, 0, wire, needPad ? 5 : 4, be.Length);
            return wire;
        }

        public static byte[] ComputeExchangeHash(
            string clientBanner, string serverBanner,
            byte[] clientKexInitPayload, byte[] serverKexInitPayload,
            byte[] hostPublicKeyBlob,
            BigInteger e, BigInteger f, BigInteger K)
        {
            var sha = new Crypto.Sha256();
            UpdateString(sha, clientBanner);
            UpdateString(sha, serverBanner);
            UpdateBytes(sha, clientKexInitPayload);
            UpdateBytes(sha, serverKexInitPayload);
            UpdateBytes(sha, hostPublicKeyBlob);
            sha.Update(Mpint(e));
            sha.Update(Mpint(f));
            sha.Update(Mpint(K));
            return sha.Final();
        }

        private static void UpdateString(Crypto.Sha256 sha, string s)
        {
            byte[] b = Encoding.ASCII.GetBytes(s);
            byte[] len = new byte[4];
            len[0] = (byte)(b.Length >> 24); len[1] = (byte)(b.Length >> 16);
            len[2] = (byte)(b.Length >> 8); len[3] = (byte)b.Length;
            sha.Update(len);
            sha.Update(b);
        }

        private static void UpdateBytes(Crypto.Sha256 sha, byte[] b)
        {
            byte[] len = new byte[4];
            len[0] = (byte)(b.Length >> 24); len[1] = (byte)(b.Length >> 16);
            len[2] = (byte)(b.Length >> 8); len[3] = (byte)b.Length;
            sha.Update(len);
            sha.Update(b);
        }

        public static byte[] DeriveKey(BigInteger K, byte[] H, byte[] sessionId, char letter, int length)
        {
            byte[] kMpint = Mpint(K);
            var hmac = new Crypto.HmacSha256(kMpint);
            hmac.Update(H);
            hmac.Update(new byte[] { (byte)letter });
            hmac.Update(sessionId);
            byte[] block = hmac.Final();
            var result = new byte[length];
            int copied = Math.Min(block.Length, length);
            Buffer.BlockCopy(block, 0, result, 0, copied);
            while (copied < length)
            {
                var h2 = new Crypto.HmacSha256(kMpint);
                h2.Update(block);
                block = h2.Final();
                int n = Math.Min(block.Length, length - copied);
                Buffer.BlockCopy(block, 0, result, copied, n);
                copied += n;
            }
            return result;
        }
    }

    public sealed class RsaHostKey
    {
        public BigInteger N { get; }
        public BigInteger E { get; }
        public BigInteger D { get; }
        public BigInteger P { get; }
        public BigInteger Q { get; }
        public int Bits { get; }

        private RsaHostKey(BigInteger n, BigInteger e, BigInteger d, BigInteger p, BigInteger q)
        {
            N = n; E = e; D = d; P = p; Q = q;
            Bits = (int)n.GetBitLength();
        }

        public static RsaHostKey Generate(int bits = 2048)
        {
            BigInteger e = new BigInteger(65537);
            BigInteger p, q, n, phi, d;

            while (true)
            {
                p = GenerateProbablePrime(bits / 2, e);
                q = GenerateProbablePrime(bits / 2, e);
                if (p == q) continue;
                n = p * q;
                if (n.GetBitLength() != bits) continue;
                phi = (p - 1) * (q - 1);
                if (BigInteger.GreatestCommonDivisor(e, phi) != 1) continue;
                d = ModInverse(e, phi);
                break;
            }
            return new RsaHostKey(n, e, d, p, q);
        }

        public static RsaHostKey FromComponents(BigInteger n, BigInteger e, BigInteger d, BigInteger p, BigInteger q)
        {
            return new RsaHostKey(n, e, d, p, q);
        }

        public byte[] SerializePublic()
        {
            var buf = new SshBuffer(1024);
            buf.WriteString("ssh-rsa");
            buf.WriteMpint(E);
            buf.WriteMpint(N);
            return buf.ToArray();
        }

        public string FingerprintSha256()
        {
            byte[] pub = SerializePublic();
            byte[] hash = Crypto.Sha256.Hash(pub);
            return "SHA256:" + Base64Encode(hash);
        }

        public byte[] Sign(byte[] data)
        {
            byte[] hash = Crypto.Sha256.Hash(data);
            byte[] digestInfo = new byte[]
            {
                0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86,
                0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05,
                0x00, 0x04, 0x20
            };

            int k = (Bits + 7) / 8;
            byte[] em = new byte[k];
            int tLen = digestInfo.Length + hash.Length;
            int psLen = k - tLen - 3;
            if (psLen < 8) throw new InvalidOperationException("RSA key too small for SHA-256");

            em[0] = 0x00;
            em[1] = 0x01;
            for (int i = 0; i < psLen; i++) em[2 + i] = 0xFF;
            em[2 + psLen] = 0x00;
            Buffer.BlockCopy(digestInfo, 0, em, 3 + psLen, digestInfo.Length);
            Buffer.BlockCopy(hash, 0, em, 3 + psLen + digestInfo.Length, hash.Length);

            var emLe = new byte[em.Length + 1];
            for (int i = 0; i < em.Length; i++) emLe[i] = em[em.Length - 1 - i];
            var m = new BigInteger(emLe);
            var s = BigInteger.ModPow(m, D, N);

            byte[] sLe = s.ToByteArray();
            byte[] sig = new byte[k];
            int copyLen = Math.Min(sLe.Length, k);
            for (int i = 0; i < copyLen; i++) sig[k - 1 - i] = sLe[i];

            var outBuf = new SshBuffer(256);
            outBuf.WriteString("rsa-sha2-256");
            outBuf.WriteStringBytes(sig);
            return outBuf.ToArray();
        }

        private static BigInteger GenerateProbablePrime(int bits, BigInteger e)
        {
            var rng = new byte[bits / 8 + 1];
            while (true)
            {
                Crypto.Rng.GetBytes(rng);
                rng[rng.Length - 1] = 0;
                var candidate = new BigInteger(rng);
                candidate = BigInteger.Abs(candidate);
                if (candidate.GetBitLength() != bits) continue;
                if (!IsNotDivisibleBySmallPrimes(candidate)) continue;
                candidate |= BigInteger.One << (bits - 1);
                candidate |= BigInteger.One << (bits - 2);
                candidate |= BigInteger.One;
                if (candidate % e == BigInteger.One) continue;
                if (IsProbablePrime(candidate, 16)) return candidate;
            }
        }

        private static bool IsNotDivisibleBySmallPrimes(BigInteger n)
        {
            int[] smallPrimes = { 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97 };
            foreach (int p in smallPrimes)
                if (n % p == BigInteger.Zero) return false;
            return true;
        }

        private static bool IsProbablePrime(BigInteger n, int rounds)
        {
            if (n < 2) return false;
            if (n == 2 || n == 3) return true;
            if (n.IsEven) return false;

            BigInteger d = n - 1;
            int r = 0;
            while (d.IsEven) { d >>= 1; r++; }

            var rngBytes = new byte[(n.GetBitLength() + 7) / 8 + 1];
            for (int i = 0; i < rounds; i++)
            {
                Crypto.Rng.GetBytes(rngBytes);
                rngBytes[rngBytes.Length - 1] = 0;
                BigInteger a = new BigInteger(rngBytes);
                a = BigInteger.Abs(a) % (n - 3) + 2;

                BigInteger x = BigInteger.ModPow(a, d, n);
                if (x == 1 || x == n - 1) continue;
                bool composite = true;
                for (int j = 0; j < r - 1; j++)
                {
                    x = BigInteger.ModPow(x, 2, n);
                    if (x == n - 1) { composite = false; break; }
                }
                if (composite) return false;
            }
            return true;
        }

        private static BigInteger ModInverse(BigInteger a, BigInteger m)
        {
            BigInteger oldR = a, r = m;
            BigInteger oldS = BigInteger.One, s = BigInteger.Zero;
            while (!r.IsZero)
            {
                BigInteger q = oldR / r;
                (oldR, r) = (r, oldR - q * r);
                (oldS, s) = (s, oldS - q * s);
            }
            if (oldR != BigInteger.One) throw new InvalidOperationException("no inverse");
            return ((oldS % m) + m) % m;
        }

        public static string Base64Encode(byte[] data)
        {
            const string tbl = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
            var sb = new StringBuilder((data.Length + 2) / 3 * 4);
            for (int i = 0; i < data.Length; i += 3)
            {
                int b0 = data[i];
                int b1 = i + 1 < data.Length ? data[i + 1] : 0;
                int b2 = i + 2 < data.Length ? data[i + 2] : 0;
                sb.Append(tbl[b0 >> 2]);
                sb.Append(tbl[((b0 & 0x03) << 4) | (b1 >> 4)]);
                sb.Append(i + 1 < data.Length ? tbl[((b1 & 0x0F) << 2) | (b2 >> 6)] : '=');
                sb.Append(i + 2 < data.Length ? tbl[b2 & 0x3F] : '=');
            }
            return sb.ToString();
        }
    }

    public sealed class SshPacket
    {
        private const int MaxPacket = 256 * 1024;
        private const int BlockSize = 16;

        public static byte[] Build(byte[] payload, uint sequenceNumber,
            Crypto.Aes? cipher, byte[]? cipherCounter, byte[]? macKey)
        {
            int minPadding = 4;
            int payloadPlusPadLenField = payload.Length + 1;
            int remainder = (4 + payloadPlusPadLenField) % BlockSize;
            int paddingLen = remainder == 0 ? minPadding : (BlockSize - remainder);
            if (paddingLen < 4) paddingLen += BlockSize;

            int packetLength = payloadPlusPadLenField + paddingLen;
            if (packetLength > MaxPacket) throw new InvalidOperationException("packet too large");

            var frame = new byte[4 + packetLength + (macKey != null ? 32 : 0)];
            Crypto.BigEndian.WriteUInt32(frame, 0, (uint)packetLength);
            frame[4] = (byte)paddingLen;
            Buffer.BlockCopy(payload, 0, frame, 5, payload.Length);

            var pad = new byte[paddingLen];
            Crypto.Rng.GetBytes(pad);
            Buffer.BlockCopy(pad, 0, frame, 5 + payload.Length, paddingLen);

            if (macKey != null)
            {
                var macInput = new byte[4 + 4 + packetLength];
                Crypto.BigEndian.WriteUInt32(macInput, 0, sequenceNumber);
                Buffer.BlockCopy(frame, 0, macInput, 4, 4 + packetLength);
                byte[] tag = Crypto.HmacSha256.Mac(macKey, macInput);
                Buffer.BlockCopy(tag, 0, frame, 4 + packetLength, 32);
            }

            if (cipher != null && cipherCounter != null)
            {
                cipher.CtrCrypt(frame, 0, 4 + packetLength, cipherCounter);
            }
            return frame;
        }

        public static byte[]? Read(Func<byte[], int, int, int> readExact,
            uint sequenceNumber, Crypto.Aes? cipher, byte[]? cipherCounter, byte[]? macKey)
        {
            int firstBlockSize = cipher != null ? BlockSize : 5;
            var firstBlock = new byte[firstBlockSize];
            int got = readExact(firstBlock, 0, firstBlockSize);
            if (got == 0) return null;
            if (got < firstBlockSize) throw new Exception("short read on header");

            if (cipher != null)
            {
                cipher.CtrCrypt(firstBlock, 0, firstBlockSize, cipherCounter!);
            }

            uint packetLengthRaw = Crypto.BigEndian.ReadUInt32(firstBlock, 0);
            if (packetLengthRaw < 8 || packetLengthRaw > MaxPacket)
                throw new Exception($"invalid SSH packet length {packetLengthRaw}");
            int packetLength = (int)packetLengthRaw;

            int remainingEnc = packetLength - (firstBlockSize - 4);
            if (remainingEnc < 0)
                throw new Exception($"invalid SSH packet length {packetLengthRaw} (smaller than first block)");
            int macLen = macKey != null ? 32 : 0;
            var rest = new byte[remainingEnc + macLen];
            if (readExact(rest, 0, rest.Length) < rest.Length)
                throw new Exception("short read on body");

            if (cipher != null)
            {
                cipher.CtrCrypt(rest, 0, remainingEnc, cipherCounter!);
            }

            var fullPacket = new byte[4 + packetLength];
            Buffer.BlockCopy(firstBlock, 0, fullPacket, 0, firstBlockSize);
            Buffer.BlockCopy(rest, 0, fullPacket, firstBlockSize, remainingEnc);

            if (macKey != null)
            {
                var macInput = new byte[4 + 4 + packetLength];
                Crypto.BigEndian.WriteUInt32(macInput, 0, sequenceNumber);
                Buffer.BlockCopy(fullPacket, 0, macInput, 4, 4 + packetLength);
                byte[] expected = Crypto.HmacSha256.Mac(macKey, macInput);
                byte[] actual = new byte[32];
                Buffer.BlockCopy(rest, remainingEnc, actual, 0, 32);
                if (!ConstantTimeEquals(expected, actual))
                    throw new Exception("SSH MAC verification failed");
            }

            int paddingLen = fullPacket[4];
            int payloadLen = packetLength - paddingLen - 1;
            var payload = new byte[payloadLen];
            Buffer.BlockCopy(fullPacket, 5, payload, 0, payloadLen);
            return payload;
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            byte diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= (byte)(a[i] ^ b[i]);
            return diff == 0;
        }
    }

    public sealed class SshTransport : IDisposable
    {
        public const string ServerBanner = "SSH-2.0-VoidOS_1.0";

        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly RsaHostKey _hostKey;

        public string? KexAlgorithm { get; private set; }
        public string? HostKeyAlgorithm { get; private set; }
        public string? CipherC2S { get; private set; }
        public string? CipherS2C { get; private set; }
        public string? MacC2S { get; private set; }
        public string? MacS2C { get; private set; }

        private Crypto.Aes? _encCipherS2C;
        private byte[]? _encCounterS2C;
        private byte[]? _macKeyS2C;

        private Crypto.Aes? _decCipherC2S;
        private byte[]? _decCounterC2S;
        private byte[]? _macKeyC2S;

        private uint _sendSeq = 0;
        private uint _recvSeq = 0;

        public byte[]? SessionId { get; private set; }

        public SshTransport(TcpClient client, RsaHostKey hostKey)
        {
            _client = client;
            _stream = client.GetStream();
            _hostKey = hostKey;
        }

        public void Handshake()
        {
            ExchangeBanners();
            var serverKexInitPayload = SendKexInit();
            var clientKexInitPayload = ReceiveKexInit(out var clientKexAlgos,
                                                      out var clientHostKeyAlgos,
                                                      out var clientCiphersC2S,
                                                      out var clientCiphersS2C,
                                                      out var clientMacsC2S,
                                                      out var clientMacsS2C);

            KexAlgorithm = SshAlgorithms.Negotiate(SshAlgorithms.KexAlgorithms, clientKexAlgos)
                           ?? throw new Exception("no common kex algorithm");
            HostKeyAlgorithm = SshAlgorithms.Negotiate(SshAlgorithms.HostKeyAlgorithms, clientHostKeyAlgos)
                           ?? throw new Exception("no common host key algorithm");
            CipherC2S = SshAlgorithms.Negotiate(SshAlgorithms.CiphersClientToServ, clientCiphersC2S)
                           ?? throw new Exception("no common cipher (c->s)");
            CipherS2C = SshAlgorithms.Negotiate(SshAlgorithms.CiphersServToClient, clientCiphersS2C)
                           ?? throw new Exception("no common cipher (s->c)");
            MacC2S = SshAlgorithms.Negotiate(SshAlgorithms.MacsClientToServ, clientMacsC2S)
                           ?? throw new Exception("no common mac (c->s)");
            MacS2C = SshAlgorithms.Negotiate(SshAlgorithms.MacsServToClient, clientMacsS2C)
                           ?? throw new Exception("no common mac (s->c)");

            if (KexAlgorithm != SshAlgorithms.KexDiffieHellmanGroup14Sha256)
                throw new Exception($"unsupported negotiated kex: {KexAlgorithm}");

            var kexDhInit = ReadPacket();
            if (kexDhInit == null || kexDhInit[0] != SshMsg.KexDhInit)
                throw new Exception($"expected KEXDH_INIT, got msg type {(kexDhInit != null ? kexDhInit[0] : 0)}");
            var initBuf = new SshBuffer(kexDhInit);
            initBuf.Position = 1;
            BigInteger e = initBuf.ReadMpint();

            var y = DiffieHellman.GenerateServerSecret();
            var f = DiffieHellman.ComputePublic(y);
            var K = DiffieHellman.ComputeShared(e, y);

            byte[] hostPubKey = _hostKey.SerializePublic();
            byte[] H = KeyDerivation.ComputeExchangeHash(
                _clientBanner!, ServerBanner,
                clientKexInitPayload, serverKexInitPayload,
                hostPubKey, e, f, K);

            if (SessionId == null) SessionId = H;

            byte[] sig = _hostKey.Sign(H);

            var reply = new SshBuffer(512);
            reply.WriteByte(SshMsg.KexDhReply);
            reply.WriteStringBytes(hostPubKey);
            reply.WriteMpint(f);
            reply.WriteStringBytes(sig);
            WritePacket(reply.ToArray());

            WritePacket(new byte[] { SshMsg.NewKeys });
            var nks = ReadPacket();
            if (nks == null || nks.Length != 1 || nks[0] != SshMsg.NewKeys)
                throw new Exception("expected NEWKEYS from client");

            int cipherKeyLen = CipherC2S == SshAlgorithms.CipherAes256Ctr ? 32 : 16;
            int macKeyLen = 32;
            int ivLen = 16;

            byte[] ivC2S = KeyDerivation.DeriveKey(K, H, SessionId, 'A', ivLen);
            byte[] ivS2C = KeyDerivation.DeriveKey(K, H, SessionId, 'B', ivLen);
            byte[] keyC2S = KeyDerivation.DeriveKey(K, H, SessionId, 'C', cipherKeyLen);
            byte[] keyS2C = KeyDerivation.DeriveKey(K, H, SessionId, 'D', cipherKeyLen);
            byte[] intKeyC2S = KeyDerivation.DeriveKey(K, H, SessionId, 'E', macKeyLen);
            byte[] intKeyS2C = KeyDerivation.DeriveKey(K, H, SessionId, 'F', macKeyLen);

            _decCipherC2S = new Crypto.Aes(keyC2S);
            _decCounterC2S = ivC2S;
            _macKeyC2S = intKeyC2S;

            _encCipherS2C = new Crypto.Aes(keyS2C);
            _encCounterS2C = ivS2C;
            _macKeyS2C = intKeyS2C;
        }

        private string? _clientBanner;

        private void ExchangeBanners()
        {
            Console.WriteLine("[SSH] Reading client banner...");

            var line = new StringBuilder();
            int totalWaited = 0;
            while (line.Length < 256)
            {
                if (!_stream.DataAvailable)
                {
                    TimerManager.Wait(10);
                    totalWaited++;
                    if (totalWaited > 1500)
                    {
                        try { _client.Client.ReceiveTimeout = 5000; } catch { }
                    }
                    if (totalWaited > 2000)
                        throw new Exception("timeout waiting for client banner (20s)");
                    continue;
                }

                int b = _stream.ReadByte();
                if (b == -1) throw new Exception("connection closed during banner read");
                if (b == '\n') break;
                if (b != '\r') line.Append((char)b);
            }
            _clientBanner = line.ToString();
            Console.WriteLine($"[SSH] Client banner: {_clientBanner}");
            if (!_clientBanner.StartsWith("SSH-2.0-"))
                throw new Exception($"client is not SSH v2: {_clientBanner}");
        }

        private byte[] SendKexInit()
        {
            var payload = new SshBuffer(512);
            payload.WriteByte(SshMsg.KexInit);
            var cookie = new byte[16];
            Crypto.Rng.GetBytes(cookie);
            payload.WriteBytes(cookie);

            payload.WriteNameList(SshAlgorithms.KexAlgorithms);
            payload.WriteNameList(SshAlgorithms.HostKeyAlgorithms);
            payload.WriteNameList(SshAlgorithms.CiphersClientToServ);
            payload.WriteNameList(SshAlgorithms.CiphersServToClient);
            payload.WriteNameList(SshAlgorithms.MacsClientToServ);
            payload.WriteNameList(SshAlgorithms.MacsServToClient);
            payload.WriteNameList(SshAlgorithms.CompressionClientToServ);
            payload.WriteNameList(SshAlgorithms.CompressionServToClient);
            payload.WriteNameList(Array.Empty<string>());
            payload.WriteNameList(Array.Empty<string>());
            payload.WriteBool(false);
            payload.WriteUInt32(0);

            byte[] payloadBytes = payload.ToArray();
            WritePacket(payloadBytes);
            return payloadBytes;
        }

        private byte[] ReceiveKexInit(
            out string[] kex, out string[] hostKey,
            out string[] c2s, out string[] s2c,
            out string[] macC2S, out string[] macS2C)
        {
            var payload = ReadPacket();
            if (payload == null || payload.Length == 0 || payload[0] != SshMsg.KexInit)
                throw new Exception("expected KEXINIT from client");

            var buf = new SshBuffer(payload);
            buf.Position = 1;
            buf.ReadBytes(16);
            kex = buf.ReadNameList();
            hostKey = buf.ReadNameList();
            c2s = buf.ReadNameList();
            s2c = buf.ReadNameList();
            macC2S = buf.ReadNameList();
            macS2C = buf.ReadNameList();
            buf.ReadNameList();
            buf.ReadNameList();
            buf.ReadNameList();
            buf.ReadNameList();
            buf.ReadBool();
            buf.ReadUInt32();
            return payload;
        }

        public void WritePacket(byte[] payload)
        {
            var frame = SshPacket.Build(payload, _sendSeq,
                _encCipherS2C, _encCounterS2C, _macKeyS2C);
            _stream.Write(frame, 0, frame.Length);
            _sendSeq++;
        }

        public byte[]? ReadPacket()
        {
            int ReadExact(byte[] buf, int off, int len)
            {
                int total = 0;
                while (total < len)
                {
                    int waited = 0;
                    while (!_stream.DataAvailable && waited < 1000)
                    {
                        TimerManager.Wait(10);
                        waited++;
                    }
                    if (!_stream.DataAvailable) return total;

                    int n = _stream.Read(buf, off + total, len - total);
                    if (n == 0) return total;
                    total += n;
                }
                return total;
            }

            var payload = SshPacket.Read(ReadExact, _recvSeq, _decCipherC2S, _decCounterC2S, _macKeyC2S);
            _recvSeq++;
            return payload;
        }

        public void Dispose()
        {
            try { _stream?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }
        }
    }

    public sealed class ChannelShell
    {
        private readonly SshTransport _transport;
        private readonly uint _clientChannelId;
        private readonly uint _serverWindow;
        private readonly uint _maxPacketSize;

        private readonly StringBuilder _line = new();
        private static readonly System.Collections.Generic.List<string> _history = new();
        private int _historyIndex = -1;
        private string _currentPath = Kernel.CurrentPath;

        public ChannelShell(SshTransport transport, uint clientChannelId, uint serverWindow, uint maxPacketSize)
        {
            _transport = transport;
            _clientChannelId = clientChannelId;
            _serverWindow = serverWindow;
            _maxPacketSize = maxPacketSize;
        }

        public void Start()
        {
            SendMessage("\r\nVoidOS SSH Shell v1.0\r\n");
            SendMessage("Welcome to VoidOS!\r\n");
            SendMessage("Type 'help' for commands, 'exit' to disconnect\r\n\r\n");
            SendPrompt();
        }

        public void RunSingleCommand(string command)
        {
            var output = CommandManager.Execute(command);
            if (!string.IsNullOrEmpty(output)) SendMessage(output + "\r\n");
        }

        public void FeedInput(byte[] data)
        {
            int i = 0;
            while (i < data.Length)
            {
                byte b = data[i++];
                if (b == 255 && i + 1 < data.Length) { i += 2; continue; }

                if (b == 13 || b == 10)
                {
                    SendMessage("\r\n");
                    DispatchLine();
                    _line.Clear();
                    SendPrompt();
                }
                else if (b == 3)
                {
                    SendMessage("^C\r\n");
                    _line.Clear();
                    SendPrompt();
                }
                else if (b == 4)
                {
                    SendMessage("\r\n");
                    var eof = new SshBuffer(16);
                    eof.WriteByte(SshMsg.ChannelEof);
                    eof.WriteUInt32(_clientChannelId);
                    _transport.WritePacket(eof.ToArray());
                    return;
                }
                else if (b == 127 || b == 8)
                {
                    if (_line.Length > 0)
                    {
                        _line.Remove(_line.Length - 1, 1);
                        SendMessage("\b \b");
                    }
                }
                else if (b == 27 && i + 2 < data.Length)
                {
                    if (data[i] == 91)
                    {
                        byte c = data[i + 1];
                        i += 2;
                        if (c == 65) ApplyHistory(-1);
                        else if (c == 66) ApplyHistory(+1);
                    }
                }
                else if (b >= 32 && b < 127)
                {
                    _line.Append((char)b);
                    SendMessage(((char)b).ToString());
                }
            }
        }

        private void ApplyHistory(int delta)
        {
            if (_history.Count == 0) return;
            if (_historyIndex == -1) _historyIndex = _history.Count;
            _historyIndex += delta;
            if (_historyIndex < 0) _historyIndex = 0;
            if (_historyIndex >= _history.Count) { _historyIndex = _history.Count; _line.Clear(); }
            else { _line.Clear(); _line.Append(_history[_historyIndex]); }

            SendMessage("\r\x1b[K");
            SendPrompt();
            SendMessage(_line.ToString());
        }

        private void DispatchLine()
        {
            string cmd = _line.ToString().Trim();
            if (string.IsNullOrEmpty(cmd)) return;
            if (cmd.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                var close = new SshBuffer(16);
                close.WriteByte(SshMsg.ChannelClose);
                close.WriteUInt32(_clientChannelId);
                _transport.WritePacket(close.ToArray());
                return;
            }

            _history.Add(cmd);
            if (_history.Count > 50) _history.RemoveAt(0);
            _historyIndex = -1;

            if (cmd.StartsWith("cd ", StringComparison.OrdinalIgnoreCase))
            {
                CommandManager.Execute(cmd);
                _currentPath = Kernel.CurrentPath;
                return;
            }

            var output = CommandManager.Execute(cmd);
            if (!string.IsNullOrEmpty(output)) SendMessage(output + "\r\n");
        }

        private void SendPrompt()
        {
            SendMessage($"{_currentPath} $> ");
        }

        public void SendMessage(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            int offset = 0;
            while (offset < bytes.Length)
            {
                int chunkLen = Math.Min(bytes.Length - offset, (int)_maxPacketSize - 16);
                var b = new SshBuffer(16 + chunkLen);
                b.WriteByte(SshMsg.ChannelData);
                b.WriteUInt32(_clientChannelId);
                b.WriteStringBytes(bytes.AsSpan(offset, chunkLen).ToArray());
                _transport.WritePacket(b.ToArray());
                offset += chunkLen;
            }
        }
    }

    public sealed class SshSession
    {
        private readonly SshTransport _transport;
        private readonly TcpClient _client;
        private uint _clientChannelId;
        private uint _serverChannelId;
        private uint _clientWindow = 0;
        private uint _serverWindow = 1024 * 1024;
        private uint _maxPacketSize = 32 * 1024;

        public SshSession(SshTransport transport, TcpClient client)
        {
            _transport = transport;
            _client = client;
        }

        public void Run()
        {
            var pkt = _transport.ReadPacket();
            if (pkt == null || pkt[0] != SshMsg.ServiceRequest)
                throw new Exception("expected SERVICE_REQUEST");
            var buf = new SshBuffer(pkt);
            buf.Position = 1;
            string service = buf.ReadString();
            if (service != "ssh-userauth")
                throw new Exception($"unsupported service: {service}");
            var accept = new SshBuffer(64);
            accept.WriteByte(SshMsg.ServiceAccept);
            accept.WriteString("ssh-userauth");
            _transport.WritePacket(accept.ToArray());

            if (!Authenticate()) return;

            bool shellStarted = false;
            ChannelShell? shell = null;
            while (true)
            {
                pkt = _transport.ReadPacket();
                if (pkt == null) break;
                var b = new SshBuffer(pkt);
                byte msgType = b.ReadByte();

                switch (msgType)
                {
                    case SshMsg.ChannelOpen:
                        HandleChannelOpen(b, out shellStarted, out shell);
                        break;
                    case SshMsg.ChannelRequest:
                        HandleChannelRequest(b, ref shellStarted, ref shell);
                        break;
                    case SshMsg.ChannelWindowAdj:
                        _clientWindow = b.ReadUInt32();
                        break;
                    case SshMsg.ChannelData:
                        if (shell != null)
                        {
                            b.ReadUInt32();
                            var data = b.ReadStringBytes();
                            shell.FeedInput(data);
                        }
                        break;
                    case SshMsg.ChannelEof:
                        SendChannelClose();
                        return;
                    case SshMsg.ChannelClose:
                        return;
                    case SshMsg.GlobalRequest:
                        _transport.WritePacket(new byte[] { SshMsg.RequestFailure });
                        break;
                    case SshMsg.Disconnect:
                        return;
                    default:
                        var un = new SshBuffer(16);
                        un.WriteByte(SshMsg.Unimplemented);
                        un.WriteUInt32(0u);
                        _transport.WritePacket(un.ToArray());
                        break;
                }
            }
        }

        private bool Authenticate()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var pkt = _transport.ReadPacket();
                if (pkt == null) return false;
                if (pkt[0] != SshMsg.UserAuthRequest)
                    throw new Exception($"expected USERAUTH_REQUEST, got {pkt[0]}");

                var b = new SshBuffer(pkt);
                b.Position = 1;
                string user = b.ReadString();
                string service = b.ReadString();
                string method = b.ReadString();

                if (method == "none") { SendAuthFailure(); continue; }
                if (method != "password") { SendAuthFailure(); continue; }

                b.ReadBool();
                string password = b.ReadString();

                if (password == Kernel.RemotePassword)
                {
                    _transport.WritePacket(new byte[] { SshMsg.UserAuthSuccess });
                    Logger.Log($"SSH: {user} authenticated");
                    return true;
                }
                Logger.Log($"SSH: auth failed for {user}");
                SendAuthFailure();
            }
            return false;
        }

        private void SendAuthFailure()
        {
            var b = new SshBuffer(64);
            b.WriteByte(SshMsg.UserAuthFailure);
            b.WriteNameList(new[] { "password" });
            b.WriteBool(false);
            _transport.WritePacket(b.ToArray());
        }

        private void HandleChannelOpen(SshBuffer b, out bool shellStarted, out ChannelShell? shell)
        {
            shellStarted = false;
            shell = null;
            string chanType = b.ReadString();
            _clientChannelId = b.ReadUInt32();
            _clientWindow = b.ReadUInt32();
            _maxPacketSize = b.ReadUInt32();

            if (chanType != "session")
            {
                var fail = new SshBuffer(64);
                fail.WriteByte(SshMsg.ChannelOpenFail);
                fail.WriteUInt32(_clientChannelId);
                fail.WriteUInt32(3);
                fail.WriteString("only session channels supported");
                fail.WriteString("en");
                _transport.WritePacket(fail.ToArray());
                return;
            }

            _serverChannelId = 0;

            var ok = new SshBuffer(64);
            ok.WriteByte(SshMsg.ChannelOpenConf);
            ok.WriteUInt32(_clientChannelId);
            ok.WriteUInt32(_serverChannelId);
            ok.WriteUInt32(_serverWindow);
            ok.WriteUInt32(_maxPacketSize);
            _transport.WritePacket(ok.ToArray());
        }

        private void HandleChannelRequest(SshBuffer b, ref bool shellStarted, ref ChannelShell? shell)
        {
            uint recipientChannel = b.ReadUInt32();
            string reqType = b.ReadString();
            bool wantReply = b.ReadBool();

            switch (reqType)
            {
                case "pty-req":
                    if (wantReply) SendChannelSuccess(recipientChannel);
                    break;
                case "shell":
                    if (wantReply) SendChannelSuccess(recipientChannel);
                    shellStarted = true;
                    shell = new ChannelShell(_transport, _clientChannelId, _serverWindow, _maxPacketSize);
                    shell.Start();
                    break;
                case "exec":
                    string command = b.ReadString();
                    if (wantReply) SendChannelSuccess(recipientChannel);
                    shell = new ChannelShell(_transport, _clientChannelId, _serverWindow, _maxPacketSize);
                    shell.RunSingleCommand(command);
                    shell = null;
                    SendChannelEof();
                    SendChannelClose();
                    break;
                case "subsystem":
                    string subsystem = b.ReadString();
                    if (wantReply) SendChannelSuccess(recipientChannel);
                    shell = new ChannelShell(_transport, _clientChannelId, _serverWindow, _maxPacketSize);
                    shell.SendMessage($"Subsystem '{subsystem}' not supported.\r\n");
                    shell = null;
                    SendChannelEof();
                    SendChannelClose();
                    break;
                case "window-change":
                    break;
                case "env":
                    if (wantReply) SendChannelFailure(recipientChannel);
                    break;
                default:
                    if (wantReply) SendChannelFailure(recipientChannel);
                    break;
            }
        }

        private void SendChannelSuccess(uint recipientChannel)
        {
            var b = new SshBuffer(16);
            b.WriteByte(SshMsg.ChannelSuccess);
            b.WriteUInt32(recipientChannel);
            _transport.WritePacket(b.ToArray());
        }

        private void SendChannelFailure(uint recipientChannel)
        {
            var b = new SshBuffer(16);
            b.WriteByte(SshMsg.ChannelFailure);
            b.WriteUInt32(recipientChannel);
            _transport.WritePacket(b.ToArray());
        }

        public void SendChannelClose()
        {
            var b = new SshBuffer(16);
            b.WriteByte(SshMsg.ChannelClose);
            b.WriteUInt32(_clientChannelId);
            _transport.WritePacket(b.ToArray());
        }

        public void SendChannelEof()
        {
            var b = new SshBuffer(16);
            b.WriteByte(SshMsg.ChannelEof);
            b.WriteUInt32(_clientChannelId);
            _transport.WritePacket(b.ToArray());
        }
    }

    public static class SshServer
    {
        private static RsaHostKey? _hostKey;
        private static bool _running;
        private static TcpListener? _listener;
        private const string KeyPath = "/mnt/system/ssh_host_rsa.bin";

        private static readonly HashSet<string> _activeConnections = new();
        private static readonly object _activeConnectionsLock = new();

        public static void Start()
        {
            _running = true;

            try
            {
                _hostKey = TryLoadHostKey();
                if (_hostKey != null)
                {
                    Console.WriteLine("[SSH] Loaded existing host key from disk.");
                }
                else
                {
                    Console.WriteLine("[SSH] No saved host key found, generating RSA host key (2048 bits)...");
                    _hostKey = RsaHostKey.Generate(2048);
                    SaveHostKey(_hostKey);
                    Console.WriteLine("[SSH] Host key generated and saved.");
                }
                Console.WriteLine($"[SSH] Host key fingerprint: {_hostKey.FingerprintSha256()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SSH] FATAL: cannot obtain host key: {ex.Message}");
                return;
            }

            try
            {
                _listener = new TcpListener(IPAddress.Any, 22);
                _listener.Start();
                Console.WriteLine("[SSH] Listening on port 22");

                while (_running)
                {
                    while (_running && _activeConnections.Count > 0)
                    {
                        TimerManager.Wait(100);
                    }

                    while (_running && !_listener.Pending())
                    {
                        TimerManager.Wait(100);
                    }
                    if (!_running) break;

                    TcpClient client;
                    try
                    {
                        client = _listener.AcceptTcpClient();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SSH] Accept failed: {ex.Message}");
                        TimerManager.Wait(500);
                        continue;
                    }

                    string remoteIp;
                    try { remoteIp = client.Client.RemoteEndPoint?.ToString() ?? "Unknown"; }
                    catch { remoteIp = "Unknown"; }

                    lock (_activeConnectionsLock)
                    {
                        if (!_activeConnections.Add(remoteIp))
                        {
                            Console.WriteLine($"[SSH] Duplicate accept for {remoteIp}, dropping reference (not closing).");
                            continue;
                        }
                    }

                    Console.WriteLine($"[SSH] Connection from {remoteIp}");

                    try
                    {
                        byte[] bannerBytes = Encoding.ASCII.GetBytes(SshTransport.ServerBanner + "\r\n");
                        client.Client.Send(bannerBytes, SocketFlags.None);
                        Console.WriteLine("[SSH] Banner sent to client");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SSH] Failed to send banner: {ex.Message}");
                        try { client.Close(); } catch { }
                        lock (_activeConnectionsLock) { _activeConnections.Remove(remoteIp); }
                        continue;
                    }

                    var capturedClient = client;
                    var capturedIp = remoteIp;
                    Kernel.RunServiceAsync(() => HandleConnection(capturedClient, capturedIp));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SSH] Listener fatal error: {ex.Message}");
            }
            finally
            {
                try { _listener?.Stop(); } catch { }
                _listener = null;
            }
        }

        public static void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }

        private static void HandleConnection(TcpClient client, string remoteIp)
        {
            Console.WriteLine($"[SSH] HandleConnection entered for {remoteIp}");

            try
            {
                SshTransport transport = new SshTransport(client, _hostKey!);
                transport.Handshake();
                Console.WriteLine($"[SSH] {remoteIp} handshake OK (cipher={transport.CipherS2C})");

                var session = new SshSession(transport, client);
                session.Run();
                Logger.Log($"SSH: {remoteIp} session ended");

                try { transport.Dispose(); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SSH] {remoteIp} error: {ex.Message}");
                Console.WriteLine($"[SSH] Stack trace: {ex.StackTrace}");
                Logger.LogError($"SSH {remoteIp}: {ex}");
            }
            finally
            {
                try { client.Close(); } catch { }
                lock (_activeConnectionsLock) { _activeConnections.Remove(remoteIp); }
            }
        }

        private static RsaHostKey? TryLoadHostKey()
        {
            try
            {
                if (!System.IO.File.Exists(KeyPath)) return null;

                byte[] blob = System.IO.File.ReadAllBytes(KeyPath);
                var buf = new SshBuffer(blob);

                BigInteger n = buf.ReadMpint();
                BigInteger e = buf.ReadMpint();
                BigInteger d = buf.ReadMpint();
                BigInteger p = buf.ReadMpint();
                BigInteger q = buf.ReadMpint();

                return RsaHostKey.FromComponents(n, e, d, p, q);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SSH] Failed to load host key, will regenerate: {ex.Message}");
                return null;
            }
        }

        private static void SaveHostKey(RsaHostKey key)
        {
            try
            {
                var buf = new SshBuffer(1024);
                buf.WriteMpint(key.N);
                buf.WriteMpint(key.E);
                buf.WriteMpint(key.D);
                buf.WriteMpint(key.P);
                buf.WriteMpint(key.Q);
                System.IO.File.WriteAllBytes(KeyPath, buf.ToArray());
                Console.WriteLine($"[SSH] Host key saved to {KeyPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SSH] Host key persistence skipped: {ex.Message}");
            }
        }
    }
}
