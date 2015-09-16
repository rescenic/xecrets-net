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

using Axantum.AxCrypt.Abstractions;
using Axantum.AxCrypt.Core.IO;
using System;
using System.Linq;

namespace Axantum.AxCrypt.Core.Runtime
{
    public class WorkFolder
    {
        public WorkFolder(string path)
        {
            FileInfo = TypeMap.Resolve.New<IDataContainer>(path);
            FileInfo.CreateFolder();
        }

        public IDataContainer FileInfo { get; private set; }

        public virtual IDataContainer CreateTemporaryFolder()
        {
            string destinationFolder = Resolve.Portable.Path().Combine(TypeMap.Resolve.Singleton<WorkFolder>().FileInfo.FullName, Resolve.Portable.Path().GetFileNameWithoutExtension(Resolve.Portable.Path().GetRandomFileName()) + Resolve.Portable.Path().DirectorySeparatorChar);
            IDataContainer destinationFolderInfo = TypeMap.Resolve.New<IDataContainer>(destinationFolder);
            destinationFolderInfo.CreateFolder();

            return destinationFolderInfo;
        }
    }
}