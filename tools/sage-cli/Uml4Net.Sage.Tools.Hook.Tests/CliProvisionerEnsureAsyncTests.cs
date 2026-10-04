// -------------------------------------------------------------------------------------------------
// <copyright file="CliProvisionerEnsureAsyncTests.cs" company="Starion Group S.A.">
//
//   Copyright (C) 2019-2026 Starion Group S.A.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace Uml4Net.Sage.Tools.Hook.Tests
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    [TestFixture]
    public class CliProvisionerEnsureAsyncTests
    {
        private string pluginRoot = null!;
        private string cliDirectory = null!;
        private string runtimeIdentifier = null!;

        [SetUp]
        public void SetUp()
        {
            this.pluginRoot = Path.Combine(Path.GetTempPath(), "uml4net-sage-hook-tests", Guid.NewGuid().ToString("N"));
            this.runtimeIdentifier = RuntimeIdentifier.Current() ?? throw new InvalidOperationException("This test machine's RID is not one of the four published - cannot run.");
            this.cliDirectory = CacheLayout.CliDirectoryFor(this.pluginRoot, "1.0.0", this.runtimeIdentifier);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(this.cliDirectory))
            {
                Directory.Delete(this.cliDirectory, recursive: true);
            }
        }

        private static byte[] BuildZipContaining(string entryName, byte[] entryContent)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(entryName);
                using var entryStream = entry.Open();
                entryStream.Write(entryContent);
            }

            return memoryStream.ToArray();
        }

        [Test]
        public async Task EnsureAsync_downloads_verifies_and_extracts_a_matching_release()
        {
            var executableName = RuntimeIdentifier.ExecutableName(this.runtimeIdentifier);
            var zipBytes = BuildZipContaining(executableName, Encoding.UTF8.GetBytes("fake cli"));
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(zipBytes)).ToLowerInvariant();
            var assetName = $"uml4net-sage-1.0.0-{this.runtimeIdentifier}.zip";

            var releaseJson = JsonSerializer.Serialize(new GitHubRelease
            {
                TagName = "tools-v1.0.0",
                Body = $"{assetName} sha256:{sha256}",
                Assets = [new GitHubReleaseAsset { Name = assetName, BrowserDownloadUrl = "https://example.com/download/" + assetName }],
            });

            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(releaseJson) });
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zipBytes) });

            var provisioner = new CliProvisioner(new HttpClient(handler));

            var resultPath = await provisioner.EnsureAsync(this.pluginRoot, "1.0.0", default);

            Assert.That(resultPath, Is.Not.Null);
            Assert.That(File.Exists(resultPath!));
            Assert.That(File.Exists(CacheLayout.VerifiedMarkerPath(this.cliDirectory)));
        }

        [Test]
        public async Task EnsureAsync_returns_null_and_does_not_extract_on_a_checksum_mismatch()
        {
            var executableName = RuntimeIdentifier.ExecutableName(this.runtimeIdentifier);
            var zipBytes = BuildZipContaining(executableName, Encoding.UTF8.GetBytes("fake cli"));
            var assetName = $"uml4net-sage-1.0.0-{this.runtimeIdentifier}.zip";

            var releaseJson = JsonSerializer.Serialize(new GitHubRelease
            {
                TagName = "tools-v1.0.0",
                Body = $"{assetName} sha256:{new string('0', 64)}", // deliberately wrong
                Assets = [new GitHubReleaseAsset { Name = assetName, BrowserDownloadUrl = "https://example.com/download/" + assetName }],
            });

            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(releaseJson) });
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zipBytes) });

            var provisioner = new CliProvisioner(new HttpClient(handler));

            var resultPath = await provisioner.EnsureAsync(this.pluginRoot, "1.0.0", default);

            Assert.That(resultPath, Is.Null);
            Assert.That(Directory.Exists(this.cliDirectory), Is.False);
        }

        [Test]
        public async Task EnsureAsync_returns_null_when_the_release_is_not_found()
        {
            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            var provisioner = new CliProvisioner(new HttpClient(handler));

            var resultPath = await provisioner.EnsureAsync(this.pluginRoot, "1.0.0", default);

            Assert.That(resultPath, Is.Null);
        }

        [Test]
        public async Task EnsureAsync_returns_null_when_no_asset_matches_this_platform()
        {
            var releaseJson = JsonSerializer.Serialize(new GitHubRelease
            {
                TagName = "tools-v1.0.0",
                Body = "uml4net-sage-1.0.0-some-other-rid.zip sha256:" + new string('a', 64),
                Assets = [new GitHubReleaseAsset { Name = "uml4net-sage-1.0.0-some-other-rid.zip", BrowserDownloadUrl = "https://example.com/x.zip" }],
            });

            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(releaseJson) });

            var provisioner = new CliProvisioner(new HttpClient(handler));

            var resultPath = await provisioner.EnsureAsync(this.pluginRoot, "1.0.0", default);

            Assert.That(resultPath, Is.Null);
        }

        [Test]
        public async Task EnsureAsync_never_throws_when_the_http_client_itself_throws()
        {
            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => throw new HttpRequestException("network is down"));

            var provisioner = new CliProvisioner(new HttpClient(handler));

            string? resultPath = null;
            await Assert.DoesNotThrowAsync(async () => resultPath = await provisioner.EnsureAsync(this.pluginRoot, "1.0.0", default));
            Assert.That(resultPath, Is.Null);
        }

        [Test]
        public async Task EnsureAsync_reuses_the_cache_without_a_network_call_once_verified()
        {
            var executableName = RuntimeIdentifier.ExecutableName(this.runtimeIdentifier);
            Directory.CreateDirectory(this.cliDirectory);
            File.WriteAllText(Path.Combine(this.cliDirectory, executableName), "already here");
            File.WriteAllText(CacheLayout.VerifiedMarkerPath(this.cliDirectory), "verified");

            var handler = new StubHttpMessageHandler(); // no responses enqueued - any HTTP call would throw
            var provisioner = new CliProvisioner(new HttpClient(handler));

            var resultPath = await provisioner.EnsureAsync(this.pluginRoot, "1.0.0", default);

            Assert.That(resultPath, Is.EqualTo(Path.Combine(this.cliDirectory, executableName)));
        }
    }
}
