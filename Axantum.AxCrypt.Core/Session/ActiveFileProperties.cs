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
using System.Runtime.Serialization;

namespace Axantum.AxCrypt.Core.Session
{
    [DataContract(Namespace = "http://www.axantum.com/Serialization/")]
    public class ActiveFileProperties
    {
        public ActiveFileProperties(DateTime lastActivityTimeUtc, DateTime lastEncryptionWriteTimeUtc, Guid cryptoId)
        {
            LastActivityTimeUtc = lastActivityTimeUtc;
            LastEncryptionWriteTimeUtc = lastEncryptionWriteTimeUtc;
            CryptoId = cryptoId;
        }

        [DataMember(Name = "CryptoId")]
        public Guid CryptoId
        {
            get;
            private set;
        }

        [DataMember]
        public DateTime LastActivityTimeUtc { get; private set; }

        /// <summary>
        /// Records the Last Write Time that was valid at the most recent encryption update of the encrypted file.
        /// </summary>
        [DataMember]
        public DateTime LastEncryptionWriteTimeUtc { get; private set; }
    }
}