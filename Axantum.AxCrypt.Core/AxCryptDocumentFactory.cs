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

using Axantum.AxCrypt.Core.Crypto;
using Axantum.AxCrypt.Core.Header;
using Axantum.AxCrypt.Core.Reader;
using Axantum.AxCrypt.Core.Runtime;
using System;
using System.IO;
using System.Linq;

namespace Axantum.AxCrypt.Core
{
    public class AxCryptDocumentFactory
    {
        /// <summary>
        /// Instantiate an instance of IAxCryptDocument appropriate for the file provided, i.e. V1 or V2.
        /// </summary>
        /// <param name="passphrase">The passphrase.</param>
        /// <param name="fileInfo">The file to use.</param>
        /// <returns></returns>
        public IAxCryptDocument Create(string passphrase, Stream inputStream)
        {
            Headers headers = new Headers();
            AxCryptReader reader = headers.Load(inputStream);

            SymmetricKey key = reader.Crypto(headers, passphrase).Key;
            return Create(key, headers, reader);
        }

        /// <summary>
        /// Instantiate an instance of IAxCryptDocument appropriate for the file provided, i.e. V1 or V2.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <returns></returns>
        public IAxCryptDocument Create(SymmetricKey key, Stream inputStream)
        {
            Headers headers = new Headers();
            AxCryptReader reader = headers.Load(inputStream);

            return Create(key, headers, reader);
        }

        private static IAxCryptDocument Create(SymmetricKey key, Headers headers, AxCryptReader reader)
        {
            VersionHeaderBlock versionHeader = headers.FindHeaderBlock<VersionHeaderBlock>();
            IAxCryptDocument document;
            switch (versionHeader.FileVersionMajor)
            {
                case 1:
                case 2:
                case 3:
                    V1AxCryptDocument v1Document = new V1AxCryptDocument();
                    v1Document.Load(key, reader, headers);
                    document = v1Document;
                    break;

                case 4:
                    V2AxCryptDocument v2Document = new V2AxCryptDocument();
                    v2Document.Load(key, reader, headers);
                    document = v2Document;
                    break;

                default:
                    throw new FileFormatException("Too new file version. Please upgrade.");
            }

            return document;
        }
    }
}