﻿#region Coypright and License

/*
 * AxCrypt - Copyright 2015, Svante Seleborg, All Rights Reserved
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

using Axantum.AxCrypt.Core.Crypto.Asymmetric;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axantum.AxCrypt.Core.Service
{
    /// <summary>
    /// Prepare keys for use in the background, in order to make them available as quickly as possible. This class is thread safe.
    /// </summary>
    public class KeyPairService
    {
        private int _firstBatch;

        private int _bufferCount;

        private Queue<IAsymmetricKeyPair> _keyPairs = new Queue<IAsymmetricKeyPair>();

        public KeyPairService(int firstBatch, int bufferCount)
        {
            if (firstBatch < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstBatch));
            }
            if (bufferCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferCount));
            }

            _firstBatch = firstBatch;
            _bufferCount = bufferCount;
        }

        /// <summary>
        /// Gets a new key pair, either from the pre-generated queue or at worst by synchronously
        /// generating one.
        /// </summary>
        /// <returns></returns>
        public IAsymmetricKeyPair New()
        {
            IAsymmetricKeyPair keyPair;
            lock (_keyPairs)
            {
                if (_keyPairs.Count > 0)
                {
                    keyPair = _keyPairs.Dequeue();
                }
                else
                {
                    keyPair = CreateKeyPair();
                }
            }
            Start();
            return keyPair;
        }

        /// <summary>
        /// Starts a background process to fill up to the appropriate level of key pairs.
        /// </summary>
        public void Start()
        {
            Task.Factory.StartNew(() => { while (!IsBufferFull) { AddOneKeyPair(); } });
        }

        private void EnsureKeyPairBuffer()
        {
            if (!IsBufferFull)
            {
                AddOneKeyPair();
            }

            Start();
        }

        private void AddOneKeyPair()
        {
            IAsymmetricKeyPair keyPair = CreateKeyPair();
            lock (_keyPairs)
            {
                _keyPairs.Enqueue(keyPair);
            }
            return;
        }

        private bool IsBufferFull
        {
            get
            {
                lock (_keyPairs)
                {
                    if (_firstBatch > 0)
                    {
                        --_firstBatch;
                        return false;
                    }
                    return _keyPairs.Count >= _bufferCount;
                }
            }
        }

        public bool IsAnyAvailable
        {
            get
            {
                lock (_keyPairs)
                {
                    return _keyPairs.Count > 0;
                }
            }
        }

        private IAsymmetricKeyPair CreateKeyPair()
        {
            IAsymmetricKeyPair keyPair = Resolve.AsymmetricFactory.CreateKeyPair(Resolve.UserSettings.AsymmetricKeyBits);
            return keyPair;
        }
    }
}