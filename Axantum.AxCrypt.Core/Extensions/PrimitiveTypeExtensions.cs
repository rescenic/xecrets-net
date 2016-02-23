﻿#region Coypright and License

/*
 * AxCrypt - Copyright 2014, Svante Seleborg, All Rights Reserved
 *
 * This file is part of AxCrypt.
 *
 * AxCrypt is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * AxCrypt is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with AxCrypt.  If not, see <http://www.gnu.org/licenses/>.
 *
 * The source is maintained at http://bitbucket.org/axantum/axcrypt-net please visit for
 * updates, contributions and contact with the author. You may also visit
 * http://www.axantum.com for more information about the author.
*/

#endregion Coypright and License

using System;
using System.Linq;

namespace Axantum.AxCrypt.Core.Extensions
{
    public static class PrimitiveTypeExtensions
    {
        private static bool _isLittleEndian = OS.Current.IsLittleEndian;

        public static void SetLittleEndian(this bool isLittleEndian)
        {
            _isLittleEndian = isLittleEndian;
        }

        public static byte[] GetLittleEndianBytes(this long value)
        {
            if (_isLittleEndian)
            {
                return BitConverter.GetBytes(value);
            }

            byte[] bytes = new byte[sizeof(long)];

            for (int i = 0; value != 0 && i < bytes.Length; ++i)
            {
                bytes[i] = (byte)value;
                value >>= 8;
            }
            return bytes;
        }

        public static byte[] GetLittleEndianBytes(this int value)
        {
            if (_isLittleEndian)
            {
                return BitConverter.GetBytes(value);
            }

            byte[] bytes = new byte[sizeof(int)];

            for (int i = 0; value != 0 && i < bytes.Length; ++i)
            {
                bytes[i] = (byte)value;
                value >>= 8;
            }
            return bytes;
        }

        public static byte[] GetBigEndianBytes(this long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!_isLittleEndian)
            {
                return bytes;
            }

            byte b;

            b = bytes[0];
            bytes[0] = bytes[7];
            bytes[7] = b;

            b = bytes[1];
            bytes[1] = bytes[6];
            bytes[6] = b;

            b = bytes[2];
            bytes[2] = bytes[5];
            bytes[5] = b;

            b = bytes[3];
            bytes[3] = bytes[4];
            bytes[4] = b;

            return bytes;
        }

        public static byte[] GetBigEndianBytes(this int value)
        {
            if (!_isLittleEndian)
            {
                return BitConverter.GetBytes(value);
            }

            byte[] bytes = new byte[sizeof(int)];

            for (int i = bytes.Length - 1; value != 0 && i >= 0; --i)
            {
                bytes[i] = (byte)value;
                value >>= 8;
            }
            return bytes;
        }

        public static T Fallback<T>(this T value, T fallbackValue) where T : IEquatable<T>
        {
            return !value.Equals(default(T)) ? value : fallbackValue;
        }
    }
}