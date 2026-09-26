using CommunityToolkit.Mvvm.ComponentModel;

namespace Graviton.Models.Install
{
    public enum InstallShapes
    {
        None,
        ExternalFolder,
        TitleIDFolder,
        CLI

    }

    public enum InstallMode
    {
        SelectOne,
        SelectMany,
        Sequential,
        All
    }

    public partial class DynamicArgument : ObservableObject
    {
        public string ID { get; }
        public string Name { get; }
        public string Arg { get; }

        [ObservableProperty] private string? _value;

        public DynamicArgument(string id, string name, string arg)
        {
            ID = id;
            Name = name;
            Arg = arg;
        }

        public DynamicArgument Clone() => new(ID, Name, Arg);
    }

    public sealed record UpdateDLCShape(IReadOnlyCollection<string> paths);

    public sealed record TitleIDInstallDefinition
    (
        string? ID,
        string Name,
        IReadOnlyCollection<string> KnownEmulatorIDs,
        IReadOnlyCollection<string> ExecutableNames,
        string RuntimeArgs,
        string PathNote,
        IReadOnlyCollection<UpdateDLCShape>? InstallShapes
    );

    public sealed record CLIInstallDefinition
    (
        string? ID,
        string Name,
        IReadOnlyCollection<string> KnownEmulatorIDs,
        IReadOnlyCollection<string> ExecutableNames,
        IReadOnlyCollection<string> SupportedExtensions,
        string RuntimeArgs,
        IReadOnlyCollection<DynamicArgument>? DynamicArgsTemplate
    );

}
