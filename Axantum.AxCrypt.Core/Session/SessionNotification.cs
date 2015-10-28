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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Axantum.AxCrypt.Core.Session
{
    public class SessionNotification
    {
        public LogOnIdentity Identity { get; private set; }

        public IEnumerable<string> FullNames { get; private set; }

        public SessionNotificationType NotificationType { get; private set; }

        public SessionNotification(SessionNotificationType notificationType, LogOnIdentity identity, IEnumerable<string> fullNames)
        {
            NotificationType = notificationType;
            Identity = identity;
            FullNames = fullNames;
        }

        public SessionNotification(SessionNotificationType notificationType, LogOnIdentity identity, string fullName)
            : this(notificationType, identity, new string[] { fullName })
        {
        }

        public SessionNotification(SessionNotificationType notificationType, IEnumerable<string> fullNames)
            : this(notificationType, LogOnIdentity.Empty, fullNames)
        {
        }

        public SessionNotification(SessionNotificationType notificationType, string fullName)
            : this(notificationType, LogOnIdentity.Empty, new string[] { fullName })
        {
        }

        public SessionNotification(SessionNotificationType notificationType, LogOnIdentity identity)
            : this(notificationType, identity, new string[0])
        {
        }

        public SessionNotification(SessionNotificationType notificationType)
            : this(notificationType, LogOnIdentity.Empty, new string[0])
        {
        }
    }
}