﻿using System.Collections.Generic;
using System.Security.Principal;
using System.Web;
using JetBrains.Annotations;
using log4net;
using Umbraco.Core;
using uMigrate.Internal;

namespace uMigrate.Infrastructure {
    [UsedImplicitly, PublicAPI]
    public class MigrationApplicationEventHandler : ApplicationEventHandler {
        // that's just for some performance optimizations, e.g. you may skip reacting to changes during migration.
        [PublicAPI] public static bool MigrationRunning { get; private set; }

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext) {
            MigrationRunning = true;
            try {
                RunMigrations(applicationContext);
            }
            finally {
                MigrationRunning = false;
            }
        }

        private void RunMigrations(ApplicationContext applicationContext) {
            if (HttpContext.Current != null && HttpContext.Current.User == null)
                HttpContext.Current.User = new GenericPrincipal(new GenericIdentity("0"), new string[0]);

            var logRepository = new DatabaseMigrationRecordRepository(applicationContext.DatabaseContext.Database);

            var migrationResolver = new MigrationResolver(new AppDomainAssemblyMigrationTypeProvider());
            var context = new MigrationContext(
                new ServiceContextWrapper(applicationContext.Services),
                applicationContext.DatabaseContext.Database,
                logRepository
            );
            var logger = LogManager.GetLogger(typeof(UmbracoMigrator));
            var migrator = new UmbracoMigrator(migrationResolver, context, logger);

            migrator.Run();
        }
    }
}