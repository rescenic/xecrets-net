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
using Axantum.AxCrypt.Core.Extensions;
using Axantum.AxCrypt.Core.IO;
using Axantum.AxCrypt.Core.Runtime;
using Axantum.AxCrypt.Core.Session;
using Axantum.AxCrypt.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Axantum.AxCrypt.Core
{
    public class AxCryptFile
    {
        /// <summary>
        /// Encrypt a file
        /// </summary>
        /// <param name="file">The file to encrypt</param>
        /// <param name="destination">The destination file</param>
        /// <remarks>It is the callers responsibility to ensure that the source file exists, that the destination file
        /// does not exist and can be created etc.</remarks>
        public virtual void Encrypt(IRuntimeFileInfo sourceFile, IRuntimeFileInfo destinationFile, Passphrase key, AxCryptOptions options, IProgressContext progress)
        {
            if (sourceFile == null)
            {
                throw new ArgumentNullException("sourceFile");
            }
            if (destinationFile == null)
            {
                throw new ArgumentNullException("destinationFile");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }

            using (Stream sourceStream = new ProgressStream(sourceFile.OpenRead(), progress))
            {
                using (Stream destinationStream = destinationFile.OpenWrite())
                {
                    using (IAxCryptDocument document = new V1AxCryptDocument(key, Instance.UserSettings.GetKeyWrapIterations(CryptoFactory.Aes128V1Id)))
                    {
                        document.FileName = sourceFile.Name;
                        document.CreationTimeUtc = sourceFile.CreationTimeUtc;
                        document.LastAccessTimeUtc = sourceFile.LastAccessTimeUtc;
                        document.LastWriteTimeUtc = sourceFile.LastWriteTimeUtc;
                        document.EncryptTo(sourceStream, destinationStream, options);
                    }
                }
                if (options.HasMask(AxCryptOptions.SetFileTimes))
                {
                    destinationFile.SetFileTimes(sourceFile.CreationTimeUtc, sourceFile.LastAccessTimeUtc, sourceFile.LastWriteTimeUtc);
                }
            }
        }

        public static void Encrypt(IRuntimeFileInfo sourceFile, Stream destinationStream, Passphrase key, Guid cryptoId, AxCryptOptions options, IProgressContext progress)
        {
            if (sourceFile == null)
            {
                throw new ArgumentNullException("sourceFile");
            }
            if (destinationStream == null)
            {
                throw new ArgumentNullException("destinationStream");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }

            using (Stream sourceStream = new ProgressStream(sourceFile.OpenRead(), progress))
            {
                using (IAxCryptDocument document = Factory.New<AxCryptFactory>().CreateDocument(key, cryptoId))
                {
                    document.FileName = sourceFile.Name;
                    document.CreationTimeUtc = sourceFile.CreationTimeUtc;
                    document.LastAccessTimeUtc = sourceFile.LastAccessTimeUtc;
                    document.LastWriteTimeUtc = sourceFile.LastWriteTimeUtc;

                    document.EncryptTo(sourceStream, destinationStream, options);
                }
            }
        }

        public void EncryptFileWithBackupAndWipe(string sourceFile, string destinationFile, Passphrase key, Guid cryptoId, IProgressContext progress)
        {
            if (sourceFile == null)
            {
                throw new ArgumentNullException("sourceFile");
            }
            if (destinationFile == null)
            {
                throw new ArgumentNullException("destinationFile");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }
            IRuntimeFileInfo sourceFileInfo = Factory.New<IRuntimeFileInfo>(sourceFile);
            IRuntimeFileInfo destinationFileInfo = Factory.New<IRuntimeFileInfo>(destinationFile);
            EncryptFileWithBackupAndWipe(sourceFileInfo, destinationFileInfo, key, cryptoId, progress);
        }

        public virtual void EncryptFoldersUniqueWithBackupAndWipe(IEnumerable<IRuntimeFileInfo> folders, Passphrase encryptionKey, Guid cryptoId, IProgressContext progress)
        {
            progress.NotifyLevelStart();
            try
            {
                IEnumerable<IRuntimeFileInfo> files = folders.SelectMany((folder) => folder.ListEncryptable());
                progress.AddTotal(files.Count());
                foreach (IRuntimeFileInfo file in files)
                {
                    EncryptFileUniqueWithBackupAndWipe(file, encryptionKey, cryptoId, progress);
                    progress.AddCount(1);
                }
            }
            finally
            {
                progress.NotifyLevelFinished();
            }
        }

        public virtual void EncryptFileUniqueWithBackupAndWipe(IRuntimeFileInfo fileInfo, Passphrase encryptionKey, Guid cryptoId, IProgressContext progress)
        {
            IRuntimeFileInfo destinationFileInfo = fileInfo.CreateEncryptedName();
            destinationFileInfo = Factory.New<IRuntimeFileInfo>(destinationFileInfo.FullName.CreateUniqueFile());
            EncryptFileWithBackupAndWipe(fileInfo, destinationFileInfo, encryptionKey, cryptoId, progress);
        }

        public virtual void EncryptFileWithBackupAndWipe(IRuntimeFileInfo sourceFileInfo, IRuntimeFileInfo destinationFileInfo, Passphrase key, Guid cryptoId, IProgressContext progress)
        {
            if (sourceFileInfo == null)
            {
                throw new ArgumentNullException("sourceFileInfo");
            }
            if (destinationFileInfo == null)
            {
                throw new ArgumentNullException("destinationFileInfo");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }
            progress.NotifyLevelStart();
            using (Stream activeFileStream = sourceFileInfo.OpenRead())
            {
                WriteToFileWithBackup(destinationFileInfo, (Stream destination) =>
                {
                    Encrypt(sourceFileInfo, destination, key, cryptoId, AxCryptOptions.EncryptWithCompression, progress);
                }, progress);
            }
            Wipe(sourceFileInfo, progress);
            progress.NotifyLevelFinished();
        }

        /// <summary>
        /// Decrypt a source file to a destination file, given a passphrase
        /// </summary>
        /// <param name="sourceFile">The source file</param>
        /// <param name="destinationFile">The destination file</param>
        /// <param name="passphrase">The passphrase</param>
        /// <returns>true if the passphrase was correct</returns>
        public bool Decrypt(IRuntimeFileInfo sourceFile, IRuntimeFileInfo destinationFile, Passphrase key, AxCryptOptions options, IProgressContext progress)
        {
            if (sourceFile == null)
            {
                throw new ArgumentNullException("sourceFile");
            }
            if (destinationFile == null)
            {
                throw new ArgumentNullException("destinationFile");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }
            using (IAxCryptDocument document = Document(sourceFile, key, new ProgressContext()))
            {
                if (!document.PassphraseIsValid)
                {
                    return false;
                }
                Decrypt(document, destinationFile, options, progress);
            }
            return true;
        }

        /// <summary>
        /// Decrypt a source file to a destination file, given a passphrase
        /// </summary>
        /// <param name="sourceFile">The source file</param>
        /// <param name="destinationFile">The destination file</param>
        /// <param name="passphrase">The passphrase</param>
        /// <returns>true if the passphrase was correct</returns>
        public string Decrypt(IRuntimeFileInfo sourceFile, string destinationDirectory, Passphrase key, AxCryptOptions options, IProgressContext progress)
        {
            if (sourceFile == null)
            {
                throw new ArgumentNullException("sourceFile");
            }
            if (destinationDirectory == null)
            {
                throw new ArgumentNullException("destinationDirectory");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }
            string destinationFileName = null;
            using (IAxCryptDocument document = Document(sourceFile, key, new ProgressContext()))
            {
                if (!document.PassphraseIsValid)
                {
                    return destinationFileName;
                }
                destinationFileName = document.FileName;
                IRuntimeFileInfo destinationFullPath = Factory.New<IRuntimeFileInfo>(Path.Combine(destinationDirectory, destinationFileName));
                Decrypt(document, destinationFullPath, options, progress);
            }
            return destinationFileName;
        }

        /// <summary>
        /// Decrypt from loaded AxCryptDocument to a destination file
        /// </summary>
        /// <param name="document">The loaded AxCryptDocument</param>
        /// <param name="destinationFile">The destination file</param>
        public void Decrypt(IAxCryptDocument document, IRuntimeFileInfo destinationFile, AxCryptOptions options, IProgressContext progress)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }
            if (destinationFile == null)
            {
                throw new ArgumentNullException("destinationFile");
            }
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }
            try
            {
                if (Instance.Log.IsInfoEnabled)
                {
                    Instance.Log.LogInfo("Decrypting to '{0}'.".InvariantFormat(destinationFile.Name));
                }

                using (Stream destinationStream = destinationFile.OpenWrite())
                {
                    document.DecryptTo(destinationStream);
                }

                if (Instance.Log.IsInfoEnabled)
                {
                    Instance.Log.LogInfo("Decrypted to '{0}'.".InvariantFormat(destinationFile.Name));
                }
            }
            catch (Exception)
            {
                if (destinationFile.IsExistingFile)
                {
                    Wipe(destinationFile, progress);
                }
                throw;
            }
            if (options.HasMask(AxCryptOptions.SetFileTimes))
            {
                destinationFile.SetFileTimes(document.CreationTimeUtc, document.LastAccessTimeUtc, document.LastWriteTimeUtc);
            }
        }

        public virtual void DecryptFilesInsideFolderUniqueWithWipeOfOriginal(IRuntimeFileInfo folderInfo, Passphrase decryptionKey, IStatusChecker statusChecker, IProgressContext progress)
        {
            IEnumerable<IRuntimeFileInfo> files = folderInfo.ListEncrypted();
            Instance.ParallelFileOperation.DoFiles(files, (file, context) =>
            {
                context.LeaveSingleThread();
                return DecryptFileUniqueWithWipeOfOriginal(file, decryptionKey, context);
            },
            (status) =>
            {
                Instance.SessionNotify.Notify(new SessionNotification(SessionNotificationType.PurgeActiveFiles));
                statusChecker.CheckStatusAndShowMessage(status.Status, status.FullName);
            });
        }

        public FileOperationContext DecryptFileUniqueWithWipeOfOriginal(IRuntimeFileInfo fileInfo, Passphrase decryptionKey, IProgressContext progress)
        {
            progress.NotifyLevelStart();
            using (IAxCryptDocument document = Factory.New<AxCryptFile>().Document(fileInfo, decryptionKey, progress))
            {
                if (!document.PassphraseIsValid)
                {
                    return new FileOperationContext(fileInfo.FullName, FileOperationStatus.Canceled);
                }

                IRuntimeFileInfo destinationFileInfo = Factory.New<IRuntimeFileInfo>(Path.Combine(Path.GetDirectoryName(fileInfo.FullName), document.FileName));
                destinationFileInfo = Factory.New<IRuntimeFileInfo>(destinationFileInfo.FullName.CreateUniqueFile());
                DecryptFile(document, destinationFileInfo.FullName, progress);
            }
            Wipe(fileInfo, progress);
            progress.NotifyLevelFinished();
            return new FileOperationContext(String.Empty, FileOperationStatus.Success);
        }

        public virtual void DecryptFile(IAxCryptDocument document, string decryptedFileFullName, IProgressContext progress)
        {
            IRuntimeFileInfo decryptedFileInfo = Factory.New<IRuntimeFileInfo>(decryptedFileFullName);
            Decrypt(document, decryptedFileInfo, AxCryptOptions.SetFileTimes, progress);
        }

        /// <summary>
        /// Load an AxCryptDocument from a source file with a passphrase
        /// </summary>
        /// <param name="sourceFile">The source file</param>
        /// <param name="passphrase">The passphrase</param>
        /// <returns>An instance of AxCryptDocument. Use IsPassphraseValid property to determine validity.</returns>
        public virtual IAxCryptDocument Document(IRuntimeFileInfo sourceFile, Passphrase key, IProgressContext progress)
        {
            if (sourceFile == null)
            {
                throw new ArgumentNullException("sourceFile");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }

            try
            {
                IAxCryptDocument document = Factory.New<AxCryptFactory>().CreateDocument(key, new ProgressStream(sourceFile.OpenRead(), progress));
                return document;
            }
            catch (AxCryptException ace)
            {
                ace.DisplayContext = sourceFile.FullName;
                throw;
            }
            catch (Exception ex)
            {
                AxCryptException ace = new InternalErrorException("An unhandled exception occurred.", ErrorStatus.Unknown, ex);
                ace.DisplayContext = sourceFile.FullName;
                throw ace;
            }
        }

        public void WriteToFileWithBackup(IRuntimeFileInfo destinationFileInfo, Action<Stream> writeFileStreamTo, IProgressContext progress)
        {
            if (destinationFileInfo == null)
            {
                throw new ArgumentNullException("destinationFileInfo");
            }
            if (writeFileStreamTo == null)
            {
                throw new ArgumentNullException("writeFileStreamTo");
            }

            string temporaryFilePath = MakeAlternatePath(destinationFileInfo, ".tmp");
            IRuntimeFileInfo temporaryFileInfo = Factory.New<IRuntimeFileInfo>(temporaryFilePath);

            try
            {
                using (Stream temporaryStream = temporaryFileInfo.OpenWrite())
                {
                    writeFileStreamTo(temporaryStream);
                }
            }
            catch (Exception)
            {
                if (temporaryFileInfo.IsExistingFile)
                {
                    Wipe(temporaryFileInfo, progress);
                }
                throw;
            }

            if (destinationFileInfo.IsExistingFile)
            {
                string backupFilePath = MakeAlternatePath(destinationFileInfo, ".bak");
                IRuntimeFileInfo backupFileInfo = Factory.New<IRuntimeFileInfo>(destinationFileInfo.FullName);

                backupFileInfo.MoveTo(backupFilePath);
                temporaryFileInfo.MoveTo(destinationFileInfo.FullName);
                Wipe(backupFileInfo, progress);
            }
            else
            {
                temporaryFileInfo.MoveTo(destinationFileInfo.FullName);
            }
        }

        private static string MakeAlternatePath(IRuntimeFileInfo fileInfo, string extension)
        {
            string alternatePath = Path.Combine(Path.GetDirectoryName(fileInfo.FullName), Path.GetFileNameWithoutExtension(fileInfo.Name) + extension);
            return alternatePath.CreateUniqueFile();
        }

        public static string MakeAxCryptFileName(IRuntimeFileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                throw new ArgumentNullException("fileInfo");
            }
            string axCryptExtension = OS.Current.AxCryptExtension;
            string originalExtension = Path.GetExtension(fileInfo.Name);
            string modifiedExtension = originalExtension.Length == 0 ? String.Empty : "-" + originalExtension.Substring(1);
            string axCryptFileName = Path.Combine(Path.GetDirectoryName(fileInfo.FullName), Path.GetFileNameWithoutExtension(fileInfo.Name) + modifiedExtension + axCryptExtension);

            return axCryptFileName;
        }

        public virtual void Wipe(IRuntimeFileInfo fileInfo, IProgressContext progress)
        {
            if (fileInfo == null)
            {
                throw new ArgumentNullException("fileInfo");
            }
            if (!fileInfo.IsExistingFile)
            {
                return;
            }
            if (Instance.Log.IsInfoEnabled)
            {
                Instance.Log.LogInfo("Wiping '{0}'.".InvariantFormat(fileInfo.Name));
            }
            bool cancelPending = false;
            progress.NotifyLevelStart();

            string randomName;
            do
            {
                randomName = GenerateRandomFileName(fileInfo.FullName);
            } while (Factory.New<IRuntimeFileInfo>(randomName).IsExistingFile);
            IRuntimeFileInfo moveToFileInfo = Factory.New<IRuntimeFileInfo>(fileInfo.FullName);
            moveToFileInfo.MoveTo(randomName);

            using (Stream stream = moveToFileInfo.OpenWrite())
            {
                long length = stream.Length + OS.Current.StreamBufferSize - stream.Length % OS.Current.StreamBufferSize;
                progress.AddTotal(length);
                for (long position = 0; position < length; position += OS.Current.StreamBufferSize)
                {
                    byte[] random = Instance.RandomGenerator.Generate(OS.Current.StreamBufferSize);
                    stream.Write(random, 0, random.Length);
                    stream.Flush();
                    try
                    {
                        progress.AddCount(random.Length);
                    }
                    catch (OperationCanceledException)
                    {
                        cancelPending = true;
                        progress.AddCount(random.Length);
                    }
                }
            }

            moveToFileInfo.Delete();
            progress.NotifyLevelFinished();
            if (cancelPending)
            {
                throw new OperationCanceledException("Delayed cancel during wipe.");
            }
        }

        private static string GenerateRandomFileName(string originalFullName)
        {
            const string validFileNameChars = "abcdefghijklmnopqrstuvwxyz";

            string directory = Path.GetDirectoryName(originalFullName);
            string fileName = Path.GetFileNameWithoutExtension(originalFullName);

            int randomLength = fileName.Length < 8 ? 8 : fileName.Length;
            StringBuilder randomName = new StringBuilder(randomLength + 4);
            byte[] random = Instance.RandomGenerator.Generate(randomLength);
            for (int i = 0; i < randomLength; ++i)
            {
                randomName.Append(validFileNameChars[random[i] % validFileNameChars.Length]);
            }
            randomName.Append(".tmp");

            return Path.Combine(directory, randomName.ToString());
        }
    }
}