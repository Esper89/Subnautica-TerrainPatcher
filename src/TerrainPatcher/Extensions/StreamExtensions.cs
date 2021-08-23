// Copyright (c) 2021 Esper Thomson
//
// This file is part of TerrainPatcher.
//
// TerrainPatcher is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// TerrainPatcher is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with TerrainPatcher.  If not, see <https://www.gnu.org/licenses/>.

using System.IO;

namespace TerrainPatcher.Extensions
{
    // Extensions to System.IO.Stream.
    // Makes dealing with binary files way easier.
    internal static class StreamExtensions
    {
        // Reading different data types from a stream.

        #region reading

        private static byte CheckReadByte(this Stream stream)
        {
            int result = stream.ReadByte();
            if (result < 0) throw new EndOfStreamException("Stream ended early.");
            return (byte)result;
        }

        public static sbyte ReadSByte(this Stream stream)
            => (sbyte)stream.CheckReadByte();

        public static short ReadShort(this Stream stream)
            => (short)(stream.CheckReadByte() + (stream.CheckReadByte() << 8));

        public static ushort ReadUShort(this Stream stream)
            => (ushort)(stream.CheckReadByte() + (stream.CheckReadByte() << 8));

        public static int ReadInt(this Stream stream)
            => stream.CheckReadByte() +
               (stream.CheckReadByte() << 8) +
               (stream.CheckReadByte() << 16) +
               (stream.CheckReadByte() << 24);

        public static uint ReadUInt(this Stream stream)
            => (uint)(stream.CheckReadByte() +
               (stream.CheckReadByte() << 8) +
               (stream.CheckReadByte() << 16) +
               (stream.CheckReadByte() << 24));

        #endregion

        // Writing different data types to a stream.

        #region writing

        public static void WriteSByte(this Stream stream, sbyte value)
        {
            stream.WriteByte((byte)value);
        }

        public static void WriteShort(this Stream stream, short value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
        }

        public static void WriteUShort(this Stream stream, ushort value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
        }

        public static void WriteInt(this Stream stream, int value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        public static void WriteUInt(this Stream stream, uint value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        #endregion

        // Reading and writing multiple bytes.

        #region multiple

        public static byte[] ReadBytes(this Stream stream, int count)
        {
            byte[] result = new byte[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = (byte)stream.ReadByte();
            }

            return result;
        }

        public static void WriteBytes(this Stream stream, byte[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                stream.WriteByte(value[i]);
            }
        }

        #endregion
    }
}
