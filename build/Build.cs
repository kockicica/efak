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

using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

using FileMode = System.IO.FileMode;

[GitHubActions("continuous", GitHubActionsImage.UbuntuLatest, OnPushTagsIgnore = new[] { "v*" },
               InvokedTargets = new[] { nameof(Compile) },
               FetchDepth = 0)]
[GitHubActions("tagged", GitHubActionsImage.UbuntuLatest, InvokedTargets = new[] { nameof(Cli), nameof(Pack) }, AutoGenerate = false,
               FetchDepth = 0, OnPushTags = new[] { "v*" }, PublishArtifacts = true, EnableGitHubToken = true)]
class Build : NukeBuild {
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Self contained publish - Default is true")]
    bool SelfContained { get; set; } = true;

    [Solution(GenerateProjects = true)] readonly Solution Solution;

    [Parameter("Runtime", List = true)]
    string[] Runtime { get; set; } = { "win-x64", "win-x86" };

    AbsolutePath SourceDirectory    => RootDirectory / "src";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion]    readonly GitVersion    GitVersion;

    static string ChangeLogFile => RootDirectory / "CHANGELOG.md";

    const string DefaultNoGitVersion = "0.1.0";

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

    string[] Artifacts = Array.Empty<string>();

    Target Pack => _ => _
                        .After(Cli)
                        .Produces(ArtifactsDirectory / ".zip")
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
                                Artifacts.Append(where);

                            }

                        });

    Target CreateRelease => _ => _
                                 .TriggeredBy(Pack)
                                 .Executes<Task>(async () =>
                                 {
                                     GitHubTasks.GitHubClient = new GitHubClient(new ProductHeaderValue(nameof(NukeBuild))) {
                                         Credentials = new Credentials(GitHubActions.Instance.Token)
                                     };

                                     var changeLogSectionEntries = ChangelogTasks.ExtractChangelogSectionNotes(ChangeLogFile);
                                     var latestChangeLog = changeLogSectionEntries.Aggregate((c, n) => c + Environment.NewLine + n);

                                     var release = new NewRelease(SemVer) {
                                         TargetCommitish = GitVersion.Sha,
                                         Draft = true,
                                         Name = $"v{SemVer}",
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

    private static async Task UploadReleaseAssetToGithub(Release release, string asset) {
        if (!File.Exists(asset)) {
            return;
        }
        if (!new FileExtensionContentTypeProvider().TryGetContentType(asset, out string contentType)) {
            contentType = "application/x-binary";
        }
        var releaseAsseetUpload = new ReleaseAssetUpload {
            ContentType = contentType,
            FileName = asset,
            RawData = File.OpenRead(asset)
        };
        await GitHubTasks.GitHubClient.Repository.Release.UploadAsset(release, releaseAsseetUpload);
    }
}