# Releasing

A release of this repository is one commit that says what the version is, the changelog section that says what changed, and
the tag that names the release. The Release workflow refuses to publish anything when the tag and the version disagree, so a
build that reports a version nobody tagged is not a release.

## What the numbers mean

The version is `MAJOR.MINOR.PATCH`, and it lives in one place: `<Version>` in `Directory.Build.props`. Every assembly of the
engine reports it, which is the version a game prints and the version this repository tags.

- `MAJOR` stays `0` while the engine is not finished, which is what the leading zero of `0.3.0` says: the API is not frozen,
  and a game is expected to move with it.
- `MINOR` is a version a game moves to: new features, and the changes that break what a game wrote while the engine is `0.x`.
  A bump of this number is the one worth reading this file for.
- `PATCH` is a fix that a game takes without changing anything: a mistake, a crash, a broken save that still loads.

A version that is not ready for a player carries a suffix: `0.3.0-alpha.1`. The tag is the version with a `v` in front, so
`v0.3.0-alpha.1`, and a tag with a suffix makes the workflow publish a prerelease rather than the latest release.

### Versions of formats are their own

The version of the build says nothing about the data a build reads. A format carries its own version, and a reader refuses one
it does not know:

- `SceneData.CurrentVersion` is the version of a scene, and a scene of another version is refused with a message rather than
  read as if it were this one.
- The document of a sprite sheet declares `version: 1`, which is `SpriteSheet.CurrentVersion`.

These numbers change when the format changes, not when the product does, so a release of `0.3.0` usually changes neither.

## Cutting a release

```powershell
pwsh tools/release.ps1 -Version 0.3.0 -DryRun   # prints what it would write, touches nothing
pwsh tools/release.ps1 -Version 0.3.0
git push origin main --follow-tags
```

The script is the release, so that the two files that a release is about cannot disagree:

- it refuses a version that is not three numbers with an optional suffix, and one that is already a tag;
- it refuses a working tree that holds changes which are not committed, so a release commit holds the version and the changelog only;
- it bumps `<Version>` and turns `## [Unreleased]` into `## [0.3.0] - <date>`, leaving a fresh unreleased section above it;
- it builds and tests what it is about to release, and puts both files back untouched when either fails;
- it commits `release: 0.3.0` and tags `v0.3.0`, and prints the one push that follows.

The push of the tag starts the Release workflow, which builds and tests the tagged commit again, publishes the sample, and
attaches it with the content of the game to the release.

## What the workflow checks

Before it attaches anything, the Release workflow reads the version the way MSBuild evaluates it and refuses:

- a tag that does not name that version, because the build would report something other than the release it is in;
- a version that `CHANGELOG.md` has no section for, because a release without notes is one nobody can read.
