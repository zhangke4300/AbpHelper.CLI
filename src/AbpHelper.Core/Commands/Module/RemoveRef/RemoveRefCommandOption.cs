using EasyAbp.AbpHelper.Core.Attributes;

namespace EasyAbp.AbpHelper.Core.Commands.Module.RemoveRef
{
    public class RemoveRefCommandOption : ModuleRefCommandOption
    {
        [Argument("SrcFolder", Description = "path to the projects folder in which needs to be removed")]
        public string SrcFolder { get; set; } = null!;
    }
}