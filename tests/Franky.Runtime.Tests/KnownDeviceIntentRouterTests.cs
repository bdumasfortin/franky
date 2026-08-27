using Franky.Runtime.ControlBoard;
using Franky.Runtime.Tools;

namespace Franky.Runtime.Tests;

internal static class KnownDeviceIntentRouterTests
{
    public static Task RecognizesNaturalHowIsItGoingVariants()
    {
        var variants = new[]
        {
            "How's it going?",
            "How is it going",
            "How are you doing?",
            "Franky, how are you?",
            "Hey Franky, how’s it going!",
            "How are things going, Franky?",
        };

        foreach (var variant in variants)
        {
            TestAssert.True(
                KnownDeviceIntentRouter.TryResolve(variant, out var actionName),
                $"Expected '{variant}' to resolve to a known device action.");
            TestAssert.Equal(DeviceActionTool.FrankySuuuperAction, actionName);
        }

        return Task.CompletedTask;
    }

    public static Task DoesNotHijackLongerQuestions()
    {
        TestAssert.False(KnownDeviceIntentRouter.TryResolve(
            "How's it going with the firmware build?",
            out _));
        TestAssert.False(KnownDeviceIntentRouter.TryResolve(
            "Tell me how things are going in the project.",
            out _));

        return Task.CompletedTask;
    }
}
