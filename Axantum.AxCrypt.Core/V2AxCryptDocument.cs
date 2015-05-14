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

using Axantum.AxCrypt.Core.Algorithm;
using Axantum.AxCrypt.Core.Crypto;
using Axantum.AxCrypt.Core.Crypto.Asymmetric;
using Axantum.AxCrypt.Core.Extensions;
using Axantum.AxCrypt.Core.Header;
using Axantum.AxCrypt.Core.IO;
using Axantum.AxCrypt.Core.Portable;
using Axantum.AxCrypt.Core.Reader;
using Axantum.AxCrypt.Core.Runtime;
using Org.BouncyCastle.Utilities.Zlib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Axantum.AxCrypt.Core
{
    /// <summary>
    /// Enables a single point of interaction for an AxCrypt File Format Version 4 encrypted stream with all but the data available
    /// in-memory. File Format Version 4 is only supported by AxCrypt 2.x or higher. It builds on, and is similar to, File Format
    /// Version 3. See the specification titled "AxCrypt Version 2 Algorithms and File Format" for details.
    /// </summary>
    public class V2AxCryptDocument : IAxCryptDocument
    {
        private long _plaintextLength;

        private long _compressedPlaintextLength;

        public V2AxCryptDocument()
        {
        }

        public V2AxCryptDocument(AxCryptReader reader)
        {
            _reader = reader;
        }

        public V2AxCryptDocument(EncryptionParameters encryptionParameters, long keyWrapIterations)
        {
            DocumentHeaders = new V2DocumentHeaders(encryptionParameters, keyWrapIterations);
        }

        public V2DocumentHeaders DocumentHeaders { get; private set; }

        public ICryptoFactory CryptoFactory { get; private set; }

        private AxCryptReader _reader;

        public bool PassphraseIsValid { get; set; }

        public bool Load(Passphrase key, Guid cryptoId, Stream inputStream)
        {
            Headers headers = new Headers();
            AxCryptReader reader = headers.CreateReader(new LookAheadStream(inputStream));

            return Load(key, cryptoId, reader, headers);
        }

        public bool Load(Passphrase passphrase, Guid cryptoId, Headers headers)
        {
            return Load(passphrase, cryptoId, _reader, headers);
        }

        public bool Load(IAsymmetricPrivateKey privateKey, Guid cryptoId, Headers headers)
        {
            return Load(privateKey, cryptoId, _reader, headers);
        }

        private void ResetState()
        {
            PassphraseIsValid = false;
            if (DocumentHeaders != null)
            {
                DocumentHeaders.Dispose();
                DocumentHeaders = null;
            }
        }

        /// <summary>
        /// Loads an AxCrypt file from the specified reader. After this, the reader is positioned to
        /// read encrypted data.
        /// </summary>
        /// <param name="passphrase">The passphrase.</param>
        /// <param name="cryptoId">The crypto identifier.</param>
        /// <param name="reader">The reader.</param>
        /// <param name="headers">The headers.</param>
        /// <returns>
        /// True if the key was valid, false if it was wrong.
        /// </returns>
        private bool Load(Passphrase passphrase, Guid cryptoId, AxCryptReader reader, Headers headers)
        {
            ResetState();
            if (cryptoId == V1Aes128CryptoFactory.CryptoId)
            {
                return PassphraseIsValid;
            }

            _reader = reader;
            CryptoFactory = Resolve.CryptoFactory.Create(cryptoId);
            V2KeyWrapHeaderBlock keyWrap = headers.FindHeaderBlock<V2KeyWrapHeaderBlock>();
            IDerivedKey key = CryptoFactory.RestoreDerivedKey(passphrase, keyWrap.DerivationSalt, keyWrap.DerivationIterations);
            keyWrap.SetDerivedKey(CryptoFactory, key);
            DocumentHeaders = new V2DocumentHeaders(keyWrap);
            PassphraseIsValid = DocumentHeaders.Load(headers);

            return PassphraseIsValid;
        }

        public bool Load(IAsymmetricPrivateKey privateKey, Guid cryptoId, AxCryptReaderBase reader, Headers headers)
        {
            ICryptoFactory cryptoFactory = Resolve.CryptoFactory.Create(cryptoId);

            IEnumerable<V2AsymmetricKeyWrapHeaderBlock> keyWraps = headers.HeaderBlocks.OfType<V2AsymmetricKeyWrapHeaderBlock>();
            foreach (V2AsymmetricKeyWrapHeaderBlock keyWrap in keyWraps)
            {
                keyWrap.SetPrivateKey(cryptoFactory, privateKey);
                if (keyWrap.Crypto(0) == null)
                {
                    continue;
                }

                DocumentHeaders = new V2DocumentHeaders(keyWrap);
                PassphraseIsValid = DocumentHeaders.Load(headers);
                if (PassphraseIsValid)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Encrypt a stream with a given set of headers and write to an output stream. The caller is responsible for consistency and completeness
        /// of the headers. Headers that are not known until encryption and compression are added here.
        /// </summary>
        /// <param name="outputDocumentHeaders"></param>
        /// <param name="inputStream"></param>
        /// <param name="outputStream"></param>
        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public void EncryptTo(Stream inputStream, Stream outputStream, AxCryptOptions options)
        {
            if (inputStream == null)
            {
                throw new ArgumentNullException("inputStream");
            }
            if (outputStream == null)
            {
                throw new ArgumentNullException("outputStream");
            }
            if (options.HasMask(AxCryptOptions.EncryptWithCompression) && options.HasMask(AxCryptOptions.EncryptWithoutCompression))
            {
                throw new ArgumentException("Invalid options, cannot specify both with and without compression.");
            }
            if (!options.HasMask(AxCryptOptions.EncryptWithCompression) && !options.HasMask(AxCryptOptions.EncryptWithoutCompression))
            {
                throw new ArgumentException("Invalid options, must specify either with or without compression.");
            }
            DocumentHeaders.IsCompressed = options.HasMask(AxCryptOptions.EncryptWithCompression);
            using (V2HmacStream outputHmacStream = new V2HmacStream(new V2HmacCalculator(new SymmetricKey(DocumentHeaders.GetHmacKey())), outputStream))
            {
                DocumentHeaders.WriteStartWithHmac(outputHmacStream);
                using (ICryptoTransform encryptor = DocumentHeaders.CreateDataCrypto().CreateEncryptingTransform())
                {
                    using (Stream axCryptDataStream = new V2AxCryptDataStream(outputHmacStream))
                    {
                        using (Stream encryptingStream = TypeMap.Resolve.New<CryptoStream>().Initialize(new NonClosingStream(axCryptDataStream), encryptor, CryptoStreamMode.Write))
                        {
                            if (DocumentHeaders.IsCompressed)
                            {
                                EncryptWithCompressionInternal(inputStream, encryptingStream);
                            }
                            else
                            {
                                _compressedPlaintextLength = _plaintextLength = StreamExtensions.CopyTo(inputStream, encryptingStream);
                            }
                        }
                    }
                }
                DocumentHeaders.WriteEndWithHmac(outputHmacStream, _plaintextLength, _compressedPlaintextLength);
            }
        }

        private void EncryptWithCompressionInternal(Stream inputStream, Stream encryptingStream)
        {
            using (ZOutputStream deflatingStream = new ZOutputStream(encryptingStream, -1))
            {
                deflatingStream.FlushMode = JZlib.Z_SYNC_FLUSH;
                inputStream.CopyTo(deflatingStream);
                deflatingStream.FlushMode = JZlib.Z_FINISH;
                deflatingStream.Finish();

                _plaintextLength = deflatingStream.TotalIn;
                _compressedPlaintextLength = deflatingStream.TotalOut;
            }
        }

        /// <summary>
        /// Decrypts the encrypted data to the given stream
        /// </summary>
        /// <param name="outputPlaintextStream">The resulting plain text stream.</param>
        public void DecryptTo(Stream outputPlaintextStream)
        {
            if (outputPlaintextStream == null)
            {
                throw new ArgumentNullException("outputPlaintextStream");
            }

            if (!PassphraseIsValid)
            {
                throw new InternalErrorException("Passsphrase is not valid!");
            }

            using (ICryptoTransform decryptor = DocumentHeaders.CreateDataCrypto().CreateDecryptingTransform())
            {
                using (Stream encryptedDataStream = CreateEncryptedDataStream())
                {
                    encryptedDataStream.DecryptTo(outputPlaintextStream, decryptor, DocumentHeaders.IsCompressed);
                }
            }

            DocumentHeaders.Trailers(_reader);
            if (DocumentHeaders.HmacCalculator.Hmac != DocumentHeaders.Hmac)
            {
                throw new Axantum.AxCrypt.Core.Runtime.IncorrectDataException("HMAC validation error.", ErrorStatus.HmacValidationError);
            }
        }

        private Stream CreateEncryptedDataStream()
        {
            if (_reader.CurrentItemType != AxCryptItemType.Data)
            {
                throw new InvalidOperationException("GetEncryptedDataStream() was called when the reader is not positioned at the data.");
            }

            _reader.SetStartOfData();
            V2AxCryptDataStream encryptedDataStream = new V2AxCryptDataStream(_reader, new V2HmacStream(DocumentHeaders.HmacCalculator));
            return encryptedDataStream;
        }

        public string FileName
        {
            get { return DocumentHeaders.FileName; }
            set { DocumentHeaders.FileName = value; }
        }

        public DateTime CreationTimeUtc
        {
            get { return DocumentHeaders.CreationTimeUtc; }
            set { DocumentHeaders.CreationTimeUtc = value; }
        }

        public DateTime LastAccessTimeUtc
        {
            get { return DocumentHeaders.LastAccessTimeUtc; }
            set { DocumentHeaders.LastAccessTimeUtc = value; }
        }

        public DateTime LastWriteTimeUtc
        {
            get { return DocumentHeaders.LastWriteTimeUtc; }
            set { DocumentHeaders.LastWriteTimeUtc = value; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeInternal();
            }
        }

        private void DisposeInternal()
        {
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
            if (DocumentHeaders != null)
            {
                DocumentHeaders.Dispose();
                DocumentHeaders = null;
            }
        }
    }
}