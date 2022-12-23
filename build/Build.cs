using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.StaticFiles;

using Nuke.Common;
using Nuke.Common.ChangeLog;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;

using Octokit;

using Serilog;

using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

using FileMode = System.IO.FileMode;

[GitHubActions("continuous",
               GitHubActionsImage.UbuntuLatest,
               On = new[] { GitHubActionsTrigger.Push },
               PublishArtifacts = false,
               InvokedTargets = new[] { nameof(Compile) },
               FetchDepth = 0
)]
[GitHubActions("tagged",
               GitHubActionsImage.UbuntuLatest,
               InvokedTargets = new[] { nameof(Cli), nameof(Pack) },
               FetchDepth = 0,
               OnPushTags = new[] { "v*" },
               PublishArtifacts = true,
               EnableGitHubToken = true
)]
partial class Build : NukeBuild {

    const string DefaultNoGitVersion = "0.1.0";

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion]    readonly GitVersion    GitVersion;

    [Solution(GenerateProjects = true)] readonly Solution Solution;

    [Parameter("Self contained publish - Default is true")]
    bool SelfContained { get; set; } = true;

    [Parameter("Runtime", List = true)]
    string[] Runtime { get; set; } = { "win-x64", "win-x86" };

    AbsolutePath SourceDirectory    => RootDirectory / "src";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    static string ChangeLogFile => RootDirectory / "CHANGELOG.md";

    string SemVer               => GitVersion?.SemVer ?? DefaultNoGitVersion;
    string InformationalVersion => GitVersion?.InformationalVersion ?? DefaultNoGitVersion;
    string AssemblySemVer       => GitVersion?.AssemblySemVer ?? DefaultNoGitVersion;
    string AssemblySemFileVer   => GitVersion?.AssemblySemFileVer ?? DefaultNoGitVersion;
    string PreReleaseTag        => GitVersion?.PreReleaseTag ?? string.Empty;

    Target Clean => _ => _
                         .Before(Restore)
                         .Executes(() =>
                         {
                         });

    Target Restore => _ => _
        .Executes(() =>
        {
        });

    Target Compile => _ => _
                           .DependsOn(Restore)
                           .Executes(() =>
                           {
                           });

    Target Cli => _ => _
        .Executes(() =>
        {
            var project = Solution.efak_cli;
            var whatToPublish = from runtime in Runtime select new { project, runtime };

            var version = SemVer;

            DotNetPublish(settings =>
                              settings
                                  .SetSelfContained(SelfContained)
                                  .SetPublishSingleFile(true)
                                  .SetConfiguration(Configuration)
                                  .SetProperty("AllowedReferenceRelatedFileExtensions", "none")
                                  .SetProject(project)
                                  .SetAssemblyVersion(AssemblySemVer)
                                  .SetFileVersion(AssemblySemFileVer)
                                  .SetInformationalVersion(InformationalVersion)
                                  .SetVersion(AssemblySemFileVer)
                                  .CombineWith(whatToPublish,
                                               (publishSettings, o) =>
                                                   publishSettings
                                                       .SetProject(o.project)
                                                       .SetRuntime(o.runtime)
                                                       .SetOutput(ArtifactsDirectory /
                                                                  $"{o.project.Name}-{o.runtime}-{version}" /
                                                                  $"{o.project.Name}-{o.runtime}-{version}")
                                  )
            );

        });

    Target Pack => _ => _
                        .After(Cli)
                        .Produces(ArtifactsDirectory / "*.zip")
                        .Executes(() =>
                        {
                            var project = Solution.efak_cli;
                            var whatToPublish = from runtime in Runtime select new { project, runtime };

                            var version = SemVer;

                            foreach (var pr in whatToPublish) {
                                // var what = ArtifactsDirectory / $"{pr.project.Name}-{pr.runtime}-{version}" /
                                //     $"{pr.project.Name}-{pr.runtime}-{version}";
                                var what = ArtifactsDirectory / $"{pr.project.Name}-{pr.runtime}-{version}";
                                var where = ArtifactsDirectory / $"{pr.project.Name}-{pr.runtime}-{version}.zip";
                                CompressZip(what, where, info => true, compressionLevel: CompressionLevel.SmallestSize, fileMode: FileMode.Create);
                                EnsureCleanDirectory(what);
                                DeleteDirectory(what);
                            }

                        });

    Target CreateRelease => _ => _
                                 .TriggeredBy(Pack)
                                 .Unlisted()
                                 .Executes(async () =>
                                 {
                                     GitHubTasks.GitHubClient = new GitHubClient(new ProductHeaderValue(nameof(NukeBuild))) {
                                         Credentials = new Credentials(GitHubActions.Instance.Token)
                                     };

                                     var changeLogSectionEntries =
                                         ControlFlow.SuppressErrors(
                                             () => ChangelogTasks.ExtractChangelogSectionNotes(ChangeLogFile), Array.Empty<string>());

                                     var latestChangeLog = changeLogSectionEntries.Aggregate((c, n) => c + Environment.NewLine + n);

                                     var tag = $"v{SemVer}";

                                     var release = new NewRelease(tag) {
                                         TargetCommitish = GitVersion.Sha,
                                         Draft = true,
                                         Name = tag,
                                         Prerelease = !string.IsNullOrEmpty(PreReleaseTag),
                                         Body = latestChangeLog,
                                     };

                                     var owner = GitRepository.GetGitHubOwner();
                                     var name = GitRepository.GetGitHubName();

                                     var createdRelease = await GitHubTasks.GitHubClient.Repository.Release.Create(owner, name, release);

                                     GlobFiles(ArtifactsDirectory, "*.zip")
                                         .ForEach(async x => await UploadReleaseAssetToGithub(createdRelease, x));

                                     await GitHubTasks.GitHubClient.Repository.Release.Edit(
                                         owner, name, createdRelease.Id, new ReleaseUpdate { Draft = false });

                                 });

    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Compile);

    private static async Task UploadReleaseAssetToGithub(Release release, string asset) {
        Log.Information("Upload release asset: {file}", asset);
        if (!File.Exists(asset)) {
            Log.Warning("Release asset: {file} does not exist", asset);
            return;
        }
        if (!new FileExtensionContentTypeProvider().TryGetContentType(asset, out string contentType)) {
            contentType = "application/x-binary";
        }
        Log.Information("Content type: {type}", contentType);

        await using var artifactStream = File.OpenRead(asset);
        var fileName = Path.GetFileName(asset);
        var releaseAssetUpload = new ReleaseAssetUpload {
            ContentType = contentType,
            FileName = fileName,
            RawData = artifactStream
        };
        await GitHubTasks.GitHubClient.Repository.Release.UploadAsset(release, releaseAssetUpload);
        Log.Information("Uploaded release asset: {file}", asset);
    }
}