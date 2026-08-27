using Franky.Runtime.Tests;

var tests = new (string Name, Func<Task> Run)[]
{
    (nameof(AssistantOptionsTests.SelectsOllamaWithoutOpenAiKey), AssistantOptionsTests.SelectsOllamaWithoutOpenAiKey),
    (nameof(AssistantOptionsTests.DefaultsToDemoWithoutProviderOrKey), AssistantOptionsTests.DefaultsToDemoWithoutProviderOrKey),
    (nameof(NamedCommandToolTests.RejectsCommandOutsideAllowlist), NamedCommandToolTests.RejectsCommandOutsideAllowlist),
    (nameof(NamedCommandToolTests.MapsAllowedNameToFixedProcess), NamedCommandToolTests.MapsAllowedNameToFixedProcess),
    (nameof(AssistantTurnCoordinatorTests.PreservesOneConversationSession), AssistantTurnCoordinatorTests.PreservesOneConversationSession),
    (nameof(AssistantTurnCoordinatorTests.RejectsOverlappingTurns), AssistantTurnCoordinatorTests.RejectsOverlappingTurns),
    (nameof(OpenAiResponsesClientTests.ReturnsTextAndStoresContinuationId), OpenAiResponsesClientTests.ReturnsTextAndStoresContinuationId),
    (nameof(OpenAiResponsesClientTests.ExecutesToolAndReturnsToolOutputToModel), OpenAiResponsesClientTests.ExecutesToolAndReturnsToolOutputToModel),
    (nameof(OllamaConversationClientTests.PreservesConversationLocally), OllamaConversationClientTests.PreservesConversationLocally),
    (nameof(OllamaConversationClientTests.ExecutesToolAndReturnsToolOutputToModel), OllamaConversationClientTests.ExecutesToolAndReturnsToolOutputToModel),
    (nameof(PcmWaveValidatorTests.AcceptsFrankyMonoPcm), PcmWaveValidatorTests.AcceptsFrankyMonoPcm),
    (nameof(PcmWaveValidatorTests.RejectsStereoWakeAudio), PcmWaveValidatorTests.RejectsStereoWakeAudio),
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed.");
return failures.Count == 0 ? 0 : 1;
