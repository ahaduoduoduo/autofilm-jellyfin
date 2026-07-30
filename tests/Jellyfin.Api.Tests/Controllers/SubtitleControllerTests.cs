using System.Reflection;
using Jellyfin.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public static class SubtitleControllerTests
{
    [Fact]
    public static void UploadSubtitleStream_DisablesBufferedRequestLimit()
    {
        var method = typeof(SubtitleController).GetMethod(
            nameof(SubtitleController.UploadSubtitleStream));
        Assert.NotNull(method);

        Assert.Single(
            method.GetCustomAttributes<DisableRequestSizeLimitAttribute>());
    }
}
