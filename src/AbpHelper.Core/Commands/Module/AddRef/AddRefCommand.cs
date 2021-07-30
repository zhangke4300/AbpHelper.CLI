using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EasyAbp.AbpHelper.Core.Steps.Abp;
using EasyAbp.AbpHelper.Core.Steps.Abp.ModificationCreatorSteps.CSharp;
using EasyAbp.AbpHelper.Core.Steps.Common;
using EasyAbp.AbpHelper.Core.Workflow;
using Elsa;
using Elsa.Activities;
using Elsa.Activities.ControlFlow.Activities;
using Elsa.Expressions;
using Elsa.Scripting.JavaScript;
using Elsa.Services;
using JetBrains.Annotations;

namespace EasyAbp.AbpHelper.Core.Commands.Module.AddRef
{
    public class AddRefCommand : CommandWithOption<AddRefCommandOption>
    {
        private readonly IDictionary<string, string> _packageProjectMap = new Dictionary<string, string>
        {
            {ModuleConsts.Shared, "Domain.Shared"},
            {ModuleConsts.Domain, "Domain"},
            {ModuleConsts.EntityFrameworkCore, "EntityFrameworkCore"},
            {ModuleConsts.MongoDB, "MongoDB"},
            {ModuleConsts.Contracts, "Application.Contracts"},
            {ModuleConsts.Application, "Application"},
            {ModuleConsts.HttpApi, "HttpApi"},
            {ModuleConsts.Client, "HttpApi.Client"},
            {ModuleConsts.Web, "Web"},
        };

        public AddRefCommand([NotNull] IServiceProvider serviceProvider) : base(serviceProvider, "addref", "Add ABP module according to the specified packages")
        {
            AddValidator(result =>
            {
                if (!result.Children.Any(sr => sr.Symbol is Option opt && _packageProjectMap.Keys.Contains(opt.Name)))
                {
                    return "You must specify at least one package to add.";
                }

                return null;
            });
        }

        public override Task RunCommand(AddRefCommandOption option)
        {
            var directories = Directory.GetDirectories(option.SrcFolder).Select(x => $"{x.Substring(x.LastIndexOf('\\') + 1)}");
            var name = directories.Select(x => new { Name = x, Count = x.Split('.').Count() }).OrderBy(x => x.Count).FirstOrDefault().Name;
            option.ModuleName = name.Substring(0, name.LastIndexOf('.'));
            return base.RunCommand(option);
        }

        protected override IActivityBuilder ConfigureBuild(AddRefCommandOption option, IActivityBuilder activityBuilder)
        {
            var moduleIdToCustomsMapping = typeof(ModuleRefCommandOption).GetProperties()
                .Where(prop => prop.PropertyType == typeof(bool) && (bool) prop.GetValue(option)!)
                .Select(prop => _packageProjectMap[prop.Name.ToKebabCase()])
                .ToDictionary(x => x, x => new List<string>(new[] {$"{x}:{x}"}));
            
            if (!option.Custom.IsNullOrEmpty())
            {
                foreach (var customPart in option.Custom.Split(','))
                {
                    var moduleId = customPart.Substring(0, customPart.IndexOf(':'));
                    
                    if (!moduleIdToCustomsMapping.ContainsKey(moduleId))
                    {
                        moduleIdToCustomsMapping.Add(moduleId, new List<string>());
                    }
                    
                    moduleIdToCustomsMapping[moduleId].Add(customPart);
                }
            }            

            string cdOption = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? " /d" : "";
            return base.ConfigureBuild(option, activityBuilder)                    
                    .Then<SetVariable>(
                        step =>
                        {
                            var temps = string.Join(",", Directory.GetDirectories(option.SrcFolder).Select(x => $"\"{x.Substring(x.LastIndexOf('\\') + 1)}\""));
                            step.VariableName = VariableNames.ProjectDirectoryNames;
                            step.ValueExpression = new JavaScriptExpression<string[]>(
                                $"[{temps}]");
                        }
                    )
                    //dotnet sln add D:\code\abp\abp-github\modules\identity\src\Volo.Abp.Identity.Application -s abp\Framework
                    //add project
                    .Then<ForEach>(
                        x => { x.CollectionExpression = new JavaScriptExpression<IList<object>>(VariableNames.ProjectDirectoryNames); },
                        branch =>
                            branch.When(OutcomeNames.Iterate)
                            .Then<SetVariable>(
                                    step =>
                                    {
                                        step.VariableName = VariableNames.CurrentProjectDirectoryName;
                                        step.ValueExpression = new JavaScriptExpression<string>("CurrentValue");
                                    }
                                )
                            .Then<RunCommandStep>(
                                step =>
                                {
                                    var exp = @$"`cd{cdOption} ${{AspNetCoreDir}} && dotnet sln add ${{{CommandConsts.OptionVariableName}.{nameof(AddRefCommandOption.SrcFolder)}}}\\${{{nameof(VariableNames.CurrentProjectDirectoryName)}}} -s projects\\${{{CommandConsts.OptionVariableName}.{nameof(AddRefCommandOption.ModuleName)}}}`";
                                    step.Command = new JavaScriptExpression<string>(exp);
                                }
                            )
                            .Then(branch)
                    )


                    .Then<SetVariable>(
                        step =>
                        {
                            step.VariableName = VariableNames.TemplateDirectory;
                            step.ValueExpression = new LiteralExpression<string>("/Templates/Module");
                        })
                    .Then<SetVariable>(
                        step =>
                        {
                            step.VariableName = VariableNames.ProjectNames;
                            step.ValueExpression = new JavaScriptExpression<string[]>(
                                $"[{string.Join(",", moduleIdToCustomsMapping.SelectMany(x => x.Value).Select(x => $"\"{x}\"").JoinAsString(","))}]");
                        }
                    )
                    .Then<SetModelVariableStep>()
                    .Then<ForEach>(
                        x => { x.CollectionExpression = new JavaScriptExpression<IList<object>>(VariableNames.ProjectNames); },
                        branch =>
                            branch.When(OutcomeNames.Iterate)
                                .Then<SetVariable>(
                                    step =>
                                    {
                                        step.VariableName = VariableNames.CurrentModuleName;
                                        step.ValueExpression = new JavaScriptExpression<string>("CurrentValue.split(':')[0]");
                                    }
                                )
                                .Then<SetVariable>(
                                    step =>
                                    {
                                        step.VariableName = VariableNames.TargetAppProjectName;
                                        step.ValueExpression = new JavaScriptExpression<string>("CurrentValue.split(':')[1]");
                                    }
                                )
                                .Then<SetVariable>(
                                    step =>
                                    {
                                        step.VariableName = VariableNames.SubmoduleUsingTextPostfix;
                                        step.ValueExpression = new JavaScriptExpression<string>("CurrentValue.split(':').length > 2 ? '.' + CurrentValue.split(':')[2] : ''");
                                    }
                                )
                                .Then<SetVariable>(
                                    step =>
                                    {
                                        step.VariableName = VariableNames.PackageName;
                                        step.ValueExpression = new JavaScriptExpression<string>($"{VariableNames.CurrentModuleName} != '' ? {CommandConsts.OptionVariableName}.{nameof(ModuleRefCommandOption.ModuleName)} + '.' + {VariableNames.CurrentModuleName} : {CommandConsts.OptionVariableName}.{nameof(ModuleRefCommandOption.ModuleName)}");
                                    }
                                )
                                .Then<SetVariable>(
                                    step =>
                                    {
                                        step.VariableName = VariableNames.ModuleClassNamePostfix;
                                        step.ValueExpression = new JavaScriptExpression<string>($"{VariableNames.CurrentModuleName}.replace(/\\./g, '')");
                                    }
                                )
                                .Then<SetVariable>(
                                    step =>
                                    {
                                        step.VariableName = VariableNames.AppProjectClassNamePostfix;
                                        step.ValueExpression = new JavaScriptExpression<string>($"{VariableNames.TargetAppProjectName}.replace(/\\./g, '')");
                                    }
                                )
                                .Then<SetVariable>(
                                    step =>
                                    {
                                        step.VariableName = VariableNames.DependsOnModuleClassName;
                                        step.ValueExpression = new JavaScriptExpression<string>($"{CommandConsts.OptionVariableName}.{nameof(ModuleRefCommandOption.ModuleGroupNameWithoutCompanyName)} + {VariableNames.ModuleClassNamePostfix} + 'Module'");
                                    }
                                )
                                //add reference
                                .Then<RunCommandStep>(
                                    step =>
                                    {
                                        var exp = @$"`cd{cdOption} ${{AspNetCoreDir}}/src/${{ProjectInfo.FullName}}.${{TargetAppProjectName}} && dotnet add reference ${{{CommandConsts.OptionVariableName}.{nameof(AddRefCommandOption.SrcFolder)}}}\\${{{CommandConsts.OptionVariableName}.{nameof(AddRefCommandOption.ModuleName)}}}.${{TargetAppProjectName}}`";
                                        step.Command = new JavaScriptExpression<string>(exp);
                                    }
                                )
                                //.Then<IfElse>(
                                //    step => step.ConditionExpression = new JavaScriptExpression<bool>($"{CommandConsts.OptionVariableName}.{nameof(AddRefCommandOption.Version)} != null"),
                                //    ifElse =>
                                //    {
                                //        ifElse
                                //            .When(OutcomeNames.True) // with version specified 
                                //            .Then<RunCommandStep>(
                                //                step => step.Command = new JavaScriptExpression<string>(
                                //                    @$"`cd{cdOption} ${{AspNetCoreDir}}/src/${{ProjectInfo.FullName}}.${{TargetAppProjectName}} && dotnet add package ${{PackageName}} -v ${{Option.Version}}`"
                                //                ))
                                //            .Then(ActivityNames.AddDependsOn)
                                //            ;
                                //        ifElse
                                //            .When(OutcomeNames.False) // no version
                                //            .Then<RunCommandStep>(
                                //                step => step.Command = new JavaScriptExpression<string>(
                                //                    @$"`cd{cdOption} ${{AspNetCoreDir}}/src/${{ProjectInfo.FullName}}.${{TargetAppProjectName}} && dotnet add package ${{PackageName}}`"
                                //                ))
                                //            .Then(ActivityNames.AddDependsOn)
                                //            ;
                                //    }
                                //)
                                .Then<EmptyStep>().WithName(ActivityNames.AddDependsOn)
                                .Then<FileFinderStep>(
                                    step => { step.SearchFileName = new JavaScriptExpression<string>($"`${{ProjectInfo.Name}}${{{VariableNames.AppProjectClassNamePostfix}}}Module.cs`"); })
                                .Then<DependsOnStep>(step =>
                                {
                                    step.Action = new LiteralExpression<DependsOnStep.ActionType>(((int)DependsOnStep.ActionType.Add).ToString());
                                })
                                .Then<FileModifierStep>()
                                .Then<IfElse>(
                                    step => step.ConditionExpression = new JavaScriptExpression<bool>("TargetAppProjectName == 'EntityFrameworkCore'"),
                                    ifElse =>
                                    {
                                        // For "EntityFrameCore" package, we generate a "builder.ConfigureXXX();" in the migrations context class */
                                        ifElse
                                            .When(OutcomeNames.True)
                                            .Then<FileFinderStep>(
                                                step => { step.SearchFileName = new JavaScriptExpression<string>("`${ProjectInfo.Name}MigrationsDbContext.cs`"); }
                                            )
                                            .Then<MigrationsContextStep>(step =>
                                            {
                                                step.Action = new LiteralExpression<MigrationsContextStep.ActionType>(((int)MigrationsContextStep.ActionType.Add).ToString());
                                            })
                                            .Then<FileModifierStep>()
                                            .Then(ActivityNames.NextProject)
                                            ;
                                        ifElse
                                            .When(OutcomeNames.False)
                                            .Then(ActivityNames.NextProject)
                                            ;
                                    }
                                )
                                .Then<EmptyStep>().WithName(ActivityNames.NextProject)
                                .Then(branch)
                    )
                ;
        }
    }
}