using EasyAbp.AbpHelper;
using EasyAbp.AbpHelper.Core;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace EasyApp.AbpHelper.Tests
{
    [DependsOn(
        typeof(AbpTestBaseModule),
        typeof(AbpHelperCoreModule)
    )]
    public class AbpHelperTestModule : AbpModule
    {
    }
}