using EasyAbp.AbpHelper.Core.Attributes;

namespace EasyAbp.AbpHelper.Core.Commands.Module.AddRef
{
    public class AddRefCommandOption : ModuleRefCommandOption
    {
        [Argument("SrcFolder", Description = "path to the project(s) folder in which needs to be added")]
        public string SrcFolder { get; set; } = null!;

        [Option('v', "version", Description = "Specify the version of the package(s) to add")]
        public string Version { get; set; } = null!;
    }
}