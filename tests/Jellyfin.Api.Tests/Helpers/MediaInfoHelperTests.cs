using System;
using System.Globalization;
using Jellyfin.Api.Helpers;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Helpers
{
    public class MediaInfoHelperTests
    {
        private static MediaInfoHelper CreateHelper()
        {
            return new MediaInfoHelper(
                Mock.Of<IUserManager>(),
                Mock.Of<ILibraryManager>(),
                Mock.Of<IMediaSourceManager>(),
                Mock.Of<IMediaEncoder>(),
                Mock.Of<IServerConfigurationManager>(),
                Mock.Of<ILogger<MediaInfoHelper>>(),
                Mock.Of<INetworkManager>(),
                Mock.Of<IDeviceManager>());
        }

        private static MediaSourceInfo CreateSource(Guid itemId, int bitrate, bool supportsDirectPlay = true)
        {
            return new MediaSourceInfo
            {
                Id = itemId.ToString("N", CultureInfo.InvariantCulture),
                Protocol = MediaProtocol.File,
                Bitrate = bitrate,
                SupportsDirectPlay = supportsDirectPlay,
                SupportsDirectStream = true,
                SupportsTranscoding = true
            };
        }

        [Fact]
        public void SortMediaSources_PreferredItemExceedsBitrate_StaysDefault()
        {
            // The version the user was watching (the queried item) must stay the default
            // even when a sibling version fits the bitrate limit better, since the resume
            // position belongs to that exact version.
            var preferredItemId = Guid.NewGuid();
            var preferredSource = CreateSource(preferredItemId, bitrate: 80_000_000, supportsDirectPlay: false);
            var siblingSource = CreateSource(Guid.NewGuid(), bitrate: 8_000_000);

            var result = new PlaybackInfoResponse
            {
                MediaSources = [siblingSource, preferredSource]
            };

            CreateHelper().SortMediaSources(result, maxBitrate: 20_000_000, preferredItemId);

            Assert.Equal(preferredSource.Id, result.MediaSources[0].Id);
        }

        [Fact]
        public void SortMediaSources_NoPreferredItem_OrdersByPlayability()
        {
            var directPlay = CreateSource(Guid.NewGuid(), bitrate: 8_000_000);
            var transcodeOnly = CreateSource(Guid.NewGuid(), bitrate: 8_000_000, supportsDirectPlay: false);
            transcodeOnly.SupportsDirectStream = false;

            var result = new PlaybackInfoResponse
            {
                MediaSources = [transcodeOnly, directPlay]
            };

            CreateHelper().SortMediaSources(result, maxBitrate: 20_000_000);

            Assert.Equal(directPlay.Id, result.MediaSources[0].Id);
        }

        [Fact]
        public void SortMediaSources_PreferredIdNotInSources_KeepsPlayabilityOrder()
        {
            var directPlay = CreateSource(Guid.NewGuid(), bitrate: 8_000_000);
            var transcodeOnly = CreateSource(Guid.NewGuid(), bitrate: 8_000_000, supportsDirectPlay: false);
            transcodeOnly.SupportsDirectStream = false;

            var result = new PlaybackInfoResponse
            {
                MediaSources = [transcodeOnly, directPlay]
            };

            CreateHelper().SortMediaSources(result, maxBitrate: 20_000_000, Guid.NewGuid());

            Assert.Equal(directPlay.Id, result.MediaSources[0].Id);
        }

        [Fact]
        public void ApplyAutoFilmDirectPlayOnly_RemoteSource_RemovesTranscodingCapability()
        {
            var source = CreateSource(Guid.NewGuid(), bitrate: 80_000_000, supportsDirectPlay: false);
            source.Id = "autofilm:" + source.Id;
            source.SupportsProbing = true;
            source.TranscodingUrl = "/videos/item/master.m3u8";
            source.TranscodingContainer = "ts";

            MediaInfoHelper.ApplyAutoFilmDirectPlayOnly(source);

            Assert.True(source.SupportsDirectPlay);
            Assert.False(source.SupportsDirectStream);
            Assert.False(source.SupportsTranscoding);
            Assert.False(source.SupportsProbing);
            Assert.Null(source.TranscodingUrl);
            Assert.Null(source.TranscodingContainer);
        }

        [Fact]
        public void ApplyAutoFilmDirectPlayOnly_LocalSource_LeavesPlaybackCapabilityUnchanged()
        {
            var source = CreateSource(Guid.NewGuid(), bitrate: 80_000_000, supportsDirectPlay: false);
            source.SupportsProbing = true;
            source.TranscodingUrl = "/videos/item/master.m3u8";
            source.TranscodingContainer = "ts";

            MediaInfoHelper.ApplyAutoFilmDirectPlayOnly(source);

            Assert.False(source.SupportsDirectPlay);
            Assert.True(source.SupportsDirectStream);
            Assert.True(source.SupportsTranscoding);
            Assert.True(source.SupportsProbing);
            Assert.Equal("/videos/item/master.m3u8", source.TranscodingUrl);
            Assert.Equal("ts", source.TranscodingContainer);
        }

        [Theory]
        [InlineData("ass")]
        [InlineData("srt")]
        [InlineData("sup")]
        public void ApplyAutoFilmExternalSubtitleDelivery_RemoteSubtitle_ExposesJellyfinRoute(
            string format)
        {
            var itemId = Guid.NewGuid();
            var source = CreateSource(itemId, bitrate: 80_000_000);
            source.Id = "autofilm:" + itemId.ToString("N", CultureInfo.InvariantCulture);
            source.MediaStreams =
            [
                new MediaStream
                {
                    Type = MediaStreamType.Subtitle,
                    Index = 7,
                    Codec = format == "sup" ? "PGSSUB" : format,
                    IsExternal = true,
                    Path = $"openlist:///115/movie/example.zh.{format}"
                }
            ];

            MediaInfoHelper.ApplyAutoFilmExternalSubtitleDelivery(
                itemId,
                source,
                "test token");

            var subtitle = Assert.Single(source.MediaStreams);
            Assert.True(subtitle.SupportsExternalStream);
            Assert.Equal(SubtitleDeliveryMethod.External, subtitle.DeliveryMethod);
            Assert.False(subtitle.IsExternalUrl);
            Assert.Equal(
                $"/Videos/{itemId:N}/autofilm%3A{itemId:N}/Subtitles/7/0/Stream.{format}?ApiKey=test%20token",
                subtitle.DeliveryUrl);
            Assert.Equal(format, subtitle.Codec);
        }

        [Fact]
        public void ApplyAutoFilmExternalSubtitleDelivery_LocalSubtitle_LeavesStreamUnchanged()
        {
            var itemId = Guid.NewGuid();
            var source = CreateSource(itemId, bitrate: 80_000_000);
            source.Id = "autofilm:" + itemId.ToString("N", CultureInfo.InvariantCulture);
            source.MediaStreams =
            [
                new MediaStream
                {
                    Type = MediaStreamType.Subtitle,
                    Index = 4,
                    Codec = "srt",
                    IsExternal = true,
                    Path = "/movie/example.zh.srt"
                }
            ];

            MediaInfoHelper.ApplyAutoFilmExternalSubtitleDelivery(
                itemId,
                source,
                "test-token");

            var subtitle = Assert.Single(source.MediaStreams);
            Assert.False(subtitle.SupportsExternalStream);
            Assert.Null(subtitle.DeliveryMethod);
            Assert.Null(subtitle.DeliveryUrl);
        }
    }
}
