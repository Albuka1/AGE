#!/usr/bin/env bash
#
# Cut a release: bump the version of the build, close the section of the changelog, verify the result and tag it.
#
# A release of this repository is one commit that says what the version is plus the changelog section that says what
# changed, and the tag that GitHub names the release by. The Release workflow then checks that the tag and the version
# agree before it attaches anything, so the four files this script writes are what that check reads:
#
#   * Directory.Build.props carries <Version>, which every assembly of the engine reports.
#   * engine.version.json and Age.Sample/game.version.json carry the same number, which is what a game compares itself
#     against before it starts.
#   * CHANGELOG.md carries the section of the version, which is what a person reads.
#
# The script refuses to run when the tag exists, when the working tree holds changes it did not make, or when the build
# or the tests fail: a release commit that mixes in work in progress is one nobody can call a release. Use --dry-run to
# see what it would write without touching a file or the repository.
#
# Usage:
#   tools/release.sh --version 0.5.0 --dry-run
#   tools/release.sh --version 0.5.0
#   git push origin main --follow-tags

set -euo pipefail

usage() {
    cat <<'EOF'
Usage: release.sh --version X.Y.Z[--suffix] [--dry-run]

  -v, --version VERSION   The version to release, such as 0.3.0 or 0.3.0-alpha.1.
                          The tag is this with a v in front.
  -n, --dry-run           Print what would be written and stop before writing,
                          building, committing or tagging anything.
  -h, --help              Show this message.
EOF
}

step() {
    printf '\033[36m==> %s\033[0m\n' "$*"
}

die() {
    printf '%s\n' "$*" >&2
    exit 1
}

version=""
dry_run=0

while [ $# -gt 0 ]; do
    case "$1" in
        -v|--version)
            [ $# -ge 2 ] || die "The option $1 takes a value."
            version="$2"
            shift 2
            ;;
        -n|--dry-run)
            dry_run=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            die "Unknown option '$1'. See --help."
            ;;
    esac
done

[ -n "$version" ] || { usage; exit 1; }

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$script_dir/.." && pwd)"
cd "$root"

# A version is three numbers with an optional suffix, such as 0.3.0 or 0.3.0-alpha.1. The suffix is what a prerelease is
# cut with, and the Release workflow marks a tag that holds a dash as one.
if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$ ]]; then
    die "A version is three numbers with an optional suffix, such as 0.3.0 or 0.3.0-alpha.1, and '$version' is not one."
fi

if [ ! -f Directory.Build.props ] || [ ! -f CHANGELOG.md ]; then
    die "The repository root is not '$root', and the script finds it by the folder it lives in."
fi

tag="v$version"

if git rev-parse --verify --quiet "refs/tags/$tag" >/dev/null; then
    die "The tag $tag already exists, and a version is released once."
fi

dirty="$(git status --porcelain)"
if [ -n "$dirty" ]; then
    die "$(printf 'The working tree holds changes that are not committed, and a release commit holds the version and the changelog only. Commit or stash them first:\n%s' "$dirty")"
fi

branch="$(git rev-parse --abbrev-ref HEAD)"
date_str="$(date +%Y-%m-%d)"

props="$(< Directory.Build.props)"

# The version the build already reports. A release that names the version the build already reports is not a mistake — the
# property is often bumped while a version is being finished — so what is checked is that there is a version at all, and
# the substitution below is what writes the one that was asked for.
current="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' Directory.Build.props | head -n1 || true)"
if [ -z "$current" ]; then
    die 'Directory.Build.props holds no <Version>...</Version>, so there is nothing to bump.'
fi

bumped="$(printf '%s' "$props" | sed "s:<Version>[^<]*</Version>:<Version>$version</Version>:")"

for f in engine.version.json Age.Sample/game.version.json; do
    if [ ! -f "$f" ]; then
        die "The version file '$f' is not in the repository, so the release cannot keep it in step with the build."
    fi
done

# The changelog closes by writing the section of the version under the unreleased one. A newline is written under it so
# the next release starts on an empty section of its own, which is what a version that was cut leaves behind. The search
# and the substitution both read the file directly, rather than a variable that is piped into a reader: a reader that
# stops early closes the pipe, the writer takes a SIGPIPE, and pipefail turns that into a failure of a command that
# actually succeeded.
if ! grep -q '^## \[Unreleased\]$' CHANGELOG.md; then
    die "CHANGELOG.md holds no '## [Unreleased]' section to close, so nothing says what this release brings."
fi

closed="$(awk -v ver="$version" -v day="$date_str" '
    /^## \[Unreleased\]$/ && !done {
        print
        print ""
        printf "## [%s] - %s\n", ver, day
        done = 1
        next
    }
    { print }
' CHANGELOG.md)"

step "Version $version on the branch '$branch', tagged $tag, dated $date_str"

if [ "$dry_run" -eq 1 ]; then
    printf '    Directory.Build.props: <Version>%s</Version>\n' "$version"
    printf '    engine.version.json: engine %s\n' "$version"
    printf '    Age.Sample/game.version.json: engine %s\n' "$version"
    printf "    CHANGELOG.md: '## [Unreleased]' followed by '## [%s] - %s'\n" "$version" "$date_str"
    printf 'Dry run: nothing was written, built, committed or tagged.\n'
    exit 0
fi

# The four files are written together, so a failure of the build that follows leaves them all in the state they were in
# rather than some of them pointing at a version that was never released.
write_files() {
    printf '%s' "$bumped" > Directory.Build.props

    printf '{\n  "engine": "%s"\n}\n' "$version" > engine.version.json
    printf '{\n  "engine": "%s"\n}\n' "$version" > Age.Sample/game.version.json

    printf '%s' "$closed" > CHANGELOG.md
}

rollback_files() {
    git checkout -- Directory.Build.props CHANGELOG.md engine.version.json Age.Sample/game.version.json
}

write_files

cleanup_on_failure() {
    rollback_files
    die "$1"
}

step 'Building and testing what is about to be released'

if ! dotnet build Age.slnx -c Release; then
    cleanup_on_failure 'The build of the release failed.'
fi

if ! dotnet test --project Age.Tests/Age.Tests.csproj --no-build -c Release; then
    cleanup_on_failure 'The tests of the release failed.'
fi

step "Committing 'release: $version' and tagging $tag"

git add Directory.Build.props CHANGELOG.md engine.version.json Age.Sample/game.version.json
git commit -m "release: $version"

# An annotated tag, because that is what a release is: it carries who made it and when, and it is what a push with
# --follow-tags sends, which is the push this script tells a person to make.
git tag -a "$tag" -m "release: $version"

printf '\n'
printf '\033[32mThe release is committed and tagged. Push it, and the Release workflow checks that the tag and the version agree:\033[0m\n'
printf '    git push origin %s --follow-tags\n' "$branch"