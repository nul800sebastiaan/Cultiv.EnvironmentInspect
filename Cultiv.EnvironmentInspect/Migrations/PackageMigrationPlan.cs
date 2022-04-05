using System;
using Umbraco.Cms.Core.Packaging;

namespace Cultiv.EnvironmentInspect.Migrations
{
    public class InstalledPackagesMigrationPlan : PackageMigrationPlan
    {
        public InstalledPackagesMigrationPlan() : base("Cultiv.EnvironmentInspect Dashboard")
        {
        }

        protected override void DefinePlan()
        {
            To(new Guid("5681C92F-48BF-409A-A13F-530A712CA739"));
        }
    }
}