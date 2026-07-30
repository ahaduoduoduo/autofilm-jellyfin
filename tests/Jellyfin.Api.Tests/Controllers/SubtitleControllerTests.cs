using System.Reflection;
using Jellyfin.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public static class SubtitleControllerTests
{
    [Fact]
    public static void UploadSubtitle_AllowsLargeGraphicalSubtitleRequests()
    {
        var method = typeof(SubtitleController).GetMethod(
            nameof(SubtitleController.UploadSubtitle));
        Assert.NotNull(method);

        var attribute = Assert.Single(
            method.GetCustomAttributes<RequestSizeLimitAttribute>());
        Assert.Equal(256L * 1024 * 1024, attribute.Bytes);
    }
}
