﻿#region Coypright and License

/*
 * AxCrypt - Copyright 2013, Svante Seleborg, All Rights Reserved
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
using Axantum.AxCrypt.Core.UI;
using NUnit.Framework;
using System;
using System.Linq;

namespace Axantum.AxCrypt.Core.Test
{
    [TestFixture]
    public static class TestUserSettings
    {
        [SetUp]
        public static void Setup()
        {
            SetupAssembly.AssemblySetup();
            FakeRuntimeFileInfo.AddFolder(@"C:\Folder\");
        }

        [TearDown]
        public static void Teardown()
        {
            SetupAssembly.AssemblyTeardown();
        }

        [Test]
        public static void TestSerializeDeserialize()
        {
            UserSettings settings = new UserSettings(OS.Current.FileInfo(@"C:\Folder\UserSettings.txt"));

            Assert.That(settings.DebugMode, Is.False, "The DebugMode is always false by default.");
            settings.DebugMode = true;
            Assert.That(settings.DebugMode, Is.True, "The DebugMode was set to true.");

            settings = new UserSettings(OS.Current.FileInfo(@"C:\Folder\UserSettings.txt"));
            Assert.That(settings.DebugMode, Is.True, "The DebugMode was set to true, and should have been saved.");
        }

        [Test]
        public static void TestNamedStronglyTypedProperties()
        {
            UserSettings settings = new UserSettings(OS.Current.FileInfo(@"C:\Folder\UserSettings.txt"));

            settings.CultureName = "sv-SE";
            Assert.That(settings.CultureName, Is.EqualTo("sv-SE"), "The value should be this.");

            settings.AxCrypt2VersionCheckUrl = new Uri("http://localhost/versioncheck");
            Assert.That(settings.AxCrypt2VersionCheckUrl, Is.EqualTo(new Uri("http://localhost/versioncheck")), "The value should be this.");

            settings.UpdateUrl = new Uri("http://localhost/update");
            Assert.That(settings.UpdateUrl, Is.EqualTo(new Uri("http://localhost/update")), "The value should be this.");

            settings.LastUpdateCheckUtc = new DateTime(2001, 02, 03);
            Assert.That(settings.LastUpdateCheckUtc, Is.EqualTo(new DateTime(2001, 02, 03)), "The value should be this.");

            settings.NewestKnownVersion = "1.2.3.4";
            Assert.That(settings.NewestKnownVersion, Is.EqualTo("1.2.3.4"), "The value should be this.");

            settings.DebugMode = true;
            Assert.That(settings.DebugMode, Is.True, "The value should be this.");

            settings.AxCrypt2HelpUrl = new Uri("http://localhost/help");
            Assert.That(settings.AxCrypt2HelpUrl, Is.EqualTo(new Uri("http://localhost/help")), "The value should be this.");

            settings.DisplayEncryptPassphrase = true;
            Assert.That(settings.DisplayEncryptPassphrase, Is.True, "The value should be this.");

            settings.DisplayDecryptPassphrase = true;
            Assert.That(settings.DisplayDecryptPassphrase, Is.True, "The value should be this.");

            settings.KeyWrapIterations = 1234;
            Assert.That(settings.KeyWrapIterations, Is.EqualTo(1234), "The value should be this.");

            KeyWrapSalt salt = new KeyWrapSalt(16);
            settings.ThumbprintSalt = salt;
            Assert.That(settings.ThumbprintSalt.GetBytes(), Is.EqualTo(salt.GetBytes()), "The value should be this.");

            settings.SessionNotificationMinimumIdle = new TimeSpan(1, 2, 3);
            Assert.That(settings.SessionNotificationMinimumIdle, Is.EqualTo(new TimeSpan(1, 2, 3)), "The value should be this.");
        }
    }
}