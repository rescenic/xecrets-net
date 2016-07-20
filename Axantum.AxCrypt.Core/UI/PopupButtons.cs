﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axantum.AxCrypt.Core.UI
{
    [Flags]
    public enum PopupButtons
    {
        None = 0,
        Ok = 1,
        Cancel = 2,
        Exit = 4,
        OkCancel = Ok | Cancel,
        OkExit = Ok | Exit,
        OkCancelExit = Ok | Cancel | Exit,
    }
}