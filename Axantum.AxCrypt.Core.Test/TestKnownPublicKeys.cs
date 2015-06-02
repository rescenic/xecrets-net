﻿#region Coypright and License

/*
 * AxCrypt - Copyright 2015, Svante Seleborg, All Rights Reserved
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
using Axantum.AxCrypt.Core.Crypto.Asymmetric;
using Axantum.AxCrypt.Core.Session;
using Axantum.AxCrypt.Core.Test.Properties;
using Axantum.AxCrypt.Core.UI;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 3016 // Attribute-arguments as arrays are not CLS compliant. Ignore this here, it's how NUnit works.

namespace Axantum.AxCrypt.Core.Test
{
    [TestFixture(CryptoImplementation.Mono)]
    [TestFixture(CryptoImplementation.BouncyCastle)]
    public class TestKnownPublicKeys
    {
        private CryptoImplementation _cryptoImplementation;

        public TestKnownPublicKeys(CryptoImplementation cryptoImplementation)
        {
            _cryptoImplementation = cryptoImplementation;
        }

        [SetUp]
        public void Setup()
        {
            SetupAssembly.AssemblySetup();
            SetupAssembly.AssemblySetupCrypto(_cryptoImplementation);

            TypeMap.Register.Singleton<IRandomGenerator>(() => new FakePseudoRandomGenerator());
            TypeMap.Register.Singleton<IAsymmetricFactory>(() => new FakeAsymmetricFactory("MD5"));
            Resolve.UserSettings.AsymmetricKeyBits = 512;
        }

        [TearDown]
        public void Teardown()
        {
            SetupAssembly.AssemblyTeardown();
        }

        [Test]
        public void TestCreateSingleKey()
        {
            FakeInMemoryDataStoreItem store = new FakeInMemoryDataStoreItem("KnownPublicKeys.txt");

            IAsymmetricPublicKey key = TypeMap.Resolve.Singleton<IAsymmetricFactory>().CreatePublicKey(Resources.PublicKey1);
            UserPublicKey userPublicKey = new UserPublicKey(new EmailAddress("test@test.com"), key);
            using (KnownPublicKeys knownPublicKeys = KnownPublicKeys.Load(store, Resolve.Serializer))
            {
                Assert.That(knownPublicKeys.PublicKeys.Count(), Is.EqualTo(0), "There should be no entries now.");

                knownPublicKeys.AddOrReplace(userPublicKey);
                Assert.That(knownPublicKeys.PublicKeys.Count(), Is.EqualTo(1), "There should be one entry now.");
            }

            using (KnownPublicKeys knownPublicKeys = KnownPublicKeys.Load(store, Resolve.Serializer))
            {
                Assert.That(knownPublicKeys.PublicKeys.Count(), Is.EqualTo(1), "There should be one entry now.");

                Assert.That(knownPublicKeys.PublicKeys.First(), Is.EqualTo(userPublicKey), "The instances should compare equal");
            }
        }

        [Test]
        public void TestReplaceKey()
        {
            FakeInMemoryDataStoreItem store = new FakeInMemoryDataStoreItem("KnownPublicKeys.txt");

            IAsymmetricPublicKey key1 = TypeMap.Resolve.Singleton<IAsymmetricFactory>().CreatePublicKey(Resources.PublicKey1);
            UserPublicKey userPublicKey1 = new UserPublicKey(new EmailAddress("test@test.com"), key1);
            using (KnownPublicKeys knownPublicKeys = KnownPublicKeys.Load(store, Resolve.Serializer))
            {
                Assert.That(knownPublicKeys.PublicKeys.Count(), Is.EqualTo(0), "There should be no entries now.");

                knownPublicKeys.AddOrReplace(userPublicKey1);
                Assert.That(knownPublicKeys.PublicKeys.Count(), Is.EqualTo(1), "There should be one entry now.");
            }

            IAsymmetricPublicKey key2 = TypeMap.Resolve.Singleton<IAsymmetricFactory>().CreatePublicKey(Resources.PublicKey2);
            UserPublicKey userPublicKey2 = new UserPublicKey(new EmailAddress("test@test.com"), key2);

            Assert.That(userPublicKey1, Is.Not.EqualTo(userPublicKey2), "Checking that the two public keys really are different.");
            using (KnownPublicKeys knownPublicKeys = KnownPublicKeys.Load(store, Resolve.Serializer))
            {
                Assert.That(knownPublicKeys.PublicKeys.Count(), Is.EqualTo(1), "There should be one entry now.");

                knownPublicKeys.AddOrReplace(userPublicKey2);
                Assert.That(knownPublicKeys.PublicKeys.Count(), Is.EqualTo(1), "There should be one entry now.");
                Assert.That(knownPublicKeys.PublicKeys.First(), Is.Not.EqualTo(userPublicKey1), "The instances should not compare equal");
                Assert.That(knownPublicKeys.PublicKeys.First(), Is.EqualTo(userPublicKey2), "The instances should compare equal");
            }

            using (KnownPublicKeys knownPublicKeys = KnownPublicKeys.Load(store, Resolve.Serializer))
            {
                Assert.That(knownPublicKeys.PublicKeys.Count(), Is.EqualTo(1), "There should be one entry now.");

                Assert.That(knownPublicKeys.PublicKeys.First(), Is.Not.EqualTo(userPublicKey1), "The instances should not compare equal");
                Assert.That(knownPublicKeys.PublicKeys.First(), Is.EqualTo(userPublicKey2), "The instances should compare equal");
            }
        }
    }
}