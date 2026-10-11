using NSubstitute;
using Playnite;

namespace Graviton.Tests.Fakes.Playnite.Dialogs;

public sealed class FakeDialogs
{
    private readonly Queue<MessageBoxResult> _messageResponses = new();
    private readonly Queue<MessageBoxResponse> _customMessageResponses = new();
    private readonly Queue<List<string>?> _folderSelections = new();
    private readonly Queue<List<string>?> _fileSelections = new();

    public IDialogs Api { get; } = Substitute.For<IDialogs>();

    public FakeDialogs()
    {
        ConfigureMessages();
        ConfigureSelections();
    }

    public void EnqueueMessageResponse(MessageBoxResult response)
    {
        _messageResponses.Enqueue(response);
    }

    public void EnqueueCustomMessageResponse(MessageBoxResponse response)
    {
        _customMessageResponses.Enqueue(response);
    }

    public void EnqueueFolderSelection(IEnumerable<string>? paths)
    {
        _folderSelections.Enqueue(paths?.ToList());
    }

    public void EnqueueFileSelection(IEnumerable<string>? paths)
    {
        _fileSelections.Enqueue(paths?.ToList());
    }

    #region Setup

    private void ConfigureMessages()
    {
        Api.ShowMessageAsync(
            messageBoxText: Arg.Any<string>(),
            caption: Arg.Any<string?>(),
            button: Arg.Any<MessageBoxButtons>(),
            severity: Arg.Any<MessageBoxSeverity>(),
            defaultResult: Arg.Any<MessageBoxResult>()).Returns(_ =>
        {
            var response = Dequeue(_messageResponses, "message response");
            return Task.FromResult(response);
        });

        Api.ShowMessageAsync(
            messageBoxText: Arg.Any<string>(),
            caption: Arg.Any<string>(),
            severity: Arg.Any<MessageBoxSeverity>(),
            respones: Arg.Any<List<MessageBoxResponse>>(),
            options: Arg.Any<List<MessageBoxOption>>()).Returns(_ =>
        {
            var response = Dequeue(_customMessageResponses, "custom message response");
            return Task.FromResult<MessageBoxResponse?>(response);
        });

        Api.ShowErrorMessageAsync(
            messageBoxText: Arg.Any<string>(),
            caption: Arg.Any<string?>(),
            exception: Arg.Any<Exception?>()).Returns(_ =>
        {
            return Task.FromResult(MessageBoxResult.OK);
        });
    }

    private void ConfigureSelections()
    {
        Api.SelectFolderAsync(
            initialDir: Arg.Any<string?>(),
            allowMultiple: Arg.Any<bool>()).Returns(_ =>
        {
            var paths = Dequeue(_folderSelections, "folder selection");
            return Task.FromResult(paths);
        });

        Api.SelectFileAsync(
            fileTypeChoices: Arg.Any<Dictionary<string, string[]>?>(),
            allowMultiple: Arg.Any<bool>(),
            initialDir: Arg.Any<string?>()).Returns(_ =>
        {
            var paths = Dequeue(_fileSelections, "file selection");
            return Task.FromResult(paths);
        });
    }

    #endregion

    private static T Dequeue<T>(Queue<T> queue, string responseType)
    {
        if (!queue.TryDequeue(out var response))
        {
            throw new InvalidOperationException($"No Playnite {responseType} has been configured.");
        }

        return response;
    }
}
