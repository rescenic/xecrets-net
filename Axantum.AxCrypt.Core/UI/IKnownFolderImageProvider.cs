﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axantum.AxCrypt.Core.UI
{
    public interface IKnownFolderImageProvider
    {
        object GetImage(KnownFolderKind folderKind);
    }
}