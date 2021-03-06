﻿using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uMigrate.Internal;

namespace uMigrate.Tests.Unit {
    [TestFixture]
    public class AppDomainAssemblyMigrationTypeProviderTests {
        [Test]
        public void GetAllMigrationTypes_DoesNotIncludeInterfaces() {
            var types = new AppDomainAssemblyMigrationTypeProvider().GetAllMigrationTypes();
            Assert.That(types, Has.None.Matches<Type>(t => t.IsInterface));
        }

        [Test]
        public void GetAllMigrationTypes_DoesNotIncludeAbstractClasses() {
            var types = new AppDomainAssemblyMigrationTypeProvider().GetAllMigrationTypes();
            Assert.That(types, Has.None.Matches<Type>(t => t.IsAbstract));
        }

        public interface ITestInterfaceMigration : IUmbracoMigration {}
        public abstract class TestAbstractMigration : UmbracoMigrationBase {}
    }
}
