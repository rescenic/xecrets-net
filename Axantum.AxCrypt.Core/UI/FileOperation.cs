﻿#region Coypright and License

/*
 * AxCrypt - Copyright 2012, Svante Seleborg, All Rights Reserved
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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Axantum.AxCrypt.Core.Crypto;
using Axantum.AxCrypt.Core.IO;
using Axantum.AxCrypt.Core.Session;
using Axantum.AxCrypt.Core.System;

namespace Axantum.AxCrypt.Core.UI
{
    public static class FileOperation
    {
        public static FileOperationStatus OpenAndLaunchApplication(this FileSystemState fileSystemState, string file, IEnumerable<AesKey> keys, ProgressContext progress)
        {
            if (fileSystemState == null)
            {
                throw new ArgumentNullException("fileSystemState");
            }
            if (file == null)
            {
                throw new ArgumentNullException("file");
            }
            if (keys == null)
            {
                throw new ArgumentNullException("keys");
            }
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }

            IRuntimeFileInfo fileInfo = AxCryptEnvironment.Current.FileInfo(file);
            if (!fileInfo.Exists)
            {
                if (Logging.IsWarningEnabled)
                {
                    Logging.Warning("Tried to open non-existing '{0}'.".InvariantFormat(fileInfo.FullName)); //MLHIDE
                }
                return FileOperationStatus.FileDoesNotExist;
            }

            ActiveFile destinationActiveFile = fileSystemState.FindEncryptedPath(fileInfo.FullName);

            if (destinationActiveFile == null || !destinationActiveFile.DecryptedFileInfo.Exists)
            {
                IRuntimeFileInfo destinationFolderInfo = GetDestinationFolder(destinationActiveFile);
                destinationActiveFile = TryDecrypt(destinationFolderInfo, keys, fileInfo, progress);
            }
            else
            {
                destinationActiveFile = CheckKeysForAlreadyDecryptedFile(destinationActiveFile, keys, progress);
            }

            if (destinationActiveFile == null)
            {
                return FileOperationStatus.InvalidKey;
            }

            fileSystemState.Add(destinationActiveFile);
            fileSystemState.Save();

            FileOperationStatus status = LaunchApplicationForDocument(fileSystemState, destinationActiveFile);
            return status;
        }

        private static FileOperationStatus LaunchApplicationForDocument(FileSystemState fileSystemState, ActiveFile destinationActiveFile)
        {
            ILauncher process;
            try
            {
                if (Logging.IsInfoEnabled)
                {
                    Logging.Info("Starting process for '{0}'".InvariantFormat(destinationActiveFile.DecryptedFileInfo.FullName)); //MLHIDE
                }
                process = AxCryptEnvironment.Current.Launch(destinationActiveFile.DecryptedFileInfo.FullName);
                if (process.WasStarted)
                {
                    process.Exited += new EventHandler(process_Exited);
                }
                else
                {
                    if (Logging.IsInfoEnabled)
                    {
                        Logging.Info("Starting process for '{0}' did not start a process, assumed handled by the shell.".InvariantFormat(destinationActiveFile.DecryptedFileInfo.FullName)); //MLHIDE
                    }
                }
            }
            catch (Win32Exception w32ex)
            {
                if (Logging.IsErrorEnabled)
                {
                    Logging.Error("Could not launch application for '{0}', Win32Exception was '{1}'.".InvariantFormat(destinationActiveFile.DecryptedFileInfo.FullName, w32ex.Message)); //MLHIDE
                }
                return FileOperationStatus.CannotStartApplication;
            }

            if (Logging.IsWarningEnabled)
            {
                if (process.HasExited)
                {
                    Logging.Warning("The process seems to exit immediately for '{0}'".InvariantFormat(destinationActiveFile.DecryptedFileInfo.FullName)); //MLHIDE
                }
            }

            if (Logging.IsInfoEnabled)
            {
                Logging.Info("Launched and opened '{0}'.".InvariantFormat(destinationActiveFile.DecryptedFileInfo.FullName)); //MLHIDE
            }

            destinationActiveFile = new ActiveFile(destinationActiveFile, ActiveFileStatus.AssumedOpenAndDecrypted, process);
            fileSystemState.Add(destinationActiveFile);
            fileSystemState.Save();

            return FileOperationStatus.Success;
        }

        private static void process_Exited(object sender, EventArgs e)
        {
            if (Logging.IsInfoEnabled)
            {
                Logging.Info("Process exit event for '{0}'.".InvariantFormat(((ILauncher)sender).Path)); //MLHIDE
            }

            AxCryptEnvironment.Current.NotifyFileChanged();
        }

        private static ActiveFile TryDecrypt(IRuntimeFileInfo destinationFolderInfo, IEnumerable<AesKey> keys, IRuntimeFileInfo sourceFileInfo, ProgressContext progress)
        {
            ActiveFile destinationActiveFile = null;
            foreach (AesKey key in keys)
            {
                if (Logging.IsInfoEnabled)
                {
                    Logging.Info("Decrypting '{0}'".InvariantFormat(sourceFileInfo.FullName)); //MLHIDE
                }
                using (FileLock sourceLock = FileLock.Lock(sourceFileInfo))
                {
                    using (AxCryptDocument document = AxCryptFile.Document(sourceFileInfo, key, progress))
                    {
                        if (!document.PassphraseIsValid)
                        {
                            continue;
                        }

                        string destinationName = document.DocumentHeaders.FileName;
                        string destinationPath = Path.Combine(destinationFolderInfo.FullName, destinationName);

                        IRuntimeFileInfo destinationFileInfo = AxCryptEnvironment.Current.FileInfo(destinationPath);
                        using (FileLock fileLock = FileLock.Lock(destinationFileInfo))
                        {
                            AxCryptFile.Decrypt(document, destinationFileInfo, AxCryptOptions.SetFileTimes, progress);
                        }
                        destinationActiveFile = new ActiveFile(sourceFileInfo, destinationFileInfo, key, ActiveFileStatus.AssumedOpenAndDecrypted | ActiveFileStatus.IgnoreChange, null);
                        if (Logging.IsInfoEnabled)
                        {
                            Logging.Info("File decrypted from '{0}' to '{1}'".InvariantFormat(sourceFileInfo.FullName, destinationActiveFile.DecryptedFileInfo.FullName)); //MLHIDE
                        }
                        break;
                    }
                }
            }
            return destinationActiveFile;
        }

        private static IRuntimeFileInfo GetDestinationFolder(ActiveFile destinationActiveFile)
        {
            string destinationFolder;
            if (destinationActiveFile != null)
            {
                destinationFolder = Path.GetDirectoryName(destinationActiveFile.DecryptedFileInfo.FullName);
            }
            else
            {
                destinationFolder = Path.Combine(AxCryptEnvironment.Current.TemporaryDirectoryInfo.FullName, Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + Path.DirectorySeparatorChar); //MLHIDE
            }
            IRuntimeFileInfo destinationFolderInfo = AxCryptEnvironment.Current.FileInfo(destinationFolder);
            destinationFolderInfo.CreateDirectory();
            return destinationFolderInfo;
        }

        private static ActiveFile CheckKeysForAlreadyDecryptedFile(ActiveFile destinationActiveFile, IEnumerable<AesKey> keys, ProgressContext progress)
        {
            foreach (AesKey key in keys)
            {
                using (AxCryptDocument document = AxCryptFile.Document(destinationActiveFile.EncryptedFileInfo, key, progress))
                {
                    if (document.PassphraseIsValid)
                    {
                        if (Logging.IsWarningEnabled)
                        {
                            Logging.Warning("File was already decrypted and the key was known for '{0}' to '{1}'".InvariantFormat(destinationActiveFile.EncryptedFileInfo.FullName, destinationActiveFile.DecryptedFileInfo.FullName)); //MLHIDE
                        }
                        return new ActiveFile(destinationActiveFile, key);
                    }
                }
            }
            return null;
        }
    }
}