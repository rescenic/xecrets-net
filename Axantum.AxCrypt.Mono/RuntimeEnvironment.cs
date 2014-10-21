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

using Axantum.AxCrypt.Core;
using Axantum.AxCrypt.Core.IO;
using Axantum.AxCrypt.Core.Ipc;
using Axantum.AxCrypt.Core.Portable;
using Axantum.AxCrypt.Core.Runtime;
using Axantum.AxCrypt.Mono.Portable;
using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Axantum.AxCrypt.Mono
{
    public class RuntimeEnvironment : IRuntimeEnvironment, IDisposable
    {
        public static void RegisterTypeFactories()
        {
            TypeMap.Register.Singleton<IRuntimeEnvironment>(() => new RuntimeEnvironment(".axx"));
            TypeMap.Register.Singleton<IPortableFactory>(() => new PortableFactory());
            TypeMap.Register.Singleton<ILogging>(() => new Logging());
            TypeMap.Register.Singleton<CommandService>(() => new CommandService(new HttpRequestServer(), new HttpRequestClient()));
            TypeMap.Register.Singleton<IPlatform>(() => new MonoPlatform());

            TypeMap.Register.New<ISleep>(() => new Sleep());
            TypeMap.Register.New<IDelayTimer>(() => new DelayTimer());
            TypeMap.Register.New<string, IRuntimeFileInfo>((path) => new RuntimeFileInfo(path));
        }

        public RuntimeEnvironment(string extension)
        {
            AxCryptExtension = extension;
        }

        public bool IsLittleEndian
        {
            get
            {
                return BitConverter.IsLittleEndian;
            }
        }

        public string AxCryptExtension
        {
            get;
            set;
        }

        public Platform Platform
        {
            get
            {
                return TypeMap.Resolve.Singleton<IPlatform>().Platform;
            }
        }

        public int StreamBufferSize
        {
            get { return 65536; }
        }

        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }

        public virtual ILauncher Launch(string path)
        {
            return new Launcher(path);
        }

        public ITiming StartTiming()
        {
            return new Timing();
        }

        public IWebCaller CreateWebCaller()
        {
            return new WebCaller();
        }

        public bool CanTrackProcess
        {
            get { return Platform == Platform.WindowsDesktop; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeInternal();
            }
        }

        private void DisposeInternal()
        {
            if (_firstInstanceMutex != null)
            {
                _firstInstanceMutex.Close();
                _firstInstanceMutex = null;
            }
            if (_firstInstanceRunning != null)
            {
                _firstInstanceRunning.Close();
                _firstInstanceRunning = null;
            }
        }

        public string EnvironmentVariable(string name)
        {
            string variable = Environment.GetEnvironmentVariable(name);

            return variable;
        }

        public int MaxConcurrency
        {
            get
            {
                return Environment.ProcessorCount > 2 ? Environment.ProcessorCount - 1 : 2;
            }
        }

        private EventWaitHandle _firstInstanceRunning;

        private EventWaitHandle FirstInstanceEvent
        {
            get
            {
                if (_firstInstanceRunning == null)
                {
                    _firstInstanceRunning = new EventWaitHandle(false, EventResetMode.ManualReset, "Axantum.AxCrypt.NET-FirstInstanceRunning");
                }
                return _firstInstanceRunning;
            }
        }

        private Mutex _firstInstanceMutex;

        private bool _isFirstInstance;

        public bool IsFirstInstance
        {
            get
            {
                if (_firstInstanceMutex == null)
                {
                    _firstInstanceMutex = new Mutex(true, "Axantum.AxCrypt.NET-FirstInstance", out _isFirstInstance);
                    if (_isFirstInstance)
                    {
                        FirstInstanceEvent.Set();
                    }
                }
                return _isFirstInstance;
            }
        }

        public bool FirstInstanceRunning(TimeSpan timeout)
        {
            return FirstInstanceEvent.WaitOne(timeout, false);
        }

        public void ExitApplication(int exitCode)
        {
            Environment.Exit(exitCode);
        }

        public void DebugMode(bool enabled)
        {
            if (enabled)
            {
                ServicePointManager.ServerCertificateValidationCallback = (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
                {
                    return true;
                };
            }
            else
            {
                ServicePointManager.ServerCertificateValidationCallback = null;
            }
        }

        public SynchronizationContext SynchronizationContext
        {
            get
            {
                return SynchronizationContext.Current ?? new SynchronizationContext();
            }
        }
    }
}