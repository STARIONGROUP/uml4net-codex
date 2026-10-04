// -------------------------------------------------------------------------------------------------
// <copyright file="UvProvisionerTests.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.Knowledge.Tests
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

    using Uml4Net.Sage.Knowledge.Toolchain;

    [TestFixture]
    public class UvProvisionerTests
    {
        private DirectoryInfo cacheRoot = null!;
        private string target = null!;

        [SetUp]
        public void SetUp()
        {
            this.cacheRoot = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "uml4net-sage-knowledge-tests", Guid.NewGuid().ToString("N")));
            this.target = UvPlatform.CurrentTarget ?? throw new InvalidOperationException("This test machine's platform is not one of uv's published targets - cannot run.");
        }

        [TearDown]
        public void TearDown()
        {
            if (this.cacheRoot.Exists)
            {
                this.cacheRoot.Delete(recursive: true);
            }
        }

        private static byte[] BuildArchiveContaining(string target, string entryName, byte[] entryContent)
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
        public async Task EnsureAsync_downloads_verifies_and_caches_a_matching_release()
        {
            var executableName = UvPlatform.ExecutableName(this.target);
            var archiveBytes = BuildArchiveContaining(this.target, executableName, Encoding.UTF8.GetBytes("fake uv"));
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(archiveBytes));
            var assetName = UvPlatform.AssetName(this.target);

            var releaseJson = JsonSerializer.Serialize(new
            {
                assets = new object[]
                {
                    new { name = assetName, browser_download_url = "https://example.com/download/" + assetName },
                    new { name = assetName + ".sha256", browser_download_url = "https://example.com/download/" + assetName + ".sha256" },
                },
            });

            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(releaseJson) });
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{sha256} *{assetName}") });
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archiveBytes) });

            var provisioner = new UvProvisioner(new HttpClient(handler), this.cacheRoot);

            var executable = await provisioner.EnsureAsync();

            Assert.That(executable, Is.Not.Null);
            Assert.That(File.Exists(executable!.FullName));
            Assert.That(File.Exists(provisioner.OkMarker(UvProvisioner.PinnedVersion, this.target).FullName));
        }

        [Test]
        public async Task EnsureAsync_returns_null_and_does_not_cache_on_a_checksum_mismatch()
        {
            var executableName = UvPlatform.ExecutableName(this.target);
            var archiveBytes = BuildArchiveContaining(this.target, executableName, Encoding.UTF8.GetBytes("fake uv"));
            var assetName = UvPlatform.AssetName(this.target);

            var releaseJson = JsonSerializer.Serialize(new
            {
                assets = new object[]
                {
                    new { name = assetName, browser_download_url = "https://example.com/download/" + assetName },
                    new { name = assetName + ".sha256", browser_download_url = "https://example.com/download/" + assetName + ".sha256" },
                },
            });

            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(releaseJson) });
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{new string('0', 64)} *{assetName}") }); // deliberately wrong
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archiveBytes) });

            var provisioner = new UvProvisioner(new HttpClient(handler), this.cacheRoot);

            var executable = await provisioner.EnsureAsync();

            Assert.That(executable, Is.Null);
            Assert.That(provisioner.OkMarker(UvProvisioner.PinnedVersion, this.target).Exists, Is.False);
        }

        [Test]
        public async Task EnsureAsync_returns_null_when_the_release_is_not_found()
        {
            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            var provisioner = new UvProvisioner(new HttpClient(handler), this.cacheRoot);

            var executable = await provisioner.EnsureAsync();

            Assert.That(executable, Is.Null);
        }

        [Test]
        public async Task EnsureAsync_returns_null_when_no_asset_matches_this_platform()
        {
            var releaseJson = JsonSerializer.Serialize(new
            {
                assets = new object[]
                {
                    new { name = "uv-some-other-target.zip", browser_download_url = "https://example.com/x.zip" },
                },
            });

            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(releaseJson) });

            var provisioner = new UvProvisioner(new HttpClient(handler), this.cacheRoot);

            var executable = await provisioner.EnsureAsync();

            Assert.That(executable, Is.Null);
        }

        [Test]
        public async Task EnsureAsync_never_throws_when_the_http_client_itself_throws()
        {
            var handler = new StubHttpMessageHandler();
            handler.Enqueue(_ => throw new HttpRequestException("network is down"));

            var provisioner = new UvProvisioner(new HttpClient(handler), this.cacheRoot);

            FileInfo? executable = null;
            await Assert.DoesNotThrowAsync(async () => executable = await provisioner.EnsureAsync());
            Assert.That(executable, Is.Null);
        }

        [Test]
        public async Task EnsureAsync_reuses_the_cache_without_a_network_call_once_verified()
        {
            var binDirectory = new UvProvisioner(new HttpClient(new StubHttpMessageHandler()), this.cacheRoot)
                .BinDirectory(UvProvisioner.PinnedVersion, this.target);
            binDirectory.Create();
            var executableName = UvPlatform.ExecutableName(this.target);
            await File.WriteAllTextAsync(Path.Combine(binDirectory.FullName, executableName), "already here");
            await File.WriteAllTextAsync(Path.Combine(binDirectory.FullName, ".ok"), "verified");

            var handler = new StubHttpMessageHandler(); // no responses enqueued - any HTTP call would throw
            var provisioner = new UvProvisioner(new HttpClient(handler), this.cacheRoot);

            var executable = await provisioner.EnsureAsync();

            Assert.That(executable, Is.Not.Null);
            Assert.That(executable!.FullName, Is.EqualTo(Path.Combine(binDirectory.FullName, executableName)));
        }
    }
}
