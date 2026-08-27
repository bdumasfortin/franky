using Franky.Runtime.Configuration;
using Franky.Runtime.ControlBoard;
using Franky.Runtime.ConsoleUi;
using Franky.Runtime.Conversation;
using Franky.Runtime.Diagnostics;
using Franky.Runtime.Tools;

if (args.Contains("--control-board", StringComparer.OrdinalIgnoreCase))
{
    return await ControlBoardApplication.RunAsync(args, CancellationToken.None);
}

var options = AssistantOptions.FromEnvironment(args);
var events = new JsonEventSink(Console.Error);
var commandTool = new NamedCommandTool(new ProcessCommandRunner());
var conversationClient = ConversationClientFactory.Create(options, commandTool, events);

var application = new ConsoleApplication(
    conversationClient,
    commandTool,
    events,
    Console.In,
    Console.Out,
    options);

return await application.RunAsync(CancellationToken.None);
