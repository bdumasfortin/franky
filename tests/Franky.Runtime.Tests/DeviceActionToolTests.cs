using Franky.Runtime.Tools;

namespace Franky.Runtime.Tests;

internal static class DeviceActionToolTests
{
    public static async Task QueuesAllowedSfxAction()
    {
        var tool = new DeviceActionTool();

        var result = await tool.ExecuteAsync(
            new ToolCall(
                DeviceActionTool.ToolName,
                $"{{\"action_name\":\"{DeviceActionTool.FrankySuuuperAction}\"}}"),
            CancellationToken.None);

        TestAssert.True(result.Success);
        TestAssert.Equal(DeviceActionTool.FrankySuuuperAction, result.ActionName);
        TestAssert.Contains("queued_for_device", result.OutputJson);
    }

    public static async Task RejectsUnknownDeviceAction()
    {
        var tool = new DeviceActionTool();

        var result = await tool.ExecuteAsync(
            new ToolCall(DeviceActionTool.ToolName, "{\"action_name\":\"device.factory_reset\"}"),
            CancellationToken.None);

        TestAssert.False(result.Success);
        TestAssert.Contains("not on the application allowlist", result.OutputJson);
    }
}
