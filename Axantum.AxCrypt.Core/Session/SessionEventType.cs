﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Axantum.AxCrypt.Core.Session
{
    public enum SessionEventType
    {
        ActiveFileChange,
        WatchedFolderAdded,
        WatchedFolderRemoved,
        LogOn,
        LogOff,
        ProcessExit,
        SessionChange,
        KnownKeyChange,
        WorkFolderChange,
    }
}